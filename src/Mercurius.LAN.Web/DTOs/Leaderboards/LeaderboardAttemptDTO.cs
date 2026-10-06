namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class LeaderboardAttemptDTO
{
    public Guid Id { get; set; }
    public decimal? Score { get; set; }
    public long? DurationMilliseconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid RowVersion { get; set; }
}
