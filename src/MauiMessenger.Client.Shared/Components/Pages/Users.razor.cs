using Microsoft.AspNetCore.Components;
using MauiMessenger.Client.Shared.Services;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;

namespace MauiMessenger.Client.Shared.Components.Pages;

public partial class Users
{
    [Inject] private IApiClient ApiClient { get; set; } = default!;

    private List<UserDto>? users;
    private CreateUserForm newUser = new();
    private bool isSaving;
    private Guid? deletingUserId;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        errorMessage = null;
        users = (await ApiClient.GetUsersAsync()).ToList();
    }

    private async Task CreateUserAsync()
    {
        errorMessage = null;
        isSaving = true;

        try
        {
            var request = new CreateUserRequest(
                newUser.Username,
                newUser.DisplayName,
                newUser.Email,
                newUser.Password,
                newUser.ParticipantType);
            await ApiClient.CreateUserAsync(request);
            newUser = new CreateUserForm();
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task DeleteUserAsync(UserDto user)
    {
        errorMessage = null;
        deletingUserId = user.Id;

        try
        {
            await ApiClient.DeleteUserAsync(user.Id);
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            deletingUserId = null;
        }
    }

    private sealed class CreateUserForm
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public ParticipantType ParticipantType { get; set; } = ParticipantType.Human;
    }
}
