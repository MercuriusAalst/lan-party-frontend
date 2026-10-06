using System.Globalization;
using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.Extensions;

public static class LeaderboardFormattingExtensions
{
    public static string FormatLeaderboardScore(decimal score) =>
        score.ToString("0.######", CultureInfo.InvariantCulture);

    public static string FormatLeaderboardDuration(long durationMilliseconds)
    {
        // Durations are positive whole milliseconds; integer arithmetic keeps total hours exact and cannot overflow.
        var totalMilliseconds = Math.Max(0, durationMilliseconds);
        var milliseconds = totalMilliseconds % 1000;
        var totalSeconds = totalMilliseconds / 1000;
        var seconds = totalSeconds % 60;
        var totalMinutes = totalSeconds / 60;
        var minutes = totalMinutes % 60;
        var hours = totalMinutes / 60;

        var millisecondPart = milliseconds.ToString("000", CultureInfo.InvariantCulture);
        var secondPart = seconds.ToString("00", CultureInfo.InvariantCulture);

        if(hours >= 1)
        {
            var hourPart = hours.ToString(CultureInfo.InvariantCulture);
            var minutePart = minutes.ToString("00", CultureInfo.InvariantCulture);
            return $"{hourPart}:{minutePart}:{secondPart}.{millisecondPart}";
        }

        return $"{minutes.ToString(CultureInfo.InvariantCulture)}:{secondPart}.{millisecondPart}";
    }

    public static string FormatLeaderboardValue(
        LeaderboardRankingMetric metric,
        decimal? score,
        long? durationMilliseconds)
    {
        if(metric == LeaderboardRankingMetric.HighestScore)
            return score.HasValue ? FormatLeaderboardScore(score.Value) : string.Empty;

        return durationMilliseconds.HasValue
            ? FormatLeaderboardDuration(durationMilliseconds.Value)
            : string.Empty;
    }

    public static LeaderboardRankingMetric ResolveLeaderboardMetric(this Tournament tournament) =>
        tournament.LeaderboardRankingMetric ?? LeaderboardRankingMetric.HighestScore;

    public static bool IsLeaderboard(this Tournament tournament) =>
        tournament.BracketType == BracketType.Leaderboard;
}
