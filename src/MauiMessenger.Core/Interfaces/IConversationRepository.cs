using MauiMessenger.Core.Entities;

namespace MauiMessenger.Core.Interfaces;

public interface IConversationRepository
{
    Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> FindDirectByParticipantIdsAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task<Conversation?> TouchAsync(Guid conversationId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
