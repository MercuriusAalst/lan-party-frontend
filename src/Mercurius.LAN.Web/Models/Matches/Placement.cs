using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.Models.Participants;

namespace Mercurius.LAN.Web.Models.Matches
{
    public class Placement
    {
        public int Place { get; set; }
        public List<PublicUserDTO> Users { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
        public List<LeaderboardRowDTO> LeaderboardParticipants { get; set; } = new();
    }
}
