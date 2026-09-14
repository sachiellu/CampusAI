namespace CampusAI.Api.Models;

public record StudentProfile(
    string Id,
    string Name,
    string Department,
    int Year,
    double Gpa,
    IReadOnlyList<string> CompletedCourses,
    IReadOnlyList<string> Interests,
    bool HasFinancialNeed
);

public record CampusDocument(string Id, string Title, string Category, string Content);

public record Scholarship(
    string Id,
    string Name,
    string Description,
    double MinGpa,
    IReadOnlyList<string>? Departments,
    bool NeedBased,
    DateOnly Deadline,
    IReadOnlyList<string> RequiredDocuments
);

public record Course(
    string Id,
    string Name,
    string Department,
    int Credits,
    string DayTime,
    IReadOnlyList<string> Prerequisites
);

public record CampusActivity(
    string Id,
    string Name,
    string Category,
    string Description,
    DateOnly Date,
    int Capacity,
    int Registered
);

public record ChatRequest(
    string Message,
    string? StudentId,
    int Stage = 3,
    string Feature = "general",
    string? Role = null,
    long? ConversationId = null
);

public record CreateConversationRequest(string Feature);

public record AgentAction(string Agent, string Action, string Detail, object? Payload = null);

public record ChatResponse(
    string Reply,
    int DetectedStage,
    string Intent,
    IReadOnlyList<AgentAction> Actions,
    object? Result = null,
    string Mode = "rules",
    string? Model = null,
    long? ConversationId = null
);
