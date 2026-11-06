using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.Models.Sponsors;
using System.Net.Http;

namespace Mercurius.LAN.Web.Services
{
    public interface ISponsorService
    {
        Task<IEnumerable<Sponsor>> GetSponsorsAsync();
        Task<Sponsor> GetSponsorByIdAsync(int id);
        Task<Sponsor> CreateSponsorAsync(
            SponsorManagementDTO createSponsorDTO,
            string? logoTempFilePath,
            string? logoContentType,
            string? logoFileName,
            string? lightLogoTempFilePath,
            string? lightLogoContentType,
            string? lightLogoFileName);
        Task<Sponsor> UpdateSponsorAsync(
            int id,
            SponsorManagementDTO updateSponsorDTO,
            string? logoTempFilePath,
            string? logoContentType,
            string? logoFileName,
            string? lightLogoTempFilePath,
            string? lightLogoContentType,
            string? lightLogoFileName);
        Task DeleteSponsorAsync(int id);
    }
}
