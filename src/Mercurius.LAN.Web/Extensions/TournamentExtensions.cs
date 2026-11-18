using Mercurius.LAN.Web.Models;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Localization;

namespace Mercurius.LAN.Web.Extensions;

public static class TournamentExtensions
{
    public static string GetStatusLabel(this TournamentStatus status)
    {
        return status switch
        {
            TournamentStatus.Scheduled => "Open",
            TournamentStatus.InProgress => "Ongoing",
            TournamentStatus.Completed => "Finished",
            TournamentStatus.Canceled => "Cancelled",
            _ => status.ToString()
        };
    }

    public static string GetLabel(this BracketType bracketType)
    {
        return bracketType switch
        {
            BracketType.SingleElimination => "Single Elimination",
            BracketType.DoubleElimination => "Double Elimination",
            BracketType.RoundRobin => "Round Robin (unsupported)",
            BracketType.Swiss => "Swiss (unsupported)",
            BracketType.Leaderboard => "Leaderboard",
            _ => bracketType.ToString()
        };
    }

    public static string GetLabel(this TournamentFormat tournamentFormat)
    {
        return tournamentFormat switch
        {
            TournamentFormat.BestOf1 => "Best of 1",
            TournamentFormat.BestOf3 => "Best of 3",
            TournamentFormat.BestOf5 => "Best of 5",
            _ => tournamentFormat.ToString()
        };
    }

    public static string GetFormatDetailLabel(this Tournament tournament)
    {
        if(tournament.IsLeaderboard())
            return tournament.BracketType.GetLabel();

        return $"{tournament.BracketType.GetLabel()} · {tournament.Format.GetLabel()}";
    }

    public static string? GetPresentedByLabel(this Tournament tournament, ILocalizationService localization)
    {
        var sponsorName = tournament.SponsorPlacement?.SponsorName;
        return string.IsNullOrWhiteSpace(sponsorName)
            ? null
            : localization.Get("Feature.tournaments.partnerHeading", sponsorName);
    }

    public static TournamentSponsorBadge? GetSponsorBadge(this Tournament tournament, IConfiguration configuration)
    {
        var placement = tournament.SponsorPlacement;
        if(placement is null || string.IsNullOrWhiteSpace(placement.SponsorName))
            return null;

        var logoUrl = AssetUrlResolver.Resolve(configuration, placement.SponsorLogoUrl);
        if(logoUrl.Length == 0)
            return null;

        var lightLogoUrl = AssetUrlResolver.Resolve(configuration, placement.SponsorLightLogoUrl);
        return new TournamentSponsorBadge(placement.SponsorName, logoUrl, lightLogoUrl.Length == 0 ? null : lightLogoUrl);
    }

    public static string GetStatusClass(this TournamentStatus status)
    {
        return status switch
        {
            TournamentStatus.Scheduled => "status-scheduled",
            TournamentStatus.InProgress => "status-inprogress",
            TournamentStatus.Completed => "status-completed",
            TournamentStatus.Canceled => "status-canceled",
            _ => string.Empty
        };
    }

    public static string GetLabel(this ParticipationMode participationMode)
    {
        return participationMode switch
        {
            ParticipationMode.Individual => "Solo",
            ParticipationMode.Team => "Team",
            _ => participationMode.ToString()
        };
    }

    public static IEnumerable<(int Place, string Prize)> GetConfiguredPrizes(this Tournament tournament)
    {
        if(!string.IsNullOrWhiteSpace(tournament.FirstPlacePrize))
            yield return (1, tournament.FirstPlacePrize.Trim());
        if(!string.IsNullOrWhiteSpace(tournament.SecondPlacePrize))
            yield return (2, tournament.SecondPlacePrize.Trim());
        if(!string.IsNullOrWhiteSpace(tournament.ThirdPlacePrize))
            yield return (3, tournament.ThirdPlacePrize.Trim());
    }

    public static string GetOrdinalLabel(ILocalizationService localization, int number)
    {
        // English needs 1st/2nd/3rd suffixes, but the 11th-13th teens keep the default suffix.
        var key = number % 100 is >= 11 and <= 13
            ? "Feature.tournaments.placementOrdinal"
            : (number % 10) switch
            {
                1 => "Feature.tournaments.placementOrdinalFirst",
                2 => "Feature.tournaments.placementOrdinalSecond",
                3 => "Feature.tournaments.placementOrdinalThird",
                _ => "Feature.tournaments.placementOrdinal"
            };

        return localization.Get(key, number);
    }

    public static string GetPlacementLabel(ILocalizationService localization, int number)
    {
        // The ordinal already carries the language suffix, so the composed template only adds the noun.
        return localization.Get("Feature.tournaments.placementPlace", GetOrdinalLabel(localization, number));
    }
}
