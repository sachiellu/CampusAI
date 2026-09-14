using System.ClientModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CampusAI.Api.Models;
using CampusAI.Api.Options;
using CampusAI.Api.Services.Agents;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace CampusAI.Api.Services;

/// <summary>
/// 應用入口：Personal Orchestrator（Agent 層）→ API 服務層／MCP RAG → LLM 潤飾。
/// </summary>
public class LlmAgentService
{
    private readonly IConfiguration _config;
    private readonly OpenAiOptions _openAi;
    private readonly LlmOptions _llm;
    private readonly CampusToolService _tools;
    private readonly PersonalOrchestrator _orchestrator;
    private readonly McpRagClient _mcpRag;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmAgentService> _logger;

    public LlmAgentService(
        IConfiguration config,
        IOptions<OpenAiOptions> openAi,
        IOptions<LlmOptions> llm,
        CampusToolService tools,
        PersonalOrchestrator orchestrator,
        McpRagClient mcpRag,
        IHttpClientFactory httpClientFactory,
        ILogger<LlmAgentService> logger)
    {
        _config = config;
        _openAi = openAi.Value;
        _llm = llm.Value;
        _tools = tools;
        _orchestrator = orchestrator;
        _mcpRag = mcpRag;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsLlmEnabled =>
        !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]) || _openAi.IsConfigured;

    public async Task<ChatResponse> HandleAsync(ChatRequest request, CancellationToken ct = default)
    {
        var stage = Math.Clamp(request.Stage, 1, 3);
        var role = (request.Role ?? "student").ToLowerInvariant();
        var feature = string.IsNullOrWhiteSpace(request.Feature) ? "general" : request.Feature;

        string intent;
        string toolJson;
        List<AgentAction> actions;
        try
        {
            (intent, toolJson, actions) = _orchestrator.Handle(role, feature, request.Message, request.StudentId, stage);

            if (intent == "campus_qa")
            {
                var (ok, mcpJson, detail) = await _mcpRag.RetrieveAsync(request.Message, 3, ct);
                if (ok)
                {
                    toolJson = mcpJson;
                    actions.Add(new("MCP Client", "rag_retrieve", detail));
                    try
                    {
                        using var doc = JsonDocument.Parse(toolJson);
                        if (doc.RootElement.TryGetProperty("documents", out var docs))
                            toolJson = JsonSerializer.Serialize(new { documents = docs });
                    }
                    catch { /* keep */ }
                }
                else
                {
                    actions.Add(new("MCP Client", "RAG 備援", $"MCP 不可用：{detail}；改用本機檢索"));
                    toolJson = _tools.ExecuteTool(
                        "search_campus_documents",
                        JsonSerializer.Serialize(new { query = request.Message }),
                        request.StudentId,
                        stage,
                        actions);
                }
            }
        }
        catch (Exception ex)
        {
            actions = [new("Personal Orchestrator", "調度錯誤", ex.Message)];
            return new ChatResponse($"查詢失敗：{ex.Message}", stage, "error", actions, null, "tools_error", null);
        }

        object? result = toolJson;
        try { result = JsonSerializer.Deserialize<JsonElement>(toolJson); } catch { /* keep */ }

        if (IsLlmEnabled)
        {
            try
            {
                var reply = await PhraseWithLlmAsync(role, request.Message, toolJson, ct);
                var provider = !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]) ? "gemini" : "openai";
                actions.Insert(0, new("Personal Orchestrator", "Agent 層 → LLM 回覆", $"provider={provider}"));
                return new ChatResponse(reply, stage, intent, actions, result, provider, _llm.GeminiModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM 潤飾失敗，改用人話摘要");
                actions.Add(new("Personal Orchestrator", "LLM 暫時不可用", "已改以校務查詢結果整理回覆"));
            }
        }

        var natural = FormatNaturalReply(intent, toolJson);
        return new ChatResponse(natural, stage, intent, actions, result, IsLlmEnabled ? "rules_fallback" : "rules", null);
    }

    private async Task<string> PhraseWithLlmAsync(string role, string userMessage, string toolJson, CancellationToken ct)
    {
        var system = """
你是校園個人化 AI 助理，回答要像 Google 助理：繁體中文、清楚、可執行。
根據工具 JSON 事實回答；不可捏造；不要輸出 JSON；不要提 API、金鑰、demo。
""";
        var user = $"使用者問題：{userMessage}\n\n工具資料：\n{toolJson}";

        var geminiKey = _config["Gemini:ApiKey"];
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            // 先試 OpenAI 相容端點，再試原生 generateContent（含 503 重試）
            var models = new[]
            {
                _llm.GeminiModel,
                "gemini-flash-latest",
                "gemini-2.5-flash",
                "gemini-2.0-flash",
                "gemini-2.0-flash-lite",
                "gemini-1.5-flash"
            }.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            Exception? last = null;
            foreach (var model in models)
            {
                try
                {
                    return await CallGeminiOpenAiCompatAsync(geminiKey, model!, system, user, ct);
                }
                catch (Exception ex)
                {
                    last = ex;
                    _logger.LogDebug(ex, "Gemini OpenAI-compat {Model} failed", model);
                }

                try
                {
                    return await CallGeminiNativeAsync(geminiKey, model!, system, user, ct);
                }
                catch (Exception ex)
                {
                    last = ex;
                    _logger.LogDebug(ex, "Gemini native {Model} failed", model);
                }
            }
            throw last ?? new InvalidOperationException("Gemini 呼叫失敗");
        }

        var client = new ChatClient(_openAi.Model, new ApiKeyCredential(_openAi.ApiKey));
        var completion = await client.CompleteChatAsync(
        [
            new SystemChatMessage(system),
            new UserChatMessage(user)
        ], cancellationToken: ct);
        var sb = new StringBuilder();
        foreach (var part in completion.Value.Content)
            if (!string.IsNullOrWhiteSpace(part.Text)) sb.Append(part.Text);
        return sb.ToString().Trim();
    }

    private async Task<string> CallGeminiOpenAiCompatAsync(string apiKey, string model, string system, string user, CancellationToken ct)
    {
        model = model.Replace("models/", "", StringComparison.OrdinalIgnoreCase);
        var oai = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
            });
        var chat = oai.GetChatClient(model);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var completion = await chat.CompleteChatAsync(
                [
                    new SystemChatMessage(system),
                    new UserChatMessage(user)
                ], cancellationToken: ct);
                var sb = new StringBuilder();
                foreach (var part in completion.Value.Content)
                    if (!string.IsNullOrWhiteSpace(part.Text)) sb.Append(part.Text);
                var text = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
                throw new InvalidOperationException("empty content");
            }
            catch (Exception ex) when (attempt < 2 && IsRetryable(ex))
            {
                await Task.Delay(700 * (attempt + 1), ct);
            }
        }
        throw new InvalidOperationException($"Gemini compat failed model={model}");
    }

    private async Task<string> CallGeminiNativeAsync(string apiKey, string model, string system, string user, CancellationToken ct)
    {
        model = model.Replace("models/", "", StringComparison.OrdinalIgnoreCase);
        var http = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        // 新舊金鑰都試：query key + header
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{url}?key={Uri.EscapeDataString(apiKey)}");
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            req.Content = JsonContent.Create(new
            {
                system_instruction = new { parts = new[] { new { text = system } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = user } } }
                },
                generationConfig = new { temperature = 0.4 }
            });

            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if ((int)res.StatusCode == 503 || (int)res.StatusCode == 429)
            {
                await Task.Delay(800 * (attempt + 1), ct);
                continue;
            }
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini HTTP {(int)res.StatusCode} model={model}: {Truncate(body, 180)}");

            using var doc = JsonDocument.Parse(body);
            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("candidates", out var candidates))
            {
                foreach (var cand in candidates.EnumerateArray())
                {
                    if (!cand.TryGetProperty("content", out var content)) continue;
                    if (!content.TryGetProperty("parts", out var parts)) continue;
                    foreach (var part in parts.EnumerateArray())
                        if (part.TryGetProperty("text", out var text))
                            sb.Append(text.GetString());
                }
            }
            var reply = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(reply)) return reply;
            throw new InvalidOperationException($"Gemini empty model={model}");
        }
        throw new InvalidOperationException($"Gemini busy model={model}");
    }

    private static bool IsRetryable(Exception ex)
    {
        var m = ex.Message + ex.ToString();
        return m.Contains("503") || m.Contains("429") || m.Contains("Unavailable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>把工具 JSON 轉成使用者看得懂的回覆（永遠不回 JSON）。</summary>
    public static string FormatNaturalReply(string intent, string toolJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolJson);
            var root = doc.RootElement;
            return intent switch
            {
                "course" => FormatCourse(root),
                "scholarship" => FormatScholarship(root),
                "activity" => FormatActivity(root),
                "campus_qa" => FormatDocs(root),
                "advisee" => FormatAdvisees(root),
                "threshold" => FormatThreshold(root),
                "stats" => FormatStats(root),
                "decision" => FormatDecision(root),
                _ => "已完成查詢，若需要更細節可以再問我。"
            };
        }
        catch
        {
            return "已完成校務資料查詢。若結果不完整，請換個問法再試一次。";
        }
    }

    private static string FormatCourse(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("依你的修課紀錄，建議如下：");
        if (root.TryGetProperty("plan", out var plan) && plan.ValueKind == JsonValueKind.Array && plan.GetArrayLength() > 0)
        {
            sb.AppendLine("\n【建議課表（已避開衝堂）】");
            foreach (var c in plan.EnumerateArray())
                sb.AppendLine($"• {Str(c, "name")}｜{Str(c, "dayTime")}｜{Num(c, "credits")} 學分");
            if (root.TryGetProperty("totalCredits", out var tc))
                sb.AppendLine($"合計約 {tc.GetInt32()} 學分。");
        }
        else if (root.TryGetProperty("candidates", out var cand) && cand.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("\n【可修課程】");
            foreach (var c in cand.EnumerateArray().Take(5))
                sb.AppendLine($"• {Str(c, "name")}｜{Str(c, "dayTime")}｜{Num(c, "credits")} 學分");
        }
        sb.AppendLine("\n需要的話，我可以再幫你調整時段或學分數。");
        return sb.ToString().Trim();
    }

    private static string FormatScholarship(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("依你的條件，目前適合的獎學金：");
        if (root.TryGetProperty("eligible", out var eligible) && eligible.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in eligible.EnumerateArray())
                sb.AppendLine($"• {Str(s, "name")}（門檻 GPA {NumD(s, "minGpa")}，截止 {Str(s, "deadline")}）");
        }
        if (root.TryGetProperty("drafts", out var drafts) && drafts.ValueKind == JsonValueKind.Array && drafts.GetArrayLength() > 0)
        {
            sb.AppendLine("\n申請狀態：");
            foreach (var d in drafts.EnumerateArray())
            {
                var status = Str(d, "status");
                sb.AppendLine($"• {Str(d, "scholarshipName")}：{status}");
                if (d.TryGetProperty("missingDocuments", out var miss) && miss.ValueKind == JsonValueKind.Array && miss.GetArrayLength() > 0)
                    sb.AppendLine($"  缺件：{string.Join("、", miss.EnumerateArray().Select(x => x.GetString()))}");
            }
        }
        return sb.ToString().Trim();
    }

    private static string FormatActivity(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("依你的興趣，推薦這些活動：");
        if (root.TryGetProperty("recommendations", out var rec) && rec.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in rec.EnumerateArray())
                sb.AppendLine($"• {Str(a, "name")}（{Str(a, "category")}，{Str(a, "date")}）— {Str(a, "description")}");
        }
        if (root.TryGetProperty("registrations", out var reg) && reg.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("\n報名結果：");
            foreach (var r in reg.EnumerateArray())
                sb.AppendLine($"• {Str(r, "name")} → {Str(r, "registrationStatus")}");
        }
        return sb.ToString().Trim();
    }

    private static string FormatDocs(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("依校內文件整理如下：");
        if (root.TryGetProperty("documents", out var docs) && docs.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in docs.EnumerateArray())
                sb.AppendLine($"\n【{Str(d, "title")}】\n{Str(d, "content")}");
        }
        return sb.ToString().Trim();
    }

    private static string FormatAdvisees(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("導生關懷清單：");
        var arr = root.ValueKind == JsonValueKind.Array ? root : default;
        if (arr.ValueKind != JsonValueKind.Array) return "目前沒有導生預警資料。";
        foreach (var s in arr.EnumerateArray())
            sb.AppendLine($"• {Str(s, "name")}（GPA {NumD(s, "gpa")}）— {Str(s, "risk")}：{Str(s, "note")}");
        return sb.ToString().Trim();
    }

    private static string FormatThreshold(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("門檻提醒：");
        if (root.TryGetProperty("items", out var items))
            foreach (var i in items.EnumerateArray())
                sb.AppendLine($"• {Str(i, "student")}｜{Str(i, "item")}｜截止 {Str(i, "due")}｜{Str(i, "status")}");
        return sb.ToString().Trim();
    }

    private static string FormatStats(JsonElement root)
    {
        return $"""
校務統計摘要：
• 示範學生數：{Num(root, "enrolledStudents")}
• 開放獎學金：{Num(root, "openScholarships")}
• 開課數：{Num(root, "openCourses")}
• 近期活動：{Num(root, "upcomingActivities")}
• 平均 GPA：{NumD(root, "avgGpa")}
• 清寒比例：{NumD(root, "financialNeedRatio")}%
""".Trim();
    }

    private static string FormatDecision(JsonElement root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("決策支援建議：");
        if (root.TryGetProperty("recommendations", out var recs))
            foreach (var r in recs.EnumerateArray())
                sb.AppendLine($"• {r.GetString()}");
        return sb.ToString().Trim();
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ToString() : "";
    private static int Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;
    private static double NumD(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.TryGetDouble(out var n) ? n : 0;

    private static bool Contains(string input, params string[] keys) =>
        keys.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
