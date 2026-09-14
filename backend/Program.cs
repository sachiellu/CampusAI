using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using CampusAI.Api.Data;
using CampusAI.Api.Models;
using CampusAI.Api.Options;
using CampusAI.Api.Services;
using CampusAI.Api.Services.Agents;
using CampusAI.Api.Services.CampusApis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
        };
    });
builder.Services.AddAuthorization();

var sqlCs = builder.Configuration.GetConnectionString("Default")
    ?? "Server=.\\TEW_SQLEXPRESS;Database=CampusAI;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlServer(sqlCs));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174",
                "http://localhost:5175",
                "http://127.0.0.1:5175")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddSingleton<SecretSourceDetector>();
builder.Services.AddSingleton<CampusToolService>();
builder.Services.AddSingleton<CampusAgentService>();
builder.Services.AddSingleton<StudentDataApi>();
builder.Services.AddSingleton<CourseDataApi>();
builder.Services.AddSingleton<ActivityDataApi>();
builder.Services.AddSingleton<ApplicationServiceApi>();
builder.Services.AddSingleton<EmailNotificationApi>();
builder.Services.AddSingleton<CalendarApi>();
builder.Services.AddSingleton<CampusDataApi>();
builder.Services.AddSingleton<PlatformServicesApi>();
builder.Services.AddSingleton<RecommendAgent>();
builder.Services.AddSingleton<PlanningAgent>();
builder.Services.AddSingleton<ApplicationAgent>();
builder.Services.AddSingleton<CommunicationAgent>();
builder.Services.AddSingleton<ReminderTrackingAgent>();
builder.Services.AddSingleton<DataAnalysisAgent>();
builder.Services.AddSingleton<PersonalOrchestrator>();
builder.Services.AddSingleton<McpRagClient>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ChatHistoryService>();
builder.Services.AddSingleton<LlmAgentService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", async (LlmAgentService agent, SecretSourceDetector secrets, McpRagClient mcp, CancellationToken ct) =>
{
    var info = secrets.Detect();
    var mcpOk = await mcp.PingAsync(ct);
    return Results.Ok(new
    {
        status = "ok",
        project = "CampusAI-Agent",
        auth = "JWT",
        persistence = "SQL Server",
        llmEnabled = agent.IsLlmEnabled,
        mode = agent.IsLlmEnabled ? (info.ConfiguredProvider ?? "llm") : "rules",
        secretSource = info.Source,
        configuredProvider = info.ConfiguredProvider,
        mcp = new
        {
            role = "client",
            ragServer = mcpOk ? "up" : "down",
            architecture = "client_manages_chat__server_rag_only"
        },
        layers = new
        {
            application = new[] { "Vue 學生／教師／行政功能" },
            agent = new[]
            {
                "Personal Orchestrator",
                "推薦 Agent", "規劃 Agent", "申請 Agent",
                "溝通 Agent", "提醒／追蹤 Agent", "資料分析 Agent"
            },
            apiServices = new
            {
                campusTools = new[]
                {
                    "StudentDataApi", "CourseDataApi", "ActivityDataApi",
                    "ApplicationServiceApi", "EmailNotificationApi", "CalendarApi", "CampusDataApi"
                },
                platform = new[] { "Identity", "Permission", "ToolCalling" }
            },
            data = new[] { "SQL Server（Students／Courses／Scholarships…）", "MCP RAG / campus-docs.json" }
        }
    });
});

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "請輸入帳號與密碼" });

    var result = await auth.LoginAsync(req, ct);
    return result is null
        ? Results.Unauthorized()
        : Results.Ok(result);
});

app.MapGet("/api/auth/accounts", async (AppDbContext db) =>
{
    var users = await db.Users
        .OrderBy(u => u.Role).ThenBy(u => u.Id)
        .Select(u => new
        {
            u.Username,
            u.DisplayName,
            u.Role,
            u.Subtitle,
            u.StudentProfileId,
            passwordHint = DbSeeder.DemoPassword
        })
        .ToListAsync();
    return Results.Ok(users);
});

app.MapGet("/api/me", (ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    return Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        username = user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name,
        role = user.FindFirstValue(ClaimTypes.Role),
        studentProfileId = user.FindFirstValue("student_profile_id")
    });
}).RequireAuthorization();

app.MapGet("/api/conversations", async (ClaimsPrincipal user, ChatHistoryService history, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
    var list = await history.ListAsync(userId, ct);
    return Results.Ok(list.Select(c => new
    {
        c.Id,
        c.Feature,
        c.Title,
        c.CreatedAtUtc,
        c.UpdatedAtUtc
    }));
}).RequireAuthorization();

