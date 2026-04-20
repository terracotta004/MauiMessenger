using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MauiMessenger.Api.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IHubContext<MessageHub> _hubContext;

    public ConversationService(
        IConversationRepository conversationRepository,
        IHubContext<MessageHub> hubContext)
    {
        _conversationRepository = conversationRepository;
        _hubContext = hubContext;
    }

    public async Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var participants = request.ParticipantIds
            .Distinct()
            .Select(id => new ConversationUser
            {
                ConversationId = Guid.Empty,
                UserId = id,
                JoinedAt = now
            })
            .ToList();

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            ConversationUsers = participants
        };

        foreach (var participant in participants)
        {
            participant.ConversationId = conversation.Id;
        }

        var saved = await _conversationRepository.AddAsync(conversation, cancellationToken);
        var dto = ToDto(saved);
        await BroadcastConversationUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<ConversationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken);
        return conversation is null ? null : ToDto(conversation);
    }

    public async Task<IReadOnlyList<ConversationDto>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var conversations = await _conversationRepository.ListByUserIdAsync(userId, cancellationToken);
        return conversations.Select(ToDto).ToList();
    }

    private static ConversationDto ToDto(Conversation conversation)
    {
        var participants = conversation.ConversationUsers.Select(cu => cu.UserId).ToList();
        return new ConversationDto(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt, participants);
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
}
