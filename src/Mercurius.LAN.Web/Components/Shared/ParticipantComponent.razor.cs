using Mercurius.LAN.Web.DTOs.Users;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Shared;

public partial class ParticipantComponent
{
    [Parameter] public ParticipantViewModel? Participant { get; set; }
    [Parameter] public string EmptyLabel { get; set; } = string.Empty;
    [Parameter] public bool ShowIdentityHeader { get; set; } = true;
    [Parameter] public bool CanViewUserDetails { get; set; }
    [Parameter] public EventCallback<PublicUserDTO> OnUserSelected { get; set; }

    /// <summary>Optional: the team name becomes a button raising this instead of a link to the team page.</summary>
    [Parameter] public EventCallback<ParticipantViewModel> OnTeamSelected { get; set; }

    /// <summary>When true every team member can be selected, not only the captain (administrators).</summary>
    [Parameter] public bool CanSelectAllMembers { get; set; }

    private bool CanOpenUserInfo => OnUserSelected.HasDelegate;

    private string GetUserLabel(PublicUserDTO user)
    {
        if(!string.IsNullOrWhiteSpace(user.Username))
            return user.Username.Trim();

        return Localization["shared.user"];
    }

    private Task SelectUserAsync(PublicUserDTO user) => OnUserSelected.InvokeAsync(user);

    private static string GetTeamProfileHref(Models.Participants.Team team)
    {
        return $"/teams/{Uri.EscapeDataString(team.Name.Trim())}";
    }

    private static bool HasTeamName(Models.Participants.Team team) =>
        !string.IsNullOrWhiteSpace(team.Name);

}
