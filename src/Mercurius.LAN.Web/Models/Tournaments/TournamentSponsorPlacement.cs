using Mercurius.LAN.Web.Models.Sponsors;

namespace Mercurius.LAN.Web.Models.Tournaments;

public class TournamentSponsorPlacement
{
    public int Id { get; set; }
    public int SponsorId { get; set; }
    public string SponsorName { get; set; } = string.Empty;
    public SponsorTier SponsorTier { get; set; }
    public string SponsorLogoUrl { get; set; } = string.Empty;
    public string? SponsorLightLogoUrl { get; set; }
    public string SponsorInfoUrl { get; set; } = string.Empty;
    public string? SponsorDescription { get; set; }
    public SponsorContext Context { get; set; }
    public string? Headline { get; set; }
    public string? SupportLine { get; set; }
    public int DisplayOrder { get; set; }
}
