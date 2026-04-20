using MauiMessenger.Api.Hubs;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MauiMessenger.Api.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserService _userService;
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly AgentMessengerOptions _agentMessengerOptions;

    public MessageService(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IUserService userService,
        IOptions<AgentMessengerOptions> agentMessengerOptions,
        IHubContext<MessageHub> hubContext)
    {
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _userService = userService;
        _agentMessengerOptions = agentMessengerOptions.Value;
        _hubContext = hubContext;
    }

    public async Task<MessageDto> CreateAsync(CreateMessageRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
        {
            throw new InvalidOperationException("Conversation not found.");
        }

        await ValidateSenderCanPostAsync(conversation, request.SenderId, cancellationToken);

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = request.SenderId,
            Content = request.Content.Trim(),
            SentAt = now,
            EditedAt = null,
            IsDeleted = false
        };

        var saved = await _messageRepository.AddAsync(message, cancellationToken);
        var dto = ToDto(saved);
        var updatedConversation = await _conversationRepository.TouchAsync(
            request.ConversationId,
            now,
            cancellationToken)
            ?? conversation;

        await _hubContext.Clients.Group(GetConversationGroup(request.ConversationId))
            .SendAsync("MessageReceived", dto, cancellationToken);

        await BroadcastConversationUpdatedAsync(ToConversationDto(updatedConversation), cancellationToken);

        return dto;
    }

    public async Task<MessageDto> DeleteAsync(
        Guid messageId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            throw new InvalidOperationException("Message not found.");
        }

        if (message.SenderId != requesterId)
        {
            throw new UnauthorizedAccessException("You can only delete your own messages.");
        }

        if (message.IsDeleted)
        {
            return ToDto(message);
        }

        var deleted = await _messageRepository.MarkDeletedAsync(
            messageId,
            DateTime.UtcNow,
            cancellationToken);
        if (deleted is null)
        {
            throw new InvalidOperationException("Message not found.");
        }

        var dto = ToDto(deleted);
        await BroadcastMessageDeletedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<AgentMessageDto> SendAgentMessageAsync(
        AgentMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAgentMessage(request);

        var sender = await _userService.EnsureParticipantAsync(request.From, cancellationToken: cancellationToken);
        var recipient = await _userService.EnsureParticipantAsync(request.To, cancellationToken: cancellationToken);
        var observer = await GetHumanObserverAsync(sender, recipient, cancellationToken);
        var conversation = await _conversationRepository.FindDirectByParticipantIdsAsync(
            sender.Id,
            recipient.Id,
            cancellationToken);

        if (conversation is null)
        {
            var conversationId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            conversation = await _conversationRepository.AddAsync(new Conversation
            {
                Id = conversationId,
                Title = $"{sender.DisplayName} / {recipient.DisplayName}",
                CreatedAt = now,
                UpdatedAt = now,
                ConversationUsers =
                [
                    new ConversationUser
                    {
                        ConversationId = conversationId,
                        UserId = sender.Id,
                        JoinedAt = now
                    },
                    new ConversationUser
                    {
                        ConversationId = conversationId,
                        UserId = recipient.Id,
                        JoinedAt = now
                    }
                ]
            }, cancellationToken);
        }

        if (observer is not null)
        {
            await _conversationRepository.AddParticipantAsync(conversation.Id, observer.Id, cancellationToken);
            await DeleteHumanObserverMessagesAsync(conversation, observer.Id, cancellationToken);
        }

        var created = await CreateAsync(
            new CreateMessageRequest(conversation.Id, sender.Id, request.Body),
            cancellationToken);

        var metadata = new Dictionary<string, string>(request.Metadata ?? new Dictionary<string, string>())
        {
            ["mauiConversationId"] = created.ConversationId.ToString(),
            ["mauiSenderId"] = created.SenderId.ToString()
        };

        return new AgentMessageDto(
            string.IsNullOrWhiteSpace(request.Id) ? created.Id.ToString("N") : request.Id.Trim(),
            request.From.Trim(),
            request.To.Trim(),
            request.Subject.Trim(),
            created.Content,
            new DateTimeOffset(created.SentAt, TimeSpan.Zero),
            metadata);
    }

    public async Task<IReadOnlyList<AgentMessageDto>> ListAgentInboxAsync(
        string identity,
        CancellationToken cancellationToken = default)
    {
        var participant = await _userService.EnsureParticipantAsync(identity, cancellationToken: cancellationToken);
        var conversations = await _conversationRepository.ListByUserIdAsync(participant.Id, cancellationToken);
        var conversationIds = conversations.Select(conversation => conversation.Id).ToArray();
        var messages = await _messageRepository.ListByConversationIdsAsync(conversationIds, cancellationToken);

        return messages
            .Where(message => message.SenderId != participant.Id && !message.IsDeleted)
            .Select(message => ToAgentMessageDto(message, participant.Username))
            .ToList();
    }

    public async Task<IReadOnlyList<MessageDto>> ListByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var messages = await _messageRepository.ListByConversationIdAsync(conversationId, cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    private static MessageDto ToDto(Message message)
        => new(message.Id, message.ConversationId, message.SenderId, message.Content, message.SentAt, message.EditedAt, message.IsDeleted);

    private static ConversationDto ToConversationDto(Conversation conversation)
    {
        var participants = conversation.ConversationUsers.Select(cu => cu.UserId).ToList();
        return new ConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            participants);
    }

    private static AgentMessageDto ToAgentMessageDto(Message message, string recipientIdentity)
    {
        var metadata = new Dictionary<string, string>
        {
            ["mauiConversationId"] = message.ConversationId.ToString(),
            ["mauiSenderId"] = message.SenderId.ToString()
        };

        return new AgentMessageDto(
            message.Id.ToString("N"),
            message.Sender?.UserName ?? message.SenderId.ToString(),
            recipientIdentity,
            "MauiMessenger message",
            message.Content,
            new DateTimeOffset(message.SentAt, TimeSpan.Zero),
            metadata);
    }

    private static void ValidateAgentMessage(AgentMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.From)
            || string.IsNullOrWhiteSpace(request.To)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Agent messages require from, to, and body.");
        }
    }

    private async Task<UserDto?> GetHumanObserverAsync(
        UserDto sender,
        UserDto recipient,
        CancellationToken cancellationToken)
    {
        if (sender.ParticipantType != ParticipantType.Agent
            || recipient.ParticipantType != ParticipantType.Agent
            || string.IsNullOrWhiteSpace(_agentMessengerOptions.HumanObserverUsername))
        {
            return null;
        }

        var observer = await _userService.GetByUsernameAsync(
            _agentMessengerOptions.HumanObserverUsername,
            cancellationToken);
        if (observer is null)
        {
            throw new InvalidOperationException(
                $"Agent-to-agent observer '{_agentMessengerOptions.HumanObserverUsername}' was not found.");
        }

        if (observer.ParticipantType != ParticipantType.Human)
        {
            throw new InvalidOperationException(
                $"Agent-to-agent observer '{_agentMessengerOptions.HumanObserverUsername}' must be a human user.");
        }

        return observer;
    }

    private async Task ValidateSenderCanPostAsync(
        Conversation conversation,
        Guid senderId,
        CancellationToken cancellationToken)
    {
        if (conversation.ConversationUsers.All(cu => cu.UserId != senderId))
        {
            throw new InvalidOperationException("Sender is not a participant in this conversation.");
        }

        var sender = await _userService.GetByIdAsync(senderId, cancellationToken);
        if (sender is null)
        {
            throw new InvalidOperationException("Sender not found.");
        }

        if (sender.ParticipantType != ParticipantType.Human)
        {
            return;
        }

        var agentCount = 0;
        foreach (var participant in conversation.ConversationUsers)
        {
            var participantType = participant.User?.ParticipantType
                ?? (await _userService.GetByIdAsync(participant.UserId, cancellationToken))?.ParticipantType;
            if (participantType == ParticipantType.Agent)
            {
                agentCount++;
            }
        }

        if (agentCount >= 2)
        {
            throw new InvalidOperationException("Human observers cannot send messages in agent-to-agent conversations.");
        }
    }

    private async Task BroadcastConversationUpdatedAsync(
        ConversationDto conversation,
        CancellationToken cancellationToken)
    {
        var groups = conversation.ParticipantIds
            .Select(MessageHub.GetUserGroup)
            .ToList();

        if (groups.Count == 0)
        {
            return;
        }

        await _hubContext.Clients.Groups(groups)
            .SendAsync("ConversationUpdated", conversation, cancellationToken);
    }

    private async Task DeleteHumanObserverMessagesAsync(
        Conversation conversation,
        Guid observerId,
        CancellationToken cancellationToken)
    {
        var humanObserverIds = new HashSet<Guid> { observerId };
        foreach (var participant in conversation.ConversationUsers)
        {
            var participantType = participant.User?.ParticipantType
                ?? (await _userService.GetByIdAsync(participant.UserId, cancellationToken))?.ParticipantType;
            if (participantType == ParticipantType.Human)
            {
                humanObserverIds.Add(participant.UserId);
            }
        }

        var deletedAt = DateTime.UtcNow;
        foreach (var humanObserverId in humanObserverIds)
        {
            var deletedMessages = await _messageRepository.MarkConversationMessagesDeletedBySenderAsync(
                conversation.Id,
                humanObserverId,
                deletedAt,
                cancellationToken);

            foreach (var message in deletedMessages)
            {
                await BroadcastMessageDeletedAsync(ToDto(message), cancellationToken);
            }
        }
    }

    private async Task BroadcastMessageDeletedAsync(
        MessageDto message,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group(GetConversationGroup(message.ConversationId))
            .SendAsync("MessageDeleted", message, cancellationToken);
    }

    public static string GetConversationGroup(Guid conversationId)
        => $"conversation:{conversationId}";
}
