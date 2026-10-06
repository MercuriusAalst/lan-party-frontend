using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentRegistrationWorkflowStateTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void SavedMutationRefreshFailureIsReportedOnlyForTheCurrentRoute(
        bool currentUserStateLoaded,
        bool isCurrentRoute,
        bool expected)
    {
        Assert.Equal(
            expected,
            TournamentParticipantsTab.ShouldReportSavedMutationRefreshFailure(
                currentUserStateLoaded,
                isCurrentRoute));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void DirtyRosterDraftIsKeptOnlyWhenItsTeamStillExists(
        bool hasDraftTeam,
        bool selectedTeamIsSame,
        bool expected)
    {
        Guid? draftTeamId = hasDraftTeam
            ? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            : null;
        Guid? selectedTeamId = hasDraftTeam && selectedTeamIsSame
            ? draftTeamId
            : hasDraftTeam
                ? Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                : null;

        Assert.Equal(
            expected,
            TournamentParticipantsTab.ShouldPreserveRosterDraftAfterRefresh(
                draftTeamId,
                selectedTeamId));
    }

    [Fact]
    public void CaptainedTeamOptionsRequireTheTournamentRosterSize()
    {
        var teamAtRequiredSize = new TeamManagementSummaryDTO
        {
            Id = Guid.NewGuid(),
            Members = Enumerable.Range(0, 3).Select(_ => new PublicUserDTO { Id = Guid.NewGuid() }).ToArray()
        };
        var largerTeam = new TeamManagementSummaryDTO
        {
            Id = Guid.NewGuid(),
            Members = Enumerable.Range(0, 4).Select(_ => new PublicUserDTO { Id = Guid.NewGuid() }).ToArray()
        };
        var undersizedTeam = new TeamManagementSummaryDTO
        {
            Id = Guid.NewGuid(),
            Members = Enumerable.Range(0, 2).Select(_ => new PublicUserDTO { Id = Guid.NewGuid() }).ToArray()
        };

        var options = TournamentParticipantsTab.FilterCaptainedTeamsByRequiredSize(
            [teamAtRequiredSize, undersizedTeam, largerTeam],
            requiredTeamSize: 3);

        Assert.Equal([teamAtRequiredSize.Id, largerTeam.Id], options.Select(team => team.Id));
        Assert.Empty(TournamentParticipantsTab.FilterCaptainedTeamsByRequiredSize(options, requiredTeamSize: 0));
    }

    [Fact]
    public void UndersizedCaptainManagedRegistrationRemainsCancellableBeforeSummary()
    {
        var hiddenTeamId = Guid.NewGuid();
        var hiddenTeam = new TeamManagementSummaryDTO
        {
            Id = hiddenTeamId,
            Members = Enumerable.Range(0, 2).Select(_ => new PublicUserDTO { Id = Guid.NewGuid() }).ToArray()
        };
        var registration = new TournamentRegistrationDTO
        {
            Team = new TeamParticipantDTO { Id = hiddenTeamId, Name = "Undersized" }
        };

        var visibleTeams = TournamentParticipantsTab.FilterCaptainedTeamsByRequiredSize([hiddenTeam], requiredTeamSize: 3);
        var managedRegistration = TournamentParticipantsTab.FindCaptainManagedRegistration([registration], [hiddenTeam]);

        Assert.Empty(visibleTeams);
        Assert.Same(registration, managedRegistration);
        Assert.Equal(
            hiddenTeamId,
            TournamentParticipantsTab.GetTeamCancellationTarget(
                activeStep: 1,
                selectedTeamId: null,
                canUnregisterSelectedTeam: false,
                captainManagedTeamId: managedRegistration?.Team?.Id,
                canUnregisterCaptainManagedTeam: true));
        Assert.Equal(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TournamentParticipantsTab.GetTeamCancellationTarget(
                activeStep: 2,
                selectedTeamId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                canUnregisterSelectedTeam: true,
                captainManagedTeamId: hiddenTeamId,
                canUnregisterCaptainManagedTeam: true));
    }

    [Theory]
    [InlineData(0, 1, false, true, true)]
    [InlineData(0, 2, false, true, false)]
    [InlineData(1, 2, false, false, false)]
    [InlineData(1, 2, false, true, true)]
    [InlineData(2, 0, false, false, true)]
    [InlineData(2, 1, true, false, false)]
    public void TeamStepNavigationRequiresValidAdjacentForwardSteps(
        int currentStep,
        int requestedStep,
        bool isBusy,
        bool canAdvanceCurrentStep,
        bool expected)
    {
        Assert.Equal(
            expected,
            TournamentParticipantsTab.CanNavigateTeamStep(
                currentStep,
                requestedStep,
                isBusy,
                canAdvanceCurrentStep));
    }

    [Fact]
    public void RequestStartedBeforeLeavingAndReturningToTournamentIsRejected()
    {
        var tournamentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.False(TournamentParticipantsTab.IsRequestCurrent(
            tournamentId,
            tournamentId,
            expectedRequestGeneration: 4,
            currentRequestGeneration: 6));
    }

    [Fact]
    public void RequestWithMatchingTournamentAndGenerationIsCurrent()
    {
        var tournamentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.True(TournamentParticipantsTab.IsRequestCurrent(
            tournamentId,
            tournamentId,
            expectedRequestGeneration: 4,
            currentRequestGeneration: 4));
    }

    [Fact]
    public void CandidateDiscoveryBatchesFiftyOneMembersWithoutExceedingBackendLimit()
    {
        var userIds = Enumerable.Range(1, 51)
            .Select(index => new Guid(index, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .ToArray();

        var batches = TournamentParticipantsTab.BatchRosterEligibilityUserIds(userIds);

        Assert.Equal(2, batches.Count);
        Assert.Equal(50, batches[0].Count);
        Assert.Single(batches[1]);
        Assert.Equal(userIds, batches.SelectMany(batch => batch).ToArray());
        Assert.All(batches, batch => Assert.InRange(batch.Count, 1, 50));
    }

    [Fact]
    public void EditableRosterCandidatesRetainDraftAndEligibilityOnlyUsers()
    {
        var currentTeamMember = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var draftOnlyMember = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var eligibilityOnlyMember = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var candidateIds = TournamentParticipantsTab.MergeEditableRosterCandidateIds(
            [currentTeamMember],
            [draftOnlyMember, currentTeamMember],
            [eligibilityOnlyMember, draftOnlyMember]);

        Assert.Equal(
            [currentTeamMember, draftOnlyMember, eligibilityOnlyMember],
            candidateIds);
    }

    [Fact]
    public async Task TeamInvalidationGateSerializesAndCoalescesBursts()
    {
        var gate = new TournamentParticipantsTab.TeamStateInvalidationGate();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var activeCallCount = 0;
        var maxActiveCallCount = 0;

        async Task Refresh()
        {
            var active = Interlocked.Increment(ref activeCallCount);
            while(true)
            {
                var previousMaximum = Volatile.Read(ref maxActiveCallCount);
                if(active <= previousMaximum ||
                   Interlocked.CompareExchange(ref maxActiveCallCount, active, previousMaximum) == previousMaximum)
                    break;
            }

            var call = Interlocked.Increment(ref callCount);
            try
            {
                if(call == 1)
                {
                    firstStarted.SetResult(true);
                    await releaseFirst.Task;
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeCallCount);
            }
        }

        var firstRun = gate.RunAsync(Refresh);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var burstRuns = Enumerable.Range(0, 5)
            .Select(_ => gate.RunAsync(Refresh))
            .ToArray();

        Assert.Equal(1, Volatile.Read(ref callCount));

        releaseFirst.SetResult(true);
        await Task.WhenAll(burstRuns.Append(firstRun));

        Assert.Equal(2, Volatile.Read(ref callCount));
        Assert.Equal(1, Volatile.Read(ref maxActiveCallCount));
    }

    [Fact]
    public void CaptainTransferKeepsSavedMembersAndAddsTheCurrentCaptain()
    {
        var formerCaptain = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var retainedMember = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var currentCaptain = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var reconciled = TournamentParticipantsTab.ReconcileRosterForCurrentCaptain(
            [formerCaptain, retainedMember],
            currentCaptain);

        Assert.Equal([formerCaptain, retainedMember, currentCaptain], reconciled);
    }

    [Fact]
    public void CaptainTransferInDirtyDraftKeepsOversizedRosterForExplicitRemoval()
    {
        var formerCaptain = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var retainedMember = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var currentCaptain = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        const int requiredTeamSize = 2;

        var reconciled = TournamentParticipantsTab.ReconcileRosterForCurrentCaptain(
            [formerCaptain, retainedMember],
            currentCaptain);

        Assert.Equal([formerCaptain, retainedMember, currentCaptain], reconciled);
        Assert.True(reconciled.Count > requiredTeamSize);
        Assert.Contains(
            "Choose a member to remove before saving.",
            TournamentParticipantsTab.GetCaptainTransferWarning(reconciled.Count, requiredTeamSize));
    }

    [Fact]
    public void CaptainTransferForSinglePlayerTeamKeepsSavedMemberForExplicitRemoval()
    {
        var formerCaptain = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var currentCaptain = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var reconciled = TournamentParticipantsTab.ReconcileRosterForCurrentCaptain(
            [formerCaptain],
            currentCaptain);

        Assert.Equal([formerCaptain, currentCaptain], reconciled);
    }
}