app.MapPost("/api/conversations", async (CreateConversationRequest req, ClaimsPrincipal user, ChatHistoryService history, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
    var feature = string.IsNullOrWhiteSpace(req.Feature) ? "general" : req.Feature;
    var conv = await history.CreateAsync(userId, feature, ct);
    return Results.Ok(new
    {
        conv.Id,
        conv.Feature,
        conv.Title,
        conv.CreatedAtUtc,
        conv.UpdatedAtUtc
    });
}).RequireAuthorization();

app.MapGet("/api/conversations/{id:long}/messages", async (long id, ClaimsPrincipal user, ChatHistoryService history, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
    var conv = await history.GetOwnedAsync(userId, id, ct);
    if (conv is null) return Results.NotFound();
    var items = await history.GetMessagesAsync(userId, id, 100, ct);
    return Results.Ok(items.Select(m => new
    {
        m.Id,
        m.ConversationId,
        m.Feature,
        m.Role,
        m.Content,
        m.CreatedAtUtc
    }));
}).RequireAuthorization();

app.MapDelete("/api/conversations/{id:long}", async (long id, ClaimsPrincipal user, ChatHistoryService history, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
    var ok = await history.DeleteAsync(userId, id, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapGet("/api/chat/history", async (ClaimsPrincipal user, ChatHistoryService history, string? feature, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
#pragma warning disable CS0618
    var items = await history.GetRecentAsync(userId, feature, 50, ct);
#pragma warning restore CS0618
    return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/chat", async (
    ChatRequest request,
    ClaimsPrincipal user,
    LlmAgentService agent,
    ChatHistoryService history,
    AppDbContext db,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "請輸入訊息" });

    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();

    var role = user.FindFirstValue(ClaimTypes.Role) ?? "student";
    var studentProfileId = user.FindFirstValue("student_profile_id");
    if (string.IsNullOrWhiteSpace(studentProfileId)) studentProfileId = null;

    var feature = string.IsNullOrWhiteSpace(request.Feature) ? "general" : request.Feature;
    long conversationId;
    if (request.ConversationId is long cid)
    {
        var owned = await history.GetOwnedAsync(userId, cid, ct);
        if (owned is null) return Results.BadRequest(new { error = "對話不存在" });
        conversationId = owned.Id;
        feature = owned.Feature;
    }
    else
    {
        var created = await history.CreateAsync(userId, feature, ct);
        conversationId = created.Id;
    }

    var effective = request with
    {
        Role = role,
        Feature = feature,
        ConversationId = conversationId,
        StudentId = role == "student" ? (studentProfileId ?? request.StudentId) : request.StudentId
    };

    var response = await agent.HandleAsync(effective, ct);
    await history.SaveTurnAsync(userId, conversationId, feature, effective.Message, response, ct);
    response = response with { ConversationId = conversationId };

    // 獎學金草稿持久化
    if (role == "student" && effective.Feature == "scholarship" && response.Result is not null)
    {
        try
        {
            var json = JsonSerializer.Serialize(response.Result);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("drafts", out var drafts))
            {
                foreach (var d in drafts.EnumerateArray())
                {
                    var sid = d.GetProperty("scholarshipId").GetString() ?? "";
                    var name = d.GetProperty("scholarshipName").GetString() ?? "";
                    var draft = d.GetProperty("draft").GetString() ?? "";
                    var status = d.GetProperty("status").GetString() ?? "draft";
                    var existing = await db.ScholarshipApplications
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.ScholarshipId == sid, ct);
                    if (existing is null)
                    {
                        db.ScholarshipApplications.Add(new ScholarshipApplicationEntity
                        {
                            UserId = userId,
                            ScholarshipId = sid,
                            ScholarshipName = name,
                            DraftText = draft,
                            Status = status,
                            UpdatedAtUtc = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.DraftText = draft;
                        existing.Status = status;
                        existing.UpdatedAtUtc = DateTime.UtcNow;
                    }
                }
                await db.SaveChangesAsync(ct);
            }
        }
        catch { /* ignore parse issues */ }
    }

    return Results.Ok(response);
}).RequireAuthorization();

app.MapGet("/api/applications", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(idValue, out var userId)) return Results.Unauthorized();
    var list = await db.ScholarshipApplications
        .Where(a => a.UserId == userId)
        .OrderByDescending(a => a.UpdatedAtUtc)
        .ToListAsync(ct);
    return Results.Ok(list);
}).RequireAuthorization();

// —— 校務與工具 API 服務頁（架構圖第三層，可進入瀏覽）——
static string? RoleOf(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role);
static string? StudentOf(ClaimsPrincipal user) => user.FindFirstValue("student_profile_id");

