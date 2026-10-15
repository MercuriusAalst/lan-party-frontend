using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Participants;

namespace Mercurius.LAN.Web.Models.Tournaments;

public class TournamentExtended : Tournament
{
    // Internal mock assignment metadata; public tournament clones clear it.
    public Guid? AssignedAdminUserId { get; set; }
    public IEnumerable<Placement> Placements { get; set; } = [];
    public IEnumerable<Match> Matches { get; set; } = [];
    public IEnumerable<PublicTournamentRegistrationDTO> Registrations { get; set; } = [];

    // These collections are derived from the canonical registration projection for existing
    // bracket and identity components. They are not populated from separate API calls.
    public IEnumerable<PublicUserDTO> Users { get; set; } = [];
    public IEnumerable<Team> Teams { get; set; } = [];
}
