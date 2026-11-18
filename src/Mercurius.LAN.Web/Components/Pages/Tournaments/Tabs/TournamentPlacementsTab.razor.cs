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
    [Parameter] public IReadOnlyList<(int Place, string Prize)> Prizes { get; set; } = [];

    private string? GetPrizeForPlace(int place) =>
        Prizes.Where(p => p.Place == place).Select(p => p.Prize).FirstOrDefault();

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
}
