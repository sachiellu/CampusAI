using System.Text.Json;
using CampusAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusAI.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ChatConversationEntity> ChatConversations => Set<ChatConversationEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<ScholarshipApplicationEntity> ScholarshipApplications => Set<ScholarshipApplicationEntity>();
    public DbSet<StudentEntity> Students => Set<StudentEntity>();
    public DbSet<StudentCompletedCourseEntity> StudentCompletedCourses => Set<StudentCompletedCourseEntity>();
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();
    public DbSet<ScholarshipEntity> Scholarships => Set<ScholarshipEntity>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<CampusDocumentEntity> CampusDocuments => Set<CampusDocumentEntity>();
    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
        });
        modelBuilder.Entity<ChatConversationEntity>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Feature).HasMaxLength(64);
        });
        modelBuilder.Entity<ChatMessageEntity>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Feature, x.CreatedAtUtc });
            e.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<StudentEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Department).HasMaxLength(100);
            e.HasMany(x => x.CompletedCourses)
                .WithOne(x => x.Student!)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StudentCompletedCourseEntity>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.CourseName }).IsUnique();
            e.Property(x => x.CourseName).HasMaxLength(100);
            e.Property(x => x.StudentId).HasMaxLength(32);
        });
        modelBuilder.Entity<CourseEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Department).HasMaxLength(100);
            e.Property(x => x.DayTime).HasMaxLength(80);
        });
        modelBuilder.Entity<ScholarshipEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(120);
        });
        modelBuilder.Entity<ActivityEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Category).HasMaxLength(60);
        });
        modelBuilder.Entity<CampusDocumentEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(60);
        });
        modelBuilder.Entity<CalendarEventEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(200);
        });
        modelBuilder.Entity<NotificationEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(32);
            e.Property(x => x.Subject).HasMaxLength(200);
        });
    }
}

public static class CampusEntityMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<string> ReadList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string WriteList(IEnumerable<string>? items) =>
        JsonSerializer.Serialize(items?.ToList() ?? [], JsonOpts);

    public static StudentProfile ToProfile(this StudentEntity e) =>
        new(
            e.Id,
            e.Name,
            e.Department,
            e.Year,
            e.Gpa,
            e.CompletedCourses.Select(c => c.CourseName).OrderBy(x => x).ToList(),
            ReadList(e.InterestsJson),
            e.HasFinancialNeed);

    public static Course ToModel(this CourseEntity e) =>
        new(e.Id, e.Name, e.Department, e.Credits, e.DayTime, ReadList(e.PrerequisitesJson));

    public static Scholarship ToModel(this ScholarshipEntity e) =>
        new(
            e.Id,
            e.Name,
            e.Description,
            e.MinGpa,
            string.IsNullOrWhiteSpace(e.DepartmentsJson) ? null : ReadList(e.DepartmentsJson),
            e.NeedBased,
            e.Deadline,
            ReadList(e.RequiredDocumentsJson));

    public static CampusActivity ToModel(this ActivityEntity e) =>
        new(e.Id, e.Name, e.Category, e.Description, e.Date, e.Capacity, e.Registered);

    public static CampusDocument ToModel(this CampusDocumentEntity e) =>
        new(e.Id, e.Title, e.Category, e.Content);
}
