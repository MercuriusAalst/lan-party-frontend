using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class LeaderboardRowDTO
{
    public int Rank { get; set; }
    public Guid ParticipantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public LeaderboardParticipantKind ParticipantKind { get; set; }
    public Guid? LinkedUserId { get; set; }
    public decimal? Score { get; set; }
    public long? DurationMilliseconds { get; set; }
}
