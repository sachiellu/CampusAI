namespace CampusAI.Api.Data;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "student";
    public string? StudentProfileId { get; set; }
    public string Subtitle { get; set; } = string.Empty;
}

public class ChatConversationEntity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Feature { get; set; } = "general";
    public string Title { get; set; } = "新對話";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ChatMessageEntity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public long? ConversationId { get; set; }
    public string Feature { get; set; } = "general";
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? ActionsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ScholarshipApplicationEntity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string ScholarshipId { get; set; } = string.Empty;
    public string ScholarshipName { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string DraftText { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class StudentEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public double Gpa { get; set; }
    public bool HasFinancialNeed { get; set; }
    public string InterestsJson { get; set; } = "[]";
    public List<StudentCompletedCourseEntity> CompletedCourses { get; set; } = [];
}

public class StudentCompletedCourseEntity
{
    public long Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public StudentEntity? Student { get; set; }
}

public class CourseEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string DayTime { get; set; } = string.Empty;
    public string PrerequisitesJson { get; set; } = "[]";
}

public class ScholarshipEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinGpa { get; set; }
    public string? DepartmentsJson { get; set; }
    public bool NeedBased { get; set; }
    public DateOnly Deadline { get; set; }
    public string RequiredDocumentsJson { get; set; } = "[]";
}

public class ActivityEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Capacity { get; set; }
    public int Registered { get; set; }
}

public class CampusDocumentEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CalendarEventEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string WhenText { get; set; } = string.Empty;
    public bool Synced { get; set; } = true;
    public string Provider { get; set; } = "Calendar API";
}

public class NotificationEntity
{
    public string Id { get; set; } = string.Empty;
    public string Channel { get; set; } = "in_app";
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "delivered";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
