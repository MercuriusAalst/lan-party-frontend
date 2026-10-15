namespace Mercurius.LAN.Web.DTOs.Matches;

/// <summary>A match that waits for an admin to set its final score (GET /v1/lan/matches/pending-resolution).</summary>
public sealed class PendingMatchResolutionDTO
{
    public Guid MatchId { get; init; }
    public Guid TournamentId { get; init; }
    public string TournamentName { get; init; } = string.Empty;
    public string Participant1Name { get; init; } = string.Empty;
    public string Participant2Name { get; init; } = string.Empty;

    /// <summary>Participant 1's score in the reported result, when the backend exposes one.</summary>
    public int? Participant1Score { get; init; }

    /// <summary>Participant 2's score in the reported result, when the backend exposes one.</summary>
    public int? Participant2Score { get; init; }

    public MatchResolutionReason Reason { get; init; }
    public DateTime RequiredSinceUtc { get; init; }
}
