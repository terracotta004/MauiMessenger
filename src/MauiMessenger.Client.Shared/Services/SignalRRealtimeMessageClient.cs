using MauiMessenger.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace MauiMessenger.Client.Shared.Services;

public sealed class SignalRRealtimeMessageClient : IRealtimeMessageClient
{
    private HubConnection? _hubConnection;
    private Guid? _currentConversationId;
    private string? _accessToken;

    public event Func<MessageDto, Task>? MessageReceived;
    public event Func<MessageDto, Task>? MessageDeleted;
    public event Func<ConversationDto, Task>? ConversationUpdated;

    public async Task ConnectAsync(Uri apiBaseAddress, string accessToken, CancellationToken cancellationToken = default)
    {
        _accessToken = accessToken;

        if (_hubConnection is not null)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _hubConnection.StartAsync(cancellationToken);
                }
                catch
                {
                    await _hubConnection.DisposeAsync();
                    _hubConnection = null;
                    throw;
                }
            }

            return;
        }

        var hubUrl = new Uri(apiBaseAddress, "hubs/messages").ToString();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<MessageDto>("MessageReceived", message =>
        {
            var handler = MessageReceived;
            return handler is null ? Task.CompletedTask : handler(message);
        });

        _hubConnection.On<MessageDto>("MessageDeleted", message =>
        {
            var handler = MessageDeleted;
            return handler is null ? Task.CompletedTask : handler(message);
        });

        _hubConnection.On<ConversationDto>("ConversationUpdated", conversation =>
        {
            var handler = ConversationUpdated;
            return handler is null ? Task.CompletedTask : handler(conversation);
        });

        _hubConnection.Reconnected += async _ =>
        {
            if (_currentConversationId is not null)
            {
                await _hubConnection.InvokeAsync("JoinConversation", _currentConversationId.Value);
            }
        };

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
        }
        catch
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            throw;
        }
    }

    public async Task JoinConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        _currentConversationId = conversationId;
        if (_hubConnection is not null)
        {
            await _hubConnection.InvokeAsync("JoinConversation", conversationId, cancellationToken);
        }
    }

    public async Task LeaveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (_currentConversationId == conversationId)
        {
            _currentConversationId = null;
        }

        if (_hubConnection is not null)
        {
            await _hubConnection.InvokeAsync("LeaveConversation", conversationId, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
