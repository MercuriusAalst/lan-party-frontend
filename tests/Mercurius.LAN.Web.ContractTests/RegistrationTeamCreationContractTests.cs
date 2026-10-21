using System.Text.Json;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class RegistrationTeamCreationContractTests
{
    [Fact]
    public void EmptyAndUndersizedStatesAreDistinctAndKeepExistingMemberRosterStatus()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor");
        var branchStart = markup.IndexOf("else if(!RegistrableCaptainedTeams.Any())", StringComparison.Ordinal);
        var stepperStart = markup.IndexOf("<MudStepper", branchStart, StringComparison.Ordinal);
        var noRegistrableTeams = markup[branchStart..stepperStart];

        Assert.Contains("if(CaptainedTeams.Count == 0)", noRegistrableTeams);
        Assert.Contains("Feature.tournaments.noCaptainedTeamTitle", noRegistrableTeams);
        Assert.Contains("Feature.tournaments.createTeam", noRegistrableTeams);
        Assert.Contains("Feature.tournaments.undersizedTeamsHidden", noRegistrableTeams);
        Assert.Contains("Feature.tournaments.manageTeams", noRegistrableTeams);
        Assert.DoesNotContain("Feature.tournaments.noTeamAvailable", noRegistrableTeams);
        Assert.True(noRegistrableTeams.IndexOf("registration-review-roster", StringComparison.Ordinal)
            < noRegistrableTeams.IndexOf("registration-team-empty-state", StringComparison.Ordinal));
        Assert.Contains("GetConfirmationLabel(member.ConfirmationStatus)", noRegistrableTeams);
    }

    [Fact]
    public void NewTeamCreationCopyExistsInBothSupportedLocales()
    {
        using var english = JsonDocument.Parse(ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json"));
        using var dutch = JsonDocument.Parse(ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.nl-BE.json"));
        var keys = new[]
        {
            "Feature.tournaments.noCaptainedTeamTitle",
            "Feature.tournaments.noCaptainedTeamHelp",
            "Feature.tournaments.createTeam",
            "Feature.tournaments.manageTeams"
        };

        foreach(var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(english.RootElement.GetProperty(key).GetString()));
            Assert.False(string.IsNullOrWhiteSpace(dutch.RootElement.GetProperty(key).GetString()));
        }
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }
}
