using CampusAI.Api.Models;

namespace CampusAI.Api.Services;

/// <summary>無 API Key 時的規則引擎（仍可完整 Demo；資料來自 SQL Server）。</summary>
public class CampusAgentService(CampusToolService tools)
{
    public ChatResponse Handle(ChatRequest request)
    {
        var message = (request.Message ?? string.Empty).Trim();
        var stage = Math.Clamp(request.Stage, 1, 3);
        var intent = DetectIntent(message);
        var actions = new List<AgentAction>();
        var studentId = request.StudentId;

        if (stage == 1 || intent == "campus_qa")
        {
            var docs = tools.SearchDocuments(message);
            actions.Add(new("檢索", "RAG 文件檢索", $"命中 {docs.Count} 份校園文件（SQL）",
                docs.Select(d => new { d.Id, d.Title, d.Category })));
            return new ChatResponse(
                CampusToolService.BuildDocReply(docs), 1, "campus_qa", actions,
                new { documents = docs }, "rules", null);
        }

        if (string.IsNullOrWhiteSpace(studentId) && stage >= 2)
        {
            return new ChatResponse(
                "要進入個人化問答或助理模式，請先選擇一位示範學生（例如林小芸／陳大偉／王雅婷）。",
                stage, intent, actions, null, "rules", null);
        }

        return intent switch
        {
            "scholarship" => FromTool("recommend_scholarships", studentId!, stage, actions, "scholarship"),
            "course" => FromTool("plan_courses", studentId!, stage, actions, "course"),
            "activity" => FromTool("recommend_activities", studentId!, stage, actions, "activity"),
            _ => FromTool("get_student_profile", studentId!, stage, actions, "general")
        };
    }

    private ChatResponse FromTool(string tool, string studentId, int stage, List<AgentAction> actions, string intent)
    {
        var json = tools.ExecuteTool(tool, "{}", studentId, stage, actions);
        object? result = json;
        try { result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json); } catch { /* keep */ }
        var reply = intent switch
        {
            "scholarship" => "已依資料庫學生檔與獎學金條件整理資格與申請建議。",
            "course" => "已依資料庫修課紀錄與開課資料整理選課建議。",
            "activity" => "已依資料庫興趣與活動資料整理推薦。",
            _ => "已讀取資料庫中的學生檔案。"
        };
        return new ChatResponse(reply, stage, intent, actions, result, "rules", null);
    }

    private static string DetectIntent(string message)
    {
        if (ContainsAny(message, "獎學金", "助學", "書卷", "清寒")) return "scholarship";
        if (ContainsAny(message, "選課", "課表", "衝堂", "先修")) return "course";
        if (ContainsAny(message, "活動", "社團", "志工", "報名")) return "activity";
        if (ContainsAny(message, "規定", "辦法", "申請流程", "校規")) return "campus_qa";
        return "general";
    }

    private static bool ContainsAny(string text, params string[] keys) =>
        keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
