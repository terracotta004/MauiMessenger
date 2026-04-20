using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Client.Shared.Services;

public interface IApiClient
{
    Uri BaseAddress { get; }

    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<RealtimeTokenResponse> GetRealtimeTokenAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationDto> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<MessageDto> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default);
    Task<MessageDto> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
