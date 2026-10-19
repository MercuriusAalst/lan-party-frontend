namespace Mercurius.LAN.Web.DTOs.Matches;

/// <summary>Why a match is waiting for its assigned admin.</summary>
public enum MatchResolutionReason
{
    /// <summary>Both sides reported and the reported scores conflict.</summary>
    Disputed = 0,

    /// <summary>The score window closed and the opponent never reported a score.</summary>
    MissingOpponentReport = 1
}
