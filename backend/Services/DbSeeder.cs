using CampusAI.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusAI.Api.Services;

public static class DbSeeder
{
    public const string DemoPassword = "Passw0rd!";

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureConversationSchemaAsync(db);
        await EnsureCampusSchemaAsync(db);
        await MigrateLegacyMessagesAsync(db);
        await SeedUsersAsync(db);
        await SeedCampusDataAsync(db);
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        db.Users.AddRange(
            new AppUser
            {
                Username = "lin.yun",
                PasswordHash = hash,
                DisplayName = "林小芸",
                Role = "student",
                StudentProfileId = "S001",
                Subtitle = "資訊工程系｜GPA 3.72"
            },
            new AppUser
            {
                Username = "chen.wei",
                PasswordHash = hash,
                DisplayName = "陳大偉",
                Role = "student",
                StudentProfileId = "S002",
                Subtitle = "企業管理系｜GPA 3.15"
            },
            new AppUser
            {
                Username = "wang.ya",
                PasswordHash = hash,
                DisplayName = "王雅婷",
                Role = "student",
                StudentProfileId = "S003",
                Subtitle = "音樂系｜GPA 3.90"
            },
            new AppUser
            {
                Username = "advisor.chen",
                PasswordHash = hash,
                DisplayName = "陳導師",
                Role = "teacher",
                StudentProfileId = null,
                Subtitle = "資訊工程系｜導生關懷"
            },
            new AppUser
            {
                Username = "admin.lin",
                PasswordHash = hash,
                DisplayName = "林承辦",
                Role = "admin",
                StudentProfileId = null,
                Subtitle = "教務處｜校務研究"
            }
        );
        await db.SaveChangesAsync();
    }

    private static async Task EnsureCampusSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Students]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Students] (
                    [Id] nvarchar(32) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [Department] nvarchar(100) NOT NULL,
                    [Year] int NOT NULL,
                    [Gpa] float NOT NULL,
                    [HasFinancialNeed] bit NOT NULL,
                    [InterestsJson] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_Students] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[StudentCompletedCourses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[StudentCompletedCourses] (
                    [Id] bigint NOT NULL IDENTITY,
                    [StudentId] nvarchar(32) NOT NULL,
                    [CourseName] nvarchar(100) NOT NULL,
                    CONSTRAINT [PK_StudentCompletedCourses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_StudentCompletedCourses_Students] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[Students] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_StudentCompletedCourses_StudentId_CourseName]
                    ON [dbo].[StudentCompletedCourses] ([StudentId], [CourseName]);
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Courses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Courses] (
                    [Id] nvarchar(32) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [Department] nvarchar(100) NOT NULL,
                    [Credits] int NOT NULL,
                    [DayTime] nvarchar(80) NOT NULL,
                    [PrerequisitesJson] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_Courses] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Scholarships]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Scholarships] (
                    [Id] nvarchar(32) NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Description] nvarchar(max) NOT NULL,
                    [MinGpa] float NOT NULL,
                    [DepartmentsJson] nvarchar(max) NULL,
                    [NeedBased] bit NOT NULL,
                    [Deadline] date NOT NULL,
                    [RequiredDocumentsJson] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_Scholarships] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Activities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Activities] (
                    [Id] nvarchar(32) NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Category] nvarchar(60) NOT NULL,
                    [Description] nvarchar(max) NOT NULL,
                    [Date] date NOT NULL,
                    [Capacity] int NOT NULL,
                    [Registered] int NOT NULL,
                    CONSTRAINT [PK_Activities] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[CampusDocuments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CampusDocuments] (
                    [Id] nvarchar(32) NOT NULL,
                    [Title] nvarchar(200) NOT NULL,
                    [Category] nvarchar(60) NOT NULL,
                    [Content] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CampusDocuments] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[CalendarEvents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CalendarEvents] (
                    [Id] nvarchar(32) NOT NULL,
                    [Title] nvarchar(200) NOT NULL,
                    [WhenText] nvarchar(200) NOT NULL,
                    [Synced] bit NOT NULL,
                    [Provider] nvarchar(120) NOT NULL,
                    CONSTRAINT [PK_CalendarEvents] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Notifications] (
                    [Id] nvarchar(32) NOT NULL,
                    [Channel] nvarchar(40) NOT NULL,
                    [Subject] nvarchar(200) NOT NULL,
                    [Status] nvarchar(40) NOT NULL,
                    [AtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[ScholarshipApplications]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ScholarshipApplications] (
                    [Id] bigint NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ScholarshipId] nvarchar(max) NOT NULL,
                    [ScholarshipName] nvarchar(max) NOT NULL,
                    [Status] nvarchar(max) NOT NULL,
                    [DraftText] nvarchar(max) NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ScholarshipApplications] PRIMARY KEY ([Id])
                );
            END
            """);
    }

    private static async Task SeedCampusDataAsync(AppDbContext db)
    {
        if (await db.Students.AnyAsync()) return;

        static string J(params string[] items) => CampusEntityMapper.WriteList(items);

        db.Students.AddRange(
            new StudentEntity
            {
                Id = "S001",
                Name = "林小芸",
                Department = "資訊工程系",
                Year = 3,
                Gpa = 3.72,
                HasFinancialNeed = true,
                InterestsJson = J("程式設計", "音樂", "志工"),
                CompletedCourses =
                [
                    new() { CourseName = "程式設計" },
                    new() { CourseName = "資料結構" },
                    new() { CourseName = "離散數學" },
                    new() { CourseName = "線性代數" },
                    new() { CourseName = "作業系統" }
                ]
            },
            new StudentEntity
            {
                Id = "S002",
                Name = "陳大偉",
                Department = "企業管理系",
                Year = 2,
                Gpa = 3.15,
                HasFinancialNeed = false,
                InterestsJson = J("社團", "運動", "創業"),
                CompletedCourses =
                [
                    new() { CourseName = "會計學" },
                    new() { CourseName = "經濟學" },
                    new() { CourseName = "管理學" },
                    new() { CourseName = "統計學" }
                ]
            },
            new StudentEntity
            {
                Id = "S003",
                Name = "王雅婷",
                Department = "音樂系",
                Year = 4,
                Gpa = 3.90,
                HasFinancialNeed = true,
                InterestsJson = J("音樂", "展演", "教學"),
                CompletedCourses =
                [
                    new() { CourseName = "和聲學" },
                    new() { CourseName = "鋼琴主修" },
                    new() { CourseName = "音樂史" },
                    new() { CourseName = "配器法" }
                ]
            }
        );

        db.Courses.AddRange(
            new CourseEntity { Id = "C101", Name = "演算法", Department = "資訊工程系", Credits = 3, DayTime = "週二 09:00-12:00", PrerequisitesJson = J("資料結構") },
            new CourseEntity { Id = "C102", Name = "資料庫系統", Department = "資訊工程系", Credits = 3, DayTime = "週三 13:00-16:00", PrerequisitesJson = J("資料結構") },
            new CourseEntity { Id = "C103", Name = "機器學習導論", Department = "資訊工程系", Credits = 3, DayTime = "週二 10:00-13:00", PrerequisitesJson = J("線性代數", "程式設計") },
            new CourseEntity { Id = "C104", Name = "專題研究(一)", Department = "資訊工程系", Credits = 2, DayTime = "週五 14:00-16:00", PrerequisitesJson = J("作業系統") },
            new CourseEntity { Id = "C201", Name = "行銷管理", Department = "企業管理系", Credits = 3, DayTime = "週一 09:00-12:00", PrerequisitesJson = J("管理學") },
            new CourseEntity { Id = "C202", Name = "財務管理", Department = "企業管理系", Credits = 3, DayTime = "週三 09:00-12:00", PrerequisitesJson = J("會計學") },
            new CourseEntity { Id = "C301", Name = "室內樂", Department = "音樂系", Credits = 2, DayTime = "週四 15:00-17:00", PrerequisitesJson = J("鋼琴主修") },
            new CourseEntity { Id = "C302", Name = "音樂教學法", Department = "音樂系", Credits = 3, DayTime = "週一 13:00-16:00", PrerequisitesJson = J("音樂史") }
        );

        db.Scholarships.AddRange(
            new ScholarshipEntity
            {
                Id = "SCH001",
                Name = "優秀書卷獎",
                Description = "GPA 達 3.7 以上之大學部學生",
                MinGpa = 3.7,
                DepartmentsJson = null,
                NeedBased = false,
                Deadline = new DateOnly(2026, 10, 15),
                RequiredDocumentsJson = J("成績單", "申請表")
            },
            new ScholarshipEntity
            {
                Id = "SCH002",
                Name = "清寒助學金",
                Description = "經濟弱勢且 GPA 達 2.8 以上",
                MinGpa = 2.8,
                DepartmentsJson = null,
                NeedBased = true,
                Deadline = new DateOnly(2026, 10, 20),
                RequiredDocumentsJson = J("成績單", "清寒證明", "申請表")
            },
            new ScholarshipEntity
            {
                Id = "SCH003",
                Name = "資工系專題獎學金",
                Description = "資訊工程系學生且 GPA 達 3.5",
                MinGpa = 3.5,
                DepartmentsJson = J("資訊工程系"),
                NeedBased = false,
                Deadline = new DateOnly(2026, 11, 1),
                RequiredDocumentsJson = J("成績單", "專題計畫書", "申請表")
            },
            new ScholarshipEntity
            {
                Id = "SCH004",
                Name = "藝文卓越獎",
                Description = "音樂、美術相關科系 GPA 3.5 以上",
                MinGpa = 3.5,
                DepartmentsJson = J("音樂系"),
                NeedBased = false,
                Deadline = new DateOnly(2026, 10, 30),
                RequiredDocumentsJson = J("成績單", "作品或展演證明", "申請表")
            }
        );

        db.Activities.AddRange(
            new ActivityEntity { Id = "A001", Name = "程式馬拉松工作坊", Category = "程式設計", Description = "24 小時黑客松與業界導師諮詢", Date = new DateOnly(2026, 10, 18), Capacity = 40, Registered = 22 },
            new ActivityEntity { Id = "A002", Name = "校園音樂會", Category = "音樂", Description = "學生社團聯合演出", Date = new DateOnly(2026, 10, 25), Capacity = 200, Registered = 80 },
            new ActivityEntity { Id = "A003", Name = "公益路跑", Category = "運動", Description = "慈善路跑並可折抵服務時數", Date = new DateOnly(2026, 11, 2), Capacity = 300, Registered = 150 },
            new ActivityEntity { Id = "A004", Name = "創業講座：從 0 到 1", Category = "創業", Description = "新創創辦人經驗分享", Date = new DateOnly(2026, 10, 22), Capacity = 80, Registered = 45 },
            new ActivityEntity { Id = "A005", Name = "志工培訓營", Category = "志工", Description = "社區服務基礎培訓", Date = new DateOnly(2026, 11, 8), Capacity = 50, Registered = 18 }
        );

        db.CampusDocuments.AddRange(
            new CampusDocumentEntity
            {
                Id = "D001",
                Title = "獎學金申請總則",
                Category = "規章",
                Content = "本校獎學金分為優秀獎學金、清寒助學金與系所獎學金。申請期間通常為每學期第 3–5 週。需檢附成績單、身分證明，清寒類另需證明文件。重複領取同一學期多項優秀獎學金者，以金額較高者為準。"
            },
            new CampusDocumentEntity
            {
                Id = "D002",
                Title = "選課注意事項",
                Category = "規章",
                Content = "選課分為預選、正式選課與加退選。衝堂不得選修。先修科目未通過不得修習進階課。每學期最低 9 學分、最高 25 學分。畢業門檻依各系課程規劃表為準。"
            },
            new CampusDocumentEntity
            {
                Id = "D003",
                Title = "校園活動參與辦法",
                Category = "規章",
                Content = "學生可透過活動系統報名社團、講座與競賽。部分活動可折抵服務學習時數。報名後可加入個人行事曆並接收提醒。名額有限，採先報先得。"
            },
            new CampusDocumentEntity
            {
                Id = "D004",
                Title = "資工系課程地圖摘要",
                Category = "課程",
                Content = "資工系核心課程：程式設計→資料結構→演算法；作業系統、資料庫系統、計算機網路為進階核心。建議大三前完成核心必修，大三下開始專題。"
            },
            new CampusDocumentEntity
            {
                Id = "D005",
                Title = "辦公室與聯絡資訊",
                Category = "校園資訊",
                Content = "學務處：行政大樓 3 樓；教務處選課窗口：行政大樓 2 樓；獎助學金窗口：學務處生活輔導組。服務時間週一至週五 09:00–16:30。"
            }
        );

        db.CalendarEvents.AddRange(
            new CalendarEventEntity { Id = "E1", Title = "選課開放提醒", WhenText = "下學期選課週", Synced = true, Provider = "Calendar API（SQL Server）" },
            new CalendarEventEntity { Id = "E2", Title = "校園音樂會", WhenText = "2026-10-25", Synced = true, Provider = "Calendar API（SQL Server）" }
        );

        db.Notifications.AddRange(
            new NotificationEntity { Id = "N1", Channel = "in_app", Subject = "選課週即將開始", Status = "delivered", AtUtc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc) },
            new NotificationEntity { Id = "N2", Channel = "email", Subject = "獎學金申請截止提醒", Status = "queued", AtUtc = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc) }
        );

        await db.SaveChangesAsync();

        // 同步帳號副標與學生 GPA
        var students = await db.Students.AsNoTracking().ToListAsync();
        foreach (var u in await db.Users.Where(x => x.StudentProfileId != null).ToListAsync())
        {
            var s = students.FirstOrDefault(x => x.Id == u.StudentProfileId);
            if (s is null) continue;
            u.Subtitle = $"{s.Department}｜GPA {s.Gpa:0.00}";
            u.DisplayName = s.Name;
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureConversationSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[ChatConversations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChatConversations] (
                    [Id] bigint NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [Feature] nvarchar(64) NOT NULL,
                    [Title] nvarchar(200) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ChatConversations] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_ChatConversations_UserId_UpdatedAtUtc]
                    ON [dbo].[ChatConversations] ([UserId], [UpdatedAtUtc]);
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.ChatMessages', N'ConversationId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatMessages] ADD [ConversationId] bigint NULL;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ChatMessages_ConversationId_CreatedAtUtc'
                  AND object_id = OBJECT_ID(N'dbo.ChatMessages'))
            BEGIN
                CREATE INDEX [IX_ChatMessages_ConversationId_CreatedAtUtc]
                    ON [dbo].[ChatMessages] ([ConversationId], [CreatedAtUtc]);
            END
            """);
    }

    private static async Task MigrateLegacyMessagesAsync(AppDbContext db)
    {
        var orphans = await db.ChatMessages
            .Where(m => m.ConversationId == null)
            .Select(m => new { m.UserId, m.Feature })
            .Distinct()
            .ToListAsync();

        if (orphans.Count == 0) return;

        foreach (var group in orphans)
        {
            var firstUser = await db.ChatMessages
                .Where(m => m.UserId == group.UserId && m.Feature == group.Feature && m.ConversationId == null && m.Role == "user")
                .OrderBy(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var title = firstUser is null
                ? "先前對話"
                : TruncateTitle(firstUser.Content);

            var created = await db.ChatMessages
                .Where(m => m.UserId == group.UserId && m.Feature == group.Feature && m.ConversationId == null)
                .MinAsync(m => m.CreatedAtUtc);
            var updated = await db.ChatMessages
                .Where(m => m.UserId == group.UserId && m.Feature == group.Feature && m.ConversationId == null)
                .MaxAsync(m => m.CreatedAtUtc);

            var conv = new ChatConversationEntity
            {
                UserId = group.UserId,
                Feature = group.Feature,
                Title = title,
                CreatedAtUtc = created,
                UpdatedAtUtc = updated
            };
            db.ChatConversations.Add(conv);
            await db.SaveChangesAsync();

            await db.ChatMessages
                .Where(m => m.UserId == group.UserId && m.Feature == group.Feature && m.ConversationId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ConversationId, conv.Id));
        }
    }

    public static string TruncateTitle(string text, int max = 36)
    {
        var t = (text ?? string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
        if (t.Length <= max) return string.IsNullOrEmpty(t) ? "新對話" : t;
        return t[..max] + "…";
    }
}
