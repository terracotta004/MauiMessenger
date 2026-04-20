using MauiMessenger.Core.Entities;

namespace MauiMessenger.Core.DTOs;

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string Email,
    string Password,
    ParticipantType ParticipantType = ParticipantType.Human);

public sealed record UserDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    ParticipantType ParticipantType,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record RegisterAgentRequest(
    string Identity,
    string DisplayName,
    string? Email = null);
