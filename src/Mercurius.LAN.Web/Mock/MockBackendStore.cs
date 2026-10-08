using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.Extensions.Options;

namespace Mercurius.LAN.Web.Mock;

internal sealed class MockBackendStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Guid FeaturedDoubleEliminationTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid CounterStrike2TournamentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DefaultRegistrationFixtureTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111115");
    private static readonly Guid DefaultRegistrationFixtureTeamId = Guid.Parse("21111111-1111-1111-1111-111111111120");
    private static readonly Guid CounterStrike2RegistrationFixtureTeamId = Guid.Parse("21111111-1111-1111-1111-111111111141");
    private static readonly Guid IndividualProfileFixtureTournamentId = Guid.Parse("91111111-1111-1111-1111-111111111111");
    private static readonly Guid IndividualProfileFixturePlayerId = Guid.Parse("91111111-1111-1111-1111-111111111112");
    private static readonly Guid IndividualProfileFixtureOpponentId = Guid.Parse("91111111-1111-1111-1111-111111111113");
    private static readonly Guid IndividualProfileFixturePreviousMatchId = Guid.Parse("92111111-1111-1111-1111-111111111111");
    private static readonly Guid IndividualProfileFixtureUpcomingMatchId = Guid.Parse("92111111-1111-1111-1111-111111111112");
    private static readonly Guid LeaderboardTimeFixtureTournamentId = Guid.Parse("1a111111-1111-1111-1111-111111111112");
    private static readonly DateTime FeaturedFixtureCreatedAtUtc = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FeaturedFixtureUpdatedAtUtc = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    private const int MaxDownstreamMatches = 512;

    private readonly object _syncRoot = new();
    private readonly string _dataFilePath;
    private readonly Dictionary<Guid, List<TournamentRegistrationDTO>> _registrationDetails = [];
    private readonly Dictionary<Guid, Dictionary<Guid, MockRegistrationSnapshot>> _publicRegistrationSnapshots = [];
    private readonly Dictionary<Guid, List<MockLeaderboardParticipant>> _leaderboardParticipants = [];
    private readonly HashSet<Guid> _deletedTeamIds = [];
    private MockBackendDocument _document;

    public MockBackendStore(IHostEnvironment environment, IOptions<MockBackendOptions> options)
    {
        _dataFilePath = Path.Combine(environment.ContentRootPath, options.Value.DataFilePath);
        _document = LoadDocument(_dataFilePath);
        SeedFeaturedDoubleEliminationFixture();
        SeedDefaultRegistrationFixture();
        SeedIndividualProfileFixture();
        SeedLeaderboardFixtures();
        InitializeRegistrationDetails();
    }

    public List<Tournament> GetTournaments(int? page = null, int? pageSize = null)
    {
        lock(_syncRoot)
        {
            var tournaments = _document.Tournaments
                .OrderBy(tournament => tournament.PlannedStartTime)
                .ThenBy(tournament => tournament.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToTournament);

            if(pageSize is > 0)
            {
                var pageNumber = Math.Max(page ?? 1, 1);
                tournaments = tournaments.Skip((pageNumber - 1) * pageSize.Value).Take(pageSize.Value);
            }

            return Clone(tournaments.ToList())!;
        }
    }

    public List<GlobalSearchResultDTO> SearchGlobal(string query)
    {
        lock(_syncRoot)
        {
            var trimmedQuery = query.Trim();
            if(trimmedQuery.Length < 3)
                return [];

            var userResults = _document.Users
                .Where(user =>
                    !user.IsDeleted &&
                    !string.IsNullOrWhiteSpace(user.Username) &&
                    user.Username.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.Username!.StartsWith(trimmedQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                .Select(user => new GlobalSearchResultDTO
                {
                    Type = GlobalSearchResultType.User,
                    DisplayLabel = user.Username!,
                    SupportingText = "User",
                    UserId = user.Id,
                    Username = user.Username
                });

            var teamResults = _document.Teams
                .Where(team =>
                    !string.IsNullOrWhiteSpace(team.Name) &&
                    team.Name.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(team => team.Name.StartsWith(trimmedQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team => new GlobalSearchResultDTO
                {
                    Type = GlobalSearchResultType.Team,
                    DisplayLabel = team.Name,
                    SupportingText = "Team",
                    TeamName = team.Name
                });

            var tournamentResults = _document.Tournaments
                .Where(tournament =>
                    !string.IsNullOrWhiteSpace(tournament.Name) &&
                    tournament.Name.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tournament => tournament.Name.StartsWith(trimmedQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(tournament => tournament.Name, StringComparer.OrdinalIgnoreCase)
                .Select(tournament => new GlobalSearchResultDTO
                {
                    Type = GlobalSearchResultType.Tournament,
                    DisplayLabel = tournament.Name,
                    SupportingText = "Tournament",
                    TournamentId = tournament.Id
                });

            return userResults
                .Concat(teamResults)
                .Concat(tournamentResults)
                .ToList();
        }
    }

    public PublicUserProfileDTO? GetPublicUserByUsername(string username)
    {
        lock(_syncRoot)
        {
            var normalizedUsername = username.Trim();
            if(string.IsNullOrWhiteSpace(normalizedUsername))
                return null;

            var user = _document.Users.FirstOrDefault(candidate =>
                !candidate.IsDeleted &&
                string.Equals(candidate.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));

            if(user == null || string.IsNullOrWhiteSpace(user.Username))
                return null;

            return new PublicUserProfileDTO
            {
                Username = user.Username!
            };
        }
    }

    public PublicTeamProfileDTO? GetPublicTeamByName(string teamName)
    {
        lock(_syncRoot)
        {
            var normalizedTeamName = teamName.Trim();
            if(string.IsNullOrWhiteSpace(normalizedTeamName))
                return null;

            var team = _document.Teams.FirstOrDefault(candidate =>
                !_deletedTeamIds.Contains(candidate.Id) &&
                string.Equals(candidate.Name, normalizedTeamName, StringComparison.OrdinalIgnoreCase));

            if(team == null)
                return null;

            var members = team.Members
                .Where(member => !string.IsNullOrWhiteSpace(member.Username))
                .GroupBy(member => member.Username!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PublicTeamMemberDTO
                {
                    Username = group.First().Username!
                })
                .OrderBy(member => member.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var captainUsername = team.Members
                .FirstOrDefault(member => member.Id == team.CaptainUserId)?.Username
                ?? _document.Users.FirstOrDefault(member => member.Id == team.CaptainUserId)?.Username;

            var tournaments = _document.Tournaments
                .Where(tournament => tournament.Status != TournamentStatus.Canceled)
                .Where(tournament =>
                {
                    EnsureRegistrationProjection(tournament);
                    return tournament.Registrations.Any(registration =>
                        registration.Kind == TournamentRegistrationKind.Team &&
                        registration.Status == TournamentRegistrationStatus.Active &&
                        registration.Team?.Id == team.Id);
                })
                .OrderBy(tournament => tournament.StartTime)
                .ThenBy(tournament => tournament.Name)
                .Select(tournament => new PublicTeamTournamentDTO
                {
                    TournamentId = tournament.Id,
                    Name = tournament.Name
                })
                .ToList();

            return new PublicTeamProfileDTO
            {
                TeamName = team.Name,
                CaptainUsername = captainUsername,
                LogoUrl = team.LogoUrl,
                Members = members,
                Tournaments = tournaments
            };
        }
    }

    public PublicProfileMatchSummariesDTO? GetPublicUserMatchSummaries(string username)
    {
        lock(_syncRoot)
        {
            var normalizedUsername = username.Trim();
            if(string.IsNullOrWhiteSpace(normalizedUsername))
                return null;

            var user = _document.Users.FirstOrDefault(candidate =>
                !candidate.IsDeleted &&
                string.Equals(candidate.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));

            if(user is null ||
               string.IsNullOrWhiteSpace(user.Username) ||
               string.IsNullOrWhiteSpace(user.Firstname) ||
               string.IsNullOrWhiteSpace(user.Lastname))
                return null;

            return BuildPublicProfileMatchSummaries(user.Id, new HashSet<Guid>());
        }
    }

    public PublicProfileMatchSummariesDTO? GetPublicTeamMatchSummaries(string teamName)
    {
        lock(_syncRoot)
        {
            var normalizedTeamName = teamName.Trim();
            if(string.IsNullOrWhiteSpace(normalizedTeamName))
                return null;

            var team = _document.Teams.FirstOrDefault(candidate =>
                !_deletedTeamIds.Contains(candidate.Id) &&
                string.Equals(candidate.Name, normalizedTeamName, StringComparison.OrdinalIgnoreCase));

            return team is null
                ? null
                : BuildPublicProfileMatchSummaries(null, new HashSet<Guid> { team.Id });
        }
    }

    private PublicProfileMatchSummariesDTO BuildPublicProfileMatchSummaries(Guid? userId, IReadOnlySet<Guid> teamIds)
    {
        var previous = new List<MockProfileMatchCandidate>();
        var upcoming = new List<MockProfileMatchCandidate>();

        foreach(var tournament in _document.Tournaments)
        {
            if(tournament.Status == TournamentStatus.Canceled)
                continue;

            EnsureRegistrationProjection(tournament);
            var activeRegistrations = GetRegistrationDetails(tournament)
                .Where(registration => registration.Status == TournamentRegistrationStatus.Active)
                .ToList();
            var subjectRegistrations = activeRegistrations
                .Where(registration => IsSubjectRegistration(registration, userId, teamIds))
                .ToList();

            if(subjectRegistrations.Count == 0)
                continue;

            var snapshots = _publicRegistrationSnapshots.TryGetValue(tournament.Id, out var tournamentSnapshots)
                ? tournamentSnapshots
                : new Dictionary<Guid, MockRegistrationSnapshot>();

            foreach(var match in tournament.Matches)
            {
                if(!TryCreateMatchCandidate(tournament, match, subjectRegistrations, out var candidate))
                    continue;

                if(IsPreviousMatch(candidate))
                {
                    previous.Add(candidate with { OpponentDisplayName = GetOpponentDisplayName(candidate, snapshots) });
                }
                else if(IsUpcomingMatch(candidate))
                {
                    upcoming.Add(candidate with { OpponentDisplayName = GetOpponentDisplayName(candidate, snapshots) });
                }
            }
        }

        var previousSummaries = previous
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.OpponentDisplayName))
            .GroupBy(candidate => candidate.TournamentId)
            .Select(group => group
                .OrderByDescending(candidate => PreviousSortTime(candidate))
                .ThenByDescending(candidate => candidate.RoundNumber)
                .ThenByDescending(candidate => candidate.MatchNumber)
                .ThenByDescending(candidate => candidate.MatchId)
                .First())
            .Select(candidate => ToPublicProfileMatchSummary(candidate, isPrevious: true))
            .OrderBy(summary => summary.TournamentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TournamentName, StringComparer.Ordinal)
            .ThenBy(summary => summary.TournamentId)
            .ToList();

        var upcomingSummaries = upcoming
            .Where(candidate =>
                !HasAssignedOpponent(candidate) ||
                !string.IsNullOrWhiteSpace(candidate.OpponentDisplayName))
            .GroupBy(candidate => candidate.TournamentId)
            .Select(group => group
                .OrderBy(candidate => UpcomingSortTime(candidate).HasValue ? 0 : 1)
                .ThenBy(candidate => UpcomingSortTime(candidate))
                .ThenBy(candidate => candidate.RoundNumber)
                .ThenBy(candidate => candidate.MatchNumber)
                .ThenBy(candidate => candidate.MatchId)
                .First())
            .Select(candidate => ToPublicProfileMatchSummary(candidate, isPrevious: false))
            .OrderBy(summary => summary.TournamentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TournamentName, StringComparer.Ordinal)
            .ThenBy(summary => summary.TournamentId)
            .ToList();

        return new PublicProfileMatchSummariesDTO
        {
            PreviousMatches = previousSummaries,
            UpcomingMatches = upcomingSummaries
        };
    }

    private static bool IsSubjectRegistration(
        TournamentRegistrationDTO registration,
        Guid? userId,
        IReadOnlySet<Guid> teamIds)
    {
        if(registration.Kind == TournamentRegistrationKind.Individual)
            return userId.HasValue && registration.User?.Id == userId.Value;

        if(registration.Team?.Id is not { } teamId)
            return false;

        return userId is null
            ? teamIds.Contains(teamId)
            : registration.Team.CaptainUserId == userId.Value ||
              registration.RosterMembers.Any(member =>
                  member.User.Id == userId.Value &&
                  member.ConfirmationStatus != RosterMemberConfirmationStatus.Pending);
    }

    private static bool TryCreateMatchCandidate(
        TournamentExtended tournament,
        Match match,
        IReadOnlyCollection<TournamentRegistrationDTO> subjectRegistrations,
        out MockProfileMatchCandidate candidate)
    {
        candidate = default!;
        if(match.Id == Guid.Empty || tournament.Id == Guid.Empty)
            return false;

        var individual = match.ParticipationMode == ParticipationMode.Individual;
        var participant1Id = individual ? match.UserParticipant1Id : match.TeamParticipant1Id;
        var participant2Id = individual ? match.UserParticipant2Id : match.TeamParticipant2Id;
        var subjectIds = subjectRegistrations
            .Select(registration => registration.Kind == TournamentRegistrationKind.Individual
                ? registration.User?.Id
                : registration.Team?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var subjectIsParticipant1 = participant1Id.HasValue && subjectIds.Contains(participant1Id.Value);
        var subjectIsParticipant2 = participant2Id.HasValue && subjectIds.Contains(participant2Id.Value);
        if((subjectIsParticipant1 && subjectIsParticipant2) || (!subjectIsParticipant1 && !subjectIsParticipant2))
            return false;

        candidate = new MockProfileMatchCandidate
        {
            MatchId = match.Id,
            TournamentId = tournament.Id,
            TournamentName = tournament.Name,
            ParticipationMode = match.ParticipationMode,
            Participant1Id = participant1Id,
            Participant2Id = participant2Id,
            SubjectIsParticipant1 = subjectIsParticipant1,
            Participant1IsBYE = match.Participant1IsBYE,
            Participant2IsBYE = match.Participant2IsBYE,
            StartTime = match.StartTime,
            EndTime = match.EndTime,
            EstimatedStartTime = match.EstimatedStartTime,
            EstimatedEndTime = match.EstimatedEndTime,
            ResultRecordedAtUtc = match.ResultRecordedAtUtc,
            LifecycleState = match.LifecycleState,
            ResultKind = match.ResultKind,
            Participant1Score = match.Participant1Score,
            Participant2Score = match.Participant2Score,
            RoundNumber = match.RoundNumber,
            MatchNumber = match.MatchNumber,
            IsLowerBracketMatch = match.IsLowerBracketMatch
        };

        return true;
    }

    private static bool IsPreviousMatch(MockProfileMatchCandidate candidate) =>
        (candidate.LifecycleState == MatchLifecycleState.Completed ||
         candidate.LifecycleState == MatchLifecycleState.Forfeited) &&
        (candidate.LifecycleState == MatchLifecycleState.Forfeited ||
         (candidate.Participant1Score.HasValue && candidate.Participant2Score.HasValue)) &&
        candidate.Participant1Id.HasValue &&
        candidate.Participant2Id.HasValue &&
        candidate.Participant1Id != candidate.Participant2Id &&
        !candidate.Participant1IsBYE &&
        !candidate.Participant2IsBYE;

    private static bool IsUpcomingMatch(MockProfileMatchCandidate candidate)
    {
        if(candidate.LifecycleState != MatchLifecycleState.AwaitingEndedConfirmation ||
           candidate.StartTime != default ||
           candidate.Participant1IsBYE ||
           candidate.Participant2IsBYE ||
           (candidate.Participant1Id.HasValue && candidate.Participant2Id.HasValue && candidate.Participant1Id == candidate.Participant2Id))
            return false;

        return true;
    }

    private static DateTime? PreviousSortTime(MockProfileMatchCandidate candidate) =>
        Normalize(candidate.ResultRecordedAtUtc) ??
        Normalize(candidate.EndTime) ??
        Normalize(candidate.StartTime);

    private static DateTime? UpcomingSortTime(MockProfileMatchCandidate candidate) =>
        Normalize(candidate.EstimatedStartTime);

    private static MockRegistrationSnapshot ToRegistrationSnapshot(TournamentRegistrationDTO registration) =>
        new(
            registration.Kind == TournamentRegistrationKind.Individual ? registration.User?.Id : registration.Team?.Id,
            registration.Kind == TournamentRegistrationKind.Individual
                ? registration.User?.Username ?? registration.User?.DisplayName
                : registration.Team?.Name);

    private string? GetOpponentDisplayName(
        MockProfileMatchCandidate candidate,
        IReadOnlyDictionary<Guid, MockRegistrationSnapshot> snapshots)
    {
        var opponentId = candidate.SubjectIsParticipant1
            ? candidate.Participant2Id
            : candidate.Participant1Id;
        if(opponentId is not { } id)
            return null;

        var currentDisplayName = candidate.ParticipationMode == ParticipationMode.Individual
            ? _document.Users.FirstOrDefault(user =>
                user.Id == id &&
                !user.IsDeleted &&
                !string.IsNullOrWhiteSpace(user.Username) &&
                !string.IsNullOrWhiteSpace(user.Firstname) &&
                !string.IsNullOrWhiteSpace(user.Lastname))?.Username
            : _document.Teams.FirstOrDefault(team =>
                team.Id == id &&
                !_deletedTeamIds.Contains(team.Id) &&
                !string.IsNullOrWhiteSpace(team.Name))?.Name;

        if(!string.IsNullOrWhiteSpace(currentDisplayName))
            return currentDisplayName;

        return snapshots.TryGetValue(id, out var snapshot)
            ? snapshot.DisplayName
            : null;
    }

    private static bool HasAssignedOpponent(MockProfileMatchCandidate candidate) =>
        candidate.SubjectIsParticipant1
            ? candidate.Participant2Id.HasValue
            : candidate.Participant1Id.HasValue;

    private static PublicProfileMatchSummaryDTO ToPublicProfileMatchSummary(
        MockProfileMatchCandidate candidate,
        bool isPrevious) =>
        new()
        {
            MatchId = candidate.MatchId,
            TournamentId = candidate.TournamentId,
            TournamentName = candidate.TournamentName,
            OpponentDisplayName = candidate.OpponentDisplayName,
            OpponentIsTbd = string.IsNullOrWhiteSpace(candidate.OpponentDisplayName),
            EstimatedStartTime = Normalize(candidate.EstimatedStartTime),
            EstimatedEndTime = Normalize(candidate.EstimatedEndTime),
            ScheduledStartTime = null,
            StartedAtUtc = isPrevious ? Normalize(candidate.StartTime) : null,
            CompletedAtUtc = isPrevious ? Normalize(candidate.EndTime) : null,
            LifecycleState = candidate.LifecycleState,
            ResultKind = candidate.ResultKind,
            ParticipantScore = candidate.SubjectIsParticipant1 ? candidate.Participant1Score : candidate.Participant2Score,
            OpponentScore = candidate.SubjectIsParticipant1 ? candidate.Participant2Score : candidate.Participant1Score,
            RoundNumber = candidate.RoundNumber,
            MatchNumber = candidate.MatchNumber,
            IsLowerBracketMatch = candidate.IsLowerBracketMatch
        };

    private static DateTime? Normalize(DateTime value) =>
        value == DateTime.MinValue
            ? null
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Normalize(DateTime? value) => value is null ? null : Normalize(value.Value);

    public TournamentExtended? GetTournament(Guid id)
    {
        lock(_syncRoot)
        {
            return ClonePublicTournament(_document.Tournaments.FirstOrDefault(tournament => tournament.Id == id));
        }
    }

    public List<PublicUserDTO> GetAdminUsers(string? query = null, int? pageSize = null)
    {
        lock(_syncRoot)
        {
            var adminUserIds = _document.Profiles
                .Where(profile => string.Equals(profile.Persona, "admin", StringComparison.OrdinalIgnoreCase))
                .Select(profile => profile.Profile.User?.Id)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            var trimmedQuery = query?.Trim();

            var admins = _document.Users
                .Where(user => !user.IsDeleted && adminUserIds.Contains(user.Id))
                .Where(user =>
                    string.IsNullOrWhiteSpace(trimmedQuery) ||
                    (user.Username ?? string.Empty).Contains(trimmedQuery!, StringComparison.OrdinalIgnoreCase) ||
                    user.DisplayName.Contains(trimmedQuery!, StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                .Select(ToContactAdminProjection);

            if(pageSize is > 0)
                admins = admins.Take(pageSize.Value);

            return admins.ToList();
        }
    }

    public TournamentExtended CreateTournament(CreateTournamentDTO dto)
    {
        lock(_syncRoot)
        {
            var bracketType = dto.BracketType;
            var participationMode = dto.ParticipationMode.GetValueOrDefault();
            var leaderboardRankingMetric = ValidateLeaderboardConfiguration(bracketType, dto.LeaderboardRankingMetric, participationMode);

            var tournament = new TournamentExtended
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                StartTime = dto.PlannedStartTime,
                EndTime = dto.PlannedStartTime.AddHours(4),
                PlannedStartTime = dto.PlannedStartTime,
                AverageGameDurationMinutes = NormalizeLeaderboardScheduleDuration(bracketType, dto.AverageGameDurationMinutes),
                RoundBreakDurationMinutes = NormalizeLeaderboardScheduleDuration(bracketType, dto.RoundBreakDurationMinutes),
                EstimatedEndTime = null,
                ImageUrl = "/mock-data-local/generated-tournament.svg",
                Status = TournamentStatus.Scheduled,
                BracketType = bracketType,
                LeaderboardRankingMetric = leaderboardRankingMetric,
                Format = dto.Format,
                FinalsFormat = dto.FinalsFormat,
                ParticipationMode = participationMode,
                TeamSize = dto.TeamSize,
                AssignedAdminUserId = dto.AssignedAdminUserId,
                FirstPlacePrize = NormalizePrize(dto.FirstPlacePrize),
                SecondPlacePrize = NormalizePrize(dto.SecondPlacePrize),
                ThirdPlacePrize = NormalizePrize(dto.ThirdPlacePrize)
            };

            _document.Tournaments.Add(tournament);
            return WithContactAdminProjection(Clone(tournament)!);
        }
    }

    public TournamentExtended UpdateTournament(Guid id, UpdateTournamentDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(id);
            var bracketType = dto.BracketType;
            var participationMode = dto.ParticipationMode ?? tournament.ParticipationMode;
            var leaderboardRankingMetric = ValidateLeaderboardConfiguration(bracketType, dto.LeaderboardRankingMetric, participationMode);
            if(tournament.LeaderboardRankingMetric != leaderboardRankingMetric &&
               tournament.Status != TournamentStatus.Scheduled)
                throw new InvalidOperationException("Leaderboard ranking metric cannot be changed after the tournament has started.");

            tournament.Name = dto.Name;
            tournament.Format = dto.Format;
            tournament.FinalsFormat = dto.FinalsFormat;
            tournament.BracketType = bracketType;
            tournament.LeaderboardRankingMetric = leaderboardRankingMetric;
            tournament.ParticipationMode = participationMode;
            tournament.TeamSize = dto.TeamSize;
            tournament.PlannedStartTime = dto.PlannedStartTime;
            tournament.AverageGameDurationMinutes = NormalizeLeaderboardScheduleDuration(bracketType, dto.AverageGameDurationMinutes);
            tournament.RoundBreakDurationMinutes = NormalizeLeaderboardScheduleDuration(bracketType, dto.RoundBreakDurationMinutes);
            tournament.EstimatedEndTime = tournament.Matches.Any() ? tournament.Matches.Max(match => match.EstimatedEndTime) : null;
            tournament.AssignedAdminUserId = dto.AssignedAdminUserId;
            tournament.FirstPlacePrize = NormalizePrize(dto.FirstPlacePrize);
            tournament.SecondPlacePrize = NormalizePrize(dto.SecondPlacePrize);
            tournament.ThirdPlacePrize = NormalizePrize(dto.ThirdPlacePrize);

            if(dto.Image != null)
                tournament.ImageUrl = "/mock-data-local/generated-tournament.svg";

            return WithContactAdminProjection(Clone(tournament)!);
        }
    }

    public TournamentExtended ReplaceTournamentSponsors(Guid id, ReplaceTournamentSponsorsDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(id);
            var placements = (dto.SponsorPlacements ?? []).Take(1).ToList();

            tournament.SponsorPlacement = placements
                .Select((placement, index) =>
                {
                    var sponsor = _document.Sponsors.Single(existing => existing.Id == placement.SponsorId);
                    return new TournamentSponsorPlacement
                    {
                        Id = index + 1,
                        SponsorId = sponsor.Id,
                        SponsorName = sponsor.Name,
                        SponsorTier = sponsor.SponsorTier,
                        SponsorLogoUrl = sponsor.LogoUrl,
                        SponsorInfoUrl = sponsor.InfoUrl,
                        SponsorDescription = sponsor.Description,
                        Context = placement.Context,
                        Headline = placement.Headline,
                        SupportLine = placement.SupportLine,
                        DisplayOrder = placement.DisplayOrder
                    };
                })
                .OrderBy(placement => placement.Context)
                .ThenBy(placement => placement.DisplayOrder)
                .ThenBy(placement => placement.SponsorName)
                .FirstOrDefault();

            return Clone(tournament)!;
        }
    }

    public void SetTournamentLifecycleState(Guid tournamentId, TournamentStatus state)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);

            switch(state)
            {
                case TournamentStatus.Canceled:
                    if(tournament.Status == TournamentStatus.Completed)
                        throw new InvalidOperationException("Tournament cannot be canceled when it is already completed.");

                    tournament.Status = TournamentStatus.Canceled;
                    break;
                case TournamentStatus.InProgress:
                    if(tournament.Status != TournamentStatus.Scheduled)
                        throw new InvalidOperationException("Tournament has to be scheduled to be able to start.");
                    if(tournament.BracketType == BracketType.Swiss)
                        throw new NotSupportedException("Swiss tournaments are not supported by the mock backend.");

                    if(tournament.BracketType == BracketType.Leaderboard)
                    {
                        tournament.StartTime = DateTime.UtcNow;
                        tournament.Status = TournamentStatus.InProgress;
                        break;
                    }

                    var registrations = GetRegistrationDetails(tournament);
                    var activeParticipantCount = registrations.Count(registration =>
                        registration.Status == TournamentRegistrationStatus.Active &&
                        registration.Kind == (tournament.ParticipationMode == ParticipationMode.Team
                            ? TournamentRegistrationKind.Team
                            : TournamentRegistrationKind.Individual) &&
                        (tournament.ParticipationMode == ParticipationMode.Team
                            ? registration.Team is not null && registration.Team.Id != Guid.Empty
                            : registration.User is not null && registration.User.Id != Guid.Empty));
                    if(activeParticipantCount < 2)
                        throw new InvalidOperationException("At least 2 participants required.");

                    tournament.StartTime = DateTime.UtcNow;
                    tournament.Status = TournamentStatus.InProgress;
                    UpdatePublicRegistrationProjection(tournament, registrations);
                    if(!tournament.Matches.Any())
                        tournament.Matches = BuildGeneratedMatches(tournament, registrations);
                    break;
                case TournamentStatus.Completed:
                    if(tournament.Status != TournamentStatus.InProgress)
                        throw new InvalidOperationException("Tournament has to be in progress to be able to complete.");

                    if(tournament.BracketType == BracketType.Leaderboard)
                    {
                        var leaderboardRows = BuildLeaderboardRows(tournament, GetLeaderboardParticipants(tournament.Id));
                        if(leaderboardRows.Count == 0)
                            throw new InvalidOperationException("A leaderboard tournament requires at least one recorded result to be completed.");

                        tournament.EndTime = DateTime.UtcNow;
                        tournament.Status = TournamentStatus.Completed;
                        tournament.Placements = leaderboardRows
                            .GroupBy(row => row.Rank)
                            .Select(group => new Placement
                            {
                                Place = group.Key,
                                LeaderboardParticipants = group.ToList()
                            })
                            .ToList();
                        break;
                    }

                    TournamentProjectionMapper.PopulateParticipantProjection(tournament);
                    var placements = BuildPlacements(tournament);
                    tournament.EndTime = DateTime.UtcNow;
                    tournament.Status = TournamentStatus.Completed;
                    tournament.Placements = placements;
                    break;
                case TournamentStatus.Scheduled:
                    if(tournament.Status is not (TournamentStatus.Completed or TournamentStatus.Canceled))
                        throw new InvalidOperationException("Tournament has to be completed or canceled to be able to reset.");

                    tournament.Status = TournamentStatus.Scheduled;
                    tournament.StartTime = DateTime.MinValue;
                    tournament.EndTime = DateTime.MinValue;
                    tournament.EstimatedEndTime = null;
                    tournament.Matches = [];
                    tournament.Placements = [];
                    _leaderboardParticipants.Remove(tournament.Id);
                    break;
                default:
                    throw new InvalidOperationException("A supported tournament lifecycle state is required.");
            }
        }
    }

    public void DeleteTournament(Guid tournamentId)
    {
        lock(_syncRoot)
        {
            if(_document.Tournaments.FirstOrDefault(tournament => tournament.Id == tournamentId)?.Status == TournamentStatus.InProgress)
                throw new InvalidOperationException("Tournament cannot be deleted while it is in progress.");

            _document.Tournaments.RemoveAll(tournament => tournament.Id == tournamentId);
            _registrationDetails.Remove(tournamentId);
            _publicRegistrationSnapshots.Remove(tournamentId);
            _leaderboardParticipants.Remove(tournamentId);
        }
    }

    public PublicLeaderboardDTO GetLeaderboard(Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredLeaderboardTournament(tournamentId);
            return new PublicLeaderboardDTO
            {
                TournamentId = tournament.Id,
                RankingMetric = ResolveLeaderboardMetric(tournament),
                Rows = BuildLeaderboardRows(tournament, GetLeaderboardParticipants(tournament.Id))
            };
        }
    }

    public AdminLeaderboardResponseDTO GetAdminLeaderboard(Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredLeaderboardTournament(tournamentId);
            return BuildAdminLeaderboardResponse(tournament);
        }
    }

    public AdminLeaderboardParticipantDTO RecordLeaderboardAttempt(
        Guid tournamentId,
        RecordLeaderboardAttemptDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredLeaderboardTournament(tournamentId);
            EnsureLeaderboardInProgress(tournament);
            ValidateLeaderboardAttemptValue(tournament, dto.Score, dto.DurationMilliseconds);

            var participants = GetOrCreateLeaderboardParticipantList(tournament.Id);
            var participant = ResolveOrCreateLeaderboardParticipant(tournament, participants, dto);
            var now = DateTime.UtcNow;
            participant.Attempts.Add(new MockLeaderboardAttempt
            {
                Id = Guid.NewGuid(),
                Score = dto.Score,
                DurationMilliseconds = dto.DurationMilliseconds,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                RowVersion = Guid.NewGuid()
            });

            return ToAdminLeaderboardParticipantDTO(participant);
        }
    }

    public LeaderboardAttemptDTO UpdateLeaderboardAttempt(
        Guid tournamentId,
        Guid attemptId,
        UpdateLeaderboardAttemptDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredLeaderboardTournament(tournamentId);
            EnsureLeaderboardInProgress(tournament);
            ValidateLeaderboardAttemptValue(tournament, dto.Score, dto.DurationMilliseconds);

            var attempt = FindLeaderboardAttempt(tournament.Id, attemptId);
            if(!dto.RowVersion.HasValue || dto.RowVersion.Value != attempt.RowVersion)
                throw new LeaderboardConflictException();

            attempt.Score = dto.Score;
            attempt.DurationMilliseconds = dto.DurationMilliseconds;
            attempt.UpdatedAtUtc = DateTime.UtcNow;
            attempt.RowVersion = Guid.NewGuid();

            return ToLeaderboardAttemptDTO(attempt);
        }
    }

    public void DeleteLeaderboardAttempt(Guid tournamentId, Guid attemptId, Guid rowVersion)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredLeaderboardTournament(tournamentId);
            EnsureLeaderboardInProgress(tournament);

            var attempt = FindLeaderboardAttempt(tournament.Id, attemptId);
            if(rowVersion != attempt.RowVersion)
                throw new LeaderboardConflictException();

            foreach(var participant in GetLeaderboardParticipants(tournament.Id))
                participant.Attempts.RemoveAll(candidate => candidate.Id == attemptId);
        }
    }

    public Match UpdateMatch(string persona, Guid matchId, UpdateMatchDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = _document.Tournaments.FirstOrDefault(candidate => candidate.Matches.Any(match => match.Id == matchId))
                ?? throw new InvalidOperationException($"Mock match '{matchId}' was not found.");

            var match = tournament.Matches.First(existing => existing.Id == matchId);
            EnsureMockTournamentInProgress(tournament);
            if(!string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a tournament administrator may resolve this match.");
            ApplyMockDeadline(tournament, match);
            if(match.LifecycleState is not (MatchLifecycleState.Disputed or MatchLifecycleState.AdminResolutionRequired))
                throw new InvalidOperationException("This match does not require administrator resolution.");
            if(!HasBothMockParticipants(match) || match.Participant1IsBYE || match.Participant2IsBYE)
                throw new InvalidOperationException("Both non-BYE participants must be assigned before resolving this match.");
            ValidateMockScore(match, dto.Participant1Score, dto.Participant2Score);
            CompleteMockScore(tournament, match, dto.Participant1Score, dto.Participant2Score, MatchResultKind.AdminResolution);

            return ClonePublicMatch(match);
        }
    }

    public Match UpdateMatch(Guid matchId, UpdateMatchDTO dto)
    {
        lock(_syncRoot)
        {
            var tournament = _document.Tournaments.FirstOrDefault(candidate => candidate.Matches.Any(match => match.Id == matchId))
                ?? throw new InvalidOperationException($"Mock match '{matchId}' was not found.");

            var match = tournament.Matches.First(existing => existing.Id == matchId);
            match.Participant1Score = dto.Participant1Score;
            match.Participant2Score = dto.Participant2Score;

            if(dto.Participant1Score == dto.Participant2Score)
            {
                match.UserWinnerId = null;
                match.UserLoserId = null;
                match.TeamWinnerId = null;
                match.TeamLoserId = null;
            }
            else if(match.ParticipationMode == ParticipationMode.Team)
            {
                var participant1Won = dto.Participant1Score > dto.Participant2Score;
                match.TeamWinnerId = participant1Won ? match.TeamParticipant1Id : match.TeamParticipant2Id;
                match.TeamLoserId = participant1Won ? match.TeamParticipant2Id : match.TeamParticipant1Id;
            }
            else
            {
                var participant1Won = dto.Participant1Score > dto.Participant2Score;
                match.UserWinnerId = participant1Won ? match.UserParticipant1Id : match.UserParticipant2Id;
                match.UserLoserId = participant1Won ? match.UserParticipant2Id : match.UserParticipant1Id;
            }

            PropagateMatchResult(tournament, match);
            return ClonePublicMatch(match);
        }
    }

    public MatchActionStateDTO GetMatchActionState(string persona, Guid matchId)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            ApplyMockDeadline(tournament, match);

            var side = FindMockParticipantSide(persona, tournament, match);
            var isAdmin = string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase);
            var currentUserId = isAdmin ? GetCurrentProfile(persona).User?.Id : null;
            var canViewPrivateReports = CanViewPrivateReports(
                persona,
                tournament.AssignedAdminUserId,
                currentUserId,
                side.HasValue);
            var canAct = tournament.Status == TournamentStatus.InProgress && side.HasValue;
            var hasBothParticipants = GetMockParticipantId(match, MatchParticipantSide.Participant1).HasValue &&
                GetMockParticipantId(match, MatchParticipantSide.Participant2).HasValue;
            var hasByeParticipant = match.Participant1IsBYE || match.Participant2IsBYE;
            var canConfirm = canAct &&
                hasBothParticipants && !hasByeParticipant &&
                match.LifecycleState is not (MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired) &&
                !HasMockEnded(match, side!.Value);
            var scoreStateAllowsSubmission = side.HasValue &&
                (match.LifecycleState == MatchLifecycleState.AwaitingScore ||
                 (match.LifecycleState == MatchLifecycleState.ScoreConfirmation && !HasMockSubmittedScore(match, side.Value)) ||
                 (match.LifecycleState == MatchLifecycleState.Disputed && GetMockCorrectionCount(match, side.Value) < 1));
            var canSubmit = canAct && hasBothParticipants && !hasByeParticipant && match.Participant1Ended && match.Participant2Ended &&
                scoreStateAllowsSubmission;
            var canForfeit = canAct && match.LifecycleState is not (MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired);
            canForfeit &= hasBothParticipants && !hasByeParticipant;
            var canResolve = isAdmin &&
                tournament.Status == TournamentStatus.InProgress &&
                match.LifecycleState is MatchLifecycleState.Disputed or MatchLifecycleState.AdminResolutionRequired;
            var canForceForfeit = isAdmin &&
                tournament.Status == TournamentStatus.InProgress &&
                hasBothParticipants &&
                !hasByeParticipant &&
                match.LifecycleState is not (MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired);
            var canReverse = false;
            string? reverseBlockedReason;
            if(!isAdmin)
                reverseBlockedReason = "admin_required";
            else if(tournament.Status != TournamentStatus.InProgress)
                reverseBlockedReason = "tournament_not_in_progress";
            else if(match.LifecycleState is not (MatchLifecycleState.Completed or MatchLifecycleState.Forfeited))
                reverseBlockedReason = "match_not_completed";
            else
            {
                var downstream = GetMockDownstreamMatches(tournament, match);
                if(downstream is null)
                    reverseBlockedReason = "downstream_graph_too_large";
                else
                {
                    canReverse = !HasMockUnprovenancedDownstreamAssignment(match, downstream) &&
                        downstream.All(downstreamMatch => !HasMockPlayedResult(downstreamMatch));
                    reverseBlockedReason = canReverse ? null : "match_reversal_blocked";
                }
            }

            return new MatchActionStateDTO
            {
                Match = ClonePublicMatch(match),
                AuthorizedParticipant = side,
                CanConfirmEnded = canConfirm,
                CanSubmitScore = canSubmit,
                CanForfeit = canForfeit,
                CanResolve = canResolve,
                ResolveBlockedReason = canResolve ? null : isAdmin
                    ? tournament.Status != TournamentStatus.InProgress ? "tournament_not_in_progress" : "match_not_disputed"
                    : "admin_required",
                CanForceForfeit = canForceForfeit,
                ForceForfeitBlockedReason = canForceForfeit ? null : isAdmin
                    ? tournament.Status != TournamentStatus.InProgress ? "tournament_not_in_progress"
                    : match.LifecycleState is MatchLifecycleState.Completed or MatchLifecycleState.Forfeited ? "match_already_completed"
                    : match.LifecycleState == MatchLifecycleState.AdminResolutionRequired ? "match_requires_admin_resolution"
                    : !hasBothParticipants ? "match_not_ready" : "match_not_forfeitable"
                    : "admin_required",
                CanReverse = canReverse,
                ReverseBlockedReason = canReverse ? null : reverseBlockedReason,
                Participant1ReportedScore1 = canViewPrivateReports ? match.Participant1ReportedScore1 : null,
                Participant1ReportedScore2 = canViewPrivateReports ? match.Participant1ReportedScore2 : null,
                Participant2ReportedScore1 = canViewPrivateReports ? match.Participant2ReportedScore1 : null,
                Participant2ReportedScore2 = canViewPrivateReports ? match.Participant2ReportedScore2 : null
            };
        }
    }

    public MatchOpponentProfileDTO GetMatchOpponentProfile(string persona, Guid matchId)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            var authorizedSide = FindMockParticipantSide(persona, tournament, match)
                ?? throw new UnauthorizedAccessException("Only a matched individual or team captain can view the opponent profile.");
            var opponentId = match.ParticipationMode == ParticipationMode.Team
                ? authorizedSide == MatchParticipantSide.Participant1 ? match.TeamParticipant2Id : match.TeamParticipant1Id
                : authorizedSide == MatchParticipantSide.Participant1 ? match.UserParticipant2Id : match.UserParticipant1Id;

            if(!opponentId.HasValue)
                throw new KeyNotFoundException("The opponent profile is not available.");

            PublicUserDTO? user;
            if(match.ParticipationMode == ParticipationMode.Team)
            {
                var teams = tournament.Teams.Concat(_document.Teams)
                    .GroupBy(team => team.Id)
                    .Select(group => group.First());
                var opponentTeam = teams.FirstOrDefault(team => team.Id == opponentId.Value);
                var captainId = opponentTeam?.CaptainUserId;
                user = opponentTeam?.Members.FirstOrDefault(member => member.Id == captainId);
                if(user is null && captainId.HasValue)
                {
                    var captain = _document.Users.FirstOrDefault(candidate => candidate.Id == captainId.Value);
                    if(captain is not null)
                        user = PublicUserDTO.FromUser(captain);
                }
            }
            else
            {
                user = tournament.Users.FirstOrDefault(candidate => candidate.Id == opponentId.Value);
                if(user is null)
                {
                    var opponent = _document.Users.FirstOrDefault(candidate => candidate.Id == opponentId.Value);
                    if(opponent is not null)
                        user = PublicUserDTO.FromUser(opponent);
                }
            }

            if(user is null || string.IsNullOrWhiteSpace(user.Username))
                throw new KeyNotFoundException("The opponent profile is not available.");

            return new MatchOpponentProfileDTO
            {
                Username = user.Username,
                Firstname = user.Firstname,
                Lastname = user.Lastname,
                DiscordId = user.DiscordId,
                SteamId = user.SteamId,
                RiotId = user.RiotId
            };
        }
    }

    public Match ConfirmMatchEnded(string persona, Guid matchId)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            EnsureMockTournamentInProgress(tournament);
            ApplyMockDeadline(tournament, match);
            var side = FindMockParticipantSide(persona, tournament, match)
                ?? throw new InvalidOperationException("Only an assigned participant may confirm this match.");

            EnsureMockParticipantNumber(side);
            if(!HasBothMockParticipants(match) || match.Participant1IsBYE || match.Participant2IsBYE)
                throw new InvalidOperationException("A BYE match cannot accept participant actions.");
            if(match.LifecycleState is MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired)
                throw new InvalidOperationException("This match is not accepting participant actions.");
            if(HasMockEnded(match, side))
                throw new InvalidOperationException("Your end confirmation has already been saved.");

            if(side == MatchParticipantSide.Participant1)
                match.Participant1Ended = true;
            else
                match.Participant2Ended = true;

            if(match.Participant1Ended && match.Participant2Ended)
                match.LifecycleState = MatchLifecycleState.AwaitingScore;

            match.ResultVersion++;
            return Clone(match)!;
        }
    }

    public Match SubmitMatchScore(string persona, Guid matchId, SubmitMatchScoreDTO request)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            EnsureMockTournamentInProgress(tournament);
            ApplyMockDeadline(tournament, match);
            var side = FindMockParticipantSide(persona, tournament, match)
                ?? throw new InvalidOperationException("Only an assigned participant may submit a score.");
            if(!HasBothMockParticipants(match) || match.Participant1IsBYE || match.Participant2IsBYE)
                throw new InvalidOperationException("Both non-BYE participants must be assigned before submitting a score.");

            ValidateMockScore(match, request.Participant1Score, request.Participant2Score);
            if(!match.Participant1Ended || !match.Participant2Ended)
                throw new InvalidOperationException("Both participants must confirm the match ended before submitting a score.");
            if(match.LifecycleState is MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired)
                throw new InvalidOperationException("This match is no longer accepting score reports.");
            if(match.LifecycleState == MatchLifecycleState.ScoreConfirmation && HasMockSubmittedScore(match, side))
                throw new InvalidOperationException("Your initial score report has already been submitted.");
            if(match.LifecycleState == MatchLifecycleState.Disputed && GetMockCorrectionCount(match, side) >= 1)
                throw new InvalidOperationException("Each side may correct its score only once.");

            SetMockReportedScore(match, side, request.Participant1Score, request.Participant2Score);
            if(match.LifecycleState == MatchLifecycleState.AwaitingScore)
            {
                match.LifecycleState = MatchLifecycleState.ScoreConfirmation;
                match.ScoreConfirmationDeadlineUtc = DateTime.UtcNow.AddMinutes(5);
            }
            else if(match.LifecycleState == MatchLifecycleState.Disputed)
            {
                IncrementMockCorrectionCount(match, side);
            }

            if(match.Participant1ReportedScore1.HasValue && match.Participant2ReportedScore1.HasValue)
            {
                if(ReportedMockScoresMatch(match))
                    CompleteMockScore(tournament, match, request.Participant1Score, request.Participant2Score);
                else
                {
                    match.ScoreConfirmationDeadlineUtc = null;
                    match.LifecycleState = MatchLifecycleState.Disputed;
                    match.CorrectionDeadlineUtc = DateTime.UtcNow.AddMinutes(5);
                }
            }

            match.ResultVersion++;
            return Clone(match)!;
        }
    }

    public Match ForfeitMatch(string persona, Guid matchId, ForfeitMatchDTO request)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            EnsureMockTournamentInProgress(tournament);
            ApplyMockDeadline(tournament, match);
            if(!HasBothMockParticipants(match) || match.Participant1IsBYE || match.Participant2IsBYE)
                throw new InvalidOperationException("A match must have two assigned non-BYE participants before it can be forfeited.");
            var isAdmin = string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase);
            var participant = request.Participant ?? (isAdmin ? null : FindMockParticipantSide(persona, tournament, match));
            if(!participant.HasValue)
                throw new InvalidOperationException("Select the side to forfeit.");
            if(!isAdmin && FindMockParticipantSide(persona, tournament, match) != participant)
                throw new InvalidOperationException("Only your assigned side may be forfeited.");
            if(match.LifecycleState is MatchLifecycleState.Completed or MatchLifecycleState.Forfeited or MatchLifecycleState.AdminResolutionRequired)
                throw new InvalidOperationException("This match cannot be forfeited in its current state.");

            var participantNumber = participant == MatchParticipantSide.Participant1 ? 1 : 2;
            match.Participant1Score = participantNumber == 1 ? 0 : 1;
            match.Participant2Score = participantNumber == 2 ? 0 : 1;
            SetMockWinnerAndLoser(match, participantNumber == 1 ? 2 : 1);
            match.ForfeitedParticipantNumber = participantNumber;
            match.ResultKind = MatchResultKind.Forfeit;
            match.ScoreConfirmationDeadlineUtc = null;
            match.CorrectionDeadlineUtc = null;
            match.LifecycleState = MatchLifecycleState.Forfeited;
            match.EndTime = DateTime.UtcNow;
            match.ResultVersion++;
            PropagateMockResult(tournament, match);
            return Clone(match)!;
        }
    }

    public Match ResolveMatch(string persona, Guid matchId, ResolveMatchDTO request)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            EnsureMockTournamentInProgress(tournament);
            ApplyMockDeadline(tournament, match);
            if(!string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a tournament administrator may resolve this match.");
            if(match.LifecycleState is not (MatchLifecycleState.Disputed or MatchLifecycleState.AdminResolutionRequired))
                throw new InvalidOperationException("This match does not require administrator resolution.");

            ValidateMockScore(match, request.Participant1Score, request.Participant2Score);
            CompleteMockScore(tournament, match, request.Participant1Score, request.Participant2Score, MatchResultKind.AdminResolution);
            return Clone(match)!;
        }
    }

    public Match ReverseMatch(string persona, Guid matchId)
    {
        lock(_syncRoot)
        {
            var (tournament, match) = GetMatchContext(matchId);
            EnsureMockTournamentInProgress(tournament);
            if(!string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a tournament administrator may reverse this result.");
            if(match.LifecycleState is not (MatchLifecycleState.Completed or MatchLifecycleState.Forfeited))
                throw new InvalidOperationException("Only a completed or forfeited match may be reversed.");

            var downstream = GetMockDownstreamMatches(tournament, match)
                ?? throw new InvalidOperationException("downstream_graph_too_large: the linked bracket is too large to reverse safely.");
            if(downstream.Any(HasMockPlayedResult))
                throw new InvalidOperationException("match_reversal_blocked: a downstream match already has a result.");
            if(HasMockUnprovenancedDownstreamAssignment(match, downstream))
                throw new InvalidOperationException("match_reversal_blocked: a linked participant assignment cannot be proven to originate from it.");

            foreach(var next in downstream)
                ClearMockParticipantsFromSource(next, match.Id);

            match.Participant1Score = null;
            match.Participant2Score = null;
            match.TeamWinnerId = null;
            match.TeamLoserId = null;
            match.UserWinnerId = null;
            match.UserLoserId = null;
            match.EndTime = default;
            match.ForfeitedParticipantNumber = null;
            match.ResultKind = null;
            match.LifecycleState = MatchLifecycleState.Reversed;
            match.Participant1Ended = false;
            match.Participant2Ended = false;
            match.Participant1ReportedScore1 = null;
            match.Participant1ReportedScore2 = null;
            match.Participant2ReportedScore1 = null;
            match.Participant2ReportedScore2 = null;
            match.ScoreConfirmationDeadlineUtc = null;
            match.CorrectionDeadlineUtc = null;
            match.Participant1CorrectionCount = 0;
            match.Participant2CorrectionCount = 0;
            match.ResultVersion++;
            return Clone(match)!;
        }
    }

    private (TournamentExtended Tournament, Match Match) GetMatchContext(Guid matchId)
    {
        var tournament = _document.Tournaments.FirstOrDefault(candidate => candidate.Matches.Any(match => match.Id == matchId))
            ?? throw new InvalidOperationException($"Mock match '{matchId}' was not found.");
        return (tournament, tournament.Matches.First(match => match.Id == matchId));
    }

    private static Match ClonePublicMatch(Match match)
    {
        var clone = Clone(match)!;
        clone.Participant1ReportedScore1 = null;
        clone.Participant1ReportedScore2 = null;
        clone.Participant2ReportedScore1 = null;
        clone.Participant2ReportedScore2 = null;
        return clone;
    }

    private TournamentExtended? ClonePublicTournament(TournamentExtended? tournament)
    {
        if(tournament is not null)
        {
            foreach(var match in tournament.Matches)
                ApplyMockDeadline(tournament, match);
        }

        var clone = Clone(tournament);
        if(clone == null)
            return null;

        clone.ContactAdmin = ResolveContactAdmin(clone.AssignedAdminUserId);
        clone.AssignedAdminUserId = null;
        clone.Matches = clone.Matches.Select(ClonePublicMatch).ToList();
        return clone;
    }

    // Contact admin is exposed as a public-safe projection only; raw assignment stays internal.
    private TournamentExtended WithContactAdminProjection(TournamentExtended tournament)
    {
        tournament.ContactAdmin = ResolveContactAdmin(tournament.AssignedAdminUserId);
        return tournament;
    }

    private PublicUserDTO? ResolveContactAdmin(Guid? userId)
    {
        if(!userId.HasValue)
            return null;

        var user = _document.Users.FirstOrDefault(candidate => candidate.Id == userId.Value && !candidate.IsDeleted);
        return user is null ? null : ToContactAdminProjection(user);
    }

    private static PublicUserDTO ToContactAdminProjection(UserDTO user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username ?? string.Empty : user.DisplayName
    };

    private static string? NormalizePrize(string? prize) =>
        string.IsNullOrWhiteSpace(prize) ? null : prize.Trim();

    internal static bool CanViewPrivateReports(
        string persona,
        Guid? assignedAdminUserId,
        Guid? currentUserId,
        bool isParticipant) =>
        isParticipant ||
        (string.Equals(persona, "admin", StringComparison.OrdinalIgnoreCase) &&
         (!assignedAdminUserId.HasValue || assignedAdminUserId == currentUserId));

    private static void EnsureMockTournamentInProgress(TournamentExtended tournament)
    {
        if(tournament.Status != TournamentStatus.InProgress)
            throw new InvalidOperationException("Match actions are available only while the tournament is in progress.");
    }

    private static void EnsureMockParticipantNumber(MatchParticipantSide side)
    {
        if(side is not (MatchParticipantSide.Participant1 or MatchParticipantSide.Participant2))
            throw new InvalidOperationException("The match participant side is invalid.");
    }

    private MatchParticipantSide? FindMockParticipantSide(
        string persona,
        TournamentExtended tournament,
        Match match)
    {
        if(string.Equals(persona, "anonymous", StringComparison.OrdinalIgnoreCase))
            return null;

        var profile = GetCurrentProfile(persona).User;
        if(profile == null)
            return null;

        if(match.ParticipationMode == ParticipationMode.Individual)
        {
            if(match.UserParticipant1Id == profile.Id)
                return MatchParticipantSide.Participant1;
            if(match.UserParticipant2Id == profile.Id)
                return MatchParticipantSide.Participant2;
            return null;
        }

        var teams = tournament.Teams.Concat(_document.Teams)
            .GroupBy(team => team.Id)
            .Select(group => group.First());
        if(match.TeamParticipant1Id.HasValue && teams.FirstOrDefault(team => team.Id == match.TeamParticipant1Id.Value)?.CaptainUserId == profile.Id)
            return MatchParticipantSide.Participant1;
        if(match.TeamParticipant2Id.HasValue && teams.FirstOrDefault(team => team.Id == match.TeamParticipant2Id.Value)?.CaptainUserId == profile.Id)
            return MatchParticipantSide.Participant2;
        return null;
    }

    private static bool HasMockEnded(Match match, MatchParticipantSide side) =>
        side == MatchParticipantSide.Participant1 ? match.Participant1Ended : match.Participant2Ended;

    private static bool HasMockSubmittedScore(Match match, MatchParticipantSide side) =>
        side == MatchParticipantSide.Participant1
            ? match.Participant1ReportedScore1.HasValue
            : match.Participant2ReportedScore1.HasValue;

    private static int GetMockCorrectionCount(Match match, MatchParticipantSide side) =>
        side == MatchParticipantSide.Participant1 ? match.Participant1CorrectionCount : match.Participant2CorrectionCount;

    private static void SetMockReportedScore(Match match, MatchParticipantSide side, int participant1Score, int participant2Score)
    {
        if(side == MatchParticipantSide.Participant1)
        {
            match.Participant1ReportedScore1 = participant1Score;
            match.Participant1ReportedScore2 = participant2Score;
        }
        else
        {
            match.Participant2ReportedScore1 = participant1Score;
            match.Participant2ReportedScore2 = participant2Score;
        }
    }

    private static void IncrementMockCorrectionCount(Match match, MatchParticipantSide side)
    {
        if(side == MatchParticipantSide.Participant1)
            match.Participant1CorrectionCount++;
        else
            match.Participant2CorrectionCount++;
    }

    private static bool ReportedMockScoresMatch(Match match) =>
        match.Participant1ReportedScore1 == match.Participant2ReportedScore1 &&
        match.Participant1ReportedScore2 == match.Participant2ReportedScore2;

    private static void ValidateMockScore(Match match, int participant1Score, int participant2Score)
    {
        var winsNeeded = match.Format switch
        {
            TournamentFormat.BestOf1 => 1,
            TournamentFormat.BestOf3 => 2,
            TournamentFormat.BestOf5 => 3,
            _ => throw new InvalidOperationException("The match format is invalid.")
        };

        if(participant1Score < 0 || participant2Score < 0 ||
           participant1Score > winsNeeded || participant2Score > winsNeeded ||
           participant1Score == participant2Score ||
           (participant1Score != winsNeeded && participant2Score != winsNeeded))
            throw new InvalidOperationException($"Enter a decisive {match.Format} score.");
    }

    private static void CompleteMockScore(
        TournamentExtended tournament,
        Match match,
        int participant1Score,
        int participant2Score,
        MatchResultKind resultKind = MatchResultKind.Score)
    {
        match.Participant1Score = participant1Score;
        match.Participant2Score = participant2Score;
        SetMockWinnerAndLoser(match, participant1Score > participant2Score ? 1 : 2);
        match.LifecycleState = MatchLifecycleState.Completed;
        match.ResultKind = resultKind;
        match.ScoreConfirmationDeadlineUtc = null;
        match.CorrectionDeadlineUtc = null;
        match.EndTime = DateTime.UtcNow;
        match.ResultVersion++;
        PropagateMockResult(tournament, match);
    }

    private static void SetMockWinnerAndLoser(Match match, int winningParticipantNumber)
    {
        var participant1Won = winningParticipantNumber == 1;
        if(match.ParticipationMode == ParticipationMode.Team)
        {
            match.TeamWinnerId = participant1Won ? match.TeamParticipant1Id : match.TeamParticipant2Id;
            match.TeamLoserId = participant1Won ? match.TeamParticipant2Id : match.TeamParticipant1Id;
            match.UserWinnerId = null;
            match.UserLoserId = null;
        }
        else
        {
            match.UserWinnerId = participant1Won ? match.UserParticipant1Id : match.UserParticipant2Id;
            match.UserLoserId = participant1Won ? match.UserParticipant2Id : match.UserParticipant1Id;
            match.TeamWinnerId = null;
            match.TeamLoserId = null;
        }
    }

    private static Guid? GetMockWinnerId(Match match) =>
        match.ParticipationMode == ParticipationMode.Team ? match.TeamWinnerId : match.UserWinnerId;

    private static Guid? GetMockLoserId(Match match) =>
        match.ParticipationMode == ParticipationMode.Team ? match.TeamLoserId : match.UserLoserId;

    private static Guid? GetMockParticipantId(Match match, MatchParticipantSide side) =>
        match.ParticipationMode == ParticipationMode.Team
            ? side == MatchParticipantSide.Participant1 ? match.TeamParticipant1Id : match.TeamParticipant2Id
            : side == MatchParticipantSide.Participant1 ? match.UserParticipant1Id : match.UserParticipant2Id;

    private static bool HasBothMockParticipants(Match match) =>
        GetMockParticipantId(match, MatchParticipantSide.Participant1).HasValue &&
        GetMockParticipantId(match, MatchParticipantSide.Participant2).HasValue;

    private static void ClearMockParticipantsFromSource(Match match, Guid sourceMatchId)
    {
        if(match.Participant1SourceMatchId == sourceMatchId)
        {
            ClearMockParticipantSlot(match, MatchParticipantSide.Participant1);
            match.Participant1SourceMatchId = null;
            match.Participant1IsBYE = false;
            match.ResultVersion++;
        }

        if(match.Participant2SourceMatchId == sourceMatchId)
        {
            ClearMockParticipantSlot(match, MatchParticipantSide.Participant2);
            match.Participant2SourceMatchId = null;
            match.Participant2IsBYE = false;
            match.ResultVersion++;
        }
    }

    private static void ClearMockParticipantSlot(Match match, MatchParticipantSide side)
    {
        if(match.ParticipationMode == ParticipationMode.Team)
        {
            if(side == MatchParticipantSide.Participant1)
                match.TeamParticipant1Id = null;
            else
                match.TeamParticipant2Id = null;
        }
        else if(side == MatchParticipantSide.Participant1)
        {
            match.UserParticipant1Id = null;
        }
        else
        {
            match.UserParticipant2Id = null;
        }
    }

    internal static void PropagateMockResult(TournamentExtended tournament, Match match)
    {
        if(GetMockWinnerId(match) is Guid winnerId && match.WinnerNextMatchId is Guid winnerNextId)
            AssignMockNextParticipant(tournament, winnerNextId, winnerId, match, isWinner: true);
        if(GetMockLoserId(match) is Guid loserId && match.LoserNextMatchId is Guid loserNextId)
            AssignMockNextParticipant(tournament, loserNextId, loserId, match, isWinner: false);
    }

    private static void AssignMockNextParticipant(
        TournamentExtended tournament,
        Guid nextMatchId,
        Guid participantId,
        Match source,
        bool isWinner)
    {
        var next = tournament.Matches.FirstOrDefault(candidate => candidate.Id == nextMatchId);
        if(next == null)
            return;

        var targetSide = isWinner
            ? GetMockWinnerTargetSide(source, next, participantId)
            : GetMockLoserTargetSide(source);
        SetMockParticipant(next, targetSide, participantId, source.Id);
    }

    private static MatchParticipantSide GetMockWinnerTargetSide(
        Match source,
        Match target,
        Guid participantId) =>
        target.IsLowerBracketMatch
            ? GetMockParticipantId(target, MatchParticipantSide.Participant2) is null ||
              GetMockParticipantId(target, MatchParticipantSide.Participant2) == participantId
                ? MatchParticipantSide.Participant2
                : MatchParticipantSide.Participant1
            : source.MatchNumber % 2 != 0 && !source.IsLowerBracketMatch
                ? MatchParticipantSide.Participant1
                : MatchParticipantSide.Participant2;

    private static MatchParticipantSide GetMockLoserTargetSide(Match source) =>
        source.RoundNumber == 1 && source.MatchNumber % 2 == 0
            ? MatchParticipantSide.Participant2
            : MatchParticipantSide.Participant1;

    private static void SetMockParticipant(
        Match match,
        MatchParticipantSide side,
        Guid participantId,
        Guid sourceMatchId)
    {
        var currentParticipant = GetMockParticipantId(match, side);
        var currentSource = side == MatchParticipantSide.Participant1
            ? match.Participant1SourceMatchId
            : match.Participant2SourceMatchId;
        if(currentParticipant == participantId && currentSource == sourceMatchId)
            return;

        if(match.ParticipationMode == ParticipationMode.Team)
        {
            if(side == MatchParticipantSide.Participant1)
            {
                match.TeamParticipant1Id = participantId;
                match.Participant1SourceMatchId = sourceMatchId;
            }
            else
            {
                match.TeamParticipant2Id = participantId;
                match.Participant2SourceMatchId = sourceMatchId;
            }
        }
        else if(side == MatchParticipantSide.Participant1)
        {
            match.UserParticipant1Id = participantId;
            match.Participant1SourceMatchId = sourceMatchId;
        }
        else
        {
            match.UserParticipant2Id = participantId;
            match.Participant2SourceMatchId = sourceMatchId;
        }

        match.ResultVersion++;
    }

    private static bool HasMockUnprovenancedDownstreamAssignment(
        Match source,
        IReadOnlyList<Match> downstream)
    {
        var sourceParticipants = new[]
        {
            GetMockWinnerId(source),
            GetMockLoserId(source)
        };

        if(sourceParticipants.Any(participantId => !participantId.HasValue))
        {
            return downstream.Any(match =>
                (GetMockParticipantId(match, MatchParticipantSide.Participant1).HasValue &&
                 match.Participant1SourceMatchId != source.Id) ||
                (GetMockParticipantId(match, MatchParticipantSide.Participant2).HasValue &&
                 match.Participant2SourceMatchId != source.Id));
        }

        return downstream.Any(match => sourceParticipants.Any(participantId =>
            participantId.HasValue &&
            ((GetMockParticipantId(match, MatchParticipantSide.Participant1) == participantId &&
              match.Participant1SourceMatchId != source.Id) ||
             (GetMockParticipantId(match, MatchParticipantSide.Participant2) == participantId &&
              match.Participant2SourceMatchId != source.Id))));
    }

    private static bool HasMockPlayedResult(Match match) =>
        match.LifecycleState is MatchLifecycleState.Completed or MatchLifecycleState.Forfeited ||
        match.Participant1Score.HasValue || match.Participant2Score.HasValue ||
        match.TeamWinnerId.HasValue || match.UserWinnerId.HasValue ||
        match.TeamLoserId.HasValue || match.UserLoserId.HasValue ||
        match.Participant1Ended || match.Participant2Ended ||
        match.Participant1ReportedScore1.HasValue || match.Participant2ReportedScore1.HasValue ||
        HasActualMockStartOrEnd(match);

    private static bool HasActualMockStartOrEnd(Match match) =>
        (match.StartTime != default && (!match.EstimatedStartTime.HasValue || match.StartTime != match.EstimatedStartTime.Value)) ||
        (match.EndTime != default && (!match.EstimatedEndTime.HasValue || match.EndTime != match.EstimatedEndTime.Value));

    private static List<Match>? GetMockDownstreamMatches(TournamentExtended tournament, Match source)
    {
        var queue = new Queue<Guid>();
        if(source.WinnerNextMatchId.HasValue)
            queue.Enqueue(source.WinnerNextMatchId.Value);
        if(source.LoserNextMatchId.HasValue)
            queue.Enqueue(source.LoserNextMatchId.Value);

        var visited = new HashSet<Guid> { source.Id };
        var result = new List<Match>();
        while(queue.TryDequeue(out var matchId))
        {
            if(!visited.Add(matchId))
                continue;
            if(visited.Count > MaxDownstreamMatches + 1)
                return null;

            var downstream = tournament.Matches.FirstOrDefault(candidate => candidate.Id == matchId);
            if(downstream is null)
                continue;

            result.Add(downstream);
            if(downstream.WinnerNextMatchId.HasValue)
                queue.Enqueue(downstream.WinnerNextMatchId.Value);
            if(downstream.LoserNextMatchId.HasValue)
                queue.Enqueue(downstream.LoserNextMatchId.Value);
        }

        return result;
    }

    private static void ApplyMockDeadline(TournamentExtended tournament, Match match)
    {
        if(tournament.Status != TournamentStatus.InProgress)
            return;

        var now = DateTime.UtcNow;
        if(match.LifecycleState == MatchLifecycleState.ScoreConfirmation &&
           match.ScoreConfirmationDeadlineUtc.HasValue && now >= match.ScoreConfirmationDeadlineUtc.Value)
        {
            if(match.Participant1ReportedScore1.HasValue && match.Participant2ReportedScore1.HasValue)
            {
                if(ReportedMockScoresMatch(match))
                {
                    CompleteMockScore(tournament, match, match.Participant1ReportedScore1.Value, match.Participant1ReportedScore2!.Value);
                    return;
                }

                match.ScoreConfirmationDeadlineUtc = null;
                match.LifecycleState = MatchLifecycleState.Disputed;
                match.CorrectionDeadlineUtc = now.AddMinutes(5);
            }
            else if(match.Participant1ReportedScore1.HasValue)
            {
                CompleteMockScore(tournament, match, match.Participant1ReportedScore1.Value, match.Participant1ReportedScore2!.Value);
                return;
            }
            else if(match.Participant2ReportedScore1.HasValue)
            {
                CompleteMockScore(tournament, match, match.Participant2ReportedScore1!.Value, match.Participant2ReportedScore2!.Value);
                return;
            }
            else
            {
                match.LifecycleState = MatchLifecycleState.AdminResolutionRequired;
                match.ScoreConfirmationDeadlineUtc = null;
            }
            match.ResultVersion++;
        }

        if(match.LifecycleState == MatchLifecycleState.Disputed &&
           match.CorrectionDeadlineUtc.HasValue && now >= match.CorrectionDeadlineUtc.Value)
        {
            match.LifecycleState = MatchLifecycleState.AdminResolutionRequired;
            match.CorrectionDeadlineUtc = null;
            match.ResultVersion++;
        }
    }

    public Match GetMatch(Guid matchId)
    {
        lock(_syncRoot)
        {
            var match = _document.Tournaments
                .SelectMany(tournament => tournament.Matches)
                .FirstOrDefault(candidate => candidate.Id == matchId);

            if(match == null)
                throw new InvalidOperationException($"Mock match '{matchId}' was not found.");

            var tournament = _document.Tournaments.First(candidate => candidate.Matches.Any(existing => existing.Id == matchId));
            ApplyMockDeadline(tournament, match);
            return ClonePublicMatch(match);
        }
    }

    public CurrentUserTournamentRegistrationStateDTO GetCurrentUserTournamentRegistrationState(
        string persona,
        Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var individual = registrations.FirstOrDefault(registration =>
                registration.Kind == TournamentRegistrationKind.Individual &&
                registration.User?.Id == currentUser.Id);
            var pendingRoster = registrations
                .SelectMany(registration => registration.RosterMembers)
                .FirstOrDefault(member =>
                    member.User.Id == currentUser.Id &&
                    member.ConfirmationStatus == RosterMemberConfirmationStatus.Pending);
            var activeTeam = registrations.FirstOrDefault(registration =>
                registration.Kind == TournamentRegistrationKind.Team &&
                registration.Status == TournamentRegistrationStatus.Active &&
                registration.RosterMembers.Any(member => member.User.Id == currentUser.Id));
            var currentTeam = registrations
                .Where(registration =>
                    registration.Kind == TournamentRegistrationKind.Team &&
                    registration.RosterMembers.Any(member => member.User.Id == currentUser.Id))
                .OrderByDescending(registration => registration.Status == TournamentRegistrationStatus.Active)
                .ThenByDescending(registration => registration.UpdatedAtUtc)
                .ThenBy(registration => registration.Id)
                .FirstOrDefault();
            var captainRegistrations = registrations
                .Where(registration =>
                    registration.Kind == TournamentRegistrationKind.Team &&
                    registration.Team?.CaptainUserId == currentUser.Id)
                .Select(Clone)
                .Where(registration => registration != null)
                .Cast<TournamentRegistrationDTO>()
                .ToList();

            var individualEligible = CheckIndividualEligibilityCore(tournament, registrations, currentUser.Id);

            return new CurrentUserTournamentRegistrationStateDTO
            {
                TournamentId = tournamentId,
                IndividualRegistration = Clone(individual),
                PendingRosterConfirmation = Clone(pendingRoster),
                CurrentTeamRegistration = Clone(currentTeam),
                ActiveTeamRegistration = Clone(activeTeam),
                CaptainManagedRegistrations = captainRegistrations,
                CanRegisterIndividual = individualEligible.Eligible && currentTeam == null && pendingRoster == null,
                CanConfirmRoster = pendingRoster != null,
                CanUnregister = individual != null || activeTeam != null || captainRegistrations.Count > 0
            };
        }
    }

    public IReadOnlyList<PendingRosterConfirmationDTO> GetPendingRosterConfirmations(string persona)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var confirmations = new List<PendingRosterConfirmationDTO>();

            foreach(var tournament in _document.Tournaments)
            {
                if(tournament.Status != TournamentStatus.Scheduled)
                    continue;

                var registration = GetRegistrationDetails(tournament).FirstOrDefault(candidate =>
                    candidate.Kind == TournamentRegistrationKind.Team &&
                    candidate.Team is not null &&
                    candidate.RosterMembers.Any(member =>
                        member.User.Id == currentUser.Id &&
                        member.ConfirmationStatus == RosterMemberConfirmationStatus.Pending));
                var member = registration?.RosterMembers.FirstOrDefault(candidate =>
                    candidate.User.Id == currentUser.Id &&
                    candidate.ConfirmationStatus == RosterMemberConfirmationStatus.Pending);
                if(registration?.Team is null || member is null)
                    continue;

                confirmations.Add(new PendingRosterConfirmationDTO
                {
                    RosterMemberId = member.Id,
                    TournamentId = tournament.Id,
                    TournamentName = tournament.Name,
                    TeamId = registration.Team.Id,
                    TeamName = registration.Team.Name,
                    TeamLogoUrl = registration.Team.LogoUrl,
                    SelectedAtUtc = registration.UpdatedAtUtc
                });
            }

            return confirmations.OrderByDescending(item => item.SelectedAtUtc).ToList();
        }
    }

    public EligibilityResponseDTO CheckIndividualTournamentRegistrationEligibility(
        string persona,
        Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            return CheckIndividualEligibilityCore(tournament, GetRegistrationDetails(tournament), currentUser.Id);
        }
    }

    public EligibilityResponseDTO CheckTeamTournamentRegistrationEligibility(
        string persona,
        Guid tournamentId,
        Guid teamId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            var reasons = GetTeamEligibilityFailures(tournament, GetRegistrationDetails(tournament), team, currentUser.Id, excludedRegistrationId: null);
            return new EligibilityResponseDTO
            {
                Eligible = reasons.Count == 0,
                ReasonCodes = reasons
            };
        }
    }

    public RosterCandidateEligibilityResponseDTO CheckTeamRosterEligibility(
        string persona,
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster)
    {
        return CheckTeamRosterEligibilityCore(persona, tournamentId, teamId, roster, excludeExistingRegistration: false);
    }

    private RosterCandidateEligibilityResponseDTO CheckTeamRosterEligibilityCore(
        string persona,
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        bool excludeExistingRegistration)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            var registrations = GetRegistrationDetails(tournament);
            var existing = registrations.FirstOrDefault(registration =>
                registration.Kind == TournamentRegistrationKind.Team &&
                registration.Team?.Id == teamId);
            var excludedRegistrationId = excludeExistingRegistration ? existing?.Id : null;
            var reasons = GetTeamEligibilityFailures(tournament, registrations, team, currentUser.Id, excludedRegistrationId);
            var userIds = roster?.UserIds ?? [];
            if(userIds.Count != userIds.Distinct().Count())
                reasons.Add("roster_user_ids_must_be_unique");
            if(tournament.TeamSize.HasValue && userIds.Distinct().Count() != tournament.TeamSize.Value)
                reasons.Add("exact_roster_size_required");
            if(!userIds.Contains(team.CaptainUserId))
                reasons.Add("captain_required");

            var teamMemberIds = team.Members.Select(member => member.Id).ToHashSet();
            var participatingUserIds = registrations
                .Where(registration => registration.Id != excludedRegistrationId)
                .SelectMany(registration =>
                    registration.Kind == TournamentRegistrationKind.Individual
                        ? registration.User is null ? [] : [registration.User.Id]
                        : registration.RosterMembers.Select(member => member.User.Id))
                .ToHashSet();
            var candidates = userIds
                .Distinct()
                .Select(userId =>
                {
                    var user = _document.Users.FirstOrDefault(candidate => candidate.Id == userId && !candidate.IsDeleted);
                    var candidateReasons = new List<string>();
                    if(user == null)
                        candidateReasons.Add("user_not_found");
                    if(!teamMemberIds.Contains(userId))
                        candidateReasons.Add("not_team_member");
                    if(participatingUserIds.Contains(userId))
                        candidateReasons.Add("duplicate_participation");
                    return new RosterCandidateEligibilityDTO
                    {
                        UserId = userId,
                        User = user == null ? null : ToPublicUser(user),
                        Eligible = candidateReasons.Count == 0,
                        ReasonCodes = candidateReasons
                    };
                })
                .ToList();

            reasons.AddRange(candidates.SelectMany(candidate => candidate.ReasonCodes));
            if(candidates.Any(candidate => !candidate.Eligible))
                reasons.Add("roster_candidate_ineligible");
            return new RosterCandidateEligibilityResponseDTO
            {
                Eligible = reasons.Count == 0,
                ReasonCodes = reasons.Distinct(StringComparer.Ordinal).ToList(),
                Candidates = candidates
            };
        }
    }

    public TournamentRegistrationDTO RegisterCurrentUserForTournament(string persona, Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var eligibility = CheckIndividualEligibilityCore(tournament, registrations, currentUser.Id);
            EnsureEligible(eligibility);

            var now = DateTime.UtcNow;
            var registration = new TournamentRegistrationDTO
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Kind = TournamentRegistrationKind.Individual,
                Status = TournamentRegistrationStatus.Active,
                User = ToPublicUser(currentUser),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            registrations.Add(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
            return Clone(registration)!;
        }
    }

    public void DeleteCurrentUserTournamentRegistration(string persona, Guid tournamentId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            EnsureScheduledTournament(tournament);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Individual &&
                candidate.User?.Id == currentUser.Id &&
                candidate.Status == TournamentRegistrationStatus.Active);

            if(registration == null)
                throw new InvalidOperationException("Individual registration not found.");

            registrations.Remove(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
        }
    }

    public TournamentRegistrationDTO SubmitTeamTournamentRoster(
        string persona,
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            var registrations = GetRegistrationDetails(tournament);
            var existing = registrations.FirstOrDefault(registration =>
                registration.Kind == TournamentRegistrationKind.Team &&
                registration.Team?.Id == teamId);
            var eligibility = CheckTeamRosterEligibilityCore(persona, tournamentId, teamId, roster, excludeExistingRegistration: true);
            EnsureEligible(new EligibilityResponseDTO
            {
                Eligible = eligibility.Eligible,
                ReasonCodes = eligibility.ReasonCodes
            });

            if(existing != null)
                registrations.Remove(existing);

            var now = DateTime.UtcNow;
            var userIds = roster?.UserIds ?? [];
            var rosterMembers = userIds
                .Distinct()
                .Select(userId =>
                {
                    var user = GetRequiredUser(userId);
                    return new TournamentRosterMemberDTO
                    {
                        Id = Guid.NewGuid(),
                        User = ToPublicUser(user),
                        IsCaptain = userId == team.CaptainUserId,
                        ConfirmationStatus = userId == team.CaptainUserId
                            ? RosterMemberConfirmationStatus.AutoConfirmed
                            : RosterMemberConfirmationStatus.Pending
                    };
                })
                .ToList();
            var registration = new TournamentRegistrationDTO
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Kind = TournamentRegistrationKind.Team,
                Status = rosterMembers.Any(member => member.ConfirmationStatus == RosterMemberConfirmationStatus.Pending)
                    ? TournamentRegistrationStatus.PendingConfirmation
                    : TournamentRegistrationStatus.Active,
                Team = new TeamParticipantDTO
                {
                    Id = team.Id,
                    Name = team.Name,
                    CaptainUserId = team.CaptainUserId,
                    LogoUrl = team.LogoUrl,
                    Members = Clone(team.Members.ToList())!
                },
                RosterMembers = rosterMembers,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            registrations.Add(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
            return Clone(registration)!;
        }
    }

    public void DeleteTeamTournamentRegistration(string persona, Guid tournamentId, Guid teamId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            EnsureScheduledTournament(tournament);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Team &&
                candidate.Team?.Id == teamId &&
                candidate.Team.CaptainUserId == currentUser.Id);

            if(registration == null)
                throw new InvalidOperationException("Team registration not found.");

            registrations.Remove(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
        }
    }

    public TournamentRegistrationDTO ConfirmTournamentRosterMember(
        string persona,
        Guid tournamentId,
        Guid rosterMemberId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            EnsureScheduledTournament(tournament);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Team &&
                candidate.RosterMembers.Any(member => member.Id == rosterMemberId && member.User.Id == currentUser.Id));
            var member = registration?.RosterMembers.FirstOrDefault(candidate => candidate.Id == rosterMemberId);

            if(registration == null || member == null || member.ConfirmationStatus != RosterMemberConfirmationStatus.Pending)
                throw new InvalidOperationException("Pending roster confirmation not found.");

            if(registration.Team is null ||
               !registration.Team.Members.Any(candidate => candidate.Id == currentUser.Id))
                throw new InvalidOperationException("not_team_member");

            if(registrations
                .Where(candidate => candidate.Id != registration.Id)
                .Any(candidate =>
                    candidate.User?.Id == currentUser.Id ||
                    candidate.RosterMembers.Any(rosterMember => rosterMember.User.Id == currentUser.Id)))
            {
                throw new InvalidOperationException("duplicate_participation");
            }

            var updatedRosterMembers = registration.RosterMembers
                .Select(candidate => candidate.Id == rosterMemberId
                    ? new TournamentRosterMemberDTO
                    {
                        Id = candidate.Id,
                        User = Clone(candidate.User)!,
                        IsCaptain = candidate.IsCaptain,
                        ConfirmationStatus = RosterMemberConfirmationStatus.Confirmed
                    }
                    : candidate)
                .ToList();
            var updatedRegistration = new TournamentRegistrationDTO
            {
                Id = registration.Id,
                TournamentId = registration.TournamentId,
                Kind = registration.Kind,
                Status = updatedRosterMembers.All(candidate =>
                    candidate.ConfirmationStatus != RosterMemberConfirmationStatus.Pending)
                    ? TournamentRegistrationStatus.Active
                    : TournamentRegistrationStatus.PendingConfirmation,
                User = Clone(registration.User),
                Team = Clone(registration.Team),
                RosterMembers = updatedRosterMembers,
                CreatedAtUtc = registration.CreatedAtUtc,
                UpdatedAtUtc = DateTime.UtcNow
            };
            var registrationIndex = registrations.IndexOf(registration);
            registrations[registrationIndex] = updatedRegistration;
            registration = updatedRegistration;
            UpdatePublicRegistrationProjection(tournament, registrations);
            return Clone(registration)!;
        }
    }

    public void DeclineTournamentRosterMember(string persona, Guid tournamentId, Guid rosterMemberId)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            EnsureScheduledTournament(tournament);
            var currentUser = GetCurrentProfile(persona).User
                ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Team &&
                candidate.RosterMembers.Any(member =>
                    member.Id == rosterMemberId &&
                    member.User.Id == currentUser.Id &&
                    member.ConfirmationStatus == RosterMemberConfirmationStatus.Pending));
            if(registration is null)
                return;

            var updatedRegistration = new TournamentRegistrationDTO
            {
                Id = registration.Id,
                TournamentId = registration.TournamentId,
                Kind = registration.Kind,
                Status = TournamentRegistrationStatus.PendingConfirmation,
                User = Clone(registration.User),
                Team = Clone(registration.Team),
                RosterMembers = registration.RosterMembers
                    .Where(member => member.Id != rosterMemberId)
                    .Select(Clone)
                    .Where(member => member is not null)
                    .Cast<TournamentRosterMemberDTO>()
                    .ToList(),
                CreatedAtUtc = registration.CreatedAtUtc,
                UpdatedAtUtc = DateTime.UtcNow
            };
            registrations[registrations.IndexOf(registration)] = updatedRegistration;
            UpdatePublicRegistrationProjection(tournament, registrations);
        }
    }

    public List<AdminTournamentRegistrationDTO> GetAdminTournamentRegistrations(
        Guid tournamentId,
        int? page,
        int? pageSize)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var registrations = GetRegistrationDetails(tournament)
                .OrderBy(registration => registration.Kind)
                .ThenBy(registration => registration.Status)
                .ThenBy(registration => registration.CreatedAtUtc)
                .ThenBy(registration => registration.Id)
                .Select(ToAdminRegistration)
                .AsEnumerable();

            if(pageSize is > 0)
            {
                var pageNumber = Math.Max(page ?? 1, 1);
                registrations = registrations.Skip((pageNumber - 1) * pageSize.Value).Take(pageSize.Value);
            }

            return registrations.ToList();
        }
    }

    public void RemoveTournamentUserRegistrationAsAdmin(Guid tournamentId, Guid userId, string? reason)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Individual && candidate.User?.Id == userId);
            if(registration == null)
                throw new InvalidOperationException("Individual registration not found.");

            registrations.Remove(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
        }
    }

    public void RemoveTournamentTeamRegistrationAsAdmin(Guid tournamentId, Guid teamId, string? reason)
    {
        lock(_syncRoot)
        {
            var tournament = GetRequiredTournament(tournamentId);
            var registrations = GetRegistrationDetails(tournament);
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.Kind == TournamentRegistrationKind.Team && candidate.Team?.Id == teamId);
            if(registration == null)
                throw new InvalidOperationException("Team registration not found.");

            registrations.Remove(registration);
            UpdatePublicRegistrationProjection(tournament, registrations);
        }
    }

    private void InitializeRegistrationDetails()
    {
        foreach(var tournament in _document.Tournaments)
            _ = GetRegistrationDetails(tournament);
    }

    private List<TournamentRegistrationDTO> GetRegistrationDetails(TournamentExtended tournament)
    {
        if(_registrationDetails.TryGetValue(tournament.Id, out var registrations))
        {
            PreservePublicRegistrationSnapshots(tournament.Id, registrations);
            return registrations;
        }

        var projectedRegistrations = tournament.Registrations
            .Select(ToRegistrationDetails)
            .ToList();
        PreservePublicRegistrationSnapshots(tournament.Id, projectedRegistrations);
        registrations = projectedRegistrations
            .Where(registration => registration.Status == TournamentRegistrationStatus.Active)
            .ToList();
        _registrationDetails[tournament.Id] = registrations;
        return registrations;
    }

    private void PreservePublicRegistrationSnapshots(
        Guid tournamentId,
        IEnumerable<TournamentRegistrationDTO> registrations)
    {
        if(!_publicRegistrationSnapshots.TryGetValue(tournamentId, out var snapshots))
        {
            snapshots = [];
            _publicRegistrationSnapshots[tournamentId] = snapshots;
        }

        foreach(var registration in registrations)
        {
            var snapshot = ToRegistrationSnapshot(registration);
            if(snapshot.ParticipantId is { } participantId && !string.IsNullOrWhiteSpace(snapshot.DisplayName))
                snapshots[participantId] = snapshot;
        }
    }

    private static TournamentRegistrationDTO ToRegistrationDetails(PublicTournamentRegistrationDTO registration)
    {
        var now = DateTime.UtcNow;
        var rosterMembers = registration.RosterMembers
            .Select(member => new TournamentRosterMemberDTO
            {
                Id = Guid.NewGuid(),
                User = Clone(member.User)!,
                IsCaptain = member.IsCaptain,
                ConfirmationStatus = RosterMemberConfirmationStatus.AutoConfirmed
            })
            .ToList();

        return new TournamentRegistrationDTO
        {
            Id = registration.Id,
            TournamentId = registration.TournamentId,
            Kind = registration.Kind,
            Status = registration.Status,
            User = Clone(registration.User),
            Team = registration.Team == null
                ? null
                : new TeamParticipantDTO
                {
                    Id = registration.Team.Id,
                    Name = registration.Team.Name,
                    CaptainUserId = registration.Team.CaptainUserId,
                    LogoUrl = registration.Team.LogoUrl,
                    Members = []
                },
            RosterMembers = rosterMembers,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static PublicTournamentRegistrationDTO ToPublicRegistration(TournamentRegistrationDTO registration)
    {
        return new PublicTournamentRegistrationDTO
        {
            Id = registration.Id,
            TournamentId = registration.TournamentId,
            Kind = registration.Kind,
            Status = registration.Status,
            User = Clone(registration.User),
            Team = registration.Team == null
                ? null
                : new PublicTournamentTeamDTO
                {
                    Id = registration.Team.Id,
                    Name = registration.Team.Name,
                    CaptainUserId = registration.Team.CaptainUserId,
                    LogoUrl = registration.Team.LogoUrl,
                    Members = []
                },
            RosterMembers = registration.RosterMembers
                .OrderByDescending(member => member.IsCaptain)
                .ThenBy(member => member.User.Username, StringComparer.OrdinalIgnoreCase)
                .Select(member => new PublicTournamentRosterMemberDTO
                {
                    User = Clone(member.User)!,
                    IsCaptain = member.IsCaptain
                })
                .ToList()
        };
    }

    private static AdminTournamentRegistrationDTO ToAdminRegistration(TournamentRegistrationDTO registration)
    {
        return new AdminTournamentRegistrationDTO
        {
            Id = registration.Id,
            TournamentId = registration.TournamentId,
            Kind = registration.Kind,
            Status = registration.Status,
            User = Clone(registration.User),
            Team = registration.Team == null
                ? null
                : new TeamParticipantDTO
                {
                    Id = registration.Team.Id,
                    Name = registration.Team.Name,
                    CaptainUserId = registration.Team.CaptainUserId,
                    LogoUrl = registration.Team.LogoUrl,
                    Members = Clone(registration.Team.Members)!
                },
            RosterMembers = registration.RosterMembers.Select(Clone).Where(member => member != null).Cast<TournamentRosterMemberDTO>().ToList(),
            CreatedAtUtc = registration.CreatedAtUtc,
            UpdatedAtUtc = registration.UpdatedAtUtc
        };
    }

    private static EligibilityResponseDTO CheckIndividualEligibilityCore(
        TournamentExtended tournament,
        IReadOnlyCollection<TournamentRegistrationDTO> registrations,
        Guid userId)
    {
        var reasons = new List<string>();
        if(tournament.ParticipationMode != ParticipationMode.Individual)
            reasons.Add("not_individual_tournament");
        if(tournament.Status != TournamentStatus.Scheduled)
            reasons.Add("tournament_not_scheduled");
        if(registrations.Any(registration =>
               registration.User?.Id == userId ||
               registration.RosterMembers.Any(member => member.User.Id == userId)))
        {
            reasons.Add("duplicate_participation");
        }

        return new EligibilityResponseDTO
        {
            Eligible = reasons.Count == 0,
            ReasonCodes = reasons
        };
    }

    private static List<string> GetTeamEligibilityFailures(
        TournamentExtended tournament,
        IReadOnlyCollection<TournamentRegistrationDTO> registrations,
        Team team,
        Guid captainUserId,
        Guid? excludedRegistrationId)
    {
        var reasons = new List<string>();
        if(tournament.ParticipationMode != ParticipationMode.Team)
            reasons.Add("not_team_tournament");
        if(tournament.Status != TournamentStatus.Scheduled)
            reasons.Add("tournament_not_scheduled");
        if(!tournament.TeamSize.HasValue || tournament.TeamSize.Value <= 0)
            reasons.Add("team_size_required");
        if(team.CaptainUserId != captainUserId)
            reasons.Add("captain_required");
        if(registrations.Any(registration =>
               registration.Id != excludedRegistrationId &&
               registration.Kind == TournamentRegistrationKind.Team &&
               registration.Team?.Id == team.Id))
        {
            reasons.Add("team_already_registered");
        }
        if(registrations.Any(registration =>
               registration.Id != excludedRegistrationId &&
               (registration.User?.Id == captainUserId ||
                registration.RosterMembers.Any(member => member.User.Id == captainUserId))))
        {
            reasons.Add("captain_duplicate_participation");
        }

        return reasons;
    }

    private static void EnsureEligible(EligibilityResponseDTO eligibility)
    {
        if(!eligibility.Eligible)
        {
            var reason = eligibility.ReasonCodes.Count == 0
                ? "Registration is not eligible."
                : string.Join(", ", eligibility.ReasonCodes);
            throw new InvalidOperationException(reason);
        }
    }

    private static void UpdatePublicRegistrationProjection(
        TournamentExtended tournament,
        IEnumerable<TournamentRegistrationDTO> registrations)
    {
        tournament.Registrations = registrations
            .Where(registration => registration.Status == TournamentRegistrationStatus.Active)
            .Select(ToPublicRegistration)
            .ToList();
        TournamentProjectionMapper.PopulateParticipantProjection(tournament);
    }

    public List<Team> GetTeams(int page, int pageSize)
    {
        lock(_syncRoot)
        {
            if(page < 1)
                throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");

            if(pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

            var offset = ((long)page - 1) * pageSize;
            if(offset > int.MaxValue)
                return [];

            var teams = _document.Teams
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(team => team.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToList();

            return Clone(teams)!;
        }
    }

    public Team CreateTeam(CreateTeamDTO dto)
    {
        lock(_syncRoot)
        {
            var captain = GetRequiredUser(dto.CaptainUserId);
            var team = new Team
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                CaptainUserId = dto.CaptainUserId,
                Members = [PublicUserDTO.FromUser(captain)],
                TeamInvites = []
            };

            AddOrReplaceTeam(team);
            return Clone(team)!;
        }
    }

    public CurrentUserTeamSummaryDTO GetCurrentUserTeamSummary(string persona)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var teams = EnsureCurrentUserTeamFixtures(currentUser);
            var captainedTeams = teams
                .Where(team => team.CaptainUserId == currentUser.Id)
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToManagementSummary)
                .ToList();
            var memberTeams = teams
                .Where(team => team.CaptainUserId != currentUser.Id && team.Members.Any(member => member.Id == currentUser.Id))
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToManagementSummary)
                .ToList();
            var invites = teams
                .SelectMany(team => team.TeamInvites.Select(invite => (team, invite)))
                .Where(entry => entry.invite.UserId == currentUser.Id && IsPending(entry.invite))
                .OrderBy(entry => entry.invite.CreatedAt)
                .Select(entry => ToInviteSummary(entry.team, entry.invite))
                .ToList();
            var sentInvites = teams
                .Where(team => team.CaptainUserId == currentUser.Id)
                .SelectMany(team => team.TeamInvites.Select(invite => (team, invite)))
                .Where(entry => IsPending(entry.invite))
                .OrderBy(entry => entry.invite.CreatedAt)
                .Select(entry => ToInviteSummary(entry.team, entry.invite))
                .ToList();

            return new CurrentUserTeamSummaryDTO
            {
                CaptainedTeams = captainedTeams,
                MemberTeams = memberTeams,
                ReceivedPendingInvites = invites,
                SentPendingInvites = sentInvites
            };
        }
    }

    public TeamManagementSummaryDTO CreateCurrentUserTeam(string persona, CreateTeamDTO dto)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            EnsureCurrentUserTeamFixtures(currentUser);

            if(_document.Teams.Count(team => team.CaptainUserId == currentUser.Id) >= 3)
                throw new InvalidOperationException("You already captain the maximum number of mock teams.");

            var team = new Team
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                CaptainUserId = currentUser.Id,
                Members = [ToPublicUser(currentUser)],
                TeamInvites = []
            };

            AddOrReplaceTeam(team);
            return ToManagementSummary(team);
        }
    }

    public TeamInvite CreateTeamInvite(string persona, Guid teamId, Guid userId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can send invites.");

            if(team.Members.Any(member => member.Id == userId))
                throw new InvalidOperationException("That player is already a team member.");

            if(team.TeamInvites.Any(invite => invite.UserId == userId && IsPending(invite)))
                throw new InvalidOperationException("That player already has a pending invite.");

            var createdAt = DateTime.UtcNow;
            var invite = new TeamInvite
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                UserId = userId,
                Status = "Pending",
                CreatedAt = createdAt,
                ExpiresAt = createdAt.AddDays(7)
            };
            team.TeamInvites = team.TeamInvites.Append(invite).ToList();
            AddOrReplaceTeam(team);
            return Clone(invite)!;
        }
    }

    public TeamInvite CancelTeamInvite(string persona, Guid teamId, Guid inviteId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can cancel invites.");

            var invite = team.TeamInvites.First(candidate => candidate.Id == inviteId);
            EnsureInviteDates(invite);
            invite.Status = "Cancelled";
            invite.CancelledAt = DateTime.UtcNow;
            AddOrReplaceTeam(team);
            return Clone(invite)!;
        }
    }

    public TeamInvite RespondToTeamInvite(string persona, Guid inviteId, bool accept)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = _document.Teams.First(team => team.TeamInvites.Any(invite => invite.Id == inviteId));
            var invite = team.TeamInvites.First(candidate => candidate.Id == inviteId);
            EnsureInviteDates(invite);
            if(invite.UserId != currentUser.Id)
                throw new InvalidOperationException("This invite belongs to another user.");

            invite.Status = accept ? "Accepted" : "Declined";
            invite.RespondedAt = DateTime.UtcNow;
            if(accept && team.Members.All(member => member.Id != currentUser.Id))
                team.Members = team.Members.Append(PublicUserDTO.FromUser(currentUser)).ToList();

            AddOrReplaceTeam(team);
            return Clone(invite)!;
        }
    }

    public TeamManagementSummaryDTO LeaveTeam(string persona, Guid teamId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId == currentUser.Id)
                throw new InvalidOperationException("Transfer captainship before leaving a team you captain.");

            if(team.Name.Contains("Roster", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mock roster lock: this team cannot be left during an active tournament.");

            team.Members = team.Members.Where(member => member.Id != currentUser.Id).ToList();
            AddOrReplaceTeam(team);
            return ToManagementSummary(team);
        }
    }

    public TeamManagementSummaryDTO RemoveTeamMember(string persona, Guid teamId, Guid userId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can remove members.");

            if(team.CaptainUserId == userId)
                throw new InvalidOperationException("Transfer captainship before removing the captain.");

            if(team.Name.Contains("Roster", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mock roster lock: this team cannot remove members during an active tournament.");

            if(team.Members.All(member => member.Id != userId))
                throw new InvalidOperationException("That player is not a member of this team.");

            team.Members = team.Members.Where(member => member.Id != userId).ToList();
            AddOrReplaceTeam(team);
            return ToManagementSummary(team);
        }
    }

    public TeamManagementSummaryDTO TransferCaptain(string persona, Guid teamId, Guid newCaptainUserId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the current captain can transfer captainship.");

            if(team.Members.All(member => member.Id != newCaptainUserId))
                throw new InvalidOperationException("Captainship can only be transferred to a current member.");

            team.CaptainUserId = newCaptainUserId;
            AddOrReplaceTeam(team);
            return ToManagementSummary(team);
        }
    }

    public TeamLogoResponseDTO UploadTeamLogo(string persona, Guid teamId, string contentType, string fileName)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can update the team logo.");

            if(!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mock validation: choose an image file for the team logo.");

            var extension = Path.GetExtension(fileName);
            team.LogoUrl = $"/mock-data-local/sponsors/mock-sponsor.svg?team={team.Id:N}{extension}";
            AddOrReplaceTeam(team);
            return new TeamLogoResponseDTO { TeamId = team.Id, LogoUrl = team.LogoUrl };
        }
    }

    public TeamLogoResponseDTO RemoveTeamLogo(string persona, Guid teamId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can remove the team logo.");

            team.LogoUrl = null;
            AddOrReplaceTeam(team);
            return new TeamLogoResponseDTO { TeamId = team.Id, LogoUrl = null };
        }
    }

    public void DeleteCurrentUserTeam(string persona, Guid teamId)
    {
        lock(_syncRoot)
        {
            var currentUser = GetCurrentProfile(persona).User ?? throw new InvalidOperationException("Mock profile does not have a user.");
            var team = GetRequiredTeam(teamId);
            if(team.CaptainUserId != currentUser.Id)
                throw new InvalidOperationException("Only the team captain can delete the team.");

            DeleteTeam(teamId);
        }
    }

    public Team UpdateTeam(Guid id, UpdateTeamDTO dto)
    {
        lock(_syncRoot)
        {
            var team = GetRequiredTeam(id);
            team.Name = string.IsNullOrWhiteSpace(dto.Name) ? team.Name : dto.Name;
            team.CaptainUserId = dto.CaptainUserId ?? team.CaptainUserId;

            if(dto.CaptainUserId.HasValue && team.Members.All(member => member.Id != dto.CaptainUserId.Value))
                team.Members = team.Members.Append(PublicUserDTO.FromUser(GetRequiredUser(dto.CaptainUserId.Value))).ToList();

            AddOrReplaceTeam(team);
            return Clone(team)!;
        }
    }

    public void DeleteTeam(Guid id)
    {
        lock(_syncRoot)
        {
            _deletedTeamIds.Add(id);
            _document.Teams.RemoveAll(team => team.Id == id);

            foreach(var tournament in _document.Tournaments)
            {
                tournament.Teams = tournament.Teams.Where(team => team.Id != id).ToList();
            }
        }
    }

    public List<Sponsor> GetSponsors()
    {
        lock(_syncRoot)
        {
            return Clone(_document.Sponsors.OrderBy(sponsor => sponsor.SponsorTier).ToList())!;
        }
    }

    public Sponsor GetSponsorById(int id)
    {
        lock(_syncRoot)
        {
            return Clone(_document.Sponsors.Single(sponsor => sponsor.Id == id))!;
        }
    }

    public Sponsor CreateSponsor(SponsorManagementDTO dto)
    {
        lock(_syncRoot)
        {
            var nextId = _document.Sponsors.Count == 0 ? 1 : _document.Sponsors.Max(sponsor => sponsor.Id) + 1;
            var sponsor = new Sponsor
            {
                Id = nextId,
                Name = dto.Name,
                SponsorTier = dto.SponsorTier,
                InfoUrl = dto.InfoUrl,
                Description = dto.Description,
                LogoUrl = "/mock-data-local/sponsors/mock-sponsor.svg"
            };

            _document.Sponsors.Add(sponsor);
            return Clone(sponsor)!;
        }
    }

    public Sponsor UpdateSponsor(int id, SponsorManagementDTO dto)
    {
        lock(_syncRoot)
        {
            var sponsor = _document.Sponsors.Single(existing => existing.Id == id);
            sponsor.Name = dto.Name;
            sponsor.SponsorTier = dto.SponsorTier;
            sponsor.InfoUrl = dto.InfoUrl;
            sponsor.Description = dto.Description;

            if(dto.Logo != null)
                sponsor.LogoUrl = "/mock-data-local/sponsors/mock-sponsor.svg";

            return Clone(sponsor)!;
        }
    }

    public void DeleteSponsor(int id)
    {
        lock(_syncRoot)
        {
            _document.Sponsors.RemoveAll(sponsor => sponsor.Id == id);

            foreach(var tournament in _document.Tournaments)
            {
                if(tournament.SponsorPlacement?.SponsorId == id)
                    tournament.SponsorPlacement = null;
            }
        }
    }

    public CurrentUserProfileResponse GetCurrentProfile(string persona)
    {
        lock(_syncRoot)
        {
            var normalizedPersona = NormalizePersona(persona);
            var profile = _document.Profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Persona, normalizedPersona, StringComparison.OrdinalIgnoreCase));

            if(profile == null)
                throw new InvalidOperationException($"Mock profile for persona '{normalizedPersona}' was not found.");

            return Clone(profile.Profile)!;
        }
    }

    public UserProfileDTO CompleteCurrentProfile(string persona, CompleteUserProfileRequest request)
    {
        lock(_syncRoot)
        {
            return UpdateCurrentProfileCore(persona, request.Username, request.Firstname, request.Lastname, request.DiscordId, request.SteamId, request.RiotId, isComplete: true);
        }
    }

    public UserProfileDTO UpdateCurrentProfile(string persona, UpdateUserProfileRequest request)
    {
        lock(_syncRoot)
        {
            return UpdateCurrentProfileCore(persona, request.Username, request.Firstname, request.Lastname, request.DiscordId, request.SteamId, request.RiotId, isComplete: true);
        }
    }

    public UsernameAvailabilityResponse CheckUsernameAvailability(string persona, string username)
    {
        lock(_syncRoot)
        {
            var normalized = username.Trim();
            var currentProfile = GetCurrentProfile(persona);
            var currentUserId = currentProfile.User?.Id;

            var existingUser = _document.Users.FirstOrDefault(user =>
                string.Equals(user.Username, normalized, StringComparison.OrdinalIgnoreCase) &&
                user.Id != currentUserId &&
                !user.IsDeleted);

            return new UsernameAvailabilityResponse
            {
                Username = username,
                NormalizedUsername = normalized,
                IsAvailable = existingUser == null,
                Reason = existingUser == null ? null : "Username is already in use."
            };
        }
    }

    public UserActionResponse DeleteCurrentUser(string persona)
    {
        lock(_syncRoot)
        {
            var normalizedPersona = NormalizePersona(persona);
            var profileRecord = _document.Profiles.First(candidate =>
                string.Equals(candidate.Persona, normalizedPersona, StringComparison.OrdinalIgnoreCase));

            if(profileRecord.Profile.User != null)
            {
                profileRecord.Profile.User.IsDeleted = true;
                var user = _document.Users.FirstOrDefault(existing => existing.Id == profileRecord.Profile.User.Id);
                if(user != null)
                    user.IsDeleted = true;
            }

            return new UserActionResponse("Mock account deleted.");
        }
    }

    public List<UserDTO> GetUsers()
    {
        lock(_syncRoot)
        {
            return Clone(_document.Users.Where(user => !user.IsDeleted).ToList())!;
        }
    }

    public UserDTO GetUserById(Guid id)
    {
        lock(_syncRoot)
        {
            return Clone(GetRequiredUser(id))!;
        }
    }

    public UserDTO GetUserByUsername(string username)
    {
        lock(_syncRoot)
        {
            var user = _document.Users.FirstOrDefault(existing =>
                !existing.IsDeleted && string.Equals(existing.Username, username, StringComparison.OrdinalIgnoreCase));
            return Clone(user ?? throw new KeyNotFoundException("Mock user was not found."))!;
        }
    }

    public void DeleteUser(string username)
    {
        lock(_syncRoot)
        {
            var user = _document.Users.First(existing =>
                string.Equals(existing.Username, username, StringComparison.OrdinalIgnoreCase));
            user.IsDeleted = true;
        }
    }

    private static MockBackendDocument LoadDocument(string dataFilePath)
    {
        if(!File.Exists(dataFilePath))
        {
            throw new InvalidOperationException(
                $"Mock backend is enabled but the local fixture file was not found at '{dataFilePath}'.");
        }

        var json = File.ReadAllText(dataFilePath);
        var document = JsonSerializer.Deserialize<MockBackendDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize mock backend fixture '{dataFilePath}'.");

        foreach(var user in document.Users)
        {
            user.DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? BuildDisplayName(user.Firstname, user.Lastname, user.Username)
                : user.DisplayName;
        }

        foreach(var team in document.Teams)
        {
            foreach(var invite in team.TeamInvites)
                EnsureInviteDates(invite);

            var sourceTeam = document.Tournaments
                .SelectMany(tournament => tournament.Teams)
                .FirstOrDefault(candidate => candidate.Id == team.Id);

            if(sourceTeam != null)
            {
                team.Members = team.Members.Any() ? team.Members : Clone(sourceTeam.Members)!;
                team.TeamInvites = team.TeamInvites.Any() ? team.TeamInvites : Clone(sourceTeam.TeamInvites)!;
            }

            foreach(var invite in team.TeamInvites)
                EnsureInviteDates(invite);
        }

        foreach(var profile in document.Profiles.Where(profile => profile.Profile.User != null))
        {
            profile.Profile.User!.DisplayName = string.IsNullOrWhiteSpace(profile.Profile.User.DisplayName)
                ? BuildDisplayName(profile.Profile.User.Firstname, profile.Profile.User.Lastname, profile.Profile.User.Username)
                : profile.Profile.User.DisplayName;
        }

        foreach(var tournament in document.Tournaments)
        {
            EnsureScheduleFields(tournament);
            NormalizeMatchLifecycleDefaults(tournament);
            PopulateRegistrationProjection(tournament);
        }

        return document;
    }

    private static void EnsureScheduleFields(TournamentExtended tournament)
    {
        if(tournament.PlannedStartTime == default)
            tournament.PlannedStartTime = tournament.StartTime == default ? DateTime.UtcNow.AddDays(7) : tournament.StartTime;

        if(tournament.AverageGameDurationMinutes <= 0)
            tournament.AverageGameDurationMinutes = 30;

        if(tournament.RoundBreakDurationMinutes <= 0)
            tournament.RoundBreakDurationMinutes = 10;

        foreach(var match in tournament.Matches)
        {
            match.EstimatedStartTime ??= match.StartTime == default ? null : match.StartTime;
            match.EstimatedEndTime ??= match.EndTime == default ? null : match.EndTime;
        }

        tournament.EstimatedEndTime ??= tournament.Matches
            .Select(match => match.EstimatedEndTime)
            .Where(estimatedEnd => estimatedEnd.HasValue)
            .Max();
    }

    private static void NormalizeMatchLifecycleDefaults(TournamentExtended tournament)
    {
        foreach(var match in tournament.Matches)
        {
            if(match.LifecycleState == MatchLifecycleState.AwaitingEndedConfirmation &&
               (match.UserWinnerId.HasValue || match.TeamWinnerId.HasValue))
            {
                match.LifecycleState = MatchLifecycleState.Completed;
                match.Participant1Ended = true;
                match.Participant2Ended = true;
                match.ResultKind ??= MatchResultKind.Score;
                match.ResultVersion = Math.Max(match.ResultVersion, 1);
            }
        }
    }

    private void SeedFeaturedDoubleEliminationFixture()
    {
        var tournament = _document.Tournaments.FirstOrDefault(candidate => candidate.Id == FeaturedDoubleEliminationTournamentId);
        if(tournament == null)
            return;

        var teams = BuildFeaturedDoubleEliminationTeams();

        tournament.Name = "Valorant";
        tournament.StartTime = new DateTime(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc);
        tournament.EndTime = default;
        tournament.PlannedStartTime = tournament.StartTime;
        tournament.AverageGameDurationMinutes = 30;
        tournament.RoundBreakDurationMinutes = 15;
        tournament.Status = TournamentStatus.InProgress;
        tournament.BracketType = BracketType.DoubleElimination;
        tournament.Format = TournamentFormat.BestOf3;
        tournament.FinalsFormat = TournamentFormat.BestOf5;
        tournament.ParticipationMode = ParticipationMode.Team;
        tournament.TeamSize = 5;
        tournament.AssignedAdminUserId = Guid.Parse("41111111-1111-1111-1111-111111111121");
        tournament.FirstPlacePrize = "Gaming keyboard + 250 EUR";
        tournament.SecondPlacePrize = "Gaming mouse";
        tournament.ThirdPlacePrize = null;
        tournament.Placements = [];
        tournament.Users = [];
        tournament.Teams = Clone(teams)!;
        tournament.Matches = BuildFeaturedDoubleEliminationMatches(tournament.Id, teams);
        foreach(var match in tournament.Matches)
        {
            foreach(var (nextId, participantId) in new[]
            {
                (match.WinnerNextMatchId, match.TeamWinnerId),
                (match.LoserNextMatchId, match.TeamLoserId)
            })
            {
                var next = tournament.Matches.FirstOrDefault(candidate => candidate.Id == nextId);
                if(next is null || !participantId.HasValue)
                    continue;
                if(next.TeamParticipant1Id == participantId)
                    next.Participant1SourceMatchId = match.Id;
                else if(next.TeamParticipant2Id == participantId)
                    next.Participant2SourceMatchId = match.Id;
            }
        }
        AddMockUpcomingFixture(tournament);
        tournament.EstimatedEndTime = tournament.Matches.Max(match => match.EstimatedEndTime);
        tournament.Registrations = [];
        EnsureRegistrationProjection(tournament);

        foreach(var team in teams)
        {
            AddOrReplaceTeam(team);
        }
    }

    private void SeedDefaultRegistrationFixture()
    {
        var tournament = _document.Tournaments.FirstOrDefault(candidate =>
                candidate.Id == DefaultRegistrationFixtureTournamentId &&
                candidate.Status == TournamentStatus.Scheduled &&
                candidate.ParticipationMode == ParticipationMode.Team &&
                candidate.TeamSize is > 0)
            ?? _document.Tournaments
            .Where(candidate =>
                candidate.Status == TournamentStatus.Scheduled &&
                candidate.ParticipationMode == ParticipationMode.Team &&
                candidate.TeamSize is > 0)
            .OrderBy(candidate => candidate.PlannedStartTime)
            .FirstOrDefault();
        var captain = _document.Users.FirstOrDefault(user =>
            !user.IsDeleted &&
            string.Equals(user.Username, "mockuser", StringComparison.OrdinalIgnoreCase));
        if(captain is null)
            return;

        if(tournament is not null && tournament.TeamSize is > 0)
            SeedRegistrationTeamFixture(tournament, captain, DefaultRegistrationFixtureTeamId, "Mock Registration Crew");

        var counterStrikeTournament = _document.Tournaments.FirstOrDefault(candidate =>
            candidate.Id == CounterStrike2TournamentId &&
            candidate.Status == TournamentStatus.Scheduled &&
            candidate.ParticipationMode == ParticipationMode.Team &&
            candidate.TeamSize == 5);
        if(counterStrikeTournament is not null && counterStrikeTournament.Id != tournament?.Id)
            SeedRegistrationTeamFixture(counterStrikeTournament, captain, CounterStrike2RegistrationFixtureTeamId, "Mock CS2 Registration Crew");
    }

    private void SeedRegistrationTeamFixture(
        TournamentExtended tournament,
        UserDTO captain,
        Guid teamId,
        string teamName)
    {
        if(tournament.TeamSize is not > 0)
            return;

        var existingTeam = _document.Teams.FirstOrDefault(team => team.Id == teamId);
        var requiredTeammateCount = tournament.TeamSize.Value - 1;
        var pendingMember = _document.Users.FirstOrDefault(user =>
            !user.IsDeleted &&
            string.Equals(user.Username, "mockadmin", StringComparison.OrdinalIgnoreCase) &&
            user.Id != captain.Id);
        var teammates = _document.Users
            .Where(user => !user.IsDeleted && user.Id != captain.Id && user.Id != pendingMember?.Id)
            .Take(Math.Max(requiredTeammateCount - (pendingMember is null ? 0 : 1), 0))
            .Prepend(pendingMember)
            .Where(user => user is not null)
            .Cast<UserDTO>()
            .Take(requiredTeammateCount)
            .ToList();
        if(teammates.Count != requiredTeammateCount)
            return;

        var team = existingTeam ?? new Team
        {
            Id = teamId,
            Name = teamName,
            TeamInvites = []
        };
        team.Name = teamName;
        team.CaptainUserId = captain.Id;
        team.Members = new[] { PublicUserDTO.FromUser(captain) }
            .Concat(teammates.Select(PublicUserDTO.FromUser))
            .ToList();
        team.TeamInvites = [];
        AddOrReplaceTeam(team);
    }

    private void SeedLeaderboardFixtures()
    {
        var tournament = _document.Tournaments.FirstOrDefault(candidate =>
            candidate.Id == LeaderboardTimeFixtureTournamentId);
        if(tournament is null || _leaderboardParticipants.ContainsKey(tournament.Id))
            return;

        var linkedUser = _document.Users.FirstOrDefault(user =>
            string.Equals(user.Username, "track1", StringComparison.OrdinalIgnoreCase));
        var createdAtUtc = new DateTime(2026, 6, 19, 10, 15, 0, DateTimeKind.Utc);
        var participants = new List<MockLeaderboardParticipant>();

        if(linkedUser is not null)
        {
            var linkedParticipant = new MockLeaderboardParticipant
            {
                Id = Guid.Parse("1b111111-1111-1111-1111-111111111111"),
                DisplayName = ResolveUserDisplayName(linkedUser),
                Kind = LeaderboardParticipantKind.LinkedUser,
                LinkedUserId = linkedUser.Id
            };
            linkedParticipant.Attempts.Add(CreateSeedAttempt(
                "1c111111-1111-1111-1111-111111111111",
                42_150,
                createdAtUtc,
                createdAtUtc.AddMinutes(6)));
            linkedParticipant.Attempts.Add(CreateSeedAttempt(
                "1c111111-1111-1111-1111-111111111112",
                39_480,
                createdAtUtc.AddMinutes(12),
                createdAtUtc.AddMinutes(12)));
            participants.Add(linkedParticipant);
        }

        var tiedGuest = new MockLeaderboardParticipant
        {
            Id = Guid.Parse("1b111111-1111-1111-1111-111111111112"),
            DisplayName = "Speedy Sam",
            Kind = LeaderboardParticipantKind.Guest
        };
        tiedGuest.Attempts.Add(CreateSeedAttempt(
            "1c111111-1111-1111-1111-111111111113",
            39_480,
            createdAtUtc.AddMinutes(18),
            createdAtUtc.AddMinutes(18)));
        tiedGuest.Attempts.Add(CreateSeedAttempt(
            "1c111111-1111-1111-1111-111111111114",
            41_000,
            createdAtUtc.AddMinutes(24),
            createdAtUtc.AddMinutes(24)));
        participants.Add(tiedGuest);

        var slowerGuest = new MockLeaderboardParticipant
        {
            Id = Guid.Parse("1b111111-1111-1111-1111-111111111113"),
            DisplayName = "Speedy Sam",
            Kind = LeaderboardParticipantKind.Guest
        };
        slowerGuest.Attempts.Add(CreateSeedAttempt(
            "1c111111-1111-1111-1111-111111111115",
            47_120,
            createdAtUtc.AddMinutes(30),
            createdAtUtc.AddMinutes(30)));
        participants.Add(slowerGuest);

        _leaderboardParticipants[tournament.Id] = participants;
    }

    private static MockLeaderboardAttempt CreateSeedAttempt(
        string id,
        long durationMilliseconds,
        DateTime createdAtUtc,
        DateTime updatedAtUtc) =>
        new()
        {
            Id = Guid.Parse(id),
            DurationMilliseconds = durationMilliseconds,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            RowVersion = Guid.NewGuid()
        };

    private void SeedIndividualProfileFixture()
    {
        if(_document.Tournaments.Any(tournament => tournament.Id == IndividualProfileFixtureTournamentId))
            return;

        var player = new PublicUserDTO
        {
            Id = IndividualProfileFixturePlayerId,
            Username = "solo1",
            Firstname = "Sofia",
            Lastname = "Solo",
            DisplayName = "solo1"
        };
        var opponent = new PublicUserDTO
        {
            Id = IndividualProfileFixtureOpponentId,
            Username = "solo2",
            Firstname = "Omar",
            Lastname = "Opponent",
            DisplayName = "solo2"
        };

        UpsertUserPublicFields(player);
        UpsertUserPublicFields(opponent);

        var nowUtc = DateTime.UtcNow;
        var previousStart = nowUtc.AddDays(-2);
        var upcomingStart = nowUtc.AddDays(2);
        var tournament = new TournamentExtended
        {
            Id = IndividualProfileFixtureTournamentId,
            Name = "Trackmania Showcase",
            StartTime = previousStart,
            EndTime = default,
            PlannedStartTime = previousStart,
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10,
            EstimatedEndTime = upcomingStart.AddMinutes(45),
            Status = TournamentStatus.InProgress,
            BracketType = BracketType.DoubleElimination,
            Format = TournamentFormat.BestOf3,
            FinalsFormat = TournamentFormat.BestOf3,
            ParticipationMode = ParticipationMode.Individual,
            Users = [player, opponent],
            Teams = [],
            Matches =
            [
                new Match
                {
                    Id = IndividualProfileFixturePreviousMatchId,
                    StartTime = previousStart,
                    EndTime = previousStart.AddMinutes(45),
                    EstimatedStartTime = previousStart,
                    EstimatedEndTime = previousStart.AddMinutes(45),
                    BracketType = BracketType.DoubleElimination,
                    Format = TournamentFormat.BestOf3,
                    ParticipationMode = ParticipationMode.Individual,
                    RoundNumber = 1,
                    MatchNumber = 1,
                    TournamentId = IndividualProfileFixtureTournamentId,
                    UserParticipant1Id = IndividualProfileFixturePlayerId,
                    UserParticipant2Id = IndividualProfileFixtureOpponentId,
                    UserWinnerId = IndividualProfileFixturePlayerId,
                    UserLoserId = IndividualProfileFixtureOpponentId,
                    Participant1Score = 2,
                    Participant2Score = 1,
                    LifecycleState = MatchLifecycleState.Completed,
                    ResultKind = MatchResultKind.Score,
                    ResultRecordedAtUtc = previousStart.AddMinutes(45),
                    Participant1Ended = true,
                    Participant2Ended = true,
                    WinnerNextMatchId = IndividualProfileFixtureUpcomingMatchId,
                    LoserNextMatchId = IndividualProfileFixtureUpcomingMatchId,
                    ResultVersion = 1
                },
                new Match
                {
                    Id = IndividualProfileFixtureUpcomingMatchId,
                    EstimatedStartTime = upcomingStart,
                    EstimatedEndTime = upcomingStart.AddMinutes(45),
                    BracketType = BracketType.DoubleElimination,
                    Format = TournamentFormat.BestOf3,
                    ParticipationMode = ParticipationMode.Individual,
                    RoundNumber = 2,
                    MatchNumber = 1,
                    TournamentId = IndividualProfileFixtureTournamentId,
                    UserParticipant1Id = IndividualProfileFixturePlayerId,
                    UserParticipant2Id = IndividualProfileFixtureOpponentId,
                    Participant1SourceMatchId = IndividualProfileFixturePreviousMatchId,
                    Participant2SourceMatchId = IndividualProfileFixturePreviousMatchId,
                    Participant1Score = null,
                    Participant2Score = null,
                    LifecycleState = MatchLifecycleState.AwaitingEndedConfirmation
                }
            ]
        };

        EnsureRegistrationProjection(tournament);
        _document.Tournaments.Add(tournament);
    }

    private static List<Team> BuildFeaturedDoubleEliminationTeams()
    {
        return
        [
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111111", "Valorant Team Alpha", "41111111-1111-1111-1111-111111111111", "alpha1", "Alex", "Alder", "alex@example.test", "alpha#1111", "alpha#VAL"),
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111112", "Valorant Binary Bandits", "41111111-1111-1111-1111-111111111113", "binary1", "Ben", "Binary", "ben@example.test", "binary#1111", "binary#VAL"),
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111113", "Valorant Gamma Grid", "41111111-1111-1111-1111-111111111114", "gamma1", "Gina", "Grid", "gina@example.test", "gamma#1111", "gamma#VAL"),
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111114", "Valorant Delta Drop", "41111111-1111-1111-1111-111111111115", "delta1", "Dana", "Drop", "dana@example.test", "delta#1111", "delta#VAL"),
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111115", "Valorant Echo Unit", "41111111-1111-1111-1111-111111111116", "echo1", "Eli", "Echo", "eli@example.test", "echo#1111", "echo#VAL"),
            BuildFeaturedTeam("23111111-1111-1111-1111-111111111116", "Valorant Frame Perfect", "41111111-1111-1111-1111-111111111117", "frame1", "Finn", "Frame", "finn@example.test", "frame#1111", "frame#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111121", "Pixel Pushers", "42111111-1111-1111-1111-111111111121", "pixel1", "Pia", "Pixel", "pia@example.test", "pixel#1111", "pixel#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111122", "Quantum Queue", "42111111-1111-1111-1111-111111111122", "queue1", "Quinn", "Queue", "quinn@example.test", "queue#1111", "queue#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111123", "Radiant Rift", "42111111-1111-1111-1111-111111111123", "radiant1", "Rhea", "Rift", "rhea@example.test", "rift#1111", "rift#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111124", "Spike Syndicate", "42111111-1111-1111-1111-111111111124", "spike1", "Soren", "Spike", "soren@example.test", "spike#1111", "spike#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111125", "Vector Vipers", "42111111-1111-1111-1111-111111111125", "vector1", "Vera", "Vector", "vera@example.test", "vector#1111", "vector#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111126", "Neon Knights", "42111111-1111-1111-1111-111111111126", "neon1", "Nia", "Neon", "nia@example.test", "neon#1111", "neon#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111127", "Orbital Ops", "42111111-1111-1111-1111-111111111127", "orbital1", "Owen", "Orbital", "owen@example.test", "orbital#1111", "orbital#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111128", "Prism Protocol", "42111111-1111-1111-1111-111111111128", "prism1", "Priya", "Prism", "priya@example.test", "prism#1111", "prism#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111129", "Haven Hackers", "42111111-1111-1111-1111-111111111129", "haven1", "Harper", "Haven", "harper@example.test", "haven#1111", "haven#VAL"),
            BuildFeaturedTeam("22111111-1111-1111-1111-111111111130", "Mid Control", "42111111-1111-1111-1111-111111111130", "mid1", "Milo", "Control", "milo@example.test", "mid#1111", "mid#VAL")
        ];
    }

    private static Team BuildFeaturedTeam(
        string teamId,
        string teamName,
        string captainUserId,
        string username,
        string firstname,
        string lastname,
        string email,
        string discordId,
        string riotId)
    {
        var captain = new PublicUserDTO
        {
            Id = Guid.Parse(captainUserId),
            Username = username,
            Firstname = firstname,
            Lastname = lastname,
            DiscordId = discordId,
            SteamId = $"steam-{username}",
            RiotId = riotId,
            DisplayName = username
        };

        var teammateUsername = $"{username}-teammate-2";
        var teammateOmitsExternalIds = teamName.EndsWith("Team Alpha", StringComparison.Ordinal);
        var teammate = new PublicUserDTO
        {
            Id = Guid.Parse(captainUserId.Replace("-1111-1111-1111-", "-2222-2222-2222-", StringComparison.Ordinal)),
            Username = teammateUsername,
            Firstname = $"{firstname} Mate",
            Lastname = lastname,
            DiscordId = teammateOmitsExternalIds ? null : discordId.Replace("#", "2#", StringComparison.Ordinal),
            SteamId = teammateOmitsExternalIds ? null : $"steam-{teammateUsername}",
            RiotId = teammateOmitsExternalIds ? null : riotId.Replace("#", "2#", StringComparison.Ordinal),
            DisplayName = teammateUsername
        };

        var members = new List<PublicUserDTO> { captain, teammate };
        for(var memberNumber = 3; memberNumber <= 5; memberNumber++)
        {
            var idSegment = new string((char)('0' + memberNumber), 4);
            var memberUsername = $"{username}-teammate-{memberNumber}";
            members.Add(new PublicUserDTO
            {
                Id = Guid.Parse(captainUserId.Replace(
                    "-1111-1111-1111-",
                    $"-{idSegment}-{idSegment}-{idSegment}-",
                    StringComparison.Ordinal)),
                Username = memberUsername,
                Firstname = $"{firstname} Mate {memberNumber}",
                Lastname = lastname,
                DiscordId = discordId.Replace("#", $"{memberNumber}#", StringComparison.Ordinal),
                SteamId = $"steam-{memberUsername}",
                RiotId = string.IsNullOrWhiteSpace(riotId)
                    ? null
                    : riotId.Replace("#", $"{memberNumber}#", StringComparison.Ordinal),
                DisplayName = memberUsername
            });
        }

        return new Team
        {
            Id = Guid.Parse(teamId),
            Name = teamName,
            CaptainUserId = captain.Id,
            Members = members,
            TeamInvites = []
        };
    }

    private static List<Match> BuildFeaturedDoubleEliminationMatches(Guid tournamentId, IReadOnlyList<Team> teams)
    {
        var teamIds = teams.ToDictionary(team => team.Name switch
        {
            "Valorant Team Alpha" => "Team Alpha",
            "Valorant Binary Bandits" => "Binary Bandits",
            "Valorant Gamma Grid" => "Gamma Grid",
            "Valorant Delta Drop" => "Delta Drop",
            "Valorant Echo Unit" => "Echo Unit",
            "Valorant Frame Perfect" => "Frame Perfect",
            _ => team.Name
        }, team => team.Id);
        var startTime = new DateTime(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc);

        const string ubRound1Match1Id = "31111111-1111-1111-1111-111111111001";
        const string ubRound1Match2Id = "31111111-1111-1111-1111-111111111002";
        const string ubRound1Match3Id = "31111111-1111-1111-1111-111111111003";
        const string ubRound1Match4Id = "31111111-1111-1111-1111-111111111004";
        const string ubRound1Match5Id = "31111111-1111-1111-1111-111111111005";
        const string ubRound1Match6Id = "31111111-1111-1111-1111-111111111006";
        const string ubRound1Match7Id = "31111111-1111-1111-1111-111111111007";
        const string ubRound1Match8Id = "31111111-1111-1111-1111-111111111008";
        const string ubRound2Match1Id = "31111111-1111-1111-1111-111111111009";
        const string ubRound2Match2Id = "31111111-1111-1111-1111-111111111010";
        const string ubRound2Match3Id = "31111111-1111-1111-1111-111111111011";
        const string ubRound2Match4Id = "31111111-1111-1111-1111-111111111012";
        const string ubRound3Match1Id = "31111111-1111-1111-1111-111111111013";
        const string ubRound3Match2Id = "31111111-1111-1111-1111-111111111014";
        const string ubFinalMatchId = "31111111-1111-1111-1111-111111111015";
        const string lbRound1Match1Id = "31111111-1111-1111-1111-111111111101";
        const string lbRound1Match2Id = "31111111-1111-1111-1111-111111111102";
        const string lbRound1Match3Id = "31111111-1111-1111-1111-111111111103";
        const string lbRound1Match4Id = "31111111-1111-1111-1111-111111111104";
        const string lbRound2Match1Id = "31111111-1111-1111-1111-111111111105";
        const string lbRound2Match2Id = "31111111-1111-1111-1111-111111111106";
        const string lbRound2Match3Id = "31111111-1111-1111-1111-111111111107";
        const string lbRound2Match4Id = "31111111-1111-1111-1111-111111111108";
        const string lbRound3Match1Id = "31111111-1111-1111-1111-111111111109";
        const string lbRound3Match2Id = "31111111-1111-1111-1111-111111111110";
        const string lbRound4Match1Id = "31111111-1111-1111-1111-111111111111";
        const string lbRound4Match2Id = "31111111-1111-1111-1111-111111111112";
        const string lbRound5MatchId = "31111111-1111-1111-1111-111111111113";
        const string lbRound6MatchId = "31111111-1111-1111-1111-111111111114";
        const string grandFinalMatchId = "31111111-1111-1111-1111-111111111115";

        return
        [
            BuildFeaturedMatch(ubRound1Match1Id, tournamentId, startTime, 1, 1, false, teamIds["Team Alpha"], teamIds["Mid Control"], 2, 0, teamIds["Team Alpha"], teamIds["Mid Control"], ubRound2Match1Id, lbRound1Match1Id),
            BuildFeaturedMatch(ubRound1Match2Id, tournamentId, startTime, 1, 2, false, teamIds["Quantum Queue"], teamIds["Orbital Ops"], 2, 1, teamIds["Quantum Queue"], teamIds["Orbital Ops"], ubRound2Match1Id, lbRound1Match1Id),
            BuildFeaturedMatch(ubRound1Match3Id, tournamentId, startTime, 1, 3, false, teamIds["Echo Unit"], teamIds["Neon Knights"], 2, 0, teamIds["Echo Unit"], teamIds["Neon Knights"], ubRound2Match2Id, lbRound1Match2Id),
            BuildFeaturedMatch(ubRound1Match4Id, tournamentId, startTime, 1, 4, false, teamIds["Delta Drop"], teamIds["Vector Vipers"], 2, 1, teamIds["Delta Drop"], teamIds["Vector Vipers"], ubRound2Match2Id, lbRound1Match2Id),
            BuildFeaturedMatch(ubRound1Match5Id, tournamentId, startTime, 1, 5, false, teamIds["Binary Bandits"], teamIds["Haven Hackers"], 2, 0, teamIds["Binary Bandits"], teamIds["Haven Hackers"], ubRound2Match3Id, lbRound1Match3Id),
            BuildFeaturedMatch(ubRound1Match6Id, tournamentId, startTime, 1, 6, false, teamIds["Radiant Rift"], teamIds["Prism Protocol"], 2, 1, teamIds["Radiant Rift"], teamIds["Prism Protocol"], ubRound2Match3Id, lbRound1Match3Id),
            BuildFeaturedMatch(ubRound1Match7Id, tournamentId, startTime, 1, 7, false, teamIds["Frame Perfect"], teamIds["Spike Syndicate"], 1, 2, teamIds["Spike Syndicate"], teamIds["Frame Perfect"], ubRound2Match4Id, lbRound1Match4Id),
            BuildFeaturedMatch(ubRound1Match8Id, tournamentId, startTime, 1, 8, false, teamIds["Gamma Grid"], teamIds["Pixel Pushers"], 2, 0, teamIds["Gamma Grid"], teamIds["Pixel Pushers"], ubRound2Match4Id, lbRound1Match4Id),

            BuildFeaturedMatch(lbRound1Match1Id, tournamentId, startTime.AddMinutes(90), 1, 1, true, teamIds["Mid Control"], teamIds["Orbital Ops"], 0, 2, teamIds["Orbital Ops"], teamIds["Mid Control"], lbRound2Match1Id, null),
            BuildFeaturedMatch(lbRound1Match2Id, tournamentId, startTime.AddMinutes(90), 1, 2, true, teamIds["Neon Knights"], teamIds["Vector Vipers"], 1, 2, teamIds["Vector Vipers"], teamIds["Neon Knights"], lbRound2Match2Id, null),
            BuildFeaturedMatch(lbRound1Match3Id, tournamentId, startTime.AddMinutes(90), 1, 3, true, teamIds["Haven Hackers"], teamIds["Prism Protocol"], 1, 2, teamIds["Prism Protocol"], teamIds["Haven Hackers"], lbRound2Match3Id, null),
            BuildFeaturedMatch(lbRound1Match4Id, tournamentId, startTime.AddMinutes(90), 1, 4, true, teamIds["Frame Perfect"], teamIds["Pixel Pushers"], 2, 0, teamIds["Frame Perfect"], teamIds["Pixel Pushers"], lbRound2Match4Id, null),

            BuildFeaturedMatch(ubRound2Match1Id, tournamentId, startTime.AddMinutes(180), 2, 1, false, teamIds["Team Alpha"], teamIds["Quantum Queue"], 2, 0, teamIds["Team Alpha"], teamIds["Quantum Queue"], ubRound3Match1Id, lbRound2Match1Id),
            BuildFeaturedMatch(ubRound2Match2Id, tournamentId, startTime.AddMinutes(180), 2, 2, false, teamIds["Echo Unit"], teamIds["Delta Drop"], 1, 2, teamIds["Delta Drop"], teamIds["Echo Unit"], ubRound3Match1Id, lbRound2Match2Id),
            BuildFeaturedMatch(ubRound2Match3Id, tournamentId, startTime.AddMinutes(180), 2, 3, false, teamIds["Binary Bandits"], teamIds["Radiant Rift"], 2, 1, teamIds["Binary Bandits"], teamIds["Radiant Rift"], ubRound3Match2Id, lbRound2Match3Id),
            BuildFeaturedMatch(ubRound2Match4Id, tournamentId, startTime.AddMinutes(180), 2, 4, false, teamIds["Spike Syndicate"], teamIds["Gamma Grid"], 0, 2, teamIds["Gamma Grid"], teamIds["Spike Syndicate"], ubRound3Match2Id, lbRound2Match4Id),

            BuildFeaturedMatch(lbRound2Match1Id, tournamentId, startTime.AddMinutes(270), 2, 1, true, teamIds["Orbital Ops"], teamIds["Quantum Queue"], 0, 2, teamIds["Quantum Queue"], teamIds["Orbital Ops"], lbRound3Match1Id, null),
            BuildFeaturedMatch(lbRound2Match2Id, tournamentId, startTime.AddMinutes(270), 2, 2, true, teamIds["Vector Vipers"], teamIds["Echo Unit"], 0, 2, teamIds["Echo Unit"], teamIds["Vector Vipers"], lbRound3Match1Id, null),
            BuildFeaturedMatch(lbRound2Match3Id, tournamentId, startTime.AddMinutes(270), 2, 3, true, teamIds["Prism Protocol"], teamIds["Radiant Rift"], 1, 2, teamIds["Radiant Rift"], teamIds["Prism Protocol"], lbRound3Match2Id, null),
            BuildFeaturedMatch(lbRound2Match4Id, tournamentId, startTime.AddMinutes(270), 2, 4, true, teamIds["Frame Perfect"], teamIds["Spike Syndicate"], 1, 2, teamIds["Spike Syndicate"], teamIds["Frame Perfect"], lbRound3Match2Id, null),

            BuildFeaturedMatch(ubRound3Match1Id, tournamentId, startTime.AddMinutes(360), 3, 1, false, teamIds["Team Alpha"], teamIds["Delta Drop"], 2, 1, teamIds["Team Alpha"], teamIds["Delta Drop"], ubFinalMatchId, lbRound4Match1Id),
            BuildFeaturedMatch(ubRound3Match2Id, tournamentId, startTime.AddMinutes(360), 3, 2, false, teamIds["Binary Bandits"], teamIds["Gamma Grid"], 2, 1, teamIds["Binary Bandits"], teamIds["Gamma Grid"], ubFinalMatchId, lbRound4Match2Id),

            BuildFeaturedMatch(lbRound3Match1Id, tournamentId, startTime.AddMinutes(450), 3, 1, true, teamIds["Quantum Queue"], teamIds["Echo Unit"], 1, 2, teamIds["Echo Unit"], teamIds["Quantum Queue"], lbRound4Match1Id, null),
            BuildFeaturedMatch(lbRound3Match2Id, tournamentId, startTime.AddMinutes(450), 3, 2, true, teamIds["Radiant Rift"], teamIds["Spike Syndicate"], 1, 2, teamIds["Spike Syndicate"], teamIds["Radiant Rift"], lbRound4Match2Id, null),

            BuildFeaturedMatch(lbRound4Match1Id, tournamentId, startTime.AddMinutes(540), 4, 1, true, teamIds["Echo Unit"], teamIds["Delta Drop"], 0, 2, teamIds["Delta Drop"], teamIds["Echo Unit"], lbRound5MatchId, null),
            BuildFeaturedMatch(lbRound4Match2Id, tournamentId, startTime.AddMinutes(540), 4, 2, true, teamIds["Spike Syndicate"], teamIds["Gamma Grid"], 0, 2, teamIds["Gamma Grid"], teamIds["Spike Syndicate"], lbRound5MatchId, null),
            BuildFeaturedMatch(ubFinalMatchId, tournamentId, startTime.AddMinutes(540), 4, 3, false, teamIds["Team Alpha"], teamIds["Binary Bandits"], 3, 1, teamIds["Team Alpha"], teamIds["Binary Bandits"], grandFinalMatchId, lbRound6MatchId, TournamentFormat.BestOf5),

            BuildFeaturedMatch(lbRound5MatchId, tournamentId, startTime.AddMinutes(630), 5, 1, true, teamIds["Delta Drop"], teamIds["Gamma Grid"], 1, 3, teamIds["Gamma Grid"], teamIds["Delta Drop"], lbRound6MatchId, null, TournamentFormat.BestOf5),
            BuildFeaturedMatch(lbRound6MatchId, tournamentId, startTime.AddMinutes(720), 6, 1, true, teamIds["Gamma Grid"], teamIds["Binary Bandits"], 3, 2, teamIds["Gamma Grid"], teamIds["Binary Bandits"], grandFinalMatchId, null, TournamentFormat.BestOf5),
            BuildFeaturedMatch(grandFinalMatchId, tournamentId, startTime.AddMinutes(810), 7, 1, false, teamIds["Team Alpha"], teamIds["Gamma Grid"], null, null, null, null, null, null, TournamentFormat.BestOf5)
        ];
    }

    private static void AddMockUpcomingFixture(TournamentExtended tournament)
    {
        var upcomingMatch = tournament.Matches.LastOrDefault();
        if(upcomingMatch is null)
            return;

        var upcomingStart = DateTime.UtcNow.AddDays(2);
        upcomingMatch.StartTime = default;
        upcomingMatch.EndTime = default;
        upcomingMatch.EstimatedStartTime = upcomingStart;
        upcomingMatch.EstimatedEndTime = upcomingStart.AddMinutes(75);
    }

    private static Match BuildFeaturedMatch(
        string matchId,
        Guid tournamentId,
        DateTime startTime,
        int roundNumber,
        int matchNumber,
        bool isLowerBracketMatch,
        Guid? participant1Id,
        Guid? participant2Id,
        int? participant1Score,
        int? participant2Score,
        Guid? teamWinnerId,
        Guid? teamLoserId,
        string? winnerNextMatchId,
        string? loserNextMatchId,
        TournamentFormat? format = null)
    {
        return new Match
        {
            Id = Guid.Parse(matchId),
            StartTime = startTime,
            EndTime = startTime.AddMinutes(format == TournamentFormat.BestOf5 ? 75 : 60),
            EstimatedStartTime = startTime,
            EstimatedEndTime = startTime.AddMinutes(format == TournamentFormat.BestOf5 ? 75 : 60),
            BracketType = BracketType.DoubleElimination,
            Format = format ?? TournamentFormat.BestOf3,
            ParticipationMode = ParticipationMode.Team,
            RoundNumber = roundNumber,
            MatchNumber = matchNumber,
            IsLowerBracketMatch = isLowerBracketMatch,
            TournamentId = tournamentId,
            TeamParticipant1Id = participant1Id,
            TeamParticipant2Id = participant2Id,
            Participant1IsBYE = false,
            Participant2IsBYE = false,
            TeamWinnerId = teamWinnerId,
            TeamLoserId = teamLoserId,
            Participant1Score = participant1Score,
            Participant2Score = participant2Score,
            LifecycleState = teamWinnerId.HasValue ? MatchLifecycleState.Completed : MatchLifecycleState.AwaitingEndedConfirmation,
            Participant1Ended = teamWinnerId.HasValue,
            Participant2Ended = teamWinnerId.HasValue,
            ResultKind = teamWinnerId.HasValue ? MatchResultKind.Score : null,
            ResultRecordedAtUtc = teamWinnerId.HasValue ? startTime.AddMinutes(format == TournamentFormat.BestOf5 ? 75 : 60) : null,
            ResultVersion = teamWinnerId.HasValue ? 1 : 0,
            WinnerNextMatchId = string.IsNullOrWhiteSpace(winnerNextMatchId) ? null : Guid.Parse(winnerNextMatchId),
            LoserNextMatchId = string.IsNullOrWhiteSpace(loserNextMatchId) ? null : Guid.Parse(loserNextMatchId)
        };
    }

    private static List<Match> BuildGeneratedMatches(
        TournamentExtended tournament,
        IReadOnlyCollection<TournamentRegistrationDTO> registrations)
    {
        var participantIds = registrations
            .Where(registration => registration.Status == TournamentRegistrationStatus.Active)
            .Where(registration => registration.Kind == (tournament.ParticipationMode == ParticipationMode.Team
                ? TournamentRegistrationKind.Team
                : TournamentRegistrationKind.Individual))
            .Select(registration => tournament.ParticipationMode == ParticipationMode.Team
                ? registration.Team?.Id
                : registration.User?.Id)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .ToList();
        var matches = tournament.BracketType switch
        {
            BracketType.SingleElimination => BuildSingleEliminationMatches(tournament, participantIds),
            BracketType.DoubleElimination => BuildDoubleEliminationMatches(tournament, participantIds),
            BracketType.RoundRobin => BuildRoundRobinMatches(tournament, participantIds),
            _ => throw new NotSupportedException($"Bracket type {tournament.BracketType} is not supported by the mock backend.")
        };

        PropagateKnownMatchResults(tournament, matches);
        AssignEstimatedSchedule(tournament, matches);
        return matches;
    }

    private static List<Match> BuildSingleEliminationMatches(
        TournamentExtended tournament,
        IReadOnlyList<Guid> participantIds,
        bool useFinalsFormat = true)
    {
        var nextPowerOfTwo = 1;
        while(nextPowerOfTwo < participantIds.Count)
            nextPowerOfTwo <<= 1;

        var totalRounds = (int)Math.Log2(nextPowerOfTwo);
        var slots = new Guid?[nextPowerOfTwo];
        for(var index = 0; index < participantIds.Count; index++)
            slots[index] = participantIds[index];

        var matches = new List<Match>();
        for(var round = 1; round <= totalRounds; round++)
        {
            var matchesThisRound = nextPowerOfTwo >> round;
            for(var matchNumber = 1; matchNumber <= matchesThisRound; matchNumber++)
            {
                var match = CreateGeneratedMatch(
                    tournament,
                    round,
                    matchNumber,
                    format: useFinalsFormat && round == totalRounds ? tournament.FinalsFormat : tournament.Format);

                if(round == 1)
                {
                    var participant1 = slots[(matchNumber - 1) * 2];
                    var participant2 = slots[(matchNumber - 1) * 2 + 1];
                    SetMatchParticipant(match, tournament.ParticipationMode, true, participant1);
                    SetMatchParticipant(match, tournament.ParticipationMode, false, participant2);
                    match.Participant1IsBYE = !participant1.HasValue;
                    match.Participant2IsBYE = !participant2.HasValue;

                    if(participant1.HasValue != participant2.HasValue)
                        SetMatchWinner(match, tournament.ParticipationMode, participant1 ?? participant2);
                }

                matches.Add(match);
            }
        }

        LinkSingleEliminationMatches(matches, totalRounds);
        return matches;
    }

    private static List<Match> BuildDoubleEliminationMatches(
        TournamentExtended tournament,
        IReadOnlyList<Guid> participantIds)
    {
        var upperBracket = BuildSingleEliminationMatches(tournament, participantIds, useFinalsFormat: false);
        var upperRounds = upperBracket.Max(match => match.RoundNumber);
        var lowerRounds = Math.Max((upperRounds - 1) * 2, 0);
        var matches = new List<Match>(upperBracket);
        var lowerMatches = new List<Match>();
        var matchesThisRound = upperRounds > 1 ? 1 << (upperRounds - 2) : 0;

        for(var round = 1; round <= lowerRounds; round++)
        {
            for(var matchNumber = 1; matchNumber <= matchesThisRound; matchNumber++)
            {
                var match = CreateGeneratedMatch(tournament, round, matchNumber, isLowerBracketMatch: true);
                lowerMatches.Add(match);
                matches.Add(match);
            }

            if(round % 2 == 0)
                matchesThisRound = Math.Max(matchesThisRound / 2, 1);
        }

        var grandFinal = CreateGeneratedMatch(
            tournament,
            Math.Max(upperRounds, lowerRounds) + 1,
            1,
            format: tournament.FinalsFormat);
        matches.Add(grandFinal);

        LinkSingleEliminationMatches(matches.Where(match => !match.IsLowerBracketMatch).ToList(), upperRounds);
        LinkDoubleEliminationMatches(upperBracket, lowerMatches, grandFinal, upperRounds);
        return matches;
    }

    private static List<Match> BuildRoundRobinMatches(
        TournamentExtended tournament,
        IReadOnlyList<Guid> participantIds)
    {
        var rotation = participantIds.Select(id => (Guid?)id).ToList();
        if(rotation.Count % 2 != 0)
            rotation.Add(null);

        var matches = new List<Match>();
        var matchNumber = 0;
        for(var round = 1; round < rotation.Count; round++)
        {
            var matchesThisRound = rotation.Count / 2;
            for(var index = 0; index < matchesThisRound; index++)
            {
                var participant1 = rotation[index];
                var participant2 = rotation[rotation.Count - index - 1];
                if(!participant1.HasValue || !participant2.HasValue)
                    continue;

                var match = CreateGeneratedMatch(tournament, round, matchNumber++);
                SetMatchParticipant(match, tournament.ParticipationMode, true, participant1);
                SetMatchParticipant(match, tournament.ParticipationMode, false, participant2);
                matches.Add(match);
            }

            var last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        return matches;
    }

    private static Match CreateGeneratedMatch(
        TournamentExtended tournament,
        int round,
        int matchNumber,
        bool isLowerBracketMatch = false,
        TournamentFormat? format = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            BracketType = tournament.BracketType,
            Format = format ?? tournament.Format,
            ParticipationMode = tournament.ParticipationMode,
            RoundNumber = round,
            MatchNumber = matchNumber,
            IsLowerBracketMatch = isLowerBracketMatch,
            TournamentId = tournament.Id
        };

    private static void LinkSingleEliminationMatches(List<Match> matches, int totalRounds)
    {
        foreach(var match in matches.Where(match => !match.IsLowerBracketMatch && match.RoundNumber < totalRounds))
        {
            match.WinnerNextMatchId = matches
                .Single(next => !next.IsLowerBracketMatch &&
                    next.RoundNumber == match.RoundNumber + 1 &&
                    next.MatchNumber == (match.MatchNumber + 1) / 2)
                .Id;
        }
    }

    private static void LinkDoubleEliminationMatches(
        IReadOnlyList<Match> upperMatches,
        IReadOnlyList<Match> lowerMatches,
        Match grandFinal,
        int upperRounds)
    {
        foreach(var match in upperMatches)
        {
            if(match.RoundNumber == upperRounds)
            {
                match.WinnerNextMatchId = grandFinal.Id;
                match.LoserNextMatchId = lowerMatches.LastOrDefault()?.Id;
                continue;
            }

            var nextUpper = upperMatches.FirstOrDefault(next =>
                next.RoundNumber == match.RoundNumber + 1 &&
                next.MatchNumber == (match.MatchNumber + 1) / 2);
            if(nextUpper is not null)
                match.WinnerNextMatchId = nextUpper.Id;

            if(lowerMatches.Count == 0)
                continue;

            var targetRound = match.RoundNumber <= 2 ? match.RoundNumber : (match.RoundNumber - 1) * 2;
            var targetNumber = match.RoundNumber == 1 ? (match.MatchNumber + 1) / 2 : match.MatchNumber;
            var nextLower = lowerMatches.FirstOrDefault(next =>
                next.RoundNumber == targetRound && next.MatchNumber == targetNumber);
            if(nextLower is not null)
                match.LoserNextMatchId = nextLower.Id;
        }

        foreach(var match in lowerMatches)
        {
            var targetNumber = match.RoundNumber % 2 == 1
                ? match.MatchNumber
                : (match.MatchNumber + 1) / 2;
            var nextLower = lowerMatches.FirstOrDefault(next =>
                next.RoundNumber == match.RoundNumber + 1 && next.MatchNumber == targetNumber);
            match.WinnerNextMatchId = nextLower?.Id ?? grandFinal.Id;
        }
    }

    private static void SetMatchParticipant(Match match, ParticipationMode mode, bool first, Guid? participantId)
    {
        if(!participantId.HasValue)
            return;

        if(mode == ParticipationMode.Team)
        {
            if(first)
                match.TeamParticipant1Id = participantId;
            else
                match.TeamParticipant2Id = participantId;
        }
        else
        {
            if(first)
                match.UserParticipant1Id = participantId;
            else
                match.UserParticipant2Id = participantId;
        }
    }

    private static void SetMatchWinner(Match match, ParticipationMode mode, Guid? participantId)
    {
        if(!participantId.HasValue)
            return;

        if(mode == ParticipationMode.Team)
            match.TeamWinnerId = participantId;
        else
            match.UserWinnerId = participantId;
    }

    private static Guid? GetMatchWinner(Match match, ParticipationMode mode) =>
        mode == ParticipationMode.Team ? match.TeamWinnerId : match.UserWinnerId;

    private static Guid? GetMatchLoser(Match match, ParticipationMode mode) =>
        mode == ParticipationMode.Team ? match.TeamLoserId : match.UserLoserId;

    private static void PropagateKnownMatchResults(TournamentExtended tournament, IReadOnlyList<Match> matches)
    {
        foreach(var match in matches.OrderBy(match => match.RoundNumber).ThenBy(match => match.MatchNumber))
        {
            PropagateMatchResult(tournament, matches, match);
        }
    }

    private static void PropagateMatchResult(TournamentExtended tournament, Match match)
    {
        PropagateMatchResult(
            tournament,
            tournament.Matches.ToList(),
            match);
    }

    private static void PropagateMatchResult(
        TournamentExtended tournament,
        IReadOnlyList<Match> matches,
        Match match)
    {
        PropagateMatchParticipant(tournament, matches, match.WinnerNextMatchId, GetMatchWinner(match, tournament.ParticipationMode));
        PropagateMatchParticipant(tournament, matches, match.LoserNextMatchId, GetMatchLoser(match, tournament.ParticipationMode));
    }

    private static void PropagateMatchParticipant(
        TournamentExtended tournament,
        IReadOnlyList<Match> matches,
        Guid? nextMatchId,
        Guid? participantId)
    {
        if(!nextMatchId.HasValue || !participantId.HasValue)
            return;

        var nextMatch = matches.FirstOrDefault(match => match.Id == nextMatchId.Value);
        if(nextMatch is null)
            return;

        if(tournament.ParticipationMode == ParticipationMode.Team)
        {
            if(!nextMatch.TeamParticipant1Id.HasValue)
                nextMatch.TeamParticipant1Id = participantId;
            else if(!nextMatch.TeamParticipant2Id.HasValue)
                nextMatch.TeamParticipant2Id = participantId;
        }
        else if(!nextMatch.UserParticipant1Id.HasValue)
            nextMatch.UserParticipant1Id = participantId;
        else if(!nextMatch.UserParticipant2Id.HasValue)
            nextMatch.UserParticipant2Id = participantId;
    }

    private static void AssignEstimatedSchedule(TournamentExtended tournament, IReadOnlyList<Match> matches)
    {
        var currentRoundStart = tournament.PlannedStartTime == default
            ? DateTime.UtcNow
            : tournament.PlannedStartTime;
        DateTime? latestEnd = null;
        foreach(var round in matches.GroupBy(match => match.RoundNumber).OrderBy(group => group.Key))
        {
            DateTime? roundEnd = null;
            foreach(var match in round)
            {
                var duration = GetMatchDuration(tournament, match.Format);
                var estimatedEnd = currentRoundStart + duration;
                match.EstimatedStartTime = currentRoundStart;
                match.EstimatedEndTime = estimatedEnd;
                roundEnd = !roundEnd.HasValue || estimatedEnd > roundEnd.Value ? estimatedEnd : roundEnd;
            }

            if(roundEnd.HasValue)
            {
                latestEnd = !latestEnd.HasValue || roundEnd > latestEnd ? roundEnd : latestEnd;
                currentRoundStart = roundEnd.Value + TimeSpan.FromMinutes(Math.Max(tournament.RoundBreakDurationMinutes, 0));
            }
        }

        tournament.EstimatedEndTime = latestEnd;
    }

    private static TimeSpan GetMatchDuration(TournamentExtended tournament, TournamentFormat format)
    {
        var multiplier = format switch
        {
            TournamentFormat.BestOf1 => 1,
            TournamentFormat.BestOf3 => 3,
            TournamentFormat.BestOf5 => 5,
            _ => 1
        };
        return TimeSpan.FromMinutes(Math.Max(tournament.AverageGameDurationMinutes, 1) * multiplier);
    }

    private static List<Placement> BuildPlacements(TournamentExtended tournament)
    {
        if(tournament.BracketType == BracketType.RoundRobin)
            return BuildRoundRobinPlacements(tournament);

        var finalMatch = tournament.Matches
            .OrderByDescending(match => match.RoundNumber)
            .ThenByDescending(match => match.MatchNumber)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The tournament has no final match.");

        var winnerId = tournament.ParticipationMode == ParticipationMode.Team
            ? finalMatch.TeamWinnerId
            : finalMatch.UserWinnerId;
        if(!winnerId.HasValue)
            throw new InvalidOperationException("The final match has no winner assigned.");

        var loserId = tournament.ParticipationMode == ParticipationMode.Team
            ? finalMatch.TeamLoserId
            : finalMatch.UserLoserId;
        if(tournament.BracketType == BracketType.DoubleElimination && !loserId.HasValue)
            throw new InvalidOperationException("The final match has no loser assigned.");

        var placements = new List<Placement>();
        if(tournament.ParticipationMode == ParticipationMode.Team)
        {
            var winner = tournament.Teams.FirstOrDefault(team => team.Id == winnerId.Value)
                ?? throw new InvalidOperationException("The final winner is not a registered team.");
            placements.Add(new Placement { Place = 1, Teams = [Clone(winner)!] });

            if(loserId.HasValue)
            {
                var loser = tournament.Teams.FirstOrDefault(team => team.Id == loserId.Value)
                    ?? throw new InvalidOperationException("The final loser is not a registered team.");
                placements.Add(new Placement { Place = 2, Teams = [Clone(loser)!] });
            }
        }
        else
        {
            var winner = tournament.Users.FirstOrDefault(user => user.Id == winnerId.Value)
                ?? throw new InvalidOperationException("The final winner is not a registered user.");
            placements.Add(new Placement { Place = 1, Users = [Clone(winner)!] });

            if(loserId.HasValue)
            {
                var loser = tournament.Users.FirstOrDefault(user => user.Id == loserId.Value)
                    ?? throw new InvalidOperationException("The final loser is not a registered user.");
                placements.Add(new Placement { Place = 2, Users = [Clone(loser)!] });
            }
        }

        var loserIds = tournament.Matches
            .OrderByDescending(match => match.RoundNumber)
            .ThenByDescending(match => match.MatchNumber)
            .Where(match => match != finalMatch)
            .Select(match => tournament.ParticipationMode == ParticipationMode.Team
                ? match.TeamLoserId
                : match.UserLoserId)
            .Where(id => id.HasValue && id != winnerId && id != loserId)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        for(var index = 0; index < loserIds.Count; index++)
        {
            var place = index + (loserId.HasValue ? 3 : 2);
            if(tournament.ParticipationMode == ParticipationMode.Team)
            {
                var team = tournament.Teams.FirstOrDefault(candidate => candidate.Id == loserIds[index]);
                if(team is not null)
                    placements.Add(new Placement { Place = place, Teams = [Clone(team)!] });
            }
            else
            {
                var user = tournament.Users.FirstOrDefault(candidate => candidate.Id == loserIds[index]);
                if(user is not null)
                    placements.Add(new Placement { Place = place, Users = [Clone(user)!] });
            }
        }

        return placements;
    }

    private static List<Placement> BuildRoundRobinPlacements(TournamentExtended tournament)
    {
        var participants = tournament.ParticipationMode == ParticipationMode.Team
            ? tournament.Teams.Select(team => (Id: team.Id, Wins: tournament.Matches.Count(match => match.TeamWinnerId == team.Id)))
            : tournament.Users.Select(user => (Id: user.Id, Wins: tournament.Matches.Count(match => match.UserWinnerId == user.Id)));

        var orderedParticipants = participants
            .OrderByDescending(participant => participant.Wins)
            .ThenBy(participant => participant.Id)
            .ToList();
        var placements = new List<Placement>();

        for(var index = 0; index < orderedParticipants.Count; index++)
        {
            var participant = orderedParticipants[index];
            if(tournament.ParticipationMode == ParticipationMode.Team)
            {
                var team = tournament.Teams.First(candidate => candidate.Id == participant.Id);
                placements.Add(new Placement { Place = index + 1, Teams = [Clone(team)!] });
            }
            else
            {
                var user = tournament.Users.First(candidate => candidate.Id == participant.Id);
                placements.Add(new Placement { Place = index + 1, Users = [Clone(user)!] });
            }
        }

        return placements;
    }

    private void EnsureRegistrationProjection(TournamentExtended tournament)
    {
        if(_registrationDetails.ContainsKey(tournament.Id))
            return;

        PopulateRegistrationProjection(tournament);
    }

    private static void PopulateRegistrationProjection(TournamentExtended tournament)
    {
        if(tournament.Registrations.Any())
            return;

        var registrations = tournament.Users
            .Where(user => user.Id != Guid.Empty)
            .Select(user => new PublicTournamentRegistrationDTO
            {
                Id = user.Id,
                TournamentId = tournament.Id,
                Kind = TournamentRegistrationKind.Individual,
                Status = TournamentRegistrationStatus.Active,
                User = Clone(user)
            })
            .Concat(tournament.Teams
                .Where(team => team.Id != Guid.Empty)
                .Select(team => new PublicTournamentRegistrationDTO
                {
                    Id = team.Id,
                    TournamentId = tournament.Id,
                    Kind = TournamentRegistrationKind.Team,
                    Status = TournamentRegistrationStatus.Active,
                    Team = new PublicTournamentTeamDTO
                    {
                        Id = team.Id,
                        Name = team.Name,
                        CaptainUserId = team.CaptainUserId,
                        LogoUrl = team.LogoUrl,
                        Members = []
                    },
                    RosterMembers = team.Members
                        .Select(member => new PublicTournamentRosterMemberDTO
                        {
                            User = Clone(member)!,
                            IsCaptain = member.Id == team.CaptainUserId
                        })
                        .ToList()
                }))
            .ToList();

        tournament.Registrations = registrations;
    }

    private UserProfileDTO UpdateCurrentProfileCore(
        string persona,
        string username,
        string firstname,
        string lastname,
        string? discordId,
        string? steamId,
        string? riotId,
        bool isComplete)
    {
        var normalizedPersona = NormalizePersona(persona);
        var profileRecord = _document.Profiles.First(candidate =>
            string.Equals(candidate.Persona, normalizedPersona, StringComparison.OrdinalIgnoreCase));

        var existingUser = profileRecord.Profile.User
            ?? throw new InvalidOperationException($"Mock profile for persona '{normalizedPersona}' is missing its user.");

        existingUser.Username = username.Trim();
        existingUser.Firstname = firstname.Trim();
        existingUser.Lastname = lastname.Trim();
        existingUser.DiscordId = discordId;
        existingUser.SteamId = steamId;
        existingUser.RiotId = riotId;
        existingUser.DisplayName = BuildDisplayName(existingUser.Firstname, existingUser.Lastname, existingUser.Username);
        existingUser.UpdatedAtUtc = DateTime.UtcNow;

        var userIndex = _document.Users.FindIndex(user => user.Id == existingUser.Id);
        if(userIndex >= 0)
        {
            _document.Users[userIndex] = ToUserDto(existingUser);
        }

        profileRecord.Profile = new CurrentUserProfileResponse(
            isComplete,
            Clone(existingUser),
            profileRecord.Profile.Email,
            profileRecord.Profile.EmailVerified);

        return Clone(existingUser)!;
    }

    private TournamentExtended GetRequiredLeaderboardTournament(Guid tournamentId)
    {
        var tournament = GetRequiredTournament(tournamentId);
        if(tournament.BracketType != BracketType.Leaderboard)
            throw new InvalidOperationException("Leaderboard results are only available for leaderboard tournaments.");

        return tournament;
    }

    private static void EnsureLeaderboardInProgress(TournamentExtended tournament)
    {
        if(tournament.Status != TournamentStatus.InProgress)
            throw new InvalidOperationException("Leaderboard results can only be changed while the tournament is in progress.");
    }

    private static LeaderboardRankingMetric? ValidateLeaderboardConfiguration(
        BracketType bracketType,
        LeaderboardRankingMetric? leaderboardRankingMetric,
        ParticipationMode participationMode)
    {
        if(bracketType == BracketType.Leaderboard)
        {
            if(participationMode != ParticipationMode.Individual)
                throw new InvalidOperationException("Leaderboard tournaments must use individual participation.");
            if(!leaderboardRankingMetric.HasValue || !Enum.IsDefined(leaderboardRankingMetric.Value))
                throw new InvalidOperationException("Leaderboard tournaments require a supported ranking metric.");

            return leaderboardRankingMetric;
        }

        if(leaderboardRankingMetric.HasValue)
            throw new InvalidOperationException("Ranking metric is only supported for leaderboard tournaments.");

        return null;
    }

    private static int NormalizeLeaderboardScheduleDuration(BracketType bracketType, int durationMinutes) =>
        bracketType == BracketType.Leaderboard ? 0 : durationMinutes;

    private static LeaderboardRankingMetric ResolveLeaderboardMetric(TournamentExtended tournament) =>
        tournament.LeaderboardRankingMetric ?? LeaderboardRankingMetric.HighestScore;

    private List<MockLeaderboardParticipant> GetLeaderboardParticipants(Guid tournamentId) =>
        _leaderboardParticipants.TryGetValue(tournamentId, out var participants) ? participants : [];

    private List<MockLeaderboardParticipant> GetOrCreateLeaderboardParticipantList(Guid tournamentId)
    {
        if(!_leaderboardParticipants.TryGetValue(tournamentId, out var participants))
        {
            participants = [];
            _leaderboardParticipants[tournamentId] = participants;
        }

        return participants;
    }

    private MockLeaderboardParticipant ResolveOrCreateLeaderboardParticipant(
        TournamentExtended tournament,
        List<MockLeaderboardParticipant> participants,
        RecordLeaderboardAttemptDTO dto)
    {
        var selectorCount =
            (dto.ParticipantId.HasValue ? 1 : 0) +
            (dto.LinkedUserId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(dto.GuestDisplayName) ? 1 : 0);
        if(selectorCount != 1)
            throw new InvalidOperationException("Provide exactly one existing participant, linked user, or guest display name.");

        if(dto.ParticipantId.HasValue)
        {
            return participants.FirstOrDefault(participant => participant.Id == dto.ParticipantId.Value)
                ?? throw new InvalidOperationException("The leaderboard participant was not found for this tournament.");
        }

        if(dto.LinkedUserId.HasValue)
        {
            var linkedUserId = dto.LinkedUserId.Value;
            var existing = participants.FirstOrDefault(participant => participant.LinkedUserId == linkedUserId);
            if(existing is not null)
                return existing;

            var user = _document.Users.FirstOrDefault(candidate => candidate.Id == linkedUserId)
                ?? throw new InvalidOperationException("The linked user was not found.");
            var linkedParticipant = new MockLeaderboardParticipant
            {
                Id = Guid.NewGuid(),
                DisplayName = ResolveUserDisplayName(user),
                Kind = LeaderboardParticipantKind.LinkedUser,
                LinkedUserId = linkedUserId
            };
            participants.Add(linkedParticipant);
            return linkedParticipant;
        }

        var guestName = dto.GuestDisplayName!.Trim();
        if(guestName.Length > 100)
            throw new InvalidOperationException("A guest display name must be at most 100 characters.");

        var guestParticipant = new MockLeaderboardParticipant
        {
            Id = Guid.NewGuid(),
            DisplayName = guestName,
            Kind = LeaderboardParticipantKind.Guest
        };
        participants.Add(guestParticipant);
        return guestParticipant;
    }

    private static string ResolveUserDisplayName(UserDTO user)
    {
        if(!string.IsNullOrWhiteSpace(user.DisplayName))
            return user.DisplayName.Trim();
        if(!string.IsNullOrWhiteSpace(user.Username))
            return user.Username.Trim();

        return "Player";
    }

    private static void ValidateLeaderboardAttemptValue(
        TournamentExtended tournament,
        decimal? score,
        long? durationMilliseconds)
    {
        if(ResolveLeaderboardMetric(tournament) == LeaderboardRankingMetric.HighestScore)
        {
            if(!score.HasValue || durationMilliseconds.HasValue)
                throw new InvalidOperationException("A highest-score leaderboard attempt requires a score value.");
            if(score.Value < 0 || decimal.Round(score.Value, 6) != score.Value || score.Value >= 1000000000000m)
                throw new InvalidOperationException("A score must be non-negative with at most 12 integral and 6 fractional digits.");

            return;
        }

        if(!durationMilliseconds.HasValue || score.HasValue)
            throw new InvalidOperationException("A fastest-time leaderboard attempt requires a duration value.");
        if(durationMilliseconds.Value <= 0)
            throw new InvalidOperationException("A duration must be a positive whole number of milliseconds.");
    }

    private MockLeaderboardAttempt FindLeaderboardAttempt(Guid tournamentId, Guid attemptId) =>
        GetLeaderboardParticipants(tournamentId)
            .SelectMany(participant => participant.Attempts)
            .FirstOrDefault(attempt => attempt.Id == attemptId)
        ?? throw new InvalidOperationException("The leaderboard attempt was not found for this tournament.");

    private static List<LeaderboardRowDTO> BuildLeaderboardRows(
        TournamentExtended tournament,
        IReadOnlyList<MockLeaderboardParticipant> participants)
    {
        var metric = ResolveLeaderboardMetric(tournament);
        var ranked = participants
            .Select(participant => (Participant: participant, Best: GetBestLeaderboardAttempt(participant, metric)))
            .Where(entry => entry.Best is not null)
            .Select(entry => (
                entry.Participant,
                Best: entry.Best!,
                OrderKey: metric == LeaderboardRankingMetric.HighestScore
                    ? -entry.Best!.Score!.Value
                    : entry.Best!.DurationMilliseconds!.Value))
            .OrderBy(entry => entry.OrderKey)
            .ThenBy(entry => entry.Participant.Id)
            .ToList();

        var rows = new List<LeaderboardRowDTO>();
        decimal? previousKey = null;
        var place = 0;
        for(var index = 0; index < ranked.Count; index++)
        {
            var entry = ranked[index];
            if(previousKey is null || entry.OrderKey != previousKey.Value)
                place = index + 1;
            previousKey = entry.OrderKey;

            rows.Add(new LeaderboardRowDTO
            {
                Rank = place,
                ParticipantId = entry.Participant.Id,
                DisplayName = entry.Participant.DisplayName,
                ParticipantKind = entry.Participant.Kind,
                LinkedUserId = entry.Participant.LinkedUserId,
                Score = metric == LeaderboardRankingMetric.HighestScore ? entry.Best.Score : null,
                DurationMilliseconds = metric == LeaderboardRankingMetric.FastestTime
                    ? entry.Best.DurationMilliseconds
                    : null
            });
        }

        return rows;
    }

    private static MockLeaderboardAttempt? GetBestLeaderboardAttempt(
        MockLeaderboardParticipant participant,
        LeaderboardRankingMetric metric) =>
        metric == LeaderboardRankingMetric.HighestScore
            ? participant.Attempts
                .Where(attempt => attempt.Score.HasValue)
                .OrderBy(attempt => attempt.Score!.Value)
                .LastOrDefault()
            : participant.Attempts
                .Where(attempt => attempt.DurationMilliseconds.HasValue)
                .OrderBy(attempt => attempt.DurationMilliseconds!.Value)
                .FirstOrDefault();

    private AdminLeaderboardResponseDTO BuildAdminLeaderboardResponse(TournamentExtended tournament)
    {
        var participants = GetLeaderboardParticipants(tournament.Id);
        var ranks = BuildLeaderboardRows(tournament, participants)
            .ToDictionary(row => row.ParticipantId, row => row.Rank);

        return new AdminLeaderboardResponseDTO
        {
            TournamentId = tournament.Id,
            RankingMetric = ResolveLeaderboardMetric(tournament),
            Participants = participants
                .OrderBy(participant => ranks.TryGetValue(participant.Id, out var rank) ? rank : int.MaxValue)
                .ThenBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(participant => participant.Id)
                .Select(ToAdminLeaderboardParticipantDTO)
                .ToList()
        };
    }

    private static AdminLeaderboardParticipantDTO ToAdminLeaderboardParticipantDTO(
        MockLeaderboardParticipant participant) =>
        new()
        {
            Id = participant.Id,
            DisplayName = participant.DisplayName,
            ParticipantKind = participant.Kind,
            LinkedUserId = participant.LinkedUserId,
            Attempts = participant.Attempts
                .OrderByDescending(attempt => attempt.CreatedAtUtc)
                .ThenBy(attempt => attempt.Id)
                .Select(ToLeaderboardAttemptDTO)
                .ToList()
        };

    private static LeaderboardAttemptDTO ToLeaderboardAttemptDTO(MockLeaderboardAttempt attempt) =>
        new()
        {
            Id = attempt.Id,
            Score = attempt.Score,
            DurationMilliseconds = attempt.DurationMilliseconds,
            CreatedAtUtc = attempt.CreatedAtUtc,
            UpdatedAtUtc = attempt.UpdatedAtUtc,
            RowVersion = attempt.RowVersion
        };

    private TournamentExtended GetRequiredTournament(Guid id) =>
        _document.Tournaments.FirstOrDefault(tournament => tournament.Id == id)
        ?? throw new InvalidOperationException($"Mock tournament '{id}' was not found.");

    private static void EnsureScheduledTournament(TournamentExtended tournament)
    {
        if(tournament.Status != TournamentStatus.Scheduled)
            throw new InvalidOperationException("tournament_not_scheduled");
    }

    private UserDTO GetRequiredUser(Guid id) =>
        _document.Users.FirstOrDefault(user => user.Id == id)
        ?? throw new InvalidOperationException($"Mock user '{id}' was not found.");

    private Team GetRequiredTeam(Guid id)
    {
        var team = _document.Teams.FirstOrDefault(existing => existing.Id == id);
        return team ?? throw new InvalidOperationException($"Mock team '{id}' was not found.");
    }

    private void AddOrReplaceTeam(Team team)
    {
        var storeIndex = _document.Teams.FindIndex(existing => existing.Id == team.Id);
        if(storeIndex >= 0)
        {
            _document.Teams[storeIndex] = Clone(team)!;
        }
        else
        {
            _document.Teams.Add(Clone(team)!);
        }

        foreach(var member in team.Members)
        {
            UpsertUserPublicFields(member);
        }

        foreach(var tournament in _document.Tournaments)
        {
            var teams = tournament.Teams.ToList();
            var index = teams.FindIndex(existing => existing.Id == team.Id);
            if(index >= 0)
            {
                teams[index] = Clone(team)!;
                tournament.Teams = teams;
            }
        }
    }

    private void UpsertUserPublicFields(PublicUserDTO member)
    {
        var userIndex = _document.Users.FindIndex(existing => existing.Id == member.Id);
        if(userIndex >= 0)
        {
            var existingUser = _document.Users[userIndex];
            existingUser.Username = member.Username;
            existingUser.Firstname = member.Firstname ?? existingUser.Firstname;
            existingUser.Lastname = member.Lastname ?? existingUser.Lastname;
            existingUser.DiscordId = member.DiscordId;
            existingUser.SteamId = member.SteamId;
            existingUser.RiotId = member.RiotId;
            existingUser.DisplayName = string.IsNullOrWhiteSpace(member.DisplayName)
                ? BuildDisplayName(existingUser.Firstname, existingUser.Lastname, existingUser.Username)
                : member.DisplayName;
            existingUser.UpdatedAtUtc = FeaturedFixtureUpdatedAtUtc;
            return;
        }

        _document.Users.Add(new UserDTO
        {
            Id = member.Id,
            Username = member.Username,
            Firstname = member.Firstname,
            Lastname = member.Lastname,
            Email = string.Empty,
            EmailVerified = false,
            DiscordId = member.DiscordId,
            SteamId = member.SteamId,
            RiotId = member.RiotId,
            DisplayName = string.IsNullOrWhiteSpace(member.DisplayName)
                ? BuildDisplayName(member.Firstname, member.Lastname, member.Username)
                : member.DisplayName,
            IsDeleted = false,
            CreatedAtUtc = FeaturedFixtureCreatedAtUtc,
            UpdatedAtUtc = FeaturedFixtureUpdatedAtUtc
        });
    }

    private List<Team> EnsureCurrentUserTeamFixtures(UserProfileDTO currentUser)
    {
        var captainedId = Guid.Parse("23111111-1111-1111-1111-111111111120");
        var memberId = Guid.Parse("23111111-1111-1111-1111-111111111121");
        var inviteId = Guid.Parse("23111111-1111-1111-1111-111111111122");
        var fallbackUsers = _document.Users
            .Where(user => !user.IsDeleted && user.Id != currentUser.Id)
            .ToList();
        var teammate = _document.Users.FirstOrDefault(user => user.Username == "track1")
            ?? fallbackUsers.FirstOrDefault();

        if(_document.Teams.All(team => team.Id != captainedId) && teammate is not null)
        {
            AddOrReplaceTeam(new Team
            {
                Id = captainedId,
                Name = "Mock Captains",
                CaptainUserId = currentUser.Id,
                LogoUrl = "/mock-data-local/sponsors/mock-sponsor.svg",
                Members = [ToPublicUser(currentUser), PublicUserDTO.FromUser(teammate)],
                TeamInvites =
                [
                    new TeamInvite
                    {
                        Id = Guid.Parse("24111111-1111-1111-1111-111111111120"),
                        TeamId = captainedId,
                        UserId = Guid.Parse("41111111-1111-1111-1111-111111111119"),
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    }
                ]
            });
        }

        var fallbackCaptain = _document.Users.FirstOrDefault(user => user.Username == "binary1")
            ?? fallbackUsers.FirstOrDefault(user => user.Id != teammate?.Id)
            ?? teammate;
        if(_document.Teams.All(team => team.Id != memberId) && fallbackCaptain is not null)
        {
            AddOrReplaceTeam(new Team
            {
                Id = memberId,
                Name = "Roster Lock",
                CaptainUserId = fallbackCaptain.Id,
                Members = [PublicUserDTO.FromUser(fallbackCaptain), ToPublicUser(currentUser)],
                TeamInvites = []
            });
        }

        var inviteCaptain = _document.Users.FirstOrDefault(user => user.Username == "gamma1")
            ?? fallbackCaptain;
        if(_document.Teams.All(team => team.Id != inviteId) && inviteCaptain is not null)
        {
            AddOrReplaceTeam(new Team
            {
                Id = inviteId,
                Name = "Pending Pixels",
                CaptainUserId = inviteCaptain.Id,
                Members = [PublicUserDTO.FromUser(inviteCaptain)],
                TeamInvites =
                [
                    new TeamInvite
                    {
                        Id = Guid.Parse("24111111-1111-1111-1111-111111111121"),
                        TeamId = inviteId,
                        UserId = currentUser.Id,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow.AddHours(-8)
                    }
                ]
            });
        }

        return _document.Teams;
    }

    private TeamManagementSummaryDTO ToManagementSummary(Team team)
    {
        var captainUsername = team.Members.FirstOrDefault(member => member.Id == team.CaptainUserId)?.Username
            ?? _document.Users.FirstOrDefault(user => user.Id == team.CaptainUserId)?.Username;

        return new TeamManagementSummaryDTO
        {
            Id = team.Id,
            Name = team.Name,
            CaptainUserId = team.CaptainUserId,
            CaptainUsername = captainUsername,
            LogoUrl = team.LogoUrl,
            Members = team.Members.ToList()
        };
    }

    private TeamInviteSummaryDTO ToInviteSummary(Team team, TeamInvite invite)
    {
        EnsureInviteDates(invite);
        var user = _document.Users.FirstOrDefault(candidate => candidate.Id == invite.UserId);

        return new TeamInviteSummaryDTO
        {
            Id = invite.Id,
            TeamId = team.Id,
            TeamName = team.Name,
            TeamLogoUrl = team.LogoUrl,
            UserId = invite.UserId,
            Username = user?.Username,
            Status = invite.Status,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt!.Value
        };
    }

    private static void EnsureInviteDates(TeamInvite invite)
    {
        if(invite.CreatedAt == default)
            invite.CreatedAt = DateTime.UtcNow;

        invite.ExpiresAt ??= invite.CreatedAt.AddDays(7);
    }

    private static bool IsPending(TeamInvite invite)
    {
        return string.Equals(invite.Status, "Pending", StringComparison.OrdinalIgnoreCase);
    }

    private Tournament ToTournament(TournamentExtended tournament)
    {
        return new Tournament
        {
            Id = tournament.Id,
            Name = tournament.Name,
            StartTime = tournament.StartTime,
            EndTime = tournament.EndTime,
            PlannedStartTime = tournament.PlannedStartTime,
            AverageGameDurationMinutes = tournament.AverageGameDurationMinutes,
            RoundBreakDurationMinutes = tournament.RoundBreakDurationMinutes,
            EstimatedEndTime = tournament.EstimatedEndTime,
            ImageUrl = tournament.ImageUrl,
            Status = tournament.Status,
            BracketType = tournament.BracketType,
            Format = tournament.Format,
            FinalsFormat = tournament.FinalsFormat,
            ParticipationMode = tournament.ParticipationMode,
            TeamSize = tournament.TeamSize,
            FirstPlacePrize = tournament.FirstPlacePrize,
            SecondPlacePrize = tournament.SecondPlacePrize,
            ThirdPlacePrize = tournament.ThirdPlacePrize,
            ContactAdmin = ResolveContactAdmin(tournament.AssignedAdminUserId)
        };
    }

    private static string NormalizePersona(string? persona)
    {
        if(string.IsNullOrWhiteSpace(persona))
            return "user";

        return persona.Trim().ToLowerInvariant() switch
        {
            "admin" => "admin",
            "anonymous" => "anonymous",
            _ => "user"
        };
    }

    private static string BuildDisplayName(string? firstname, string? lastname, string? username)
    {
        var fullName = string.Join(" ", new[] { firstname, lastname }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(fullName)
            ? username ?? "Mock User"
            : fullName;
    }

    private static PublicUserDTO ToPublicUser(UserProfileDTO user)
    {
        return PublicUserDTO.FromUser(user);
    }

    private static PublicUserDTO ToPublicUser(UserDTO user)
    {
        return PublicUserDTO.FromUser(user);
    }

    private static UserDTO ToUserDto(UserProfileDTO user)
    {
        return new UserDTO
        {
            Id = user.Id,
            Username = user.Username,
            Firstname = user.Firstname,
            Lastname = user.Lastname,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            DiscordId = user.DiscordId,
            SteamId = user.SteamId,
            RiotId = user.RiotId,
            DisplayName = user.DisplayName,
            IsDeleted = user.IsDeleted,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc
        };
    }

    private sealed record MockProfileMatchCandidate
    {
        public Guid MatchId { get; init; }
        public Guid TournamentId { get; init; }
        public string TournamentName { get; init; } = string.Empty;
        public ParticipationMode ParticipationMode { get; init; }
        public Guid? Participant1Id { get; init; }
        public Guid? Participant2Id { get; init; }
        public bool SubjectIsParticipant1 { get; init; }
        public bool Participant1IsBYE { get; init; }
        public bool Participant2IsBYE { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public DateTime? EstimatedStartTime { get; init; }
        public DateTime? EstimatedEndTime { get; init; }
        public DateTime? ResultRecordedAtUtc { get; init; }
        public MatchLifecycleState LifecycleState { get; init; }
        public MatchResultKind? ResultKind { get; init; }
        public int? Participant1Score { get; init; }
        public int? Participant2Score { get; init; }
        public int RoundNumber { get; init; }
        public int MatchNumber { get; init; }
        public bool IsLowerBracketMatch { get; init; }
        public string? OpponentDisplayName { get; init; }
    }

    private sealed record MockRegistrationSnapshot(Guid? ParticipantId, string? DisplayName);

    private sealed class MockLeaderboardParticipant
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public LeaderboardParticipantKind Kind { get; init; }
        public Guid? LinkedUserId { get; init; }
        public List<MockLeaderboardAttempt> Attempts { get; } = [];
    }

    private sealed class MockLeaderboardAttempt
    {
        public Guid Id { get; init; }
        public decimal? Score { get; set; }
        public long? DurationMilliseconds { get; set; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; set; }
        public Guid RowVersion { get; set; }
    }

    private static T? Clone<T>(T? value)
    {
        if(value == null)
            return default;

        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, SerializerOptions), SerializerOptions);
    }
}
