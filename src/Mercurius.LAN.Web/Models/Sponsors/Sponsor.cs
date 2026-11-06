namespace Mercurius.LAN.Web.Models.Sponsors
{
    public class Sponsor
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public SponsorTier SponsorTier { get; set; }
        public string LogoUrl { get; set; } = null!;
        public string? LightLogoUrl { get; set; }
        public string InfoUrl { get; set; } = null!;
        public string? Description { get; set; }
    }
}
