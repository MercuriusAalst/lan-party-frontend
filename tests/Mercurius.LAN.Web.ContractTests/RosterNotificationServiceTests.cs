using System.Reflection;
using System.Text.Json;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class RosterNotificationServiceTests
{
    [Fact]
    public void RegistrationSurfacePrioritizesSelectedMemberDecisionOverCaptainWizard()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor");
        var invitationIndex = markup.IndexOf("else if(_registrationState?.PendingRosterConfirmation is not null)", StringComparison.Ordinal);
        var wizardIndex = markup.IndexOf("else if(IsRegistrationOpen)", invitationIndex, StringComparison.Ordinal);

        Assert.True(invitationIndex >= 0);
        Assert.True(wizardIndex > invitationIndex);
        Assert.Contains("DeclineRosterMemberAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.acceptRosterPlace", markup, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.declineRosterPlace", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TeamWizardKeepsSelectionUntilTheLocalizedNextStepIsChosen()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor");
        var code = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor.cs");
        var start = code.IndexOf("private async Task SelectTeamAsync", StringComparison.Ordinal);
        var end = code.IndexOf("private async Task HandleTeamChangedAsync", start, StringComparison.Ordinal);
        var selectTeam = code[start..end];

        Assert.DoesNotContain("_activeTeamStep", selectTeam, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.selectedTeamAria", code, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.chooseRoster", markup, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.editRoster", markup, StringComparison.Ordinal);
        Assert.Contains("Feature.tournaments.reviewRoster", markup, StringComparison.Ordinal);

        using var english = JsonDocument.Parse(ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json"));
        using var dutch = JsonDocument.Parse(ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.nl-BE.json"));
        foreach(var key in new[]
        {
            "Feature.tournaments.chooseRoster",
            "Feature.tournaments.editRoster",
            "Feature.tournaments.editTeamRosterFor",
            "Feature.tournaments.reviewRoster",
            "Feature.tournaments.selectedTeamAria"
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(english.RootElement.GetProperty(key).GetString()), $"English key '{key}' is empty.");
            Assert.False(string.IsNullOrWhiteSpace(dutch.RootElement.GetProperty(key).GetString()), $"Dutch key '{key}' is empty.");
        }
    }

    [Fact]
    public void RosterStepDerivesCandidatesAndSelectionContextOncePerRender()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor");

        Assert.Contains("var rosterCandidates = EditableRosterCandidates;", markup, StringComparison.Ordinal);
        Assert.Contains("var rosterSelectionContext = GetRosterSelectionContext();", markup, StringComparison.Ordinal);
        Assert.Contains("CanSelectRosterMember(member.Id, rosterSelectionContext)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach(var member in EditableRosterCandidates)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CanSelectRosterMember(member.Id)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("EditableRosterCandidates.Where(", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("!CanSubmitRoster ", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("!CanAdvanceTeamStep ", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineUnregistrationConfirmationMovesFocusIntoAndBackOutOfThePrompt()
    {
        var code = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor.cs");

        Assert.Contains("_focusTeamUnregistrationConfirmation = true", code, StringComparison.Ordinal);
        Assert.Contains("await _teamUnregistrationKeepButtonElement.FocusAsync()", code, StringComparison.Ordinal);
        Assert.Contains("await _teamUnregistrationTriggerElement.FocusAsync()", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshMergesTeamInvitesAndRosterSelectionsWithDistinctStableIds()
    {
        var inviteId = Guid.NewGuid();
        var rosterMemberId = Guid.NewGuid();
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name switch
        {
            nameof(ITeamService.GetCurrentUserTeamSummaryAsync) => Task.FromResult(new CurrentUserTeamSummaryDTO
            {
                ReceivedPendingInvites = [new TeamInviteSummaryDTO
                {
                    Id = inviteId,
                    TeamId = Guid.NewGuid(),
                    TeamName = "Inviters",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2)
                }]
            }),
            _ => throw new NotSupportedException(method.Name)
        });
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>
            ([new PendingRosterConfirmationDTO
            {
                RosterMemberId = rosterMemberId,
                TournamentId = Guid.NewGuid(),
                TournamentName = "Finals",
                TeamId = Guid.NewGuid(),
                TeamName = "Selected",
                SelectedAtUtc = DateTime.UtcNow
            }]),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);

        await service.RefreshAsync();

        Assert.Equal(2, service.Notifications.Count);
        Assert.Contains(service.Notifications, item => item.Id == $"team-invite:{inviteId:N}" && item.Kind == TeamNotificationKind.TeamInvite);
        Assert.Contains(service.Notifications, item => item.Id == $"roster-selection:{rosterMemberId:N}" && item.Kind == TeamNotificationKind.RosterSelection);
    }

    [Fact]
    public async Task RefreshAppliesRosterUpdatesWhenTeamInvitesAreUnavailable()
    {
        var invite = new TeamInviteSummaryDTO
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            TeamName = "Inviters",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var initialRoster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Qualifiers",
            TeamId = Guid.NewGuid(),
            TeamName = "Initial",
            SelectedAtUtc = DateTime.UtcNow.AddMinutes(-3)
        };
        var refreshedRoster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Refreshed",
            SelectedAtUtc = DateTime.UtcNow
        };
        var teamCalls = 0;
        var rosterCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name switch
        {
            nameof(ITeamService.GetCurrentUserTeamSummaryAsync) => ++teamCalls == 1
                ? Task.FromResult(new CurrentUserTeamSummaryDTO { ReceivedPendingInvites = [invite] })
                : Task.FromException<CurrentUserTeamSummaryDTO>(new InvalidOperationException("team source unavailable")),
            _ => throw new NotSupportedException(method.Name)
        });
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => ++rosterCalls == 1
                ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([initialRoster])
                : Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([refreshedRoster]),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);

        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Contains(service.Notifications, item => item.Id == $"team-invite:{invite.Id:N}");
        Assert.Contains(service.Notifications, item => item.Id == $"roster-selection:{refreshedRoster.RosterMemberId:N}");
        Assert.DoesNotContain(service.Notifications, item => item.Id == $"roster-selection:{initialRoster.RosterMemberId:N}");
    }

    [Fact]
    public async Task RefreshAppliesTeamInviteUpdatesWhenRosterSelectionsAreUnavailable()
    {
        var initialInvite = new TeamInviteSummaryDTO
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            TeamName = "Initial",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        };
        var refreshedInvite = new TeamInviteSummaryDTO
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            TeamName = "Refreshed",
            CreatedAt = DateTime.UtcNow
        };
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        var teamCalls = 0;
        var rosterCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name switch
        {
            nameof(ITeamService.GetCurrentUserTeamSummaryAsync) => ++teamCalls == 1
                ? Task.FromResult(new CurrentUserTeamSummaryDTO { ReceivedPendingInvites = [initialInvite] })
                : Task.FromResult(new CurrentUserTeamSummaryDTO { ReceivedPendingInvites = [refreshedInvite] }),
            _ => throw new NotSupportedException(method.Name)
        });
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => ++rosterCalls == 1
                ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([roster])
                : Task.FromException<IReadOnlyList<PendingRosterConfirmationDTO>>(new InvalidOperationException("roster source unavailable")),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);

        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Contains(service.Notifications, item => item.Id == $"team-invite:{refreshedInvite.Id:N}");
        Assert.DoesNotContain(service.Notifications, item => item.Id == $"team-invite:{initialInvite.Id:N}");
        Assert.Contains(service.Notifications, item => item.Id == $"roster-selection:{roster.RosterMemberId:N}");
    }

    [Fact]
    public async Task RefreshSortsSuccessfulSourcesTogetherByLatestActivity()
    {
        var invite = new TeamInviteSummaryDTO
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            TeamName = "Inviters",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? Task.FromResult(new CurrentUserTeamSummaryDTO { ReceivedPendingInvites = [invite] })
            : throw new NotSupportedException(method.Name));
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name == nameof(ITournamentService.GetPendingRosterConfirmationsAsync)
            ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([roster])
            : throw new NotSupportedException(method.Name));
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);

        await service.RefreshAsync();

        Assert.Equal($"roster-selection:{roster.RosterMemberId:N}", service.Notifications[0].Id);
        Assert.Equal($"team-invite:{invite.Id:N}", service.Notifications[1].Id);
    }

    [Fact]
    public async Task RefreshDoesNotMutateAnAlreadyPublishedNotificationSnapshot()
    {
        var invite = new TeamInviteSummaryDTO
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            TeamName = "Inviters",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var rosterLoads = 0;
        var releaseRoster = new TaskCompletionSource();
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? Task.FromResult(new CurrentUserTeamSummaryDTO { ReceivedPendingInvites = [invite] })
            : throw new NotSupportedException(method.Name));
        var tournamentService = CreateProxy<ITournamentService>((method, _) =>
        {
            if(method.Name != nameof(ITournamentService.GetPendingRosterConfirmationsAsync))
                throw new NotSupportedException(method.Name);

            return ++rosterLoads == 1
                ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([])
                : BlockedRosterLoad();

            async Task<IReadOnlyList<PendingRosterConfirmationDTO>> BlockedRosterLoad()
            {
                await releaseRoster.Task;
                return [roster];
            }
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        await service.RefreshAsync();

        // The list handed to the UI is captured while the next refresh is still loading both sources.
        var published = service.Notifications;
        var inFlight = service.RefreshAsync();
        await service.DismissAsync($"team-invite:{invite.Id:N}");
        releaseRoster.TrySetResult();
        await inFlight;

        Assert.DoesNotContain(published, item => item.Id == $"roster-selection:{roster.RosterMemberId:N}");
        Assert.Contains(service.Notifications, item => item.Id == $"roster-selection:{roster.RosterMemberId:N}");
        Assert.DoesNotContain(service.Notifications, item => item.Id == $"team-invite:{invite.Id:N}");
    }

    [Fact]
    public async Task RosterDeclineUsesRosterRouteThenRefreshesWithoutRemovingTeamInvite()
    {
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var pending = new List<PendingRosterConfirmationDTO> { roster };
        var declineCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name switch
        {
            nameof(ITeamService.GetCurrentUserTeamSummaryAsync) => Task.FromResult(new CurrentUserTeamSummaryDTO
            {
                ReceivedPendingInvites = [new TeamInviteSummaryDTO
                {
                    Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), TeamName = "Inviters", CreatedAt = DateTime.UtcNow
                }]
            }),
            _ => throw new NotSupportedException(method.Name)
        });
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>(pending.ToList()),
            nameof(ITournamentService.DeclineTournamentRosterMemberAsync) => Decline(),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        await service.RefreshAsync();
        var notification = Assert.Single(service.Notifications.Where(item => item.Kind == TeamNotificationKind.RosterSelection));

        await service.RespondToRosterSelectionAsync(notification.Id, accept: false);

        Assert.Equal(1, declineCalls);
        Assert.DoesNotContain(service.Notifications, item => item.Kind == TeamNotificationKind.RosterSelection);
        Assert.Contains(service.Notifications, item => item.Kind == TeamNotificationKind.TeamInvite);

        Task Decline()
        {
            declineCalls++;
            pending.Clear();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RosterAcceptUsesExistingConfirmationMutationThenRefreshes()
    {
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var pending = new List<PendingRosterConfirmationDTO> { roster };
        var confirmCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? Task.FromResult(new CurrentUserTeamSummaryDTO())
            : throw new NotSupportedException(method.Name));
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>(pending.ToList()),
            nameof(ITournamentService.ConfirmTournamentRosterMemberAsync) => Confirm(),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        await service.RefreshAsync();
        var notification = Assert.Single(service.Notifications);

        await service.RespondToRosterSelectionAsync(notification.Id, accept: true);

        Assert.Equal(1, confirmCalls);
        Assert.Empty(service.Notifications);

        Task<TournamentRegistrationDTO> Confirm()
        {
            confirmCalls++;
            pending.Clear();
            return Task.FromResult(new TournamentRegistrationDTO());
        }
    }

    [Fact]
    public async Task RosterDecisionChangedIdentifiesTournamentAfterBellMutation()
    {
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var pending = new List<PendingRosterConfirmationDTO> { roster };
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? Task.FromResult(new CurrentUserTeamSummaryDTO())
            : throw new NotSupportedException(method.Name));
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>(pending.ToList()),
            nameof(ITournamentService.ConfirmTournamentRosterMemberAsync) => Confirm(),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        var changedTournamentId = Guid.Empty;
        service.RosterDecisionChanged += tournamentId =>
        {
            changedTournamentId = tournamentId;
            return Task.CompletedTask;
        };

        await service.RefreshAsync();
        var notification = Assert.Single(service.Notifications);

        await service.RespondToRosterSelectionAsync(notification.Id, accept: true);

        Assert.Equal(roster.TournamentId, changedTournamentId);

        Task<TournamentRegistrationDTO> Confirm()
        {
            pending.Clear();
            return Task.FromResult(new TournamentRegistrationDTO());
        }
    }

    [Fact]
    public async Task SuccessfulRosterMutationRemainsSuccessfulWhenNotificationRefreshFails()
    {
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var teamCalls = 0;
        var rosterCalls = 0;
        var confirmCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name switch
        {
            nameof(ITeamService.GetCurrentUserTeamSummaryAsync) => ++teamCalls == 1
                ? Task.FromResult(new CurrentUserTeamSummaryDTO())
                : Task.FromException<CurrentUserTeamSummaryDTO>(new InvalidOperationException("team source unavailable")),
            _ => throw new NotSupportedException(method.Name)
        });
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => ++rosterCalls == 1
                ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([roster])
                : Task.FromException<IReadOnlyList<PendingRosterConfirmationDTO>>(new InvalidOperationException("roster source unavailable")),
            nameof(ITournamentService.ConfirmTournamentRosterMemberAsync) => Confirm(),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        var changedTournamentId = Guid.Empty;
        service.RosterDecisionChanged += tournamentId =>
        {
            changedTournamentId = tournamentId;
            return Task.CompletedTask;
        };

        await service.RefreshAsync();
        var notification = Assert.Single(service.Notifications);

        await service.RespondToRosterSelectionAsync(notification.Id, accept: true);

        Assert.Equal(1, confirmCalls);
        Assert.Equal(roster.TournamentId, changedTournamentId);
        Assert.Contains(service.Notifications, item => item.Id == notification.Id);

        Task<TournamentRegistrationDTO> Confirm()
        {
            confirmCalls++;
            return Task.FromResult(new TournamentRegistrationDTO());
        }
    }

    [Fact]
    public async Task FailedRosterMutationPropagatesWithoutRaisingDecisionChanged()
    {
        var roster = new PendingRosterConfirmationDTO
        {
            RosterMemberId = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            TournamentName = "Finals",
            TeamId = Guid.NewGuid(),
            TeamName = "Selected",
            SelectedAtUtc = DateTime.UtcNow
        };
        var rosterCalls = 0;
        var teamService = CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? Task.FromResult(new CurrentUserTeamSummaryDTO())
            : throw new NotSupportedException(method.Name));
        var tournamentService = CreateProxy<ITournamentService>((method, _) => method.Name switch
        {
            nameof(ITournamentService.GetPendingRosterConfirmationsAsync) => ++rosterCalls == 1
                ? Task.FromResult<IReadOnlyList<PendingRosterConfirmationDTO>>([roster])
                : throw new InvalidOperationException("refresh should not run after mutation failure"),
            nameof(ITournamentService.ConfirmTournamentRosterMemberAsync) =>
                Task.FromException<TournamentRegistrationDTO>(new InvalidOperationException("mutation rejected")),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new TeamNotificationService(teamService, tournamentService, TestLocalizationService.Instance);
        var decisionChanged = false;
        service.RosterDecisionChanged += _ =>
        {
            decisionChanged = true;
            return Task.CompletedTask;
        };

        await service.RefreshAsync();
        var notification = Assert.Single(service.Notifications);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RespondToRosterSelectionAsync(notification.Id, accept: true));

        Assert.False(decisionChanged);
        Assert.Equal(1, rosterCalls);
        Assert.Contains(service.Notifications, item => item.Id == notification.Id);
    }

    [Fact]
    public async Task TournamentServiceLoadsEveryPendingRosterPage()
    {
        var requestedPages = new List<int>();
        var client = CreateProxy<ILANClient>((method, args) =>
        {
            if(method.Name != nameof(ILANClient.GetPendingRosterConfirmationsAsync))
                throw new NotSupportedException(method.Name);

            var page = (int)args![0]!;
            requestedPages.Add(page);
            return Task.FromResult(new PendingRosterConfirmationPageDTO
            {
                Page = page,
                PageSize = 50,
                TotalCount = 51,
                Items = page == 1
                    ? Enumerable.Range(0, 50).Select(_ => new PendingRosterConfirmationDTO { RosterMemberId = Guid.NewGuid() }).ToList()
                    : [new PendingRosterConfirmationDTO { RosterMemberId = Guid.NewGuid() }]
            });
        });
        var service = new TournamentService(client, new ConfigurationBuilder().Build());

        var result = await service.GetPendingRosterConfirmationsAsync();

        Assert.Equal(51, result.Count);
        Assert.Equal([1, 2], requestedPages);
    }

    private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)proxy).Handler = handler;
        return proxy;
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

        throw new InvalidOperationException($"Could not locate '{relativePath}'.");
    }

    private class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."), args);
    }
}
