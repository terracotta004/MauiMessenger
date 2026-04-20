using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MauiMessenger.Client.Shared.Services;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;

namespace MauiMessenger.Client.Shared.Components.Pages;

public partial class Conversations
{
    [Inject] private IApiClient ApiClient { get; set; } = default!;
    [Inject] private IAuthSessionClient AuthSessionClient { get; set; } = default!;
    [Inject] private CurrentUserState CurrentUserState { get; set; } = default!;
    [Inject] private IRealtimeMessageClient RealtimeClient { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private readonly List<UserDto> users = new();
    private readonly List<ConversationDto> conversations = new();
    private readonly List<MessageDto> messages = new();

    private ConversationDto? selectedConversation;
    private Guid? selectedConversationId;
    private bool isLoading;
    private bool isSaving;
    private bool isLoadingMessages;
    private bool isSending;
    private Guid? deletingMessageId;
    private string? errorMessage;
    private NewConversationForm newConversation = new();
    private NewMessageForm newMessage = new();
    private ElementReference messageListElement;
    private bool shouldScrollMessagesToBottom;
    private bool isRealtimeConnected;

    private IEnumerable<UserDto> availableUsers
        => users.Where(u => CurrentUserState.CurrentUser is null || u.Id != CurrentUserState.CurrentUser.Id);

    private bool canSendToSelectedConversation
        => selectedConversation is not null && !IsHumanObserverInAgentConversation(selectedConversation);

    private string? selectedConversationSendRestriction
        => selectedConversation is not null && IsHumanObserverInAgentConversation(selectedConversation)
            ? "Human observers can view agent-to-agent conversations, but cannot send messages."
            : null;

    protected override async Task OnInitializedAsync()
    {
        RealtimeClient.MessageReceived += OnMessageReceivedAsync;
        RealtimeClient.MessageDeleted += OnMessageDeletedAsync;
        RealtimeClient.ConversationUpdated += OnConversationUpdatedAsync;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await CurrentUserState.EnsureLoadedAsync(AuthSessionClient);

        if (CurrentUserState.CurrentUser is null)
        {
            return;
        }

        try
        {
            isLoading = true;
            errorMessage = null;

            users.Clear();
            users.AddRange(await ApiClient.GetUsersAsync());

            conversations.Clear();
            var fetched = await ApiClient.GetConversationsByUserAsync(CurrentUserState.CurrentUser.Id);
            conversations.AddRange(fetched.OrderByDescending(c => c.UpdatedAt));

            await EnsureHubConnectedAsync();
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DeleteMessageAsync(MessageDto message)
    {
        if (CurrentUserState.CurrentUser is null || message.SenderId != CurrentUserState.CurrentUser.Id || message.IsDeleted)
        {
            return;
        }

        try
        {
            deletingMessageId = message.Id;
            errorMessage = null;

            var deleted = await ApiClient.DeleteMessageAsync(message.Id);
            UpsertMessage(deleted);
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = ex.Message;
        }
        finally
        {
            deletingMessageId = null;
        }
    }

    private async Task CreateConversationAsync()
    {
        if (CurrentUserState.CurrentUser is null || newConversation.UserId is null || newConversation.UserId == Guid.Empty)
        {
            errorMessage = "Please select a user to start a conversation.";
            return;
        }

        try
        {
            isSaving = true;
            errorMessage = null;

            var participantIds = new List<Guid>
            {
                CurrentUserState.CurrentUser.Id,
                newConversation.UserId.Value
            };
            var title = string.IsNullOrWhiteSpace(newConversation.Title)
                ? GetDefaultConversationTitle(newConversation.UserId.Value)
                : newConversation.Title.Trim();

            var request = new CreateConversationRequest(title, participantIds);
            var created = await ApiClient.CreateConversationAsync(request);

            UpsertConversation(created);
            newConversation = new NewConversationForm();
            await OpenConversationAsync(created.Id);
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = ex.Message;
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task OpenConversationAsync(Guid conversationId)
    {
        if (selectedConversationId == conversationId)
        {
            return;
        }

        var previousConversationId = selectedConversationId;

        selectedConversationId = conversationId;
        selectedConversation = conversations.FirstOrDefault(c => c.Id == conversationId);

        try
        {
            await EnsureHubConnectedAsync();
            if (previousConversationId is not null)
            {
                await RealtimeClient.LeaveConversationAsync(previousConversationId.Value);
            }

            await RealtimeClient.JoinConversationAsync(conversationId);
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = $"Real-time updates are unavailable right now. {ex.Message}";
        }

        try
        {
            isLoadingMessages = true;
            messages.Clear();
            messages.AddRange(await ApiClient.GetMessagesByConversationAsync(conversationId));
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = ex.Message;
        }
        finally
        {
            isLoadingMessages = false;
        }

        RequestScrollMessagesToBottom();
    }

    private async Task SendMessageAsync()
    {
        if (CurrentUserState.CurrentUser is null || selectedConversationId is null)
        {
            errorMessage = "Please select a conversation before sending a message.";
            return;
        }

        var content = newMessage.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (!canSendToSelectedConversation)
        {
            errorMessage = selectedConversationSendRestriction ?? "You cannot send messages in this conversation.";
            return;
        }

        try
        {
            isSending = true;
            errorMessage = null;

            var request = new CreateMessageRequest(
                selectedConversationId.Value,
                CurrentUserState.CurrentUser.Id,
                content);

            var created = await ApiClient.CreateMessageAsync(request);
            if (!messages.Any(existing => existing.Id == created.Id))
            {
                messages.Add(created);
                RequestScrollMessagesToBottom();
            }
            newMessage = new NewMessageForm();
        }
        catch (Exception ex)
        {
            HandleSessionExpired(ex);
            errorMessage = ex.Message;
        }
        finally
        {
            isSending = false;
        }
    }

    private async Task EnsureHubConnectedAsync()
    {
        if (isRealtimeConnected)
        {
            return;
        }

        var token = await ApiClient.GetRealtimeTokenAsync();
        await RealtimeClient.ConnectAsync(ApiClient.BaseAddress, token.AccessToken);
        isRealtimeConnected = true;
    }

    private Task OnMessageReceivedAsync(MessageDto message)
    {
        if (selectedConversationId != message.ConversationId)
        {
            return Task.CompletedTask;
        }

        if (messages.Any(existing => existing.Id == message.Id))
        {
            return Task.CompletedTask;
        }

        messages.Add(message);
        return InvokeAsync(async () =>
        {
            RequestScrollMessagesToBottom();
            StateHasChanged();
            await Task.CompletedTask;
        });
    }

    private Task OnMessageDeletedAsync(MessageDto message)
    {
        if (selectedConversationId != message.ConversationId)
        {
            return Task.CompletedTask;
        }

        UpsertMessage(message);
        return InvokeAsync(StateHasChanged);
    }

    private Task OnConversationUpdatedAsync(ConversationDto conversation)
    {
        UpsertConversation(conversation);
        return InvokeAsync(StateHasChanged);
    }

    private void UpsertConversation(ConversationDto conversation)
    {
        var index = conversations.FindIndex(existing => existing.Id == conversation.Id);
        if (index >= 0)
        {
            conversations[index] = conversation;
        }
        else
        {
            conversations.Add(conversation);
        }

        conversations.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));

        if (selectedConversationId == conversation.Id)
        {
            selectedConversation = conversation;
        }
    }

    private void UpsertMessage(MessageDto message)
    {
        var index = messages.FindIndex(existing => existing.Id == message.Id);
        if (index >= 0)
        {
            messages[index] = message;
            return;
        }

        messages.Add(message);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!shouldScrollMessagesToBottom || messages.Count == 0)
        {
            return;
        }

        shouldScrollMessagesToBottom = false;

        try
        {
            await JSRuntime.InvokeVoidAsync("messengerConversations.scrollToBottom", messageListElement);
        }
        catch (JSException ex)
        {
            errorMessage = $"Could not scroll to the latest message. {ex.Message}";
        }
    }

    private void RequestScrollMessagesToBottom()
    {
        shouldScrollMessagesToBottom = messages.Count > 0;
    }

    private void HandleSessionExpired(Exception exception)
    {
        if (exception.Message.Contains("session has expired", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            CurrentUserState.SetUser(null);
        }
    }

    private string GetConversationTitle(ConversationDto conversation)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Title))
        {
            return conversation.Title;
        }

        var otherUsers = GetOtherParticipants(conversation).ToList();
        return otherUsers.Count > 0 ? string.Join(", ", otherUsers.Select(u => u.DisplayName)) : "Untitled";
    }

