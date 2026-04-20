using System.Net;

namespace MauiMessenger.Api.Tests.Integration;

public class ConversationsControllerTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConversationsControllerTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListByUser_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/conversations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
