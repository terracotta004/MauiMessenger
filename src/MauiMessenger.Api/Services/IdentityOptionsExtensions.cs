using Microsoft.AspNetCore.Identity;

namespace MauiMessenger.Api.Services;

public static class IdentityOptionsExtensions
{
    public const string MauiMessengerUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+:";

    public static void ConfigureMauiMessengerIdentity(this IdentityOptions options)
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 4;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = MauiMessengerUserNameCharacters;
    }
}
