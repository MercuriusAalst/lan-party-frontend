using System.Text.Json;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class StatusPageMarkupContractTests
{
    [Fact]
    public void SharedStatusPageKeepsRetryCallbackAndLinkActions()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/StatusPage.razor");

        Assert.Contains("EventCallback OnRetry", markup);
        Assert.Contains("PrimaryForceLoad", markup);
        Assert.Contains("status-page-actions", markup);
        Assert.Contains("status-page-marker", markup);
    }

    [Fact]
    public void SharedStatusPageAnnouncesStateWithASingleHeading()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/StatusPage.razor");

        Assert.Contains("role=\"alert\"", markup);
        Assert.Contains("aria-live=\"assertive\"", markup);
        Assert.Equal(1, CountOccurrences(markup, "<h1>"));
    }

    [Fact]
    public void FullPageFailuresReuseTheSharedStatusSurface()
    {
        var tournamentDetail = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentDetail.razor");
        var tournamentsOverview = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor");
        var completeProfile = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/CompleteProfile.razor");

        Assert.Contains("<StatusPage", tournamentDetail);
        Assert.DoesNotContain("tournament-empty-state", tournamentDetail);
        Assert.DoesNotContain("tournaments-load-error", tournamentsOverview);
        Assert.DoesNotContain("status-page-card", completeProfile);
    }

    [Fact]
    public void SameRouteRetriesUseInPlaceCallbacksInsteadOfForcedReloads()
    {
        var profileMarkup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor");
        var manageTeamsMarkup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor");
        var profileCode = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor.cs");
        var manageTeamsCode = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor.cs");

        Assert.Contains("OnRetry=\"LoadProfileAsync\"", profileMarkup);
        Assert.Contains("OnRetry=\"RetryLoadAsync\"", manageTeamsMarkup);
        Assert.DoesNotContain("PrimaryForceLoad", profileMarkup);
        Assert.DoesNotContain("PrimaryForceLoad", manageTeamsMarkup);
        Assert.Contains("private async Task LoadProfileAsync()", profileCode);
        Assert.Contains("private Task RetryLoadAsync()", manageTeamsCode);
    }

    [Fact]
    public void StatusCopyIsAvailableInBothLanguagesAndNamesTheFailure()
    {
        var english = ReadLocale("translations.en-US.json");
        var dutch = ReadLocale("translations.nl-BE.json");

        string[] requiredKeys =
        [
            "status.retrying",
            "status.tryAgain",
            "common.pageNotFound",
            "common.forbidden",
            "common.unexpectedError",
            "Feature.tournaments.loadDetailTitle"
        ];

        foreach(var key in requiredKeys)
        {
            Assert.True(english.ContainsKey(key), $"en-US is missing '{key}'.");
            Assert.True(dutch.ContainsKey(key), $"nl-BE is missing '{key}'.");
        }

        // The failed tournament-detail load must not present loading copy as its heading.
        Assert.DoesNotContain("loading", english["Feature.tournaments.loadDetailTitle"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("laden", dutch["Feature.tournaments.loadDetailTitle"], StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string value, string term) =>
        value.Split(term, StringSplitOptions.None).Length - 1;

    private static Dictionary<string, string> ReadLocale(string fileName) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            ReadRepositoryFile($"src/Mercurius.LAN.Web/wwwroot/locales/{fileName}"))!;

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
