using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Registrations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
    private ModalFocusTrap? _focusTrap;

    private bool IsOpen => Participant is not null && Members is not null;
    private string DialogTitleId => $"admin-roster-dialog-title-{Participant?.Id.ToString("N") ?? "team"}";

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        (_focusTrap ??= new ModalFocusTrap(JSRuntime)).SyncAsync(IsOpen, _dialogElement);

    private Task HandleKeyDown(KeyboardEventArgs args) =>
        string.Equals(args.Key, "Escape", StringComparison.Ordinal)
            ? CloseAsync()
            : Task.CompletedTask;

    private Task CloseAsync() => OnClose.InvokeAsync();

    private static string? GetTeamHref(ParticipantViewModel participant) =>
        string.IsNullOrWhiteSpace(participant.Team?.Name)
            ? null
            : $"/teams/{Uri.EscapeDataString(participant.Team.Name.Trim())}";

    private string GetUserLabel(TournamentRosterMemberDTO member) =>
        string.IsNullOrWhiteSpace(member.User?.Username)
            ? Localization["Feature.tournaments.unknownUser"]
            : member.User.Username.Trim();

    private string GetConfirmationLabel(RosterMemberConfirmationStatus status) =>
        RosterConfirmationLabels.Get(Localization, status);

    public ValueTask DisposeAsync() => _focusTrap?.DisposeAsync() ?? ValueTask.CompletedTask;
}
