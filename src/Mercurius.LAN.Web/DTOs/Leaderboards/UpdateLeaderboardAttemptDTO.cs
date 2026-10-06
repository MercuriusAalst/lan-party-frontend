namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class UpdateLeaderboardAttemptDTO
{
    public decimal? Score { get; set; }
    public long? DurationMilliseconds { get; set; }
    public Guid? RowVersion { get; set; }
}
