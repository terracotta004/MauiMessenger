using MauiMessenger.Core.DTOs;

namespace MauiMessenger.Client.Shared.Services;

public sealed class CurrentUserState
{
    public event Action? Changed;
    public UserDto? CurrentUser { get; private set; }
    public bool IsLoaded { get; private set; }

    public void SetUser(UserDto? user)
    {
        CurrentUser = user;
        IsLoaded = true;
        Changed?.Invoke();
    }

    public async Task EnsureLoadedAsync(IAuthSessionClient authSessionClient, CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        try
        {
            var user = await authSessionClient.GetCurrentUserAsync(cancellationToken);
            SetUser(user);
        }
        catch (Exception ex) when (IsRecoverableSessionLoadFailure(ex, cancellationToken))
        {
            SetUser(null);
        }
    }

    private static bool IsRecoverableSessionLoadFailure(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException
            || exception is InvalidOperationException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
}
