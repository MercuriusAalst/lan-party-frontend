using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentSponsorBadgeTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["MercuriusAPI:BaseAddress"] = "https://api.example.test" })
        .Build();

    [Fact]
    public void SponsorWithLogosProducesResolvedBadge()
    {
        var badge = Sponsored("Mercurius Tech", "/media/logo.svg", "/media/logo-light.svg").GetSponsorBadge(Configuration);

        Assert.NotNull(badge);
        Assert.Equal("Mercurius Tech", badge.SponsorName);
        Assert.Equal(AssetUrlResolver.Resolve(Configuration, "/media/logo.svg"), badge.LogoUrl);
        Assert.Equal(AssetUrlResolver.Resolve(Configuration, "/media/logo-light.svg"), badge.LightLogoUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingLightLogoProducesNullLightLogo(string? lightLogoUrl)
    {
        var badge = Sponsored("Mercurius Tech", "/media/logo.svg", lightLogoUrl).GetSponsorBadge(Configuration);

        Assert.NotNull(badge);
        Assert.Equal(AssetUrlResolver.Resolve(Configuration, "/media/logo.svg"), badge.LogoUrl);
        Assert.Null(badge.LightLogoUrl);
    }

    [Fact]
    public void UnsponsoredTournamentProducesNoBadge()
    {
        Assert.Null(new Tournament().GetSponsorBadge(Configuration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSponsorNameProducesNoBadge(string sponsorName)
    {
        Assert.Null(Sponsored(sponsorName, "/media/logo.svg", null).GetSponsorBadge(Configuration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingLogoProducesNoBadgeSoTheTextAttributionRenders(string logoUrl)
    {
        var tournament = Sponsored("Mercurius Tech", logoUrl, "/media/logo-light.svg");

        Assert.Null(tournament.GetSponsorBadge(Configuration));
        Assert.Equal("Presented by Mercurius Tech", tournament.GetPresentedByLabel(TestLocalizationService.Instance));
    }

    private static Tournament Sponsored(string sponsorName, string logoUrl, string? lightLogoUrl) => new()
    {
        SponsorPlacement = new TournamentSponsorPlacement
        {
            SponsorName = sponsorName,
            SponsorLogoUrl = logoUrl,
            SponsorLightLogoUrl = lightLogoUrl
        }
    };
}
