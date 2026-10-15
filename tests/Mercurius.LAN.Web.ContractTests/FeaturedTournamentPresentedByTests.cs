using System.Text.Json;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class FeaturedTournamentPresentedByTests
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
        Assert.Null(Sponsored(sponsorName).GetPresentedByLabel(TestLocalizationService.Instance));
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
    public void SharedFeaturedCardsRenderThePresentedByLabelOnLeadAndRow()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor");
        var css = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor.css");

        var leadBody = markup[
            markup.IndexOf("private RenderFragment LeadCard", StringComparison.Ordinal)..
            markup.IndexOf("private RenderFragment RowCard", StringComparison.Ordinal)];
        var rowBody = markup[markup.IndexOf("private RenderFragment RowCard", StringComparison.Ordinal)..];
        Assert.Contains("GetPresentedByLabel(Localization)", leadBody);
        Assert.Contains("GetPresentedByLabel(Localization)", rowBody);
        Assert.Contains("class=\"home-tournament-presented-by\"", markup);
        Assert.Contains("overflow-wrap: anywhere", css);
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
