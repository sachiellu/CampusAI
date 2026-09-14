using System.Text.RegularExpressions;
using CampusAI.Api.Models;
using CampusAI.Api.Services.CampusApis;

namespace CampusAI.Api.Services.Agents;

public sealed class AgentContext
{
    public required string Role { get; init; }
    public required string Feature { get; init; }
    public required string Message { get; init; }
    public string? StudentId { get; init; }
    public int Stage { get; init; }
    public List<AgentAction> Actions { get; } = [];
}

public interface ICampusAgent
{
    string Name { get; }
}

public sealed record ScholarshipFilterResult(StudentProfile Student, List<Scholarship> Eligible);
public sealed record CourseFilterResult(StudentProfile Student, List<Course> Candidates);
public sealed record ActivityRankResult(StudentProfile Student, List<CampusActivity> Recommendations);

/// <summary>推薦 Agent</summary>
public sealed class RecommendAgent(
    StudentDataApi students,
    CourseDataApi courses,
    ActivityDataApi activities,
    CampusDataApi campus) : ICampusAgent
{
    public string Name => "推薦 Agent";

    public StudentProfile RequireStudent(string? id) =>
        students.GetStudent(id) ?? throw new InvalidOperationException("此操作需要先選擇示範學生。");

    public StudentProfile ReadProfile(AgentContext ctx)
    {
        var s = RequireStudent(ctx.StudentId);
        ctx.Actions.Add(new(Name, "讀取學生檔案", $"{s.Name}｜{s.Department}｜GPA {s.Gpa:0.00}"));
        return s;
    }

    public ScholarshipFilterResult FilterScholarships(AgentContext ctx)
    {
        var student = RequireStudent(ctx.StudentId);
        ctx.Actions.Add(new(Name, "獎學金資格過濾", ctx.Stage >= 3 ? "推薦＋申請草稿" : "僅推薦"));
        var eligible = campus.ListScholarships().Where(s =>
        {
            if (student.Gpa < s.MinGpa) return false;
            if (s.NeedBased && !student.HasFinancialNeed) return false;
            if (s.Departments is { Count: > 0 } && !s.Departments.Contains(student.Department)) return false;
            return true;
        }).ToList();
        return new ScholarshipFilterResult(student, eligible);
    }

    public CourseFilterResult FilterCourses(AgentContext ctx)
    {
        var student = RequireStudent(ctx.StudentId);
        ctx.Actions.Add(new(Name, "先修檢查", "依修課紀錄過濾課程"));
        var candidates = courses.ListCourses()
            .Where(c => c.Department == student.Department || c.Department == "通識")
            .Where(c => c.Prerequisites.All(p => student.CompletedCourses.Contains(p)))
            .Where(c => !student.CompletedCourses.Contains(c.Name))
            .ToList();
        return new CourseFilterResult(student, candidates);
    }

    public ActivityRankResult RankActivities(AgentContext ctx)
    {
        var student = RequireStudent(ctx.StudentId);
        ctx.Actions.Add(new(Name, "活動興趣比對", "產出個人化推薦"));
        var top = activities.ListActivities()
            .OrderByDescending(a => student.Interests.Any(i =>
                a.Category.Contains(i, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(i, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(i, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(a => a.Date)
            .Take(3)
            .ToList();
        return new ActivityRankResult(student, top);
    }
}

/// <summary>規劃 Agent</summary>
public sealed class PlanningAgent : ICampusAgent
{
    public string Name => "規劃 Agent";

    public object BuildConflictFreePlan(IReadOnlyList<Course> candidates, AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "衝堂檢查與課表規劃", "已產出無衝突課表"));
        var plan = new List<Course>();
        foreach (var c in candidates)
        {
            if (plan.Any(p => Conflict(p.DayTime, c.DayTime))) continue;
            plan.Add(c);
            if (plan.Sum(x => x.Credits) >= 9) break;
        }
        return new
        {
            mode = "plan_schedule",
            candidates,
            plan,
            totalCredits = plan.Sum(x => x.Credits)
        };
    }

    private static bool Conflict(string a, string b)
    {
        var dayA = a.Split(' ')[0];
        var dayB = b.Split(' ')[0];
        if (!dayA.Equals(dayB, StringComparison.Ordinal)) return false;
        static (int start, int end) Parse(string slot)
        {
            var m = Regex.Match(slot, @"(\d{2}):(\d{2})-(\d{2}):(\d{2})");
            if (!m.Success) return (0, 0);
            return (int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[3].Value) * 60 + int.Parse(m.Groups[4].Value));
        }
        var (s1, e1) = Parse(a);
        var (s2, e2) = Parse(b);
        return s1 < e2 && s2 < e1;
    }
}

/// <summary>申請 Agent</summary>
public sealed class ApplicationAgent(ApplicationServiceApi apps) : ICampusAgent
{
    public string Name => "申請 Agent";

    public List<object> ScholarshipDrafts(StudentProfile student, IEnumerable<Scholarship> eligible, AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "產生申請草稿／缺件檢查", "已呼叫申請服務 API"));
        return eligible.Select(s => (object)apps.CreateScholarshipDraft(student, s)).ToList();
    }

    public List<object> ActivityRegistrations(IEnumerable<CampusActivity> list, AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "活動報名 API", "已呼叫申請／報名服務"));
        return list.Select(a => (object)apps.RegisterActivity(a)).ToList();
    }
}

