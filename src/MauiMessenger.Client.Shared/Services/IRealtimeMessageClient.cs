using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Client.Shared.Services;

public interface IRealtimeMessageClient : IAsyncDisposable
{
    event Func<MessageDto, Task>? MessageReceived;
    event Func<MessageDto, Task>? MessageDeleted;
    event Func<ConversationDto, Task>? ConversationUpdated;

    Task ConnectAsync(Uri apiBaseAddress, string accessToken, CancellationToken cancellationToken = default);
    Task JoinConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task LeaveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
