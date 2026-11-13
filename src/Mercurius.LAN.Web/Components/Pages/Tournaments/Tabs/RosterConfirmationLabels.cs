using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Localization;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;

internal static class RosterConfirmationLabels
{
    public static string Get(ILocalizationService localization, RosterMemberConfirmationStatus status) => status switch
    {
        RosterMemberConfirmationStatus.AutoConfirmed => localization["Feature.tournaments.confirmationCaptain"],
        RosterMemberConfirmationStatus.Pending => localization["Feature.tournaments.confirmationPending"],
        RosterMemberConfirmationStatus.Confirmed => localization["Feature.tournaments.confirmationConfirmed"],
        _ => localization.Get("Feature.tournaments.confirmationStatus", status.ToString())
    };
}