    private string GetConversationParticipants(ConversationDto conversation)
    {
        var otherUsers = GetOtherParticipants(conversation).ToList();
        if (otherUsers.Count == 0)
        {
            return "No other participants";
        }

        return string.Join(", ", otherUsers.Select(u => u.DisplayName));
    }

    private IEnumerable<UserDto> GetOtherParticipants(ConversationDto conversation)
    {
        if (CurrentUserState.CurrentUser is null)
        {
            return Enumerable.Empty<UserDto>();
        }

        var otherIds = conversation.ParticipantIds.Where(id => id != CurrentUserState.CurrentUser.Id);
        return users.Where(u => otherIds.Contains(u.Id));
    }

    private string GetDefaultConversationTitle(Guid otherUserId)
    {
        var user = users.FirstOrDefault(u => u.Id == otherUserId);
        return user is null ? "New conversation" : $"Chat with {user.DisplayName}";
    }

    private string GetUserLabel(Guid userId)
    {
        if (CurrentUserState.CurrentUser is not null && userId == CurrentUserState.CurrentUser.Id)
        {
            return "You";
        }

        var user = users.FirstOrDefault(u => u.Id == userId);
        return user is null ? userId.ToString() : user.DisplayName;
    }

    private bool IsHumanObserverInAgentConversation(ConversationDto conversation)
    {
        if (CurrentUserState.CurrentUser?.ParticipantType != ParticipantType.Human)
        {
            return false;
        }

        if (!conversation.ParticipantIds.Contains(CurrentUserState.CurrentUser.Id))
        {
            return false;
        }

        var agentCount = conversation.ParticipantIds
            .Select(id => users.FirstOrDefault(user => user.Id == id))
            .Count(user => user?.ParticipantType == ParticipantType.Agent);

        return agentCount >= 2;
    }

    public async ValueTask DisposeAsync()
    {
        RealtimeClient.MessageReceived -= OnMessageReceivedAsync;
        RealtimeClient.MessageDeleted -= OnMessageDeletedAsync;
        RealtimeClient.ConversationUpdated -= OnConversationUpdatedAsync;
        await RealtimeClient.DisposeAsync();
    }

    private sealed class NewConversationForm
    {
        public Guid? UserId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private sealed class NewMessageForm
    {
        public string Content { get; set; } = string.Empty;
    }
}
