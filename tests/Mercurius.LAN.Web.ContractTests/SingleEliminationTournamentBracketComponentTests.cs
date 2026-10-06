using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.BracketView;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Microsoft.AspNetCore.Components.Rendering;
using System.Reflection;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class SingleEliminationTournamentBracketComponentTests
{
    [Fact]
    public void BuildsBracketWhenLaterRoundHasOnlyOneEarlierMatch()
    {
        RenderBracket(
            new Match { RoundNumber = 1, MatchNumber = 1 },
            new Match { RoundNumber = 2, MatchNumber = 1 });
    }

    [Fact]
    public void BuildsBracketWhenAnEntireRoundIsMissing()
    {
        RenderBracket(
            new Match { RoundNumber = 1, MatchNumber = 1 },
            new Match { RoundNumber = 1, MatchNumber = 2 },
            new Match { RoundNumber = 3, MatchNumber = 1 });
    }

    private static void RenderBracket(params Match[] matches)
    {
        var tournament = new TournamentExtended
        {
            Name = "Valorant",
            BracketType = BracketType.SingleElimination,
            Matches = matches
        };
        var component = new SingleEliminationTournamentBracketComponent();
        typeof(SingleEliminationTournamentBracketComponent)
            .GetProperty(nameof(SingleEliminationTournamentBracketComponent.Tournament))!
            .SetValue(component, tournament);

        var buildRenderTree = typeof(SingleEliminationTournamentBracketComponent)
            .GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic)!;
        buildRenderTree.Invoke(component, [new RenderTreeBuilder()]);
    }
}
