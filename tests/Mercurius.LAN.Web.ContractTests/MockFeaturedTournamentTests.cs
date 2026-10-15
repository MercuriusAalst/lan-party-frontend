using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MockFeaturedTournamentTests
{
    private static readonly Guid CounterStrike2 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Trackmania = Guid.Parse("11111111-1111-1111-1111-111111111113");
    private static readonly Guid BeatSaber = Guid.Parse("1a111111-1111-1111-1111-111111111111");
    private static readonly Guid RegistrationDemo = Guid.Parse("11111111-1111-1111-1111-111111111115");

    [Fact]
    public void NothingSavedFallsBackToTheFirstFourEligibleTournaments()
    {
        var store = CreateStore();
        var expected = EligibleDefaultOrder(store).Take(4).ToList();

        var featured = store.GetFeaturedTournaments();

        Assert.Equal(4, featured.TournamentIds.Count);
        Assert.Equal(expected, featured.TournamentIds);
        Assert.Equal(expected, featured.Tournaments.Select(tournament => tournament.Id));
    }

    [Fact]
    public void InvalidatedSelectionKeepsRelativeOrderAndBackfillsDistinctly()
    {
        var store = CreateStore();
        store.UpdateFeaturedTournaments([CounterStrike2, Trackmania, BeatSaber, RegistrationDemo], "admin");
        var retained = new[] { Trackmania, BeatSaber, RegistrationDemo };
        store.SetTournamentLifecycleState(CounterStrike2, TournamentStatus.Canceled);

        var expected = retained
            .Concat(EligibleDefaultOrder(store).Where(id => !retained.Contains(id)))
            .Take(4)
            .ToList();

        var featured = store.GetFeaturedTournaments();

        Assert.Equal(expected, featured.TournamentIds);
        Assert.Equal(featured.TournamentIds.Count, featured.TournamentIds.Distinct().Count());
        Assert.DoesNotContain(CounterStrike2, featured.TournamentIds);
    }

    [Fact]
    public void FewerThanFourEligibleTournamentsStillRendersWithoutDuplicates()
    {
        var store = CreateStore();
        foreach(var tournament in new[]
        {
            CounterStrike2,
            Guid.Parse("11111111-1111-1111-1111-111111111112"),
            Trackmania,
            BeatSaber,
            RegistrationDemo
        })
        {
            store.SetTournamentLifecycleState(tournament, TournamentStatus.Canceled);
        }

        var expected = EligibleDefaultOrder(store);
        Assert.True(expected.Count < 4);

        var featured = store.GetFeaturedTournaments();

        Assert.Equal(expected, featured.TournamentIds);
        Assert.Equal(featured.TournamentIds.Count, featured.TournamentIds.Distinct().Count());
    }

    [Fact]
    public void FeaturedReadCarriesTheSameSponsorPlacementAsTheDetailRead()
    {
        var store = CreateStore();

        var featured = store.GetFeaturedTournaments();

        Assert.NotEmpty(featured.Tournaments);
        Assert.All(featured.Tournaments, tournament =>
            Assert.Equal(
                store.GetTournament(tournament.Id)!.SponsorPlacement?.SponsorName,
                tournament.SponsorPlacement?.SponsorName));
        Assert.Contains(featured.Tournaments, tournament => tournament.SponsorPlacement is not null);
    }

    private static List<Guid> EligibleDefaultOrder(MockBackendStore store) =>
        store.GetTournaments()
            .Where(tournament => tournament.Status != TournamentStatus.Canceled)
            .Select(tournament => tournament.Id)
            .ToList();

    private static MockBackendStore CreateStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine("src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
    }

    private static string FindRepositoryRoot()
    {
        foreach(var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while(directory is not null)
            {
                if(File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for the mock fixture.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(MockFeaturedTournamentTests);
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
