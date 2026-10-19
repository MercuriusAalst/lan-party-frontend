using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchResolutionAssignmentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("\"Assigned\"", PendingMatchResolutionAssignment.Assigned)]
    [InlineData("\"Unassigned\"", PendingMatchResolutionAssignment.Unassigned)]
    [InlineData("\"AssignedAdminUnavailable\"", PendingMatchResolutionAssignment.AssignedAdminUnavailable)]
    [InlineData("\"Escalated\"", PendingMatchResolutionAssignment.Escalated)]
    [InlineData("\"escalated\"", PendingMatchResolutionAssignment.Escalated)]
    public void AssignmentIsReadAsAString(string json, PendingMatchResolutionAssignment expected)
    {
        var item = JsonSerializer.Deserialize<PendingMatchResolutionDTO>($"{{\"reason\":\"Disputed\",\"assignment\":{json}}}", JsonOptions)!;

        Assert.Equal(expected, item.Assignment);
        Assert.Equal(MatchResolutionReason.Disputed, item.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"assignment\":null")]
    [InlineData(",\"assignment\":\"SomethingNew\"")]
    [InlineData(",\"assignment\":99")]
    [InlineData(",\"assignment\":{\"x\":1}")]
    public void MissingOrUnknownAssignmentIsNullAndKeepsTheRestOfTheItem(string assignmentJson)
    {
        var item = JsonSerializer.Deserialize<PendingMatchResolutionDTO>(
            $"{{\"tournamentName\":\"Cup\",\"reason\":\"MissingOpponentReport\"{assignmentJson}}}", JsonOptions)!;

        Assert.Null(item.Assignment);
        Assert.Equal("Cup", item.TournamentName);
        Assert.Equal(MatchResolutionReason.MissingOpponentReport, item.Reason);
    }

    [Fact]
    public void QueueIsOrderedEscalatedAndUnavailableFirstThenByRequiredSince()
    {
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            Item("assigned-new", PendingMatchResolutionAssignment.Assigned, now.AddMinutes(-1)),
            Item("assigned-old", PendingMatchResolutionAssignment.Assigned, now.AddMinutes(-9)),
            Item("escalated-new", PendingMatchResolutionAssignment.Escalated, now.AddMinutes(-11)),
            Item("unavailable-old", PendingMatchResolutionAssignment.AssignedAdminUnavailable, now.AddMinutes(-30)),
            Item("unknown-since", PendingMatchResolutionAssignment.Assigned, null),
            Item("escalated-unknown-since", PendingMatchResolutionAssignment.Escalated, null),
            Item("legacy-no-assignment", null, now.AddMinutes(-5)),
            Item("unassigned", PendingMatchResolutionAssignment.Unassigned, now.AddMinutes(-7))
        };

        var ordered = PendingMatchResolutionOrdering.ByUrgency(items).Select(item => item.TournamentName).ToArray();

        Assert.Equal(
            [
                "unavailable-old", "escalated-new", "escalated-unknown-since",
                "assigned-old", "unassigned", "legacy-no-assignment", "assigned-new", "unknown-since"
            ],
            ordered);
    }

    [Fact]
    public void RowLabelsAreLocalizedPerAssignmentAndAbsentForMissingOrUnknown()
    {
        var page = new MatchResolutions();
        typeof(MatchResolutions).GetProperty("Localization", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(page, TestLocalizationService.Instance);

        Assert.Equal("Feature.matchResolutions.assignmentAssigned", page.GetAssignmentLabel(PendingMatchResolutionAssignment.Assigned));
        Assert.Equal("Feature.matchResolutions.assignmentUnassigned", page.GetAssignmentLabel(PendingMatchResolutionAssignment.Unassigned));
        Assert.Equal("Feature.matchResolutions.assignmentUnavailable", page.GetAssignmentLabel(PendingMatchResolutionAssignment.AssignedAdminUnavailable));
        Assert.Equal("Feature.matchResolutions.assignmentEscalated", page.GetAssignmentLabel(PendingMatchResolutionAssignment.Escalated));
        Assert.Null(page.GetAssignmentLabel(null));
        Assert.Null(page.GetAssignmentLabel((PendingMatchResolutionAssignment)42));

        Assert.Contains("--urgent", MatchResolutions.GetAssignmentClass(PendingMatchResolutionAssignment.Escalated));
        Assert.Contains("--urgent", MatchResolutions.GetAssignmentClass(PendingMatchResolutionAssignment.AssignedAdminUnavailable));
        Assert.DoesNotContain("--urgent", MatchResolutions.GetAssignmentClass(PendingMatchResolutionAssignment.Assigned));
        Assert.DoesNotContain("--urgent", MatchResolutions.GetAssignmentClass(null));
    }

    private static PendingMatchResolutionDTO Item(string name, PendingMatchResolutionAssignment? assignment, DateTime? since) => new()
    {
        MatchId = Guid.NewGuid(),
        TournamentName = name,
        Assignment = assignment,
        RequiredSinceUtc = since
    };
}
