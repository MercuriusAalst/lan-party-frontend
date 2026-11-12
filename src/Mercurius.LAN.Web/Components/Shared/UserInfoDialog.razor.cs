using Mercurius.LAN.Web.DTOs.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.Components.Shared;

public partial class UserInfoDialog
{
    [Parameter] public PublicUserDTO? User { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Optional: shows a back button with <see cref="BackLabel"/> (e.g. back to a team roster).</summary>
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public string? BackLabel { get; set; }

    private ElementReference _dialogElement;
    private IJSObjectReference? _focusTrap;
    private string DialogTitleId => $"user-info-dialog-title-{User?.Id.ToString("N") ?? "details"}";
    private string DialogDescriptionId => $"user-info-dialog-description-{User?.Id.ToString("N") ?? "details"}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(User is not null && _focusTrap is null)
        {
            _focusTrap = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "activateTeamModalFocusTrap",
                _dialogElement);
        }
        else if(User is null && _focusTrap is not null)
        {
            await DisposeFocusTrapAsync();
        }
    }

    private Task HandleKeyDown(KeyboardEventArgs args) =>
        string.Equals(args.Key, "Escape", StringComparison.Ordinal)
            ? CloseAsync()
            : Task.CompletedTask;

    private Task CloseAsync() => OnClose.InvokeAsync();

    private string GetDialogTitle(PublicUserDTO user)
    {
        var fullName = string.Join(" ", new[] { user.Firstname, user.Lastname }
            .Where(HasValue)
            .Select(value => value!.Trim()));

        if(!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        return HasValue(user.Username) ? user.Username!.Trim() : Localization["shared.playerDetails"];
    }

    private static bool HasPublicDetails(PublicUserDTO user) =>
        HasValue(user.Firstname) ||
        HasValue(user.Lastname) ||
        HasValue(user.Username) ||
        HasValue(user.DiscordId) ||
        HasValue(user.SteamId) ||
        HasValue(user.RiotId);

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private async ValueTask DisposeFocusTrapAsync()
    {
        var focusTrap = _focusTrap;
        _focusTrap = null;
        if(focusTrap is null)
            return;

        try
        {
            await focusTrap.InvokeVoidAsync("dispose");
            await focusTrap.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    public ValueTask DisposeAsync() => DisposeFocusTrapAsync();
}
