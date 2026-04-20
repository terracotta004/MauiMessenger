using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Api.Tests.Integration;

public class MessagesControllerTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MessagesControllerTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListByConversation_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/messages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMessage_ReturnsNotFound_WhenConversationMissing()
    {
        var request = new CreateMessageRequest(Guid.NewGuid(), Guid.NewGuid(), "Hello");

        var response = await _client.PostAsJsonAsync("/api/messages", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateMessage_AcceptsAgentMessengerPayloadWithPascalCaseProperties()
    {
        var users = await _client.GetFromJsonAsync<IReadOnlyList<UserDto>>("/api/users")
            ?? Array.Empty<UserDto>();
        if (!users.Any(user => user.Username == "ben1"))
        {
            var userResponse = await _client.PostAsJsonAsync(
                "/api/users",
                new CreateUserRequest("ben1", "Ben", "ben1@example.com", "password123"));
            userResponse.EnsureSuccessStatusCode();
        }

        var request = new
        {
            Id = Guid.NewGuid().ToString("N"),
            From = "bot:support",
            To = "agent:ops",
            Subject = "Escalation",
            Body = "Need approval for deploy window.",
            SentAtUtc = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };
        message.Headers.Add("X-AgentMessenger-Key", "dev-agentmessenger-key");

        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
