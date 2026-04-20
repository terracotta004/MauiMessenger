using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Core.Interfaces;

public interface IMessageService
{
    Task<MessageDto> CreateAsync(CreateMessageRequest request, CancellationToken cancellationToken = default);
    Task<MessageDto> DeleteAsync(Guid messageId, Guid requesterId, CancellationToken cancellationToken = default);
    Task<AgentMessageDto> SendAgentMessageAsync(AgentMessageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentMessageDto>> ListAgentInboxAsync(string identity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageDto>> ListByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
