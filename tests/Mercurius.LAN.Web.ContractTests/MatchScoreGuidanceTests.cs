using System.Text.Json;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchScoreGuidanceTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("0", 0)]
    [InlineData("2", 2)]
    [InlineData("2.5", null)]
    [InlineData("2,5", null)]
    [InlineData("-1", null)]
    [InlineData("+1", null)]
    [InlineData("e", null)]
    [InlineData("1e1", null)]
    [InlineData(" 2", null)]
    [InlineData("99999999999", null)]
    public void ParseScoreText_AcceptsOnlyPlainNonnegativeIntegers(string? text, int? expected)
    {
        Assert.Equal(expected, TournamentMatchDetailsDialog.ParseScoreText(text));
    }

    [Fact]
    public void NonIntegerScoreText_IsInvalidAndNeverFallsBackToAnEarlierValue()
    {
        // "2" then "2.5" typed into the first field: the parsed value must not remain 2.
        Assert.Equal(2, TournamentMatchDetailsDialog.ParseScoreText("2"));
        Assert.Null(TournamentMatchDetailsDialog.ParseScoreText("2.5"));
        Assert.True(TournamentMatchDetailsDialog.ShouldShowScoreValidationError(TournamentFormat.BestOf3, "2.5", "0"));
        Assert.False(TournamentMatchDetailsDialog.IsValidSeriesScore(
            TournamentFormat.BestOf3,
            TournamentMatchDetailsDialog.ParseScoreText("2.5"),
            TournamentMatchDetailsDialog.ParseScoreText("0")));
    }

    [Fact]
    public void ScoreToText_RoundTripsThroughParseScoreText()
    {
        Assert.Equal(string.Empty, TournamentMatchDetailsDialog.ScoreToText(null));
        Assert.Equal("2", TournamentMatchDetailsDialog.ScoreToText(2));
        Assert.Equal(2, TournamentMatchDetailsDialog.ParseScoreText(TournamentMatchDetailsDialog.ScoreToText(2)));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, "Feature.match.scoreGuidanceBestOf1")]
    [InlineData(TournamentFormat.BestOf3, "Feature.match.scoreGuidanceBestOf3")]
    [InlineData(TournamentFormat.BestOf5, "Feature.match.scoreGuidanceBestOf5")]
    [InlineData((TournamentFormat)99, null)]
    public void ScoreGuidanceKey_ExistsOnlyForSupportedFormats(TournamentFormat format, string? expected)
    {
        Assert.Equal(expected, TournamentMatchDetailsDialog.GetScoreGuidanceKey(format));
    }

    [Theory]
    [InlineData(TournamentFormat.BestOf1, "player", "player-score-guidance player-score-validation")]
    [InlineData(TournamentFormat.BestOf5, "admin", "admin-score-guidance admin-score-validation")]
    [InlineData((TournamentFormat)99, "player", "player-score-validation")]
    [InlineData((TournamentFormat)99, "admin", "admin-score-validation")]
    public void ScoreDescribedBy_ReferencesGuidanceOnlyWhenItIsRendered(TournamentFormat format, string prefix, string expected)
    {
        Assert.Equal(expected, TournamentMatchDetailsDialog.GetScoreDescribedBy(format, prefix));
    }

    [Theory]
    [InlineData("translations.en-US.json", TournamentFormat.BestOf1, "Best of 1: the winner has 1 win, the other side 0.")]
    [InlineData("translations.en-US.json", TournamentFormat.BestOf3, "Best of 3: the winner has 2 wins; the other side has 0 or 1.")]
    [InlineData("translations.en-US.json", TournamentFormat.BestOf5, "Best of 5: the winner has 3 wins; the other side has 0, 1 or 2.")]
    [InlineData("translations.nl-BE.json", TournamentFormat.BestOf1, "Best of 1: de winnaar heeft 1 overwinning, de tegenstander 0.")]
    [InlineData("translations.nl-BE.json", TournamentFormat.BestOf3, "Best of 3: de winnaar heeft 2 overwinningen; de tegenstander heeft er 0 of 1.")]
    [InlineData("translations.nl-BE.json", TournamentFormat.BestOf5, "Best of 5: de winnaar heeft 3 overwinningen; de tegenstander heeft er 0, 1 of 2.")]
    public void ScoreGuidanceCopy_IsNaturalPerFormatAndLocale(string fileName, TournamentFormat format, string expected)
    {
        var translations = ReadLocale(fileName);
        var key = TournamentMatchDetailsDialog.GetScoreGuidanceKey(format)!;

        Assert.Equal(expected, translations[key]);
        Assert.DoesNotContain("0–0", translations[key]);
    }

    [Theory]
    [InlineData("translations.en-US.json")]
    [InlineData("translations.nl-BE.json")]
    public void CombinedScoreFormatHintKey_IsRemoved(string fileName)
    {
        Assert.False(ReadLocale(fileName).ContainsKey("Feature.match.scoreFormatHint"));
    }

    [Fact]
    public void ScoreMarkup_BindsTextInputsAndRendersGuidanceOnlyWhenAvailable()
    {
        var markup = File.ReadAllText(FindRepositoryFile(Path.Combine(
            "src", "Mercurius.LAN.Web", "Components", "Pages", "Tournaments", "Matches", "DetailView", "TournamentMatchDetailsDialog.razor")));

        Assert.Equal(2, CountOccurrences(markup, "@bind=\"_participant1ScoreText\""));
        Assert.Equal(2, CountOccurrences(markup, "@bind=\"_participant2ScoreText\""));
        Assert.DoesNotContain("@bind=\"_participant1Score\"", markup);
        Assert.DoesNotContain("@bind=\"_participant2Score\"", markup);
        Assert.Equal(4, CountOccurrences(markup, "type=\"number\""));
        Assert.Contains("@if(GetScoreGuidance() is { } playerScoreGuidance)", markup);
        Assert.Contains("@if(GetScoreGuidance() is { } adminScoreGuidance)", markup);
        Assert.DoesNotContain("aria-describedby=\"player-score-guidance", markup);
        Assert.DoesNotContain("aria-describedby=\"admin-score-guidance", markup);
    }

    private static Dictionary<string, string> ReadLocale(string fileName)
    {
        var path = FindRepositoryFile(Path.Combine("src", "Mercurius.LAN.Web", "wwwroot", "locales", fileName));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse '{path}'.");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
