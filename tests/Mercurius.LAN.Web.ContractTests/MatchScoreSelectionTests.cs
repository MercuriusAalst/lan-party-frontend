using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchScoreSelectionTests
{
    [Theory]
    [InlineData(TournamentFormat.BestOf1, MatchParticipantSide.Participant1, null, 1, 0)]
    [InlineData(TournamentFormat.BestOf1, MatchParticipantSide.Participant2, null, 0, 1)]
    [InlineData(TournamentFormat.BestOf1, MatchParticipantSide.Participant1, 0, 1, 0)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant1, 0, 2, 0)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant1, 1, 2, 1)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant2, 0, 0, 2)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant2, 1, 1, 2)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant1, 0, 3, 0)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant1, 1, 3, 1)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant1, 2, 3, 2)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant2, 0, 0, 3)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant2, 1, 1, 3)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant2, 2, 2, 3)]
    public void CompleteSelections_MapToDecisiveScoresInDisplayedOrder(
        TournamentFormat format,
        MatchParticipantSide winner,
        int? loserScore,
        int expectedParticipant1Score,
        int expectedParticipant2Score)
    {
        var scores = TournamentMatchDetailsDialog.MapSelectionToScores(format, winner, loserScore);

        Assert.Equal((expectedParticipant1Score, expectedParticipant2Score), scores);
        Assert.True(TournamentMatchDetailsDialog.IsValidSeriesScore(format, scores!.Value.Participant1Score, scores.Value.Participant2Score));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, new int[0])]
    [InlineData(TournamentFormat.BestOf3, new[] { 0, 1 })]
    [InlineData(TournamentFormat.BestOf5, new[] { 0, 1, 2 })]
    [InlineData((TournamentFormat)99, new int[0])]
    public void LoserScoreOptions_AreZeroUpToOneBelowTheRequiredWins(TournamentFormat format, int[] expected)
    {
        Assert.Equal(expected, TournamentMatchDetailsDialog.GetLoserScoreOptions(format));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, null, null)]
    [InlineData(TournamentFormat.BestOf3, null, null)]
    [InlineData(TournamentFormat.BestOf3, null, 1)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant1, null)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant2, null)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant1, null)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant1, 2)]
    [InlineData(TournamentFormat.BestOf3, MatchParticipantSide.Participant2, -1)]
    [InlineData(TournamentFormat.BestOf5, MatchParticipantSide.Participant1, 3)]
    [InlineData((TournamentFormat)99, MatchParticipantSide.Participant1, 0)]
    public void IncompleteOrOutOfRangeSelections_MapToNothing(
        TournamentFormat format,
        MatchParticipantSide? winner,
        int? loserScore)
    {
        Assert.Null(TournamentMatchDetailsDialog.MapSelectionToScores(format, winner, loserScore));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, 1, 0, MatchParticipantSide.Participant1, 0)]
    [InlineData(TournamentFormat.BestOf1, 0, 1, MatchParticipantSide.Participant2, 0)]
    [InlineData(TournamentFormat.BestOf3, 2, 1, MatchParticipantSide.Participant1, 1)]
    [InlineData(TournamentFormat.BestOf3, 0, 2, MatchParticipantSide.Participant2, 0)]
    [InlineData(TournamentFormat.BestOf5, 3, 2, MatchParticipantSide.Participant1, 2)]
    [InlineData(TournamentFormat.BestOf5, 1, 3, MatchParticipantSide.Participant2, 1)]
    public void DecisiveScores_RestoreTheMatchingSelection(
        TournamentFormat format,
        int participant1Score,
        int participant2Score,
        MatchParticipantSide expectedWinner,
        int expectedLoserScore)
    {
        var selection = TournamentMatchDetailsDialog.GetSelectionFromScores(format, participant1Score, participant2Score);

        Assert.Equal((expectedWinner, (int?)expectedLoserScore), selection);
        Assert.Equal(
            (participant1Score, participant2Score),
            TournamentMatchDetailsDialog.MapSelectionToScores(format, selection.Winner, selection.LoserScore));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, 0, 0)]
    [InlineData(TournamentFormat.BestOf1, 1, 1)]
    [InlineData(TournamentFormat.BestOf3, 2, 2)]
    [InlineData(TournamentFormat.BestOf3, 1, 0)]
    [InlineData(TournamentFormat.BestOf3, 3, 0)]
    [InlineData(TournamentFormat.BestOf5, 2, 1)]
    [InlineData(TournamentFormat.BestOf3, null, 0)]
    [InlineData(TournamentFormat.BestOf3, 2, null)]
    [InlineData((TournamentFormat)99, 1, 0)]
    public void ScoresThatAreNotDecisive_YieldAnEmptySelection(TournamentFormat format, int? participant1Score, int? participant2Score)
    {
        Assert.Equal(
            ((MatchParticipantSide?)null, (int?)null),
            TournamentMatchDetailsDialog.GetSelectionFromScores(format, participant1Score, participant2Score));
    }

    [Theory]
    [InlineData("translations.en-US.json")]
    [InlineData("translations.nl-BE.json")]
    public void PickerCopyKeys_ExistInEveryLocale(string fileName)
    {
        var path = FindRepositoryFile(Path.Combine("src", "Mercurius.LAN.Web", "wwwroot", "locales", fileName));
        var translations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;

        Assert.False(string.IsNullOrWhiteSpace(translations["Feature.match.scoreWinnerLegend"]));
        Assert.Contains("{0}", translations["Feature.match.scoreLoserLegend"]);
        Assert.Contains("{1}", translations["Feature.match.scoreLoserLegend"]);
        Assert.Equal("{0} {1} \u2013 {2} {3}", translations["Feature.match.scoreSummary"]);
    }

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
