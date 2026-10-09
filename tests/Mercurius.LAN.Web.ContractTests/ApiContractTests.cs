using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.Extensions.Configuration;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ApiContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("https://api.example.test", "https://api.example.test/")]
    [InlineData("https://api.example.test/", "https://api.example.test/")]
    [InlineData("https://api.example.test/v1", "https://api.example.test/")]
    [InlineData("https://api.example.test/v1/", "https://api.example.test/")]
    public void BuildApiBaseAddress_NormalizesToApiRoot(string configuredAddress, string expectedAddress)
    {
        Assert.Equal(expectedAddress, DependencyExtensions.BuildApiBaseAddress(configuredAddress));
    }

    [Fact]
    public void AssetUrlResolver_UsesApiRootForVersionedBaseAddress()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercuriusAPI:BaseAddress"] = "https://api.example.test/v1/"
            })
            .Build();

        Assert.Equal(
            "https://api.example.test/images/team.png",
            AssetUrlResolver.Resolve(configuration, "images/team.png"));
    }

    [Fact]
    public void TeamAssetUrlResolver_ResolvesNestedRegistrationTeamLogos()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercuriusAPI:BaseAddress"] = "https://api.example.test/v1/"
            })
            .Build();
        var tournament = new TournamentExtended
        {
            Registrations =
            [
                new PublicTournamentRegistrationDTO
                {
                    Kind = TournamentRegistrationKind.Team,
                    Status = TournamentRegistrationStatus.Active,
                    Team = new PublicTournamentTeamDTO { LogoUrl = "images/nested-team.png" }
                }
            ]
        };

        TeamAssetUrlResolver.Resolve(configuration, tournament);

        Assert.Equal(
            "https://api.example.test/images/nested-team.png",
            Assert.Single(tournament.Registrations).Team!.LogoUrl);
    }

    [Fact]
    public async Task CreateTeamInvite_UsesCurrentRouteAndRequestBody()
    {
        var teamId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var handler = new RecordingHandler("{\"id\":\"33333333-3333-3333-3333-333333333333\",\"teamId\":\"11111111-1111-1111-1111-111111111111\",\"userId\":\"22222222-2222-2222-2222-222222222222\",\"status\":\"Pending\",\"createdAt\":\"2026-08-28T12:00:00Z\",\"expiresAt\":\"2026-09-04T12:00:00Z\"}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        await client.CreateTeamInviteAsync(teamId, new CreateTeamInviteRequestDTO { UserId = userId });

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/teams/{teamId}/invites", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(userId.ToString(), body.RootElement.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task LifecycleAndProfileCompletion_UsePutRoutesAndCurrentBodies()
    {
        var tournamentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var lifecycleHandler = new RecordingHandler("[{\"placement\":1}]");
        using var lifecycleHttpClient = CreateHttpClient(lifecycleHandler);
        var lanClient = RestService.For<ILANClient>(lifecycleHttpClient, CreateRefitSettings());

        using var response = await lanClient.SetTournamentLifecycleStateAsync(
            tournamentId,
            new UpdateTournamentLifecycleStateRequestDTO { State = TournamentStatus.Completed });

        Assert.Equal(HttpMethod.Put, lifecycleHandler.Request!.Method);
        Assert.Equal($"/v1/lan/tournaments/{tournamentId}/lifecycle-state", lifecycleHandler.Request.RequestUri!.AbsolutePath);
        using var lifecycleBody = JsonDocument.Parse(lifecycleHandler.RequestBody!);
        Assert.Equal("Completed", lifecycleBody.RootElement.GetProperty("state").GetString());
        Assert.Null(response.Error);

        lifecycleHandler.ResponseBody = "";
        using var emptyBodyResponse = await lanClient.SetTournamentLifecycleStateAsync(
            tournamentId,
            new UpdateTournamentLifecycleStateRequestDTO { State = TournamentStatus.InProgress });

        Assert.Null(emptyBodyResponse.Error);

        var profileHandler = new RecordingHandler("{\"isComplete\":true,\"user\":null,\"email\":null,\"emailVerified\":false}");
        using var profileHttpClient = CreateHttpClient(profileHandler);
        var userClient = RestService.For<IUserClient>(profileHttpClient, CreateRefitSettings());

        await userClient.CompleteCurrentUserProfileAsync(new CompleteUserProfileRequest
        {
            Username = "testuser",
            Firstname = "Test",
            Lastname = "User"
        });

        Assert.Equal(HttpMethod.Put, profileHandler.Request!.Method);
        Assert.Equal("/v1/lan/users/me", profileHandler.Request.RequestUri!.AbsolutePath);
        using var profileBody = JsonDocument.Parse(profileHandler.RequestBody!);
        Assert.Equal("testuser", profileBody.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task LifecycleServicePreservesBackendProblemDetails()
    {
        const string message = "Tournament has to be scheduled before it can be started.";
        var handler = new RecordingHandler($"{{\"message\":\"{message}\"}}", HttpStatusCode.BadRequest);
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());
        var service = new TournamentService(client, new ConfigurationBuilder().Build());

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.SetTournamentLifecycleStateAsync(Guid.NewGuid(), TournamentStatus.InProgress));

        Assert.Equal(message, exception.GetApiError()?.Message);
    }

    [Fact]
    public async Task RegistrationRosterAndMatchRoutes_KeepTournamentResourceNames()
    {
        var tournamentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var teamId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var handler = new RecordingHandler("{\"eligible\":true,\"reasonCodes\":[],\"candidates\":[]}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        await client.CheckTeamRosterEligibilityAsync(
            tournamentId,
            teamId,
            new SubmitTeamRosterDTO { TeamId = teamId, UserIds = [teamId] });

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/roster/eligibility", handler.Request.RequestUri!.AbsolutePath);
        using var rosterBody = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(teamId.ToString(), rosterBody.RootElement.GetProperty("teamId").GetString());

        var matchId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        handler.ResponseBody = "{\"id\":\"77777777-7777-7777-7777-777777777777\",\"tournamentId\":\"55555555-5555-5555-5555-555555555555\"}";
        await client.GetMatchByIdAsync(matchId);

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}", handler.Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MatchLifecycleRoutes_UseExplicitParticipantAndAdminActions()
    {
        var matchId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var handler = new RecordingHandler($"{{\"match\":{{\"id\":\"{matchId}\",\"lifecycleState\":\"AwaitingScore\"}},\"authorizedParticipant\":\"Participant1\",\"canConfirmEnded\":false,\"canSubmitScore\":true,\"canForfeit\":true,\"canResolve\":true,\"canForceForfeit\":true,\"canReverse\":true}}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        var actionState = await client.GetMatchActionStateAsync(matchId);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/me", handler.Request.RequestUri!.AbsolutePath);
        Assert.True(actionState.CanResolve);
        Assert.True(actionState.CanForceForfeit);
        Assert.True(actionState.CanReverse);

        await client.ConfirmMatchEndedAsync(matchId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/confirm-ended", handler.Request.RequestUri!.AbsolutePath);

        await client.SubmitMatchScoreAsync(matchId, new SubmitMatchScoreDTO { Participant1Score = 2, Participant2Score = 1 });
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/score", handler.Request.RequestUri!.AbsolutePath);

        await client.ForfeitMatchAsync(matchId, new ForfeitMatchDTO { Participant = MatchParticipantSide.Participant1 });
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/forfeit", handler.Request.RequestUri!.AbsolutePath);

        await client.ResolveMatchAsync(matchId, new ResolveMatchDTO { Participant1Score = 2, Participant2Score = 0 });
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/resolve", handler.Request.RequestUri!.AbsolutePath);

        await client.ReverseMatchAsync(matchId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/v1/lan/matches/{matchId}/reverse", handler.Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UserMatchSummariesUseAdminRouteAndTeamSummariesRemainPublic()
    {
        var matchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tournamentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var handler = new RecordingHandler($"{{\"previousMatches\":[{{\"matchId\":\"{matchId}\",\"tournamentId\":\"{tournamentId}\",\"tournamentName\":\"LAN Cup\",\"opponentDisplayName\":\"Rival\",\"opponentIsTbd\":false,\"scheduledStartTime\":\"2026-09-01T10:00:00Z\",\"lifecycleState\":\"Completed\",\"participantScore\":2,\"opponentScore\":1,\"roundNumber\":1,\"matchNumber\":1}}],\"upcomingMatches\":[]}}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        var userClient = RestService.For<IUserClient>(httpClient, CreateRefitSettings());
        var userSummaries = await userClient.GetUserMatchSummariesAsync("public-user");

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/v1/lan/users/public-user/match-summaries", handler.Request.RequestUri!.AbsolutePath);
        var previous = Assert.Single(userSummaries.PreviousMatches);
        Assert.Equal(matchId, previous.MatchId);
        Assert.Equal(tournamentId, previous.TournamentId);

        handler.ResponseBody = "{\"previousMatches\":[],\"upcomingMatches\":[]}";
        var teamSummaries = await client.GetPublicTeamMatchSummariesAsync("public team");

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/v1/lan/public/teams/public%20team/match-summaries", handler.Request.RequestUri!.AbsolutePath);
        Assert.Empty(teamSummaries.PreviousMatches);
        Assert.Empty(teamSummaries.UpcomingMatches);
    }

    [Fact]
    public async Task TournamentDetail_DeserializesTeamSizeRegistrationsAndTournamentMatchId()
    {
        var tournamentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var matchId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new RecordingHandler($"{{\"id\":\"{tournamentId}\",\"name\":\"LAN Cup\",\"teamSize\":5,\"matches\":[{{\"id\":\"{matchId}\",\"tournamentId\":\"{tournamentId}\"}}],\"registrations\":[{{\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"tournamentId\":\"{tournamentId}\",\"kind\":\"Individual\",\"status\":\"Active\",\"rosterMembers\":[]}}]}}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        var tournament = await client.GetTournamentByIdAsync(tournamentId);

        Assert.NotNull(tournament);
        Assert.Equal(5, tournament!.TeamSize);
        Assert.Equal(tournamentId, tournament.Matches.Single().TournamentId);
        Assert.Equal(TournamentRegistrationKind.Individual, tournament.Registrations.Single().Kind);
    }

    [Fact]
    public async Task CurrentUserRegistrationState_DeserializesCurrentTeamRegistrationContext()
    {
        var tournamentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var teamId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var registrationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var handler = new RecordingHandler($"{{\"tournamentId\":\"{tournamentId}\",\"currentTeamRegistration\":{{\"id\":\"{registrationId}\",\"tournamentId\":\"{tournamentId}\",\"kind\":\"Team\",\"status\":\"PendingConfirmation\",\"team\":{{\"id\":\"{teamId}\",\"name\":\"Context Crew\",\"captainUserId\":\"{teamId}\",\"members\":[]}},\"rosterMembers\":[]}},\"activeTeamRegistration\":null,\"captainManagedRegistrations\":[],\"canRegisterIndividual\":false,\"canConfirmRoster\":false,\"canUnregister\":false}}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        var state = await client.GetCurrentUserTournamentRegistrationStateAsync(tournamentId);

        Assert.Equal($"/v1/lan/tournaments/{tournamentId}/registrations/me", handler.Request!.RequestUri!.AbsolutePath);
        var currentTeam = state.CurrentTeamRegistration;
        Assert.NotNull(currentTeam);
        Assert.Equal(registrationId, currentTeam.Id);
        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, currentTeam.Status);
        Assert.Equal(teamId, currentTeam.Team!.Id);
        Assert.Null(state.ActiveTeamRegistration);
    }

    [Fact]
    public async Task AdminUserList_UsesDedicatedAdminsEndpoint()
    {
        var adminId = Guid.Parse("41111111-1111-1111-1111-111111111121");
        var handler = new RecordingHandler($"[{{\"id\":\"{adminId}\",\"username\":\"mockadmin\",\"displayName\":\"Mock Admin\"}}]");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        var admins = await client.GetAdminUsersAsync("mock", 25);

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/v1/lan/users/admins", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("query=mock", handler.Request.RequestUri!.Query);
        Assert.Contains("pageSize=25", handler.Request.RequestUri!.Query);
        Assert.Contains("page=1", handler.Request.RequestUri!.Query);
        Assert.Equal(adminId, Assert.Single(admins).Id);
    }

    [Fact]
    public async Task AdminUserList_OmitsQueryFilterAndForwardsRequestedPage()
    {
        var handler = new RecordingHandler("[]");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());

        await client.GetAdminUsersAsync(null, 50, default, 2);

        Assert.Equal("/v1/lan/users/admins", handler.Request!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("query=", handler.Request.RequestUri!.Query);
        Assert.Contains("pageSize=50", handler.Request.RequestUri!.Query);
        Assert.Contains("page=2", handler.Request.RequestUri!.Query);
    }

    [Fact]
    public async Task AllAdminUsers_PagesUntilTheLastShortPageWithoutQueryFilter()
    {
        var handler = new PagedAdminHandler(totalAdmins: 52);
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());
        var service = new TournamentService(client, new ConfigurationBuilder().Build());

        var admins = await service.GetAllAdminUsersAsync();

        Assert.Equal(52, admins.Count);
        Assert.Equal(52, admins.Select(admin => admin.Id).Distinct().Count());
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.DoesNotContain("query=", request.RequestUri!.Query));
        Assert.Contains("page=1", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("pageSize=50", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task AllAdminUsers_PropagatesAFailedPageInsteadOfReturningPartialChoices()
    {
        var handler = new PagedAdminHandler(totalAdmins: 52, failFromPage: 2);
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());
        var service = new TournamentService(client, new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<ApiException>(() => service.GetAllAdminUsersAsync());

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task TournamentWrites_SendContactAdminAndPrizesWithExplicitClearValues()
    {
        var adminId = Guid.Parse("41111111-1111-1111-1111-111111111121");
        var handler = new RecordingHandler("{\"id\":\"11111111-1111-1111-1111-111111111112\",\"name\":\"Prize Cup\"}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<ILANClient>(httpClient, CreateRefitSettings());
        var service = new TournamentService(client, new ConfigurationBuilder().Build());

        await service.CreateTournamentAsync(new CreateTournamentDTO
        {
            Name = "Prize Cup",
            BracketType = BracketType.SingleElimination,
            Format = TournamentFormat.BestOf1,
            FinalsFormat = TournamentFormat.BestOf1,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10,
            AssignedAdminUserId = adminId,
            FirstPlacePrize = "  Keyboard  ",
            SecondPlacePrize = "Mouse",
            ThirdPlacePrize = null
        }, null, null, null);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/v1/lan/tournaments", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("AssignedAdminUserId", handler.RequestBody!);
        Assert.Contains(adminId.ToString(), handler.RequestBody!);
        Assert.Contains("FirstPlacePrize", handler.RequestBody!);
        Assert.Contains("Keyboard", handler.RequestBody!);
        Assert.Contains("ThirdPlacePrize", handler.RequestBody!);

        await service.UpdateTournamentAsync(Guid.Parse("11111111-1111-1111-1111-111111111112"), new UpdateTournamentDTO
        {
            Name = "Prize Cup",
            BracketType = BracketType.SingleElimination,
            Format = TournamentFormat.BestOf1,
            FinalsFormat = TournamentFormat.BestOf1,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10,
            AssignedAdminUserId = null,
            FirstPlacePrize = null,
            SecondPlacePrize = null,
            ThirdPlacePrize = null
        }, null, null, null);

        Assert.Equal(HttpMethod.Patch, handler.Request!.Method);
        // The update contract clears a previously set value only through an explicit empty field.
        Assert.Contains("AssignedAdminUserId", handler.RequestBody!);
        Assert.Contains("FirstPlacePrize", handler.RequestBody!);
        Assert.Contains("SecondPlacePrize", handler.RequestBody!);
        Assert.Contains("ThirdPlacePrize", handler.RequestBody!);
        Assert.DoesNotContain(adminId.ToString(), handler.RequestBody!);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
    }

    private static RefitSettings CreateRefitSettings() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonOptions)
    };

    private sealed class RecordingHandler(
        string responseBody = "{}",
        HttpStatusCode responseStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        public string ResponseBody { get; set; } = responseBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(responseStatus)
            {
                RequestMessage = request,
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class PagedAdminHandler(int totalAdmins, int? failFromPage = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            var parameters = request.RequestUri!.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(part => part[0], part => part.Length > 1 ? part[1] : string.Empty);
            var page = parameters.TryGetValue("page", out var pageValue) && int.TryParse(pageValue, out var parsedPage)
                ? parsedPage
                : 1;
            var pageSize = parameters.TryGetValue("pageSize", out var sizeValue) && int.TryParse(sizeValue, out var parsedSize)
                ? parsedSize
                : 20;

            if(failFromPage is { } failedPage && page >= failedPage)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    RequestMessage = request,
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });

            var skip = (page - 1) * pageSize;
            var take = Math.Clamp(totalAdmins - skip, 0, pageSize);
            var payload = "[" + string.Join(",", Enumerable.Range(skip, take).Select(index =>
                $"{{\"id\":\"41111111-1111-1111-1111-{index:D12}\",\"username\":\"admin{index:D3}\",\"displayName\":\"Admin {index:D3}\"}}")) + "]";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
