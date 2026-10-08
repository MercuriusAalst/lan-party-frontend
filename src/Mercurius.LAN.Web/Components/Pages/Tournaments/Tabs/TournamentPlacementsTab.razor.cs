using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Matches;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;

public partial class TournamentPlacementsTab
{
    [Parameter] public IEnumerable<Placement> Placements { get; set; } = Enumerable.Empty<Placement>();
    [Parameter] public ParticipationMode ParticipationMode { get; set; }
    [Parameter] public BracketType BracketType { get; set; }
    [Parameter] public LeaderboardRankingMetric? RankingMetric { get; set; }
    [Parameter] public string? FirstPlacePrize { get; set; }
    [Parameter] public string? SecondPlacePrize { get; set; }
    [Parameter] public string? ThirdPlacePrize { get; set; }

    private bool HasPrizes =>
        !string.IsNullOrWhiteSpace(FirstPlacePrize) ||
        !string.IsNullOrWhiteSpace(SecondPlacePrize) ||
        !string.IsNullOrWhiteSpace(ThirdPlacePrize);

    private IEnumerable<(int Place, string Prize)> GetConfiguredPrizes()
    {
        if(!string.IsNullOrWhiteSpace(FirstPlacePrize))
            yield return (1, FirstPlacePrize!);
        if(!string.IsNullOrWhiteSpace(SecondPlacePrize))
            yield return (2, SecondPlacePrize!);
        if(!string.IsNullOrWhiteSpace(ThirdPlacePrize))
            yield return (3, ThirdPlacePrize!);
    }

    private string? GetPrizeForPlace(int place) => place switch
    {
        1 => string.IsNullOrWhiteSpace(FirstPlacePrize) ? null : FirstPlacePrize,
        2 => string.IsNullOrWhiteSpace(SecondPlacePrize) ? null : SecondPlacePrize,
        3 => string.IsNullOrWhiteSpace(ThirdPlacePrize) ? null : ThirdPlacePrize,
        _ => null
    };

    private string FormatLeaderboardParticipantValue(LeaderboardRowDTO participant) =>
        LeaderboardFormattingExtensions.FormatLeaderboardValue(
            RankingMetric ?? LeaderboardRankingMetric.HighestScore,
            participant.Score,
            participant.DurationMilliseconds);

    private IEnumerable<string> GetUserParticipantNames(Placement placement)
    {
        return ParticipationMode == ParticipationMode.Individual
            ? placement.Users.Select(GetUserLabel)
            : Enumerable.Empty<string>();
    }

    private static string BuildTeamProfileHref(string teamName) =>
        string.IsNullOrWhiteSpace(teamName)
            ? string.Empty
            : $"/teams/{Uri.EscapeDataString(teamName.Trim())}";

    private string GetUserLabel(DTOs.Users.PublicUserDTO user)
    {
        if(!string.IsNullOrWhiteSpace(user.Username))
            return user.Username.Trim();

        return Localization["Feature.tournaments.participant"];
    }

    internal static string GetOrdinalLabel(ILocalizationService localization, int number)
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

    internal static string GetPlacementLabel(ILocalizationService localization, int number)
    {
        // The ordinal already carries the language suffix, so the composed template only adds the noun.
        return localization.Get("Feature.tournaments.placementPlace", GetOrdinalLabel(localization, number));
    }
}
