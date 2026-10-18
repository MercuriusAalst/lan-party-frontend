using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Models.Matches;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchResolutionUiTests
{
    [Fact]
    public void ResolutionReason_UsesVisiblePrivateReports()
    {
        // Public flags deliberately contradict the reports to prove the visible reports win.
        var match = new Match { LifecycleState = MatchLifecycleState.AdminResolutionRequired, Participant1Ended = true, Participant2Ended = false };
        var conflict = new MatchActionStateDTO
        {
            Participant1ReportedScore1 = 1,
            Participant1ReportedScore2 = 0,
            Participant2ReportedScore1 = 0,
            Participant2ReportedScore2 = 1
        };
        var loneReport = new MatchActionStateDTO { Participant1ReportedScore1 = 1, Participant1ReportedScore2 = 0 };

        Assert.Equal(MatchResolutionReason.Disputed, TournamentMatchDetailsDialog.GetResolutionReason(match, conflict));

        match.Participant2Ended = true;
        Assert.Equal(MatchResolutionReason.MissingOpponentReport, TournamentMatchDetailsDialog.GetResolutionReason(match, loneReport));
    }

    [Fact]
    public void ResolutionReason_FallsBackToThePublicFlagsWhenPrivateReportsAreWithheld()
    {
        // A successful action-state response can withhold both reports: every report field is null.
        var withheld = new MatchActionStateDTO();
        var bothEnded = new Match { LifecycleState = MatchLifecycleState.AdminResolutionRequired, Participant1Ended = true, Participant2Ended = true };
        var oneEnded = new Match { LifecycleState = MatchLifecycleState.AdminResolutionRequired, Participant1Ended = true, Participant2Ended = false };

        Assert.Equal(MatchResolutionReason.Disputed, TournamentMatchDetailsDialog.GetResolutionReason(bothEnded, withheld));
        Assert.Equal(MatchResolutionReason.MissingOpponentReport, TournamentMatchDetailsDialog.GetResolutionReason(oneEnded, withheld));
    }

    [Fact]
    public void ResolutionReason_UsesThePublicFlagsWithoutAnActionState()
    {
        var bothEnded = new Match { Participant1Ended = true, Participant2Ended = true };
        var oneEnded = new Match { Participant1Ended = false, Participant2Ended = true };

        Assert.Equal(MatchResolutionReason.Disputed, TournamentMatchDetailsDialog.GetResolutionReason(bothEnded, null));
        Assert.Equal(MatchResolutionReason.MissingOpponentReport, TournamentMatchDetailsDialog.GetResolutionReason(oneEnded, null));
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