app.MapGet("/api/services/catalog", (ClaimsPrincipal user) =>
{
    var role = RoleOf(user) ?? "student";
    var all = new[]
    {
        new { id = "students", group = "campusTools", name = "學生資料", path = StudentDataApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "查詢學生檔案與修課／興趣資料" },
        new { id = "courses", group = "campusTools", name = "課程資料", path = CourseDataApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "開課清單、時段、先修" },
        new { id = "activities", group = "campusTools", name = "活動資料", path = ActivityDataApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "校園活動與名額" },
        new { id = "applications", group = "campusTools", name = "申請服務", path = ApplicationServiceApi.Route, roles = new[] { "student", "admin" }, desc = "獎學金／活動申請服務" },
        new { id = "notifications", group = "campusTools", name = "Email／通知", path = EmailNotificationApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "站內與郵件通知" },
        new { id = "calendar", group = "campusTools", name = "行事曆", path = CalendarApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "行程同步與提醒事件" },
        new { id = "campus", group = "campusTools", name = "校務資料", path = CampusDataApi.Route, roles = new[] { "teacher", "admin" }, desc = "校務統計與獎助摘要" },
        new { id = "platform-identity", group = "platform", name = "身分驗證", path = PlatformServicesApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "目前登入身分與驗證狀態" },
        new { id = "platform-permission", group = "platform", name = "權限控管", path = PlatformServicesApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "角色可使用功能範圍" },
        new { id = "platform-tools", group = "platform", name = "Tool Calling", path = PlatformServicesApi.Route, roles = new[] { "student", "teacher", "admin" }, desc = "Agent 工具呼叫介面（僅供展示，尚未實作）" }
    };
    return Results.Ok(all.Where(x => x.roles.Contains(role)));
}).RequireAuthorization();

app.MapGet("/api/services/students", (ClaimsPrincipal user, StudentDataApi api) =>
{
    var role = RoleOf(user) ?? "student";
    var sid = StudentOf(user);
    if (role == "student")
    {
        var me = api.GetStudent(sid);
        return me is null ? Results.NotFound() : Results.Ok(new { endpoint = StudentDataApi.Route, scope = "self", data = me });
    }
    return Results.Ok(new { endpoint = StudentDataApi.Route, scope = "all", data = api.ListStudents() });
}).RequireAuthorization();

app.MapGet("/api/services/courses", (CourseDataApi api) =>
    Results.Ok(new { endpoint = CourseDataApi.Route, data = api.ListCourses() })).RequireAuthorization();

app.MapGet("/api/services/activities", (ActivityDataApi api) =>
    Results.Ok(new { endpoint = ActivityDataApi.Route, data = api.ListActivities() })).RequireAuthorization();

app.MapGet("/api/services/applications", (ClaimsPrincipal user, ApplicationServiceApi api, AppDbContext db) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    _ = int.TryParse(idValue, out var userId);
    var drafts = db.ScholarshipApplications.Where(a => a.UserId == userId)
        .OrderByDescending(a => a.UpdatedAtUtc).Take(20).ToList();
    return Results.Ok(new { endpoint = ApplicationServiceApi.Route, catalog = api.Catalog(), myDrafts = drafts });
}).RequireAuthorization();

app.MapGet("/api/services/notifications", (EmailNotificationApi api) =>
    Results.Ok(new { endpoint = EmailNotificationApi.Route, data = api.List() })).RequireAuthorization();

app.MapGet("/api/services/calendar", (CalendarApi api) =>
    Results.Ok(new { endpoint = CalendarApi.Route, data = api.List() })).RequireAuthorization();

app.MapGet("/api/services/campus", (ClaimsPrincipal user, CampusDataApi api) =>
{
    var role = RoleOf(user) ?? "student";
    if (role is not ("teacher" or "admin"))
        return Results.Json(new { error = "此服務僅教師／行政可進入" }, statusCode: 403);
    return Results.Ok(new { endpoint = CampusDataApi.Route, stats = api.Stats(), scholarships = api.ListScholarships() });
}).RequireAuthorization();

app.MapGet("/api/services/platform", (ClaimsPrincipal user, PlatformServicesApi api) =>
{
    var role = RoleOf(user) ?? "student";
    return Results.Ok(api.Snapshot(role, StudentOf(user)));
}).RequireAuthorization();

app.MapGet("/api/services/platform-identity", (ClaimsPrincipal user, PlatformServicesApi api) =>
{
    var role = RoleOf(user) ?? "student";
    return Results.Ok(new { endpoint = PlatformServicesApi.Route, section = "identity", data = api.Identity(role, StudentOf(user)) });
}).RequireAuthorization();

app.MapGet("/api/services/platform-permission", (ClaimsPrincipal user, PlatformServicesApi api) =>
{
    var role = RoleOf(user) ?? "student";
    return Results.Ok(new { endpoint = PlatformServicesApi.Route, section = "permission", data = api.Permission(role) });
}).RequireAuthorization();

app.MapGet("/api/services/platform-tools", (ClaimsPrincipal user, PlatformServicesApi api) =>
{
    return Results.Ok(new { endpoint = PlatformServicesApi.Route, section = "toolCalling", data = api.ToolCalling() });
}).RequireAuthorization();

app.Run("http://localhost:5088");
