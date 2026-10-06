using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.DTOs.Leaderboards;

public class AdminLeaderboardResponseDTO
{
    public Guid TournamentId { get; set; }
    public LeaderboardRankingMetric RankingMetric { get; set; }
    public List<AdminLeaderboardParticipantDTO> Participants { get; set; } = [];
}
