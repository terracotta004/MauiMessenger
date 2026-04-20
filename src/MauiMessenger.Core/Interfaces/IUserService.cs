using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Core.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> EnsureParticipantAsync(string identity, string? displayName = null, CancellationToken cancellationToken = default);
    Task<UserDto> RegisterAgentAsync(RegisterAgentRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListAgentsAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
