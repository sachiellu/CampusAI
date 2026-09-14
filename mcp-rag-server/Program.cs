using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5099");
builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1";

builder.Services.AddSingleton<CampusRagIndex>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapGet("/health", (CampusRagIndex index) => Results.Ok(new
{
    status = "ok",
    service = "CampusAI.McpRagServer",
    tools = new[] { "rag_retrieve" },
    documents = index.Count
}));
app.MapMcp();
app.Run();

public sealed class CampusDoc
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>簡易關鍵字 RAG（BM25 風格評分），作品集可 Demo 的檢索層。</summary>
public sealed class CampusRagIndex
{
    private readonly List<CampusDoc> _docs;

    public CampusRagIndex(IHostEnvironment env, ILogger<CampusRagIndex> logger)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "knowledge", "campus-docs.json")),
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "knowledge", "campus-docs.json"))
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            logger.LogWarning("找不到 campus-docs.json，使用內建文件");
            _docs = BuiltIn();
            return;
        }

        var json = File.ReadAllText(path);
        _docs = JsonSerializer.Deserialize<List<CampusDoc>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? BuiltIn();
        logger.LogInformation("RAG 知識庫載入 {Count} 份文件 from {Path}", _docs.Count, path);
    }

    public int Count => _docs.Count;

    public IReadOnlyList<object> Retrieve(string query, int topK = 3)
    {
        topK = Math.Clamp(topK, 1, 5);
        var tokens = Tokenize(query);

        var scored = _docs.Select(d =>
        {
            var score = tokens.Sum(t =>
            {
                var s = 0;
                if (d.Title.Contains(t, StringComparison.OrdinalIgnoreCase)) s += 3;
                if (d.Category.Contains(t, StringComparison.OrdinalIgnoreCase)) s += 2;
                if (d.Content.Contains(t, StringComparison.OrdinalIgnoreCase)) s += 1;
                return s;
            });
            return new { Doc = d, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.Doc.Title)
        .ToList();

        var hits = scored.Where(x => x.Score > 0).Take(topK).ToList();
        if (hits.Count == 0)
            hits = scored.Take(1).ToList();

        return hits.Select(h => (object)new
        {
            id = h.Doc.Id,
            title = h.Doc.Title,
            category = h.Doc.Category,
            content = h.Doc.Content,
            score = h.Score
        }).ToList();
    }

    private static List<string> Tokenize(string query)
    {
        var q = (query ?? string.Empty).Trim();
        var tokens = Regex.Split(q, @"\W+")
            .Where(t => t.Length >= 1)
            .ToList();

        // 中文常被切成整句；補 2-gram 提高命中（如「獎學金」）
        if (q.Length >= 2)
        {
            for (var i = 0; i < q.Length - 1; i++)
            {
                var gram = q.Substring(i, 2);
                if (!char.IsWhiteSpace(gram[0]) && !char.IsWhiteSpace(gram[1]))
                    tokens.Add(gram);
            }
        }

        return tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<CampusDoc> BuiltIn() =>
    [
        new()
        {
            Id = "D001",
            Title = "獎學金申請總則",
            Category = "規章",
            Content = "本校獎學金分為優秀獎學金、清寒助學金與系所獎學金。申請期間通常為每學期第 3–5 週。"
        }
    ];
}

[McpServerToolType]
public static class RagTools
{
    [McpServerTool(Name = "rag_retrieve"), Description("Campus RAG retrieval only. Returns top-k relevant campus policy documents for a user query.")]
    public static string RagRetrieve(
        CampusRagIndex index,
        [Description("Natural language query, e.g. scholarship application rules")] string query,
        [Description("Number of documents to return (1-5)")] int topK = 3)
    {
        var hits = index.Retrieve(query, topK);
        return JsonSerializer.Serialize(new
        {
            mode = "rag_retrieve",
            query,
            topK,
            documents = hits
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}
