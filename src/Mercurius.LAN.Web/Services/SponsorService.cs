using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Sponsors;
using System.Net.Http;

namespace Mercurius.LAN.Web.Services
{
    public class SponsorService : ISponsorService
    {
        private readonly ILANClient _lanClient;
        private readonly IConfiguration _configuration;

        public SponsorService(ILANClient lanClient, IConfiguration configuration)
        {
            _lanClient = lanClient;
            _configuration = configuration;
        }

        public async Task<IEnumerable<Sponsor>> GetSponsorsAsync()
        {
            var sponsors = await _lanClient.GetSponsorsAsync();
            foreach(var sponsor in sponsors)
                ResolveLogoUrls(sponsor);

            return sponsors;
        }

        public async Task<Sponsor> GetSponsorByIdAsync(int id)
        {
            var sponsor = await _lanClient.GetSponsorByIdAsync(id);
            return ResolveLogoUrls(sponsor);
        }

        public async Task<Sponsor> CreateSponsorAsync(
            SponsorManagementDTO createSponsorDTO,
            string? logoTempFilePath,
            string? logoContentType,
            string? logoFileName,
            string? lightLogoTempFilePath,
            string? lightLogoContentType,
            string? lightLogoFileName)
        {
            using var formData = new MultipartFormDataContent
                {
                    { new StringContent(createSponsorDTO.Name), "Name" },
                    { new StringContent(createSponsorDTO.InfoUrl), "InfoUrl" },
                    { new StringContent(createSponsorDTO.SponsorTier.ToString()), "SponsorTier" },
                    { new StringContent(createSponsorDTO.Description ?? string.Empty), "Description" },
                };

            AddImage(formData, "Logo", logoTempFilePath, logoContentType, logoFileName);
            AddImage(formData, "LightLogo", lightLogoTempFilePath, lightLogoContentType, lightLogoFileName);

            try
            {
                var sponsor = await _lanClient.CreateSponsorAsync(formData);
                return ResolveLogoUrls(sponsor);
            }
            finally
            {
                DeleteTemporaryFiles(logoTempFilePath, lightLogoTempFilePath);
            }
        }

        public async Task<Sponsor> UpdateSponsorAsync(
            int id,
            SponsorManagementDTO updateSponsorDTO,
            string? logoTempFilePath,
            string? logoContentType,
            string? logoFileName,
            string? lightLogoTempFilePath,
            string? lightLogoContentType,
            string? lightLogoFileName)
        {
            using var formData = new MultipartFormDataContent
                {
                    { new StringContent(updateSponsorDTO.Name), "Name" },
                    { new StringContent(updateSponsorDTO.InfoUrl), "InfoUrl" },
                    { new StringContent(updateSponsorDTO.SponsorTier.ToString()), "SponsorTier" },
                    { new StringContent(updateSponsorDTO.Description ?? string.Empty), "Description" },
                    { new StringContent(updateSponsorDTO.RemoveLightLogo ? "true" : "false"), "RemoveLightLogo" },
                };

            AddImage(formData, "Logo", logoTempFilePath, logoContentType, logoFileName);
            AddImage(formData, "LightLogo", lightLogoTempFilePath, lightLogoContentType, lightLogoFileName);

            try
            {
                var sponsor = await _lanClient.UpdateSponsorAsync(id, formData);
                return ResolveLogoUrls(sponsor);
            }
            finally
            {
                DeleteTemporaryFiles(logoTempFilePath, lightLogoTempFilePath);
            }
        }

        public Task DeleteSponsorAsync(int id)
        {
            return _lanClient.DeleteSponsorAsync(id);
        }

        private Sponsor ResolveLogoUrls(Sponsor sponsor)
        {
            sponsor.LogoUrl = AssetUrlResolver.Resolve(_configuration, sponsor.LogoUrl);
            if(!string.IsNullOrWhiteSpace(sponsor.LightLogoUrl))
                sponsor.LightLogoUrl = AssetUrlResolver.Resolve(_configuration, sponsor.LightLogoUrl);

            return sponsor;
        }

        private static void AddImage(
            MultipartFormDataContent formData,
            string fieldName,
            string? tempFilePath,
            string? contentType,
            string? fileName)
        {
            if(string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath))
                return;

            var streamContent = new StreamContent(File.OpenRead(tempFilePath));
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType!);
            formData.Add(streamContent, fieldName, fileName!);
        }

        private static void DeleteTemporaryFiles(params string?[] filePaths)
        {
            foreach(var filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
            {
                try
                {
                    File.Delete(filePath!);
                }
                catch(IOException)
                {
                }
            }
        }
    }
}
