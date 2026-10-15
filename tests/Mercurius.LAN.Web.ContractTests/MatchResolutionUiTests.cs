using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Models.Matches;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchResolutionUiTests
{
    [Fact]
    public void ResolutionReason_UsesTheAdminReportsWhenPresentAndTheEndedFlagsOtherwise()
    {
        var match = new Match
        {
            LifecycleState = MatchLifecycleState.AdminResolutionRequired,
            Participant1Ended = true,
            Participant2Ended = false
        };

        Assert.Equal(MatchResolutionReason.MissingOpponentReport, TournamentMatchDetailsDialog.GetResolutionReason(match, null));

        match.Participant2Ended = true;
        Assert.Equal(MatchResolutionReason.Disputed, TournamentMatchDetailsDialog.GetResolutionReason(match, null));

        var loneReport = new MatchActionStateDTO { Participant1ReportedScore1 = 1, Participant1ReportedScore2 = 0 };
        Assert.Equal(MatchResolutionReason.MissingOpponentReport, TournamentMatchDetailsDialog.GetResolutionReason(match, loneReport));

        var conflict = new MatchActionStateDTO
        {
            Participant1ReportedScore1 = 1,
            Participant1ReportedScore2 = 0,
            Participant2ReportedScore1 = 0,
            Participant2ReportedScore2 = 1
        };
        Assert.Equal(MatchResolutionReason.Disputed, TournamentMatchDetailsDialog.GetResolutionReason(match, conflict));
    }

    [Fact]
    public void ResolutionPageMarkup_CoversTheAdminRouteAndEveryState()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/MatchResolutions.razor");

        Assert.Contains("@page \"/admin/match-resolutions\"", markup);
        Assert.Contains("[Authorize(Roles = \"admin\")]", markup);
        Assert.Contains("_isLoading", markup);
        Assert.Contains("_isUnauthorized", markup);
        Assert.Contains("_loadError", markup);
        Assert.Contains("_items.Count == 0", markup);
        Assert.Contains("RequiredSinceUtc.HasValue", markup);
        Assert.Contains("TournamentMatchDetailsDialog", markup);
    }

    [Fact]
    public void NavMenuMarkup_LinksTheAdminOnlyQueueWithACountBadge()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor");

        Assert.Contains("href=\"/admin/match-resolutions\"", markup);
        Assert.Contains("PendingResolutionCount > 0", markup);
        Assert.Contains("admin-nav-pending-count", markup);
    }

    [Fact]
    public void DialogTexts_NeverPromiseAutomaticAcceptance()
    {
        var english = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json");
        var dutch = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.nl-BE.json");

        Assert.DoesNotContain("reported score becomes final", english);
        Assert.DoesNotContain("gemelde score definitief", dutch);
        Assert.Contains("descriptionAdminResolutionDisputed", english);
        Assert.Contains("descriptionAdminResolutionMissingReport", english);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory != null)
        {
            var repositoryPath = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(repositoryPath))
                return File.ReadAllText(repositoryPath);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }
}
