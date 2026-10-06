namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class RecordLeaderboardAttemptDTO
{
    public Guid? ParticipantId { get; set; }
    public Guid? LinkedUserId { get; set; }
    public string? GuestDisplayName { get; set; }
    public decimal? Score { get; set; }
    public long? DurationMilliseconds { get; set; }
}
