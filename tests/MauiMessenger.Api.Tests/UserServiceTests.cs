using MauiMessenger.Api.Services;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Infrastructure.Data;
using MauiMessenger.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MauiMessenger.Api.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_TrimsFieldsAndHashesPassword()
    {
        await using var scope = await IdentityTestScope.CreateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var request = new CreateUserRequest(" alice ", " Alice A ", " alice@example.com ", "password123");

        var created = await service.CreateAsync(request);

        var saved = (await repository.ListAsync()).Single();
        Assert.Equal("alice", saved.UserName);
        Assert.Equal("Alice A", saved.DisplayName);
        Assert.Equal("alice@example.com", saved.Email);
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
        Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
        Assert.NotEqual("password123", saved.PasswordHash);
        Assert.True(await userManager.CheckPasswordAsync(saved, "password123"));
    }

    [Fact]
    public async Task ListAsync_ReturnsDtosForStoredUsers()
    {
        await using var scope = await IdentityTestScope.CreateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        var request = new CreateUserRequest("bob", "Bob B", "bob@example.com", "pass");

        await service.CreateAsync(request);

        var users = await service.ListAsync();

        Assert.Single(users);
        Assert.Equal("bob", users[0].Username);
        Assert.Equal("bob@example.com", users[0].Email);
    }

    [Fact]
    public async Task RegisterAgentAsync_AllowsAgentIdentityCharacters()
    {
        await using var scope = await IdentityTestScope.CreateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var created = await service.RegisterAgentAsync(new RegisterAgentRequest(
            "agent:research",
            "Research Agent"));

        Assert.Equal("agent:research", created.Username);
        Assert.Equal("Research Agent", created.DisplayName);
        Assert.Equal(ParticipantType.Agent, created.ParticipantType);
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredUser()
    {
        await using var scope = await IdentityTestScope.CreateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        var created = await service.CreateAsync(new CreateUserRequest(
            "dana",
            "Dana D",
            "dana@example.com",
            "password123"));

        await service.DeleteAsync(created.Id);

        Assert.Null(await service.GetByIdAsync(created.Id));
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenUserMissing()
    {
        await using var scope = await IdentityTestScope.CreateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(Guid.NewGuid()));

        Assert.Equal("User not found.", exception.Message);
    }

    private sealed class IdentityTestScope : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private IdentityTestScope(SqliteConnection connection, ServiceProvider provider, AsyncServiceScope scope)
        {
            _connection = connection;
            _provider = provider;
            _scope = scope;
        }

        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        public static async Task<IdentityTestScope> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services
                .AddIdentityCore<User>(options =>
                {
                    options.ConfigureMauiMessengerIdentity();
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserService, UserService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            return new IdentityTestScope(connection, provider, scope);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
