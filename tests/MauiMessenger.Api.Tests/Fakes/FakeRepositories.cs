using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Api.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user => user.UserName == username));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<User>>(_users.ToList());
}

public sealed class FakeConversationRepository : IConversationRepository
{
    private readonly List<Conversation> _conversations = new();

    public Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _conversations.Add(conversation);
        return Task.FromResult(conversation);
    }

    public Task AddParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var conversation = _conversations.First(c => c.Id == conversationId);
        if (conversation.ConversationUsers.Any(cu => cu.UserId == userId))
        {
            return Task.CompletedTask;
        }

        conversation.ConversationUsers.Add(new ConversationUser
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_conversations.FirstOrDefault(conversation => conversation.Id == id));

    public Task<Conversation?> FindDirectByParticipantIdsAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_conversations.FirstOrDefault(conversation =>
            conversation.ConversationUsers.Any(cu => cu.UserId == firstUserId)
            && conversation.ConversationUsers.Any(cu => cu.UserId == secondUserId)));
    }

    public Task<Conversation?> TouchAsync(Guid conversationId, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var conversation = _conversations.FirstOrDefault(conversation => conversation.Id == conversationId);
        if (conversation is not null)
        {
            conversation.UpdatedAt = updatedAt;
        }

        return Task.FromResult(conversation);
    }

    public Task<IReadOnlyList<Conversation>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Conversation>>(_conversations.ToList());

    public Task<IReadOnlyList<Conversation>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Conversation>>(
            _conversations
                .Where(conversation => conversation.ConversationUsers.Any(cu => cu.UserId == userId))
                .ToList());
}

public sealed class FakeMessageRepository : IMessageRepository
{
    private readonly List<Message> _messages = new();

    public Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.FromResult(message);
    }

    public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_messages.FirstOrDefault(message => message.Id == id));

    public Task<Message?> MarkDeletedAsync(Guid id, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var message = _messages.FirstOrDefault(message => message.Id == id);
        if (message is null)
        {
            return Task.FromResult<Message?>(null);
        }

        MarkDeleted(message, deletedAt);
        return Task.FromResult<Message?>(message);
    }

    public Task<IReadOnlyList<Message>> MarkConversationMessagesDeletedBySenderAsync(
        Guid conversationId,
        Guid senderId,
        DateTime deletedAt,
        CancellationToken cancellationToken = default)
    {
        var messages = _messages
            .Where(message => message.ConversationId == conversationId
                && message.SenderId == senderId
                && !message.IsDeleted)
            .ToList();

        foreach (var message in messages)
        {
            MarkDeleted(message, deletedAt);
        }

        return Task.FromResult<IReadOnlyList<Message>>(messages);
    }

    public Task<IReadOnlyList<Message>> ListByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Message>>(
            _messages.Where(message => message.ConversationId == conversationId).ToList());

    public Task<IReadOnlyList<Message>> ListByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Message>>(
            _messages.Where(message => conversationIds.Contains(message.ConversationId)).ToList());

    private static void MarkDeleted(Message message, DateTime deletedAt)
    {
        message.Content = string.Empty;
        message.IsDeleted = true;
        message.EditedAt = deletedAt;
    }
}

public sealed class FakeUserService : IUserService
{
    private readonly List<UserDto> _users = new();

    public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = new UserDto(
            Guid.NewGuid(),
            request.Username,
            request.DisplayName,
            request.Email,
            request.ParticipantType,
            now,
            now);
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task<UserDto> EnsureParticipantAsync(
        string identity,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var existing = _users.FirstOrDefault(user => string.Equals(user.Username, identity, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var participantType = identity.StartsWith("agent:", StringComparison.OrdinalIgnoreCase)
            || identity.StartsWith("bot:", StringComparison.OrdinalIgnoreCase)
                ? ParticipantType.Agent
                : ParticipantType.Human;
        var now = DateTime.UtcNow;
        var created = new UserDto(
            Guid.NewGuid(),
            identity,
            displayName ?? identity,
            participantType == ParticipantType.Human ? identity : $"{identity.Replace(':', '.')}@agents.local",
            participantType,
            now,
            now);
        _users.Add(created);
        return Task.FromResult(created);
    }

    public Task<UserDto> RegisterAgentAsync(RegisterAgentRequest request, CancellationToken cancellationToken = default)
        => EnsureParticipantAsync(request.Identity, request.DisplayName, cancellationToken);

    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(user =>
            string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserDto>>(_users.ToList());

    public Task<IReadOnlyList<UserDto>> ListAgentsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserDto>>(_users.Where(user => user.ParticipantType == ParticipantType.Agent).ToList());

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(user => user.Id == id);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        _users.Remove(user);
        return Task.CompletedTask;
    }
}
