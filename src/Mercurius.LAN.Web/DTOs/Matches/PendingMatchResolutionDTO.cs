using System.Text.Json.Serialization;

namespace Mercurius.LAN.Web.DTOs.Matches;

/// <summary>A match that waits for an admin to set its final score (GET /v1/lan/matches/pending-resolution).</summary>
public sealed class PendingMatchResolutionDTO
{
    public Guid MatchId { get; init; }
    public Guid TournamentId { get; init; }
    public string TournamentName { get; init; } = string.Empty;
    public string Participant1Name { get; init; } = string.Empty;
    public string Participant2Name { get; init; } = string.Empty;

    // Unconfirmed claims, named like MatchActionStateDTO: Participant{N}ReportedScore1/2 is what side N reported
    // as (participant 1 score, participant 2 score). Never a final result.
    public int? Participant1ReportedScore1 { get; init; }
    public int? Participant1ReportedScore2 { get; init; }
    public int? Participant2ReportedScore1 { get; init; }
    public int? Participant2ReportedScore2 { get; init; }

    public MatchResolutionReason Reason { get; init; }

    /// <summary>How the match relates to the signed-in admin. Null when an older backend omits it or sends an unknown value.</summary>
    [JsonConverter(typeof(PendingMatchResolutionAssignmentConverter))]
    public PendingMatchResolutionAssignment? Assignment { get; init; }

    /// <summary>Null while the backend has not recorded when the match started waiting (or for older matches).</summary>
    public DateTime? RequiredSinceUtc { get; init; }
}
