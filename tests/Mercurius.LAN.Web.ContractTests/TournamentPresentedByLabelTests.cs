using System.Text.Json;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentPresentedByLabelTests
{
    [Fact]
    public void SponsoredTournamentProducesLocalizedPresentedByLabel()
    {
        var tournament = Sponsored("Mercurius Tech");

        Assert.Equal("Presented by Mercurius Tech", tournament.GetPresentedByLabel(TestLocalizationService.Instance));
    }

    [Fact]
    public void UnsponsoredTournamentProducesNoLabel()
    {
        Assert.Null(new Tournament().GetPresentedByLabel(TestLocalizationService.Instance));
        Assert.Null(new Tournament { SponsorPlacement = null }.GetPresentedByLabel(TestLocalizationService.Instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSponsorNameProducesNoLabel(string sponsorName)
    {
        var tournament = Sponsored(sponsorName);

        Assert.Null(tournament.GetPresentedByLabel(TestLocalizationService.Instance));
        Assert.Equal("LAN Cup", tournament.Name);
    }

    [Fact]
    public void LongSponsorNameIsKeptInFullSoItCanWrap()
    {
        var longName = "Mercurius International Technology Partners Collective";

        Assert.Equal(
            $"Presented by {longName}",
            Sponsored(longName).GetPresentedByLabel(TestLocalizationService.Instance));
    }

    [Fact]
    public void PresentedByLabelUsesTheLocalizedPartnerHeadingResource()
    {
        var english = ReadLocale("translations.en-US.json");
        var dutch = ReadLocale("translations.nl-BE.json");

        Assert.Equal("Presented by {0}", english["Feature.tournaments.partnerHeading"]);
        Assert.Equal("Mede mogelijk gemaakt door {0}", dutch["Feature.tournaments.partnerHeading"]);
    }

    [Fact]
    public void ProminentTournamentTitlesRenderThePresentedByLabelOnly()
    {
        var detail = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentDetail.razor");
        var overview = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor");
        var home = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Home.razor");

        // Hero h1 stays the tournament name; the attribution is a separate element.
        Assert.Contains("<h1>@_tournament.Name</h1>", detail);
        Assert.Contains("_tournament.GetPresentedByLabel(Localization)", detail);
        Assert.Contains("<PageTitle>@PageTitleText</PageTitle>", detail);
        Assert.DoesNotContain("GetPresentedByLabel", detail[..detail.IndexOf("<header", StringComparison.Ordinal)]);

        Assert.Contains("tournament.GetPresentedByLabel(Localization)", overview);
        Assert.DoesNotContain("GetPresentedByLabel", overview[..overview.IndexOf("<h3>@tournament.Name</h3>", StringComparison.Ordinal)]);

        // The featured lead card and every compact row card carry the attribution.
        Assert.Contains("leadTournament.GetPresentedByLabel(Localization)", home);
        Assert.Contains("tournament.GetPresentedByLabel(Localization)", home);
    }

    private static Tournament Sponsored(string sponsorName) => new()
    {
        Name = "LAN Cup",
        SponsorPlacement = new TournamentSponsorPlacement { SponsorName = sponsorName }
    };

    private static Dictionary<string, string> ReadLocale(string fileName) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(FindRepositoryFile(Path.Combine("src", "Mercurius.LAN.Web", "wwwroot", "locales", fileName))))
        ?? throw new InvalidOperationException($"Could not parse '{fileName}'.");

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(FindRepositoryFile(relativePath));

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(path))
                return path;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }
}
