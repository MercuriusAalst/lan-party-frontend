using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Registrations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;

/// <summary>
/// Admin-only popup listing a team participant's registered roster. Modelled on <see cref="UserInfoDialog"/>.
/// Renders nothing while <see cref="Participant"/> or <see cref="Members"/> is null.
/// </summary>
public partial class TournamentAdminRosterDialog
{
    [Parameter] public ParticipantViewModel? Participant { get; set; }
    [Parameter] public IReadOnlyList<TournamentRosterMemberDTO>? Members { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Raised with the member's username when an administrator wants to see that player's details.</summary>
    [Parameter] public EventCallback<string> OnMemberSelected { get; set; }

    private ElementReference _dialogElement;
    private IJSObjectReference? _focusTrap;

    private bool IsOpen => Participant is not null && Members is not null;
    private string DialogTitleId => $"admin-roster-dialog-title-{Participant?.Id.ToString("N") ?? "team"}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(IsOpen && _focusTrap is null)
        {
            _focusTrap = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "activateTeamModalFocusTrap",
                _dialogElement);
        }
        else if(!IsOpen && _focusTrap is not null)
        {
            await DisposeFocusTrapAsync();
        }
    }

    private Task HandleKeyDown(KeyboardEventArgs args) =>
        string.Equals(args.Key, "Escape", StringComparison.Ordinal)
            ? CloseAsync()
            : Task.CompletedTask;

    private Task CloseAsync() => OnClose.InvokeAsync();

    private string GetUserLabel(TournamentRosterMemberDTO member) =>
        string.IsNullOrWhiteSpace(member.User?.Username)
            ? Localization["Feature.tournaments.unknownUser"]
            : member.User.Username.Trim();

    private string GetConfirmationLabel(RosterMemberConfirmationStatus status) =>
        RosterConfirmationLabels.Get(Localization, status);

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
