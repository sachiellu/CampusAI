using System.Text.Json;
using CampusAI.Api.Models;
using CampusAI.Api.Services.CampusApis;

namespace CampusAI.Api.Services.Agents;

/// <summary>
/// Personal Agent / Orchestrator（架構圖「Agent 代理層」中樞）。
/// 依角色／功能調度 6 類 Agent；Agent 只透過 API 服務層取資料。
/// </summary>
public sealed class PersonalOrchestrator(
    RecommendAgent recommend,
    PlanningAgent planning,
    ApplicationAgent application,
    CommunicationAgent communication,
    ReminderTrackingAgent reminder,
    DataAnalysisAgent analysis,
    PlatformServicesApi platform)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public (string Intent, string ToolJson, List<AgentAction> Actions) Handle(
        string role,
        string feature,
        string message,
        string? studentId,
        int stage)
    {
        role = role.ToLowerInvariant();
        feature = string.IsNullOrWhiteSpace(feature) ? "general" : feature;
        var ctx = new AgentContext
        {
            Role = role,
            Feature = feature,
            Message = message,
            StudentId = studentId,
            Stage = stage
        };

        var id = platform.Identity(role, studentId);
        ctx.Actions.Add(new("Personal Orchestrator", "調度開始",
            $"role={role}, feature={feature}, platform=身分驗證／權限控管"));

        if (!platform.CanAccess(role, feature))
            throw new InvalidOperationException("此角色無法使用該功能");

        _ = id;

        if (role == "teacher")
        {
            if (feature is "scholarship" || Contains(message, "門檻", "畢業", "提醒"))
            {
                var data = reminder.ThresholdItems(ctx);
                return ("threshold", Ser(data), ctx.Actions);
            }

            var alerts = analysis.AdviseeAlerts(ctx);
            reminder.AdviseeFollowUp(ctx);
            return ("advisee", Ser(alerts), ctx.Actions);
        }

        if (role == "admin")
        {
            if (feature is "scholarship" || Contains(message, "決策", "建議", "資源"))
            {
                var data = analysis.DecisionSupport(ctx);
                return ("decision", Ser(data), ctx.Actions);
            }

            var stats = analysis.CampusStats(ctx);
            return ("stats", Ser(stats), ctx.Actions);
        }

        return feature switch
        {
            "scholarship" => ScholarshipFlow(ctx),
            "course" => CourseFlow(ctx),
            "activity" => ActivityFlow(ctx),
            "qa" => QaStub(ctx),
            _ when Contains(message, "獎學金", "助學") => ScholarshipFlow(ctx),
            _ when Contains(message, "選課", "課程", "衝堂") => CourseFlow(ctx),
            _ when Contains(message, "活動", "社團") => ActivityFlow(ctx),
            _ => QaStub(ctx)
        };
    }

    private static (string, string, List<AgentAction>) QaStub(AgentContext ctx)
    {
        ctx.Actions.Add(new("Personal Orchestrator", "導向 MCP RAG", "校園問答交由 MCP Server rag_retrieve"));
        return ("campus_qa", Ser(new { query = ctx.Message, route = "mcp_rag" }), ctx.Actions);
    }

    private (string, string, List<AgentAction>) ScholarshipFlow(AgentContext ctx)
    {
        _ = platform.ToolCallingMeta("recommend_scholarships");
        recommend.ReadProfile(ctx);
        var filtered = recommend.FilterScholarships(ctx);

        if (ctx.Stage <= 2)
        {
            var payload = new
            {
                mode = "recommend_only",
                student = new
                {
                    filtered.Student.Id,
                    filtered.Student.Name,
                    filtered.Student.Department,
                    filtered.Student.Gpa,
                    filtered.Student.HasFinancialNeed
                },
                eligible = filtered.Eligible
            };
            return ("scholarship", Ser(payload), ctx.Actions);
        }

        var drafts = application.ScholarshipDrafts(filtered.Student, filtered.Eligible, ctx);
        var payload3 = new
        {
            mode = "recommend_and_apply",
            student = new
            {
                filtered.Student.Id,
                filtered.Student.Name,
                filtered.Student.Department,
                filtered.Student.Gpa,
                filtered.Student.HasFinancialNeed
            },
            eligible = filtered.Eligible,
            drafts
        };
        return ("scholarship", Ser(payload3), ctx.Actions);
    }

    private (string, string, List<AgentAction>) CourseFlow(AgentContext ctx)
    {
        _ = platform.ToolCallingMeta("plan_courses");
        var filtered = recommend.FilterCourses(ctx);

        if (ctx.Stage <= 2)
        {
            var payload = new { mode = "recommend_only", candidates = filtered.Candidates };
            return ("course", Ser(payload), ctx.Actions);
        }

        var plan = planning.BuildConflictFreePlan(filtered.Candidates, ctx);
        reminder.CourseReminders(ctx);
        return ("course", Ser(plan), ctx.Actions);
    }

    private (string, string, List<AgentAction>) ActivityFlow(AgentContext ctx)
    {
        _ = platform.ToolCallingMeta("recommend_activities");
        var ranked = recommend.RankActivities(ctx);

        if (ctx.Stage <= 2)
        {
            var payload = new
            {
                mode = "recommend_only",
                interests = ranked.Student.Interests,
                recommendations = ranked.Recommendations
            };
            return ("activity", Ser(payload), ctx.Actions);
        }

        var registrations = application.ActivityRegistrations(ranked.Recommendations, ctx);
        var when = ranked.Recommendations.FirstOrDefault()?.Date.ToString() ?? "";
        communication.SyncCalendarAndNotify("校園活動", when, ctx);
        var payload3 = new
        {
            mode = "recommend_and_register",
            interests = ranked.Student.Interests,
            recommendations = ranked.Recommendations,
            registrations
        };
        return ("activity", Ser(payload3), ctx.Actions);
    }

    private static string Ser(object payload) => JsonSerializer.Serialize(payload, JsonOptions);

    private static bool Contains(string text, params string[] keys) =>
        keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
