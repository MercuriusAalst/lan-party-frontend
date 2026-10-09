using Mercurius.LAN.Web.Models.Tournaments;

namespace Mercurius.LAN.Web.DTOs.Tournaments;

/// <summary>
/// The featured selection document shared by the administrator write and its confirmation
/// response: tournament ids in curated display order, most important first.
/// </summary>
public class FeaturedTournamentIdsDTO
{
    public List<Guid> TournamentIds { get; set; } = [];
}

/// <summary>
/// The public featured read. <see cref="FeaturedTournamentIdsDTO.TournamentIds"/> is the resolved
/// display order, and <see cref="Tournaments"/> carries the home page cards in that same order.
/// The cards are a minimal summary: only name, image, status, bracket type, and format are
/// populated, so every other <see cref="Tournament"/> member must not be displayed.
/// </summary>
public sealed class FeaturedTournamentsDTO : FeaturedTournamentIdsDTO
{
    public List<Tournament> Tournaments { get; set; } = [];
}
