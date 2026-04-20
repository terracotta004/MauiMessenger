using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MauiMessenger.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _dbContext;

    public MessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Message?> MarkDeletedAsync(Guid id, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (message is null)
        {
            return null;
        }

        MarkDeleted(message, deletedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<Message>> MarkConversationMessagesDeletedBySenderAsync(
        Guid conversationId,
        Guid senderId,
        DateTime deletedAt,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.Messages
            .Where(m => m.ConversationId == conversationId
                && m.SenderId == senderId
                && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            MarkDeleted(message, deletedAt);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return messages;
    }

    public async Task<IReadOnlyList<Message>> ListByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds.Count == 0)
        {
            return Array.Empty<Message>();
        }

        return await _dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => conversationIds.Contains(m.ConversationId))
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);
    }

    private static void MarkDeleted(Message message, DateTime deletedAt)
    {
        message.Content = string.Empty;
        message.IsDeleted = true;
        message.EditedAt = deletedAt;
    }
}
