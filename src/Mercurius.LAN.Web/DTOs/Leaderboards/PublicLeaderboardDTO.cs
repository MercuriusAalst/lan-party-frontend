using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class PublicLeaderboardDTO
{
    public Guid TournamentId { get; set; }
    public LeaderboardRankingMetric RankingMetric { get; set; }
    public List<LeaderboardRowDTO> Rows { get; set; } = [];
}
