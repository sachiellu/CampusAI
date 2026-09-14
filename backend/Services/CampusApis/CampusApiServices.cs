using CampusAI.Api.Data;
using CampusAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusAI.Api.Services.CampusApis;

/// <summary>API 服務層：校務與工具 API（讀 SQL Server 正式資料表）。</summary>
public sealed class StudentDataApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/students";

    public StudentProfile? GetStudent(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var db = dbFactory.CreateDbContext();
        var e = db.Students.AsNoTracking()
            .Include(s => s.CompletedCourses)
            .FirstOrDefault(s => s.Id == id);
        return e?.ToProfile();
    }

    public IReadOnlyList<StudentProfile> ListStudents()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Students.AsNoTracking()
            .Include(s => s.CompletedCourses)
            .OrderBy(s => s.Id)
            .AsEnumerable()
            .Select(s => s.ToProfile())
            .ToList();
    }
}

public sealed class CourseDataApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/courses";

    public IReadOnlyList<Course> ListCourses()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Courses.AsNoTracking()
            .OrderBy(c => c.Id)
            .AsEnumerable()
            .Select(c => c.ToModel())
            .ToList();
    }
}

public sealed class ActivityDataApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/activities";

    public IReadOnlyList<CampusActivity> ListActivities()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Activities.AsNoTracking()
            .OrderBy(a => a.Date)
            .AsEnumerable()
            .Select(a => a.ToModel())
            .ToList();
    }
}

public sealed class ApplicationServiceApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/applications";

    public object CreateScholarshipDraft(StudentProfile student, Scholarship s)
    {
        var missing = s.RequiredDocuments.Where(doc => doc is not ("申請表" or "成績單")).ToList();
        return new
        {
            scholarshipId = s.Id,
            scholarshipName = s.Name,
            draft = $"申請人：{student.Name}\n學號：{student.Id}\n系級：{student.Department} {student.Year} 年級\nGPA：{student.Gpa:0.00}\n申請項目：{s.Name}\n申請理由：依個人學習表現與條件提出申請。",
            missingDocuments = missing,
            status = missing.Count == 0 ? "可送出" : "缺件"
        };
    }

    public object RegisterActivity(CampusActivity a) => new
    {
        a.Id,
        a.Name,
        a.Date,
        registrationStatus = a.Registered < a.Capacity ? "報名成功（已寫入申請服務）" : "額滿"
    };

    public object Catalog()
    {
        using var db = dbFactory.CreateDbContext();
        var scholarships = db.Scholarships.AsNoTracking()
            .OrderBy(s => s.Deadline)
            .Select(s => new { s.Id, s.Name, s.Deadline, s.MinGpa })
            .ToList();
        return new
        {
            endpoint = Route,
            note = "申請服務 API：獎學金草稿／活動報名；資料來自 SQL Server",
            scholarships
        };
    }
}

public sealed class EmailNotificationApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/notifications";

    public object Notify(string channel, string subject)
    {
        using var db = dbFactory.CreateDbContext();
        var item = new NotificationEntity
        {
            Id = $"N{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Channel = channel,
            Subject = subject,
            Status = "queued",
            AtUtc = DateTime.UtcNow
        };
        db.Notifications.Add(item);
        db.SaveChanges();
        return new { item.Id, item.Channel, item.Subject, status = item.Status, at = item.AtUtc.ToString("o") };
    }

    public IReadOnlyList<object> List()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.AtUtc)
            .Take(50)
            .Select(n => (object)new { n.Id, n.Channel, n.Subject, status = n.Status, at = n.AtUtc })
            .ToList();
    }
}

public sealed class CalendarApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/calendar";

    public object AddEvent(string title, string when)
    {
        using var db = dbFactory.CreateDbContext();
        var item = new CalendarEventEntity
        {
            Id = $"E{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Title = title,
            WhenText = when,
            Synced = true,
            Provider = "Calendar API（SQL Server）"
        };
        db.CalendarEvents.Add(item);
        db.SaveChanges();
        return new { item.Id, title = item.Title, when = item.WhenText, item.Synced, provider = item.Provider };
    }

    public IReadOnlyList<object> List()
    {
        using var db = dbFactory.CreateDbContext();
        return db.CalendarEvents.AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => (object)new { e.Id, title = e.Title, when = e.WhenText, synced = e.Synced, provider = e.Provider })
            .ToList();
    }
}

public sealed class CampusDataApi(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string Route = "/api/services/campus";

    public IReadOnlyList<Scholarship> ListScholarships()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Scholarships.AsNoTracking()
            .OrderBy(s => s.Deadline)
            .AsEnumerable()
            .Select(s => s.ToModel())
            .ToList();
    }

    public object Stats()
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
}

public sealed class PlatformServicesApi
{
    public const string Route = "/api/services/platform";

    public object Identity(string role, string? studentId) => new
    {
        authenticated = true,
        role,
        studentId,
        layer = "平台服務／身分驗證"
    };

    public bool CanAccess(string role, string feature) => role switch
    {
        "teacher" => feature is "qa" or "scholarship",
        "admin" => feature is "qa" or "scholarship",
        _ => true
    };

    public object Permission(string role) => new
    {
        role,
        canChatFeatures = role == "student"
            ? new[] { "qa", "scholarship", "course", "activity" }
            : new[] { "qa", "scholarship" },
        layer = "平台服務／權限控管"
    };

    public object ToolCalling() => new
    {
        status = "ready",
        reserved = true,
        layer = "平台服務／Tool Calling"
    };

    public object ToolCallingMeta(string tool) => new { tool, via = "Tool Calling API", ok = true };

    public object Snapshot(string role, string? studentId) => new
    {
        endpoint = Route,
        identity = Identity(role, studentId),
        permission = Permission(role),
        toolCalling = ToolCalling(),
        note = "平台服務頁：身分驗證／權限控管／Tool Calling（可接正式 IdP）"
    };
}
