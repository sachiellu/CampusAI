using CampusAI.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusAI.Api.Services;

public class ChatHistoryService(AppDbContext db)
{
    public async Task<ChatConversationEntity> CreateAsync(int userId, string feature, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var conv = new ChatConversationEntity
        {
            UserId = userId,
            Feature = string.IsNullOrWhiteSpace(feature) ? "general" : feature,
            Title = "新對話",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.ChatConversations.Add(conv);
        await db.SaveChangesAsync(ct);
        return conv;
    }

    public async Task<List<ChatConversationEntity>> ListAsync(int userId, CancellationToken ct = default)
    {
        return await db.ChatConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(80)
            .ToListAsync(ct);
    }

    public async Task<ChatConversationEntity?> GetOwnedAsync(int userId, long conversationId, CancellationToken ct = default)
    {
        return await db.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, ct);
    }

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(int userId, long conversationId, int take = 100, CancellationToken ct = default)
    {
        return await db.ChatMessages.AsNoTracking()
            .Where(m => m.UserId == userId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteAsync(int userId, long conversationId, CancellationToken ct = default)
    {
        var conv = await GetOwnedAsync(userId, conversationId, ct);
        if (conv is null) return false;

        await db.ChatMessages
            .Where(m => m.UserId == userId && m.ConversationId == conversationId)
            .ExecuteDeleteAsync(ct);
        db.ChatConversations.Remove(conv);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SaveTurnAsync(
        int userId,
        long conversationId,
        string feature,
        string userMessage,
        Models.ChatResponse assistant,
        CancellationToken ct = default)
    {
        var conv = await GetOwnedAsync(userId, conversationId, ct)
            ?? throw new InvalidOperationException("對話不存在");

        var now = DateTime.UtcNow;
        if (conv.Title == "新對話" || string.IsNullOrWhiteSpace(conv.Title))
            conv.Title = DbSeeder.TruncateTitle(userMessage);
        conv.Feature = string.IsNullOrWhiteSpace(feature) ? conv.Feature : feature;
        conv.UpdatedAtUtc = now;

        db.ChatMessages.Add(new ChatMessageEntity
        {
            UserId = userId,
            ConversationId = conversationId,
            Feature = conv.Feature,
            Role = "user",
            Content = userMessage,
            CreatedAtUtc = now
        });
        db.ChatMessages.Add(new ChatMessageEntity
        {
            UserId = userId,
            ConversationId = conversationId,
            Feature = conv.Feature,
            Role = "assistant",
            Content = assistant.Reply,
            ActionsJson = System.Text.Json.JsonSerializer.Serialize(assistant.Actions),
            CreatedAtUtc = now
        });
        await db.SaveChangesAsync(ct);
    }

    [Obsolete("改用 GetMessagesAsync(conversationId)")]
    public async Task<List<ChatMessageEntity>> GetRecentAsync(int userId, string? feature, int take = 40, CancellationToken ct = default)
    {
        var q = db.ChatMessages.AsNoTracking().Where(m => m.UserId == userId);
        if (!string.IsNullOrWhiteSpace(feature))
            q = q.Where(m => m.Feature == feature);

        var latest = await q
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
        return latest.OrderBy(m => m.CreatedAtUtc).ToList();
    }
}
