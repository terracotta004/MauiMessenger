using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MauiMessenger.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _dbContext;

    public ConversationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task AddParticipantAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.ConversationUsers
            .AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == userId, cancellationToken);
        if (exists)
        {
            return;
        }

        _dbContext.ConversationUsers.Add(new ConversationUser
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.ConversationUsers)
                .ThenInclude(cu => cu.User)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<Conversation?> FindDirectByParticipantIdsAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        var participantIds = new[] { firstUserId, secondUserId };

        return _dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.ConversationUsers)
                .ThenInclude(cu => cu.User)
            .FirstOrDefaultAsync(
                c => participantIds.All(id => c.ConversationUsers.Any(cu => cu.UserId == id)),
                cancellationToken);
    }

    public async Task<Conversation?> TouchAsync(
        Guid conversationId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.ConversationUsers)
                .ThenInclude(cu => cu.User)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        conversation.UpdatedAt = updatedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<IReadOnlyList<Conversation>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.ConversationUsers)
                .ThenInclude(cu => cu.User)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .AsNoTracking()
            .Include(c => c.ConversationUsers)
                .ThenInclude(cu => cu.User)
            .Where(c => c.ConversationUsers.Any(cu => cu.UserId == userId))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
