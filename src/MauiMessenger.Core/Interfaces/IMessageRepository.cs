using MauiMessenger.Core.Entities;

namespace MauiMessenger.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default);
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Message?> MarkDeletedAsync(Guid id, DateTime deletedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> MarkConversationMessagesDeletedBySenderAsync(Guid conversationId, Guid senderId, DateTime deletedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> ListByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> ListByConversationIdsAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken = default);
}
