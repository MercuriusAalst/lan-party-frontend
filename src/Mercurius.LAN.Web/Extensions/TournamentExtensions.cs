using Mercurius.LAN.Web.Models;
using Mercurius.LAN.Web.Models.Tournaments;

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
}
