using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class AdminLeaderboardParticipantDTO
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public LeaderboardParticipantKind ParticipantKind { get; set; }
    public Guid? LinkedUserId { get; set; }
    public List<LeaderboardAttemptDTO> Attempts { get; set; } = [];
}
