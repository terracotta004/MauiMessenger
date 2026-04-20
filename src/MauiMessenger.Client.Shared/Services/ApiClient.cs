using System.Net.Http.Json;
using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Client.Shared.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    public Uri BaseAddress => _httpClient.BaseAddress
        ?? throw new InvalidOperationException("API base address is not configured.");

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public virtual async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<UserDto>>("api/users", cancellationToken)
            ?? Array.Empty<UserDto>();
    }

    public virtual async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public virtual async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public virtual async Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Invalid username or password.");
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public virtual async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/auth/logout", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public virtual async Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/auth/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: cancellationToken);
    }

    public virtual async Task<RealtimeTokenResponse> GetRealtimeTokenAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/auth/realtime-token", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RealtimeTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public virtual async Task<IReadOnlyList<ConversationDto>> GetConversationsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ConversationDto>>(
                   $"api/conversations?userId={userId}",
                   cancellationToken)
               ?? Array.Empty<ConversationDto>();
    }

    public virtual async Task<ConversationDto> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/conversations", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConversationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public virtual async Task<IReadOnlyList<MessageDto>> GetMessagesByConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<MessageDto>>(
                   $"api/messages?conversationId={conversationId}",
                   cancellationToken)
               ?? Array.Empty<MessageDto>();
    }

    public virtual async Task<MessageDto> CreateMessageAsync(
        CreateMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessageDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public virtual async Task<MessageDto> DeleteMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/messages/{messageId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessageDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }
}
