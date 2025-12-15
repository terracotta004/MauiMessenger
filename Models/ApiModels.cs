using System;
using System.Collections.Generic;

namespace MauiMessenger.Api.Models;

/// <summary>
/// Represents a single user in the messenger ecosystem.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Offline;
}

public enum UserStatus
{
    Offline,
    Online,
    Busy
}

/// <summary>
/// Lightweight representation of a user's friends list entry.
/// </summary>
public class FriendSummary
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Offline;
    public DateTimeOffset StatusUpdatedAt { get; set; }
}

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Rejected
}

/// <summary>
/// Tracks the lifecycle of a friendship request between two users.
/// </summary>
public class Friendship
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Detail used when surfacing incoming or outgoing friend requests to the client.
/// </summary>
public class FriendRequestSummary
{
    public Guid RequestId { get; set; }
    public Guid FromUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; }
}

public enum MessageType
{
    Text,
    Photo,
    Video,
    Audio,
    File
}

/// <summary>
/// File-system backed payload associated with a message.
/// </summary>
public class Attachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public string StoragePath { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single chat message within a conversation.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.Text;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public Attachment? Attachment { get; set; }
}

/// <summary>
/// Aggregates participants and messages into a chat thread.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
    public string? RecentMessagePreview { get; set; }
    public DateTimeOffset? RecentMessageCreatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

