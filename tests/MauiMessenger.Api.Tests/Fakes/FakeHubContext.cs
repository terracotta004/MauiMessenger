using Microsoft.AspNetCore.SignalR;

namespace MauiMessenger.Api.Tests.Fakes;

public sealed class FakeHubContext<THub> : IHubContext<THub> where THub : Hub
{
    public FakeHubContext()
    {
        Clients = new FakeHubClients();
        Groups = new FakeGroupManager();
    }

    public IHubClients Clients { get; }
    public IGroupManager Groups { get; }
}

public sealed class FakeHubClients : IHubClients
{
    public FakeClientProxy ClientProxy { get; } = new();

    public IClientProxy All => throw new NotImplementedException();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Client(string connectionId) => throw new NotImplementedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
    public IClientProxy Group(string groupName)
    {
        ClientProxy.LastGroup = groupName;
        return ClientProxy;
    }
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames)
    {
        ClientProxy.LastGroup = string.Join(",", groupNames);
        return ClientProxy;
    }
    public IClientProxy User(string userId) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
}

public sealed class FakeClientProxy : IClientProxy
{
    public List<FakeHubCall> Calls { get; } = new();
    public string? LastMethod { get; private set; }
    public object?[]? LastArgs { get; private set; }
    public string? LastGroup { get; set; }

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        LastMethod = method;
        LastArgs = args;
        Calls.Add(new FakeHubCall(LastGroup, method, args));
        return Task.CompletedTask;
    }
}

public sealed record FakeHubCall(string? Group, string Method, object?[] Args);

public sealed class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
