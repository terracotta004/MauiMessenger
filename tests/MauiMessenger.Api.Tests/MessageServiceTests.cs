using MauiMessenger.Api.Hubs;
using MauiMessenger.Api.Services;
using MauiMessenger.Api.Tests.Fakes;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using Microsoft.Extensions.Options;

namespace MauiMessenger.Api.Tests;

public class MessageServiceTests
{
    [Fact]
    public async Task CreateAsync_ThrowsWhenConversationMissing()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(messageRepository, conversationRepository, userService, Options.Create(new AgentMessengerOptions()), hubContext);
        var request = new CreateMessageRequest(Guid.NewGuid(), Guid.NewGuid(), "Hello");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_SendsMessageToConversationGroup()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(messageRepository, conversationRepository, userService, Options.Create(new AgentMessengerOptions()), hubContext);
        var conversationId = Guid.NewGuid();
        var sender = await userService.EnsureParticipantAsync("ben@example.com", "Ben");
        await conversationRepository.AddAsync(new Conversation
        {
            Id = conversationId,
            Title = "Chat",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConversationUsers =
            [
                new ConversationUser { ConversationId = conversationId, UserId = sender.Id }
            ]
        });

        var request = new CreateMessageRequest(conversationId, sender.Id, "  Hi there  ");
        var created = await service.CreateAsync(request);

