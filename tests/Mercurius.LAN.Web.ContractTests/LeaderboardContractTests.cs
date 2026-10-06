using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class LeaderboardContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void LeaderboardEnumsMatchTheBackendContract()
    {
        Assert.Equal(4, (int)BracketType.Leaderboard);
        Assert.Equal(0, (int)LeaderboardRankingMetric.HighestScore);
        Assert.Equal(1, (int)LeaderboardRankingMetric.FastestTime);
        Assert.Equal(0, (int)LeaderboardParticipantKind.LinkedUser);
        Assert.Equal(1, (int)LeaderboardParticipantKind.Guest);
    }

    [Theory]
    [InlineData(nameof(ILANClient.GetLeaderboardAsync), "GET", "/v1/lan/tournaments/{tournamentId}/leaderboard", typeof(PublicLeaderboardDTO))]
    [InlineData(nameof(ILANClient.GetAdminLeaderboardAsync), "GET", "/v1/lan/tournaments/{tournamentId}/leaderboard/attempts", typeof(AdminLeaderboardResponseDTO))]
    [InlineData(nameof(ILANClient.RecordLeaderboardAttemptAsync), "POST", "/v1/lan/tournaments/{tournamentId}/leaderboard/attempts", typeof(AdminLeaderboardParticipantDTO))]
    [InlineData(nameof(ILANClient.UpdateLeaderboardAttemptAsync), "PUT", "/v1/lan/tournaments/{tournamentId}/leaderboard/attempts/{attemptId}", typeof(LeaderboardAttemptDTO))]
    [InlineData(nameof(ILANClient.DeleteLeaderboardAttemptAsync), "DELETE", "/v1/lan/tournaments/{tournamentId}/leaderboard/attempts/{attemptId}", typeof(Task))]
    public void LeaderboardClientRoutesMatchTheBackendEndpoints(
        string methodName,
        string httpMethod,
        string expectedRoute,
        Type expectedResultType)
    {
        var method = typeof(ILANClient)
            .GetMethods()
            .Single(candidate => candidate.Name == methodName);

        var path = httpMethod switch
        {
            "GET" => method.GetCustomAttribute<GetAttribute>()?.Path,
            "POST" => method.GetCustomAttribute<PostAttribute>()?.Path,
            "PUT" => method.GetCustomAttribute<PutAttribute>()?.Path,
            "DELETE" => method.GetCustomAttribute<DeleteAttribute>()?.Path,
            _ => null
        };

        Assert.Equal(expectedRoute, path);
        Assert.Equal(expectedResultType, ResolveClientResultType(method.ReturnType));
    }

    [Fact]
    public void DeleteLeaderboardAttemptSendsRowVersionAsQueryParameter()
    {
        var method = typeof(ILANClient)
            .GetMethods()
            .Single(candidate => candidate.Name == nameof(ILANClient.DeleteLeaderboardAttemptAsync));

        var rowVersionParameter = method
            .GetParameters()
            .Single(parameter => parameter.Name == "rowVersion");

        Assert.Equal("rowVersion", rowVersionParameter.GetCustomAttribute<AliasAsAttribute>()?.Name);
    }

    [Fact]
    public void NumericBackendEnumPayloadsDeserializeIntoTheLeaderboardContract()
    {
        const string payload = """
        {
          "tournamentId": "1a111111-1111-1111-1111-111111111112",
          "rankingMetric": 1,
          "rows": [
            {
              "rank": 1,
              "participantId": "1b111111-1111-1111-1111-111111111111",
              "displayName": "Tara Track",
              "participantKind": 0,
              "linkedUserId": "41111111-1111-1111-1111-111111111118",
              "score": null,
              "durationMilliseconds": 39480
            },
            {
              "rank": 1,
              "participantId": "1b111111-1111-1111-1111-111111111112",
              "displayName": "Speedy Sam",
              "participantKind": 1,
              "linkedUserId": null,
              "score": null,
              "durationMilliseconds": 39480
            }
          ]
        }
        """;

        var leaderboard = JsonSerializer.Deserialize<PublicLeaderboardDTO>(payload, JsonOptions);

        Assert.NotNull(leaderboard);
        Assert.Equal(LeaderboardRankingMetric.FastestTime, leaderboard!.RankingMetric);
        Assert.Equal(2, leaderboard.Rows.Count);
        Assert.All(leaderboard.Rows, row => Assert.Equal(1, row.Rank));
        Assert.Equal(LeaderboardParticipantKind.LinkedUser, leaderboard.Rows[0].ParticipantKind);
        Assert.Equal(LeaderboardParticipantKind.Guest, leaderboard.Rows[1].ParticipantKind);
        Assert.Equal(39_480, leaderboard.Rows[1].DurationMilliseconds);
        Assert.Equal(
            "0:39.480",
            LeaderboardFormattingExtensions.FormatLeaderboardValue(
                leaderboard.RankingMetric,
                leaderboard.Rows[1].Score,
                leaderboard.Rows[1].DurationMilliseconds));
    }

    [Fact]
    public void LeaderboardScoreFormattingKeepsSixFractionDigits()
    {
        Assert.Equal(
            "1234.500001",
            LeaderboardFormattingExtensions.FormatLeaderboardValue(
                LeaderboardRankingMetric.HighestScore,
                1234.500001m,
                null));
        Assert.Equal(
            "1:02:03.004",
            LeaderboardFormattingExtensions.FormatLeaderboardValue(
                LeaderboardRankingMetric.FastestTime,
                null,
                3_723_004));
    }

    [Theory]
    [InlineData(0, "0:00.000")]
    [InlineData(39_480, "0:39.480")]
    [InlineData(3_723_004, "1:02:03.004")]
    [InlineData(90_000_000, "25:00:00.000")]
    [InlineData(long.MaxValue, "2562047788015:12:55.807")]
    public void LeaderboardDurationFormattingKeepsTotalHoursWithoutOverflow(long durationMilliseconds, string expected)
    {
        Assert.Equal(expected, LeaderboardFormattingExtensions.FormatLeaderboardDuration(durationMilliseconds));
    }

    [Fact]
    public void LeaderboardTournamentsNeverShowMatchFormatInTheirDetailLabel()
    {
        var leaderboard = new Tournament
        {
            BracketType = BracketType.Leaderboard,
            Format = TournamentFormat.BestOf1,
            FinalsFormat = TournamentFormat.BestOf1
        };

        Assert.Equal("Leaderboard", leaderboard.GetFormatDetailLabel());
    }

    [Theory]
    [InlineData(BracketType.SingleElimination, TournamentFormat.BestOf1, "Single Elimination · Best of 1")]
    [InlineData(BracketType.DoubleElimination, TournamentFormat.BestOf3, "Double Elimination · Best of 3")]
    [InlineData(BracketType.RoundRobin, TournamentFormat.BestOf5, "Round Robin (unsupported) · Best of 5")]
    [InlineData(BracketType.Swiss, TournamentFormat.BestOf1, "Swiss (unsupported) · Best of 1")]
    public void BracketTournamentsKeepTheirBracketAndFormatDetailLabel(
        BracketType bracketType,
        TournamentFormat format,
        string expected)
    {
        var tournament = new Tournament
        {
            BracketType = bracketType,
            Format = format,
            FinalsFormat = format
        };

        Assert.Equal(expected, tournament.GetFormatDetailLabel());
    }

    private static Type ResolveClientResultType(Type returnType)
    {
        if(!returnType.IsGenericType)
            return returnType;

        var genericDefinition = returnType.GetGenericTypeDefinition();
        return genericDefinition == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;
    }
}
