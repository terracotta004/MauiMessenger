using MauiMessenger.Api.Services;
using MauiMessenger.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MauiMessenger.Api.Hubs;

public class MessageHub : Hub
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IRealtimeTokenService _realtimeTokenService;

    public MessageHub(
        IConversationRepository conversationRepository,
        IRealtimeTokenService realtimeTokenService)
    {
        _conversationRepository = conversationRepository;
        _realtimeTokenService = realtimeTokenService;
    }

    public override async Task OnConnectedAsync()
    {
        var token = GetAccessToken();
        if (!_realtimeTokenService.TryValidateToken(token, out var userId))
        {
            Context.Abort();
            return;
        }

        Context.Items["UserId"] = userId;
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetUserGroup(userId),
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        if (!TryGetUserId(out var userId))
        {
            throw new HubException("Real-time connection is not authenticated.");
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, Context.ConnectionAborted);
        if (conversation is null || conversation.ConversationUsers.All(cu => cu.UserId != userId))
        {
            throw new HubException("You are not a participant in this conversation.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            MessageService.GetConversationGroup(conversationId),
            Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            MessageService.GetConversationGroup(conversationId),
            Context.ConnectionAborted);
    }

    private string? GetAccessToken()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            return null;
        }

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return httpContext.Request.Query["access_token"].FirstOrDefault();
    }

    private bool TryGetUserId(out Guid userId)
    {
        if (Context.Items.TryGetValue("UserId", out var value) && value is Guid itemUserId)
        {
            userId = itemUserId;
            return true;
        }

        userId = Guid.Empty;
        return false;
    }

    public static string GetUserGroup(Guid userId)
        => $"user:{userId}";
}
