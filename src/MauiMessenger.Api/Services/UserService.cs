using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace MauiMessenger.Api.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly UserManager<User> _userManager;

    public UserService(IUserRepository userRepository, UserManager<User> userManager)
    {
        _userRepository = userRepository;
        _userManager = userManager;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            ParticipantType = request.ParticipantType,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new IdentityOperationException(result.Errors.Select(error => error.Description).ToArray());
        }

        var saved = await _userRepository.GetByIdAsync(user.Id, cancellationToken) ?? user;
        return ToDto(saved);
    }

    public async Task<UserDto> EnsureParticipantAsync(
        string identity,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentity = NormalizeIdentity(identity);
        var existing = await _userRepository.GetByUsernameAsync(normalizedIdentity, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var participantType = GetParticipantType(normalizedIdentity);
        var email = participantType == ParticipantType.Human
            ? normalizedIdentity
            : BuildAgentEmail(normalizedIdentity);

        return await CreateAsync(
            new CreateUserRequest(
                normalizedIdentity,
                string.IsNullOrWhiteSpace(displayName) ? normalizedIdentity : displayName.Trim(),
                email,
                GenerateParticipantPassword(),
                participantType),
            cancellationToken);
    }

    public Task<UserDto> RegisterAgentAsync(RegisterAgentRequest request, CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(request.Identity);
        if (GetParticipantType(identity) != ParticipantType.Agent)
        {
            throw new InvalidOperationException("Agent identities must start with 'agent:' or 'bot:'.");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? identity
            : request.DisplayName.Trim();

        return EnsureParticipantAsync(identity, displayName, cancellationToken);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(NormalizeIdentity(username), cancellationToken);
        return user is null ? null : ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.ListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<UserDto>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.ListAsync(cancellationToken);
        return users
            .Where(user => user.ParticipantType == ParticipantType.Agent)
            .Select(ToDto)
            .ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new IdentityOperationException(result.Errors.Select(error => error.Description).ToArray());
        }
    }

    private static UserDto ToDto(User user)
        => new(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.ParticipantType,
            user.CreatedAt,
            user.UpdatedAt);

    private static string NormalizeIdentity(string identity)
        => identity.Trim();

    private static ParticipantType GetParticipantType(string identity)
        => identity.StartsWith("agent:", StringComparison.OrdinalIgnoreCase)
            || identity.StartsWith("bot:", StringComparison.OrdinalIgnoreCase)
                ? ParticipantType.Agent
                : ParticipantType.Human;

    private static string BuildAgentEmail(string identity)
    {
        var safeLocalPart = new string(identity
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '.')
            .ToArray()).Trim('.');

        if (string.IsNullOrWhiteSpace(safeLocalPart))
        {
            safeLocalPart = $"agent.{Guid.NewGuid():N}";
        }

        return $"{safeLocalPart}@agents.local";
    }

    private static string GenerateParticipantPassword()
        => $"AgentMessenger-{Guid.NewGuid():N}";
}
