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

    [Fact]
    public void OverviewShowsEitherTheBadgeOrTheTextAttribution()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor");

        Assert.Contains("tournament.GetSponsorBadge(Configuration)", markup);
        Assert.Contains("@if(sponsorBadge is not null)", markup);
        Assert.Contains("@if(sponsorBadge is null && tournament.GetPresentedByLabel(Localization)", markup);
        Assert.Contains("data-logo-fallback", markup);
        Assert.Contains("tournaments-card-sponsor-name", markup);
    }

    [Fact]
    public void BrokenSponsorLogoFallsBackToTheSponsorName()
    {
        var script = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/app.js");
        var css = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor.css");

        Assert.Contains("[data-logo-fallback]", script);
        Assert.Contains("data-logo-failed", script);
        Assert.Contains(".tournaments-card-sponsor[data-logo-failed] .tournaments-card-sponsor-name", css);
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

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mercurius.LAN.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
