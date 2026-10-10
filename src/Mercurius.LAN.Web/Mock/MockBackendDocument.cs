using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Sponsors;

namespace Mercurius.LAN.Web.Mock;

internal sealed class MockBackendDocument
{
    public List<TournamentExtended> Tournaments { get; set; } = [];
    public List<Guid> FeaturedTournamentIds { get; set; } = [];
    public List<Team> Teams { get; set; } = [];
    public List<UserDTO> Users { get; set; } = [];
    public List<MockProfileRecord> Profiles { get; set; } = [];
    public List<Sponsor> Sponsors { get; set; } = [];
}

internal sealed class MockProfileRecord
{
    public string Persona { get; set; } = string.Empty;

    public CurrentUserProfileResponse Profile { get; set; } =
        new(false, null, null, false);
}
