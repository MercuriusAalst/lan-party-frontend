using Mercurius.LAN.Web.Models.Tournaments;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.LAN.Web.DTOs.Tournaments;

public class UpdateTournamentDTO : IValidatableObject
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public TournamentFormat Format { get; set; }
    public TournamentFormat FinalsFormat { get; set; }
    public BracketType BracketType { get; set; }
    public LeaderboardRankingMetric? LeaderboardRankingMetric { get; set; }

    [Required]
    public ParticipationMode? ParticipationMode { get; set; }

    public IBrowserFile? Image { get; set; }
    public int? TeamSize { get; set; }
    public DateTime PlannedStartTime { get; set; }

    [Range(1, 1440)]
    public int AverageGameDurationMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int RoundBreakDurationMinutes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if(PlannedStartTime == default)
        {
            yield return new ValidationResult(
                "Planned start time is required.",
                [nameof(PlannedStartTime)]);
        }

        if(ParticipationMode == Models.Tournaments.ParticipationMode.Team &&
           (!TeamSize.HasValue || TeamSize.Value is < 1 or > 50))
        {
            yield return new ValidationResult(
                "Team tournaments require a team size between 1 and 50.",
                [nameof(TeamSize)]);
        }

        if(BracketType == Models.Tournaments.BracketType.Leaderboard && !LeaderboardRankingMetric.HasValue)
        {
            yield return new ValidationResult(
                "Leaderboard tournaments require a ranking metric.",
                [nameof(LeaderboardRankingMetric)]);
        }

        if(BracketType == Models.Tournaments.BracketType.Leaderboard &&
           ParticipationMode == Models.Tournaments.ParticipationMode.Team)
        {
            yield return new ValidationResult(
                "Leaderboard tournaments are individual competitions.",
                [nameof(ParticipationMode)]);
        }
    }
}
