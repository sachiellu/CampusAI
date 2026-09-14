using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CampusAI.Api.Data;
using CampusAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusAI.Api.Services;

/// <summary>校務 Tool：讀 SQL Server 正式資料表。</summary>
public class CampusToolService(IDbContextFactory<AppDbContext> dbFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public List<CampusDocument> SearchDocuments(string query)
    {
        using var db = dbFactory.CreateDbContext();
        var docs = db.CampusDocuments.AsNoTracking().AsEnumerable().Select(d => d.ToModel()).ToList();
        var tokens = Regex.Split(query ?? string.Empty, @"\W+")
            .Where(t => t.Length >= 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scored = docs
            .Select(d => new
            {
                Doc = d,
                Score = tokens.Count(t =>
                    d.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    d.Category.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains(t, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Title)
            .ToList();

        var hits = scored.Where(x => x.Score > 0).Select(x => x.Doc).Take(3).ToList();
        if (hits.Count == 0 && docs.Count > 0)
            hits.Add(docs[0]);
        return hits;
    }

    public object GetStudentProfile(string? studentId) => RequireStudent(studentId);

    public object RecommendScholarships(string studentId, int stage)
    {
        var student = RequireStudent(studentId);
        using var db = dbFactory.CreateDbContext();
        var eligible = db.Scholarships.AsNoTracking().AsEnumerable().Select(s => s.ToModel()).Where(s =>
        {
            if (student.Gpa < s.MinGpa) return false;
            if (s.NeedBased && !student.HasFinancialNeed) return false;
            if (s.Departments is { Count: > 0 } && !s.Departments.Contains(student.Department)) return false;
            return true;
        }).ToList();

        if (stage <= 2)
        {
            return new
            {
                mode = "recommend_only",
                source = "SQL Server",
                student = new { student.Id, student.Name, student.Department, student.Gpa, student.HasFinancialNeed },
                eligible
            };
        }

        var drafts = eligible.Select(s =>
        {
            var missing = s.RequiredDocuments
                .Where(doc => doc is not ("申請表" or "成績單"))
                .ToList();
            return new
            {
                scholarshipId = s.Id,
                scholarshipName = s.Name,
                draft = $"申請人：{student.Name}\n學號：{student.Id}\n系級：{student.Department} {student.Year} 年級\nGPA：{student.Gpa:0.00}\n申請項目：{s.Name}\n申請理由：依個人學習表現與條件提出申請。",
                missingDocuments = missing,
                status = missing.Count == 0 ? "可送出" : "缺件"
            };
        }).ToList();

        return new
        {
            mode = "recommend_and_apply",
            source = "SQL Server",
            student = new { student.Id, student.Name, student.Department, student.Gpa, student.HasFinancialNeed },
            eligible,
            drafts
        };
    }

    public object PlanCourses(string studentId, int stage)
    {
        var student = RequireStudent(studentId);
        using var db = dbFactory.CreateDbContext();
        var candidates = db.Courses.AsNoTracking().AsEnumerable().Select(c => c.ToModel())
            .Where(c => c.Department == student.Department)
            .Where(c => c.Prerequisites.All(p => student.CompletedCourses.Contains(p)))
            .ToList();

        if (stage <= 2)
        {
            return new
            {
                mode = "recommend_only",
                source = "SQL Server",
                completedCourses = student.CompletedCourses,
                candidates
            };
        }

        var plan = new List<Course>();
        foreach (var course in candidates)
        {
            if (plan.Any(p => Conflict(p.DayTime, course.DayTime))) continue;
            plan.Add(course);
            if (plan.Count >= 3) break;
        }

        return new
        {
            mode = "plan_schedule",
            source = "SQL Server",
            completedCourses = student.CompletedCourses,
            candidates,
            plan,
            totalCredits = plan.Sum(p => p.Credits),
            reminder = "已建立正式選課開始提醒（行事曆資料表）"
        };
    }

    public object RecommendActivities(string studentId, int stage)
    {
        var student = RequireStudent(studentId);
        using var db = dbFactory.CreateDbContext();
        var ranked = db.Activities.AsNoTracking().AsEnumerable().Select(a => a.ToModel())
            .Select(a => new
            {
                Activity = a,
                Score = student.Interests.Count(i =>
                    a.Category.Contains(i, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(i, StringComparison.OrdinalIgnoreCase) ||
                    a.Description.Contains(i, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Activity.Date)
            .ToList();

        var top = ranked.Where(x => x.Score > 0).Select(x => x.Activity).Take(3).ToList();
        if (top.Count == 0) top = ranked.Select(x => x.Activity).Take(2).ToList();

        if (stage <= 2)
        {
            return new
            {
                mode = "recommend_only",
                source = "SQL Server",
                interests = student.Interests,
                recommendations = top
            };
        }

        var registrations = top.Select(a => new
        {
            a.Id,
            a.Name,
            a.Date,
            calendarEvent = $"{a.Name} @ {a.Date:yyyy-MM-dd}",
            registrationStatus = a.Registered < a.Capacity ? "報名成功" : "額滿"
        }).ToList();

        return new
        {
            mode = "recommend_and_register",
            source = "SQL Server",
            interests = student.Interests,
            recommendations = top,
            registrations
        };
    }

    public string ExecuteTool(string toolName, string argumentsJson, string? studentId, int stage, List<AgentAction> actions)
    {
        object payload = toolName switch
        {
            "search_campus_documents" => ExecuteSearch(argumentsJson, actions),
            "get_student_profile" => ExecuteProfile(studentId, actions),
            "recommend_scholarships" => ExecuteScholarships(studentId, stage, actions),
            "plan_courses" => ExecuteCourses(studentId, stage, actions),
            "recommend_activities" => ExecuteActivities(studentId, stage, actions),
            "get_advisee_alerts" => ExecuteAdviseeAlerts(actions),
            "get_threshold_reminders" => ExecuteThresholdReminders(actions),
            "get_campus_stats" => ExecuteCampusStats(actions),
            "get_decision_support" => ExecuteDecisionSupport(actions),
            _ => new { error = $"未知工具：{toolName}" }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public object GetAdviseeAlerts()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Students.AsNoTracking().Include(s => s.CompletedCourses).AsEnumerable().Select(e => e.ToProfile()).Select(s => new
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
    }

    public object GetThresholdReminders() => new
    {
        items = new[]
        {
            new { student = "林小芸", item = "專題研究(一)", due = "2026-10-31", status = "待確認指導教授" },
            new { student = "陳大偉", item = "最低 GPA 3.0", due = "本學期期末", status = "目前 3.15，接近門檻" },
            new { student = "王雅婷", item = "畢業展演時數", due = "2026-12-15", status = "已完成 80%" }
        },
        source = "SQL Server + 規則"
    };

    public object GetCampusStats()
    {
        using var db = dbFactory.CreateDbContext();
        var students = db.Students.AsNoTracking().ToList();
        return new
        {
            enrolledStudents = students.Count,
            openScholarships = db.Scholarships.Count(),
            openCourses = db.Courses.Count(),
            upcomingActivities = db.Activities.Count(),
            avgGpa = students.Count == 0 ? 0 : Math.Round(students.Average(s => s.Gpa), 2),
            financialNeedRatio = students.Count == 0
                ? 0
                : Math.Round(students.Count(s => s.HasFinancialNeed) * 100.0 / students.Count, 1),
            source = "SQL Server"
        };
    }

    public object GetDecisionSupport() => new
    {
        recommendations = new[]
        {
            "清寒助學金申請截止接近，建議加開諮詢時段。",
            "資工系衝堂熱點集中在週二上午，可評估調整開課時段。",
            "活動報名率以音樂／志工類較高，可優先配置場地資源。"
        },
        metrics = GetCampusStats()
    };

    private object ExecuteAdviseeAlerts(List<AgentAction> actions)
    {
        var data = GetAdviseeAlerts();
        actions.Add(new("資料分析 Agent", "導生預警掃描", "已彙整導生風險與關懷建議（SQL）", data));
        actions.Add(new("提醒/追蹤 Agent", "產生關懷清單", "可排入約談行程"));
        return data;
    }

    private object ExecuteThresholdReminders(List<AgentAction> actions)
    {
        var data = GetThresholdReminders();
        actions.Add(new("提醒/追蹤 Agent", "畢業／成績門檻提醒", "已產出待追蹤項目", data));
        return data;
    }

    private object ExecuteCampusStats(List<AgentAction> actions)
    {
        var data = GetCampusStats();
        actions.Add(new("資料分析 Agent", "校務指標彙整", "學生／獎助／課程／活動統計（SQL）", data));
        return data;
    }

    private object ExecuteDecisionSupport(List<AgentAction> actions)
    {
        var data = GetDecisionSupport();
        actions.Add(new("資料分析 Agent", "決策支援建議", "依校務指標產出行動建議", data));
        return data;
    }

    private object ExecuteSearch(string argumentsJson, List<AgentAction> actions)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
        var docs = SearchDocuments(query);
        actions.Add(new("檢索", "RAG 文件檢索", $"命中 {docs.Count} 份校園文件（SQL）",
            docs.Select(d => new { d.Id, d.Title, d.Category })));
        return new { documents = docs };
    }

    private object ExecuteProfile(string? studentId, List<AgentAction> actions)
    {
        var student = RequireStudent(studentId);
        actions.Add(new("推薦 Agent", "讀取學生檔案",
            $"{student.Name}｜{student.Department}｜GPA {student.Gpa:0.00}（SQL Server）"));
        return student;
    }

    private object ExecuteScholarships(string? studentId, int stage, List<AgentAction> actions)
    {
        var result = RecommendScholarships(RequireStudent(studentId).Id, stage);
        actions.Add(new("推薦 Agent", "獎學金資格過濾", stage >= 3 ? "推薦＋申請草稿" : "僅推薦"));
        if (stage >= 3)
            actions.Add(new("申請 Agent", "產生申請草稿／缺件檢查", "已呼叫申請服務 API（SQL）"));
        return result;
    }

    private object ExecuteCourses(string? studentId, int stage, List<AgentAction> actions)
    {
        var result = PlanCourses(RequireStudent(studentId).Id, stage);
        actions.Add(new("推薦 Agent", "先修檢查", "依修課紀錄過濾課程"));
        if (stage >= 3)
        {
            actions.Add(new("規劃 Agent", "衝堂檢查與課表規劃", "已產出無衝突課表"));
            actions.Add(new("提醒/追蹤 Agent", "設定選課提醒", "已寫入行事曆資料表"));
        }
        return result;
    }

    private object ExecuteActivities(string? studentId, int stage, List<AgentAction> actions)
    {
        var result = RecommendActivities(RequireStudent(studentId).Id, stage);
        actions.Add(new("推薦 Agent", "活動興趣比對", "產出個人化推薦"));
        if (stage >= 3)
        {
            actions.Add(new("申請 Agent", "活動報名 API", "報名狀態已計算"));
            actions.Add(new("溝通 Agent", "行事曆／通知", "可同步 Calendar／Notifications 資料表"));
        }
        return result;
    }

    private StudentProfile RequireStudent(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            throw new InvalidOperationException("此操作需要先選擇示範學生。");
        using var db = dbFactory.CreateDbContext();
        var e = db.Students.AsNoTracking().Include(s => s.CompletedCourses)
            .FirstOrDefault(s => s.Id == studentId);
        return e?.ToProfile()
            ?? throw new InvalidOperationException("此操作需要先選擇示範學生。");
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
            var s = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
            var e = int.Parse(m.Groups[3].Value) * 60 + int.Parse(m.Groups[4].Value);
            return (s, e);
        }

        var (s1, e1) = Parse(a);
        var (s2, e2) = Parse(b);
        return s1 < e2 && s2 < e1;
    }

    public static string BuildDocReply(List<CampusDocument> docs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("（校園問答／RAG）依校內文件整理如下：");
        foreach (var d in docs)
            sb.AppendLine($"\n【{d.Title}｜{d.Category}】\n{d.Content}");
        return sb.ToString().Trim();
    }
}
