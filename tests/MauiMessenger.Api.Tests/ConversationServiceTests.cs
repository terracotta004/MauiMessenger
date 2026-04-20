using MauiMessenger.Api.Services;
using MauiMessenger.Api.Tests.Fakes;
using MauiMessenger.Api.Hubs;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;

namespace MauiMessenger.Api.Tests;

public class ConversationServiceTests
{
    [Fact]
    public async Task CreateAsync_DeduplicatesParticipantsAndSetsConversationId()
    {
        var repository = new FakeConversationRepository();
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new ConversationService(repository, hubContext);
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var request = new CreateConversationRequest(" Team Chat ", new[] { userId, userId });

        var created = await service.CreateAsync(request);

        var saved = (await repository.ListByUserIdAsync(userId)).Single();
        Assert.Equal("Team Chat", saved.Title);
        Assert.Single(saved.ConversationUsers);
        Assert.All(saved.ConversationUsers, participant => Assert.Equal(saved.Id, participant.ConversationId));
        Assert.Equal(created.Id, saved.Id);
    }

    [Fact]
    public async Task ListByUserIdAsync_ReturnsConversationsForUser()
    {
        var repository = new FakeConversationRepository();
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new ConversationService(repository, hubContext);
        var userId = Guid.NewGuid();
        var request = new CreateConversationRequest("Chat", new[] { userId });

        await service.CreateAsync(request);

        var conversations = await service.ListByUserIdAsync(userId);

        Assert.Single(conversations);
        Assert.Equal("Chat", conversations[0].Title);
        Assert.Contains(userId, conversations[0].ParticipantIds);
    }

}