/// <summary>溝通 Agent</summary>
public sealed class CommunicationAgent(EmailNotificationApi mail, CalendarApi calendar) : ICampusAgent
{
    public string Name => "溝通 Agent";

    public object SyncCalendarAndNotify(string title, string when, AgentContext ctx)
    {
        var cal = calendar.AddEvent(title, when);
        var note = mail.Notify("in_app", title);
        ctx.Actions.Add(new(Name, "行事曆／通知", "已同步 Calendar／Notification API"));
        return new { calendar = cal, notification = note };
    }
}

/// <summary>提醒／追蹤 Agent</summary>
public sealed class ReminderTrackingAgent(CalendarApi calendar) : ICampusAgent
{
    public string Name => "提醒／追蹤 Agent";

    public object CourseReminders(AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "設定選課提醒", "已寫入行事曆 API"));
        return calendar.AddEvent("選課開放提醒", "下學期選課週");
    }

    public object AdviseeFollowUp(AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "產生關懷清單", "可排入約談行程"));
        return new { followUp = true };
    }

    public object ThresholdItems(AgentContext ctx)
    {
        ctx.Actions.Add(new(Name, "畢業／成績門檻提醒", "已產出待追蹤項目"));
        return new
        {
            items = new[]
            {
                new { student = "林小芸", item = "專題研究(一)", due = "2026-10-31", status = "待確認指導教授" },
                new { student = "陳大偉", item = "最低 GPA 3.0", due = "本學期期末", status = "目前 3.15，接近門檻" },
                new { student = "王雅婷", item = "畢業展演時數", due = "2026-12-15", status = "已完成 80%" }
            }
        };
    }
}

/// <summary>資料分析 Agent</summary>
public sealed class DataAnalysisAgent(StudentDataApi students, CampusDataApi campus) : ICampusAgent
{
    public string Name => "資料分析 Agent";

    public object AdviseeAlerts(AgentContext ctx)
    {
        var data = students.ListStudents().Select(s => new
        {
            s.Id,
            s.Name,
            s.Department,
            s.Year,
            s.Gpa,
            risk = s.Gpa < 3.2 ? "成績預警" : s.Year >= 4 ? "畢業門檻追蹤" : "穩定",
            note = s.Gpa < 3.2
                ? "建議約談並確認修課負荷"
                : s.HasFinancialNeed ? "具清寒資格，可關注獎助資源" : "定期關懷即可"
        }).ToList();
        ctx.Actions.Add(new(Name, "導生預警掃描", "已彙整導生風險與關懷建議", data));
        return data;
    }

    public object CampusStats(AgentContext ctx)
    {
        var data = campus.Stats();
        ctx.Actions.Add(new(Name, "校務指標彙整", "學生／獎助／課程／活動統計", data));
        return data;
    }

    public object DecisionSupport(AgentContext ctx)
    {
        var data = new
        {
            recommendations = new[]
            {
                "清寒助學金申請截止接近，建議加開諮詢時段。",
                "資工系衝堂熱點集中在週二上午，可評估調整開課時段。",
                "活動報名率以音樂／志工類較高，可優先配置場地資源。"
            },
            metrics = campus.Stats()
        };
        ctx.Actions.Add(new(Name, "決策支援建議", "依校務指標產出行動建議", data));
        return data;
    }
}