        var clientProxy = ((FakeHubClients)hubContext.Clients).ClientProxy;
        var messageCall = Assert.Single(clientProxy.Calls, call => call.Method == "MessageReceived");
        var conversationCall = Assert.Single(clientProxy.Calls, call => call.Method == "ConversationUpdated");
        Assert.Equal($"conversation:{conversationId}", messageCall.Group);
        Assert.Equal(created, messageCall.Args[0]);
        Assert.Equal($"user:{sender.Id}", conversationCall.Group);
        Assert.Equal("Hi there", created.Content);
    }

    [Fact]
    public async Task CreateAsync_RejectsHumanObserverMessageInAgentConversation()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var observer = await userService.EnsureParticipantAsync("ben1", "Ben");
        var gemini = await userService.EnsureParticipantAsync("agent:gemini");
        var openAi = await userService.EnsureParticipantAsync("agent:openai");
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(messageRepository, conversationRepository, userService, Options.Create(new AgentMessengerOptions()), hubContext);
        var conversationId = Guid.NewGuid();
        await conversationRepository.AddAsync(new Conversation
        {
            Id = conversationId,
            Title = "Gemini / OpenAI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConversationUsers =
            [
                new ConversationUser { ConversationId = conversationId, UserId = gemini.Id },
                new ConversationUser { ConversationId = conversationId, UserId = openAi.Id },
                new ConversationUser { ConversationId = conversationId, UserId = observer.Id }
            ]
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateMessageRequest(conversationId, observer.Id, "I should only observe.")));

        Assert.Equal("Human observers cannot send messages in agent-to-agent conversations.", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_MarksOwnMessageDeletedAndBroadcasts()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var sender = await userService.EnsureParticipantAsync("ben@example.com", "Ben");
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(messageRepository, conversationRepository, userService, Options.Create(new AgentMessengerOptions()), hubContext);
        var conversationId = Guid.NewGuid();
        var message = await messageRepository.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = sender.Id,
            Content = "Remove me",
            SentAt = DateTime.UtcNow
        });

        var deleted = await service.DeleteAsync(message.Id, sender.Id);

        Assert.True(deleted.IsDeleted);
        Assert.Equal(string.Empty, deleted.Content);
        var clientProxy = ((FakeHubClients)hubContext.Clients).ClientProxy;
        var messageCall = Assert.Single(clientProxy.Calls, call => call.Method == "MessageDeleted");
        Assert.Equal($"conversation:{conversationId}", messageCall.Group);
        Assert.Equal(deleted, messageCall.Args[0]);
    }

    [Fact]
    public async Task DeleteAsync_RejectsMessageFromDifferentSender()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var sender = await userService.EnsureParticipantAsync("ben@example.com", "Ben");
        var otherUser = await userService.EnsureParticipantAsync("sam@example.com", "Sam");
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(messageRepository, conversationRepository, userService, Options.Create(new AgentMessengerOptions()), hubContext);
        var message = await messageRepository.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderId = sender.Id,
            Content = "Keep me",
            SentAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(message.Id, otherUser.Id));

        Assert.False(message.IsDeleted);
    }

    [Fact]
    public async Task SendAgentMessageAsync_AddsBen1ObserverToAgentToAgentConversation()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        await userService.EnsureParticipantAsync("ben1", "Ben");
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(
            messageRepository,
            conversationRepository,
            userService,
            Options.Create(new AgentMessengerOptions { HumanObserverUsername = "ben1" }),
            hubContext);

        await service.SendAgentMessageAsync(new AgentMessageRequest(
            Guid.NewGuid().ToString("N"),
            "agent:gemini",
            "agent:openai",
            "Demo",
            "Hello",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>()));

        var conversations = await conversationRepository.ListAllAsync();
        var conversation = Assert.Single(conversations);
        var ben = await userService.GetByUsernameAsync("ben1");

        Assert.NotNull(ben);
        Assert.Contains(conversation.ConversationUsers, participant => participant.UserId == ben!.Id);
        Assert.Equal(3, conversation.ConversationUsers.Count);
    }

    [Fact]
    public async Task SendAgentMessageAsync_AddsBen1ObserverToExistingAgentToAgentConversation()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var ben = await userService.EnsureParticipantAsync("ben1", "Ben");
        var gemini = await userService.EnsureParticipantAsync("agent:gemini");
        var openAi = await userService.EnsureParticipantAsync("agent:openai");
        await conversationRepository.AddAsync(new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Gemini / OpenAI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConversationUsers =
            [
                new ConversationUser { UserId = gemini.Id },
                new ConversationUser { UserId = openAi.Id }
            ]
        });
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(
            messageRepository,
            conversationRepository,
            userService,
            Options.Create(new AgentMessengerOptions { HumanObserverUsername = "ben1" }),
            hubContext);

        await service.SendAgentMessageAsync(new AgentMessageRequest(
            Guid.NewGuid().ToString("N"),
            "agent:gemini",
            "agent:openai",
            "Demo",
            "Hello again",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>()));

        var conversation = Assert.Single(await conversationRepository.ListAllAsync());
        Assert.Contains(conversation.ConversationUsers, participant => participant.UserId == ben.Id);
        Assert.Equal(3, conversation.ConversationUsers.Count);
    }

    [Fact]
    public async Task SendAgentMessageAsync_DeletesPreviousHumanObserverMessagesInAgentConversation()
    {
        var messageRepository = new FakeMessageRepository();
        var conversationRepository = new FakeConversationRepository();
        var userService = new FakeUserService();
        var ben = await userService.EnsureParticipantAsync("ben1", "Ben");
        var otherObserver = await userService.EnsureParticipantAsync("casey@example.com", "Casey");
        var gemini = await userService.EnsureParticipantAsync("agent:gemini");
        var openAi = await userService.EnsureParticipantAsync("agent:openai");
        var conversationId = Guid.NewGuid();
        await conversationRepository.AddAsync(new Conversation
        {
            Id = conversationId,
            Title = "Gemini / OpenAI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConversationUsers =
            [
                new ConversationUser { ConversationId = conversationId, UserId = gemini.Id },
                new ConversationUser { ConversationId = conversationId, UserId = openAi.Id },
                new ConversationUser { ConversationId = conversationId, UserId = ben.Id },
                new ConversationUser { ConversationId = conversationId, UserId = otherObserver.Id }
            ]
        });
        var observerMessage = await messageRepository.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = ben.Id,
            Content = "Legacy observer note",
            SentAt = DateTime.UtcNow
        });
        var otherObserverMessage = await messageRepository.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = otherObserver.Id,
            Content = "Also should go",
            SentAt = DateTime.UtcNow
        });
        var hubContext = new FakeHubContext<MessageHub>();
        var service = new MessageService(
            messageRepository,
            conversationRepository,
            userService,
            Options.Create(new AgentMessengerOptions { HumanObserverUsername = "ben1" }),
            hubContext);

        await service.SendAgentMessageAsync(new AgentMessageRequest(
            Guid.NewGuid().ToString("N"),
            "agent:gemini",
            "agent:openai",
            "Demo",
            "Hello again",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>()));

        Assert.True(observerMessage.IsDeleted);
        Assert.Equal(string.Empty, observerMessage.Content);
        Assert.True(otherObserverMessage.IsDeleted);
        Assert.Equal(string.Empty, otherObserverMessage.Content);
        var clientProxy = ((FakeHubClients)hubContext.Clients).ClientProxy;
        Assert.Equal(2, clientProxy.Calls.Count(call => call.Method == "MessageDeleted"
            && call.Group == $"conversation:{conversationId}"));
    }
}
