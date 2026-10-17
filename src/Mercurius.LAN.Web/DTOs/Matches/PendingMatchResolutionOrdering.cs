namespace Mercurius.LAN.Web.DTOs.Matches;

public static class PendingMatchResolutionOrdering
{
    /// <summary>
    /// Escalated and unavailable-admin matches first, then the oldest waiting first. A match without a
    /// known since-time goes last within its group.
    /// </summary>
    public static List<PendingMatchResolutionDTO> ByUrgency(IEnumerable<PendingMatchResolutionDTO> items) =>
        items
            .OrderBy(item => item.Assignment is PendingMatchResolutionAssignment.Escalated or PendingMatchResolutionAssignment.AssignedAdminUnavailable ? 0 : 1)
            .ThenBy(item => item.RequiredSinceUtc is null)
            .ThenBy(item => item.RequiredSinceUtc)
            .ThenBy(item => item.TournamentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.MatchId)
            .ToList();
}
