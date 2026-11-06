using Mercurius.LAN.Web.Models.Sponsors;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.LAN.Web.DTOs.Sponsors
{
    public class SponsorManagementDTO
    {
        [Required]
        public string Name { get; set; } = null!;

        [Required]
        public SponsorTier SponsorTier { get; set; }


        [RequiredIfCreateMode(ErrorMessageResourceName = nameof(Logo))]
        public IBrowserFile? Logo { get; set; } = null!;

        public IBrowserFile? LightLogo { get; set; }

        public bool RemoveLightLogo { get; set; }

        public string? LogoUrl { get; set; }

        public string? LightLogoUrl { get; set; }

        public bool IsCreateMode { get; set; } = true;

        [Required]
        public string InfoUrl { get; set; } = null!;

        public string? Description { get; set; }

        public int Id { get; set; } = -1;

        public SponsorManagementDTO(Sponsor sponsor)
        {
            Name = sponsor.Name;
            SponsorTier = sponsor.SponsorTier;
            InfoUrl = sponsor.InfoUrl;
            Description = sponsor.Description;
            Id = sponsor.Id;
            LogoUrl = sponsor.LogoUrl;
            LightLogoUrl = sponsor.LightLogoUrl;
            IsCreateMode = false;
        }
        public SponsorManagementDTO()
        {
            
        }
    }
}
