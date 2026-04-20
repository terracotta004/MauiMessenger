using System.Net;
using System.Net.Http.Json;
using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Api.Tests.Integration;

public class UsersControllerTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersControllerTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ThenGetById_ReturnsUser()
    {
        var request = new CreateUserRequest("alice", "Alice A", "alice@example.com", "password123");

        var createResponse = await _client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/users/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("alice", fetched.Username);
    }

    [Fact]
    public async Task ListUsers_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCreatedUser_ReturnsUser()
    {
        var createRequest = new CreateUserRequest("charlie", "Charlie C", "charlie@example.com", "password123");
        await _client.PostAsJsonAsync("/api/users", createRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("charlie", "password123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var user = await loginResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Equal("charlie", user!.Username);
    }

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        var createRequest = new CreateUserRequest("delete-me", "Delete Me", "delete-me@example.com", "password123");
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync($"/api/users/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var getResponse = await _client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenUserMissing()
    {
        var response = await _client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
