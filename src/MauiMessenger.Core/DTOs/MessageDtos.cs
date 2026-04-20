namespace MauiMessenger.Core.DTOs;

public sealed record CreateMessageRequest(
    Guid ConversationId,
    Guid SenderId,
    string Content);

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    DateTime SentAt,
    DateTime? EditedAt,
    bool IsDeleted);

public sealed record AgentMessageRequest(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset SentAtUtc,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record AgentMessageDto(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset SentAtUtc,
    IReadOnlyDictionary<string, string> Metadata);
