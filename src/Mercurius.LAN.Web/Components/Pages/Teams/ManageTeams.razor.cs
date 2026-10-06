using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.Components.Pages.Teams;

public partial class ManageTeams : IAsyncDisposable
{
    private const long MaximumLogoBytes = 5 * 1024 * 1024;

    [Inject] private ITeamService TeamService { get; set; } = null!;
    [Inject] private ITeamNotificationService NotificationService { get; set; } = null!;
    [Inject] private ITeamRealtimeService RealtimeService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private CurrentUserTeamSummaryDTO _summary = new();
    private bool _isLoading = true;
    private bool _isRealtimeReady;
    private string? _loadError;
    private Guid? _selectedTeamId;
    private bool _isCreateTeamDialogOpen;
    private bool _isInviteDialogOpen;
    private Guid? _inviteTeamId;
    private string _inviteTeamName = string.Empty;
    private PublicUserDTO? _selectedUser;
    private ElementReference _confirmationDialogElement;
    private IJSObjectReference? _confirmationFocusTrap;
    private TeamConfirmation? _confirmation;
    private TeamManagementTab _activeTab = TeamManagementTab.Members;
    private readonly Dictionary<Guid, Guid?> _transferSelections = [];
    private readonly Dictionary<Guid, TeamLogoSelection> _selectedLogos = [];
    private readonly CancellationTokenSource _lifetimeCancellationTokenSource = new();
    private Task? _initializationTask;
    private bool _disposed;

    private IReadOnlyList<TeamManagementSummaryDTO> ManageableTeams =>
        _summary.CaptainedTeams
            .Concat(_summary.MemberTeams)
            .GroupBy(team => team.Id)
            .Select(group => group.FirstOrDefault(IsCaptain) ?? group.First())
            .ToList();

    private TeamManagementSummaryDTO? SelectedTeam =>
        _selectedTeamId.HasValue
            ? ManageableTeams.FirstOrDefault(team => team.Id == _selectedTeamId.Value)
            : null;

    protected override void OnInitialized()
    {
        RealtimeService.TeamStateInvalidated += RefreshFromSignalAsync;
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var cancellationToken = _lifetimeCancellationTokenSource.Token;
        try
        {
            await LoadSummaryAsync(cancellationToken);
            if(!IsActive(cancellationToken))
                return;

            if(_loadError is null)
            {
                var initiallyDisplayedTeamIds = ManageableTeams
                    .Select(team => team.Id)
                    .ToHashSet();
                _isLoading = false;
                await RenderIfActiveAsync();
                if(!IsActive(cancellationToken))
                    return;

                try
                {
                    await RealtimeService.StartAsync(cancellationToken);
                    if(!IsActive(cancellationToken))
                        return;

                    await RealtimeService.JoinTeamsAsync(initiallyDisplayedTeamIds, cancellationToken);
                    if(!IsActive(cancellationToken))
                        return;
                    if(!RealtimeService.IsConnected)
                    {
                        ToastService.ShowWarning(Localization["General.TeamManage.LiveUpdatesUnavailable"]);
                        return;
                    }

                    await ReconcileSummaryAfterRealtimeStartupAsync(initiallyDisplayedTeamIds, cancellationToken);
                    if(IsActive(cancellationToken) && RealtimeService.IsConnected)
                    {
                        _isRealtimeReady = true;
                        await RenderIfActiveAsync();
                    }
                }
                catch(OperationCanceledException) when(!IsActive(cancellationToken))
                {
                }
                catch(Exception)
                {
                    if(IsActive(cancellationToken))
                        ToastService.ShowWarning(Localization["General.TeamManage.LiveUpdatesUnavailable"]);
                }
            }
        }
        finally
        {
            if(IsActive(cancellationToken) && _isLoading)
            {
                _isLoading = false;
                await RenderIfActiveAsync();
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(_disposed)
            return;

        if(_confirmation is not null && _confirmationFocusTrap is null)
        {
            _confirmationFocusTrap = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "activateTeamModalFocusTrap",
                _confirmationDialogElement);
        }
        else if(_confirmation is null && _confirmationFocusTrap is not null)
        {
            await DisposeConfirmationFocusTrapAsync();
        }
    }

    private async Task LoadSummaryAsync(CancellationToken cancellationToken)
    {
        if(!IsActive(cancellationToken))
            return;

        _isLoading = true;
        _loadError = null;
        try
        {
            _summary = await TeamService.GetCurrentUserTeamSummaryAsync(cancellationToken);
            if(!IsActive(cancellationToken))
                return;

            EnsureSelectedTeam();
            await NotificationService.RefreshAsync(cancellationToken);
        }
        catch(OperationCanceledException) when(!IsActive(cancellationToken))
        {
        }
        catch(Exception exception)
        {
            if(!IsActive(cancellationToken))
                return;

            _summary = new();
            _loadError = GetErrorMessage(exception);
        }
    }

    private async Task ReconcileSummaryAfterRealtimeStartupAsync(
        IReadOnlySet<Guid> initiallyDisplayedTeamIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentSummary = await TeamService.GetCurrentUserTeamSummaryAsync(cancellationToken);
            if(!IsActive(cancellationToken))
                return;

            var newlyIntroducedTeamIds = currentSummary.CaptainedTeams
                .Concat(currentSummary.MemberTeams)
                .Select(team => team.Id)
                .Where(teamId => !initiallyDisplayedTeamIds.Contains(teamId))
                .Distinct()
                .ToArray();
            try
            {
                if(newlyIntroducedTeamIds.Length > 0)
                    await RealtimeService.JoinTeamsAsync(newlyIntroducedTeamIds, cancellationToken);
            }
            catch(OperationCanceledException) when(!IsActive(cancellationToken))
            {
                return;
            }
            catch(Exception)
            {
                if(IsActive(cancellationToken))
                    ToastService.ShowWarning(Localization["General.TeamManage.LiveUpdatesUnavailable"]);
                return;
            }
            if(!IsActive(cancellationToken))
                return;
            if(!RealtimeService.IsConnected)
            {
                ToastService.ShowWarning(Localization["General.TeamManage.LiveUpdatesUnavailable"]);
                return;
            }

            _summary = currentSummary;
            EnsureSelectedTeam();
            await RenderIfActiveAsync();
            if(IsActive(cancellationToken))
                await NotificationService.RefreshAsync(cancellationToken);
        }
        catch(OperationCanceledException) when(!IsActive(cancellationToken))
        {
        }
        catch(Exception exception)
        {
            if(IsActive(cancellationToken))
                ToastService.ShowWarning(GetErrorMessage(exception));
        }
    }

    private async Task RefreshSummaryAsync(CancellationToken cancellationToken = default)
    {
        var activeCancellationToken = cancellationToken == default
            ? _lifetimeCancellationTokenSource.Token
            : cancellationToken;
        if(!IsActive(activeCancellationToken))
            return;

        _summary = await TeamService.GetCurrentUserTeamSummaryAsync(activeCancellationToken);
        if(!IsActive(activeCancellationToken))
            return;

        EnsureSelectedTeam();
        await NotificationService.RefreshAsync(activeCancellationToken);
        if(!IsActive(activeCancellationToken))
            return;

        await RealtimeService.JoinTeamsAsync(ManageableTeams.Select(team => team.Id), activeCancellationToken);
        if(IsActive(activeCancellationToken))
        {
            _isRealtimeReady = RealtimeService.IsConnected;
            await RenderIfActiveAsync();
        }
    }

    private async Task RefreshFromSignalAsync()
    {
        if(_disposed)
            return;

        try
        {
            await RefreshSummaryAsync(_lifetimeCancellationTokenSource.Token);
        }
        catch(OperationCanceledException) when(_disposed)
        {
        }
        catch(Exception)
        {
        }
    }

    private bool IsActive(CancellationToken cancellationToken) =>
        !_disposed && !cancellationToken.IsCancellationRequested;

    protected virtual Task RequestRenderAsync() => InvokeAsync(StateHasChanged);

    private async Task RenderIfActiveAsync()
    {
        if(_disposed)
            return;

        try
        {
            await RequestRenderAsync();
        }
        catch(InvalidOperationException) when(_disposed)
        {
        }
    }

    private Task OpenCreateTeamDialogAsync()
    {
        _isCreateTeamDialogOpen = true;
        return Task.CompletedTask;
    }

    private void CloseCreateTeamDialog()
    {
        _isCreateTeamDialogOpen = false;
    }

    private async Task CreateTeamAsync(CreateTeamDialogResult createResult)
    {
        Team createdTeam;
        try
        {
            createdTeam = await TeamService.CreateTeamAsync(new CreateTeamDTO { Name = createResult.Name.Trim() });
        }
        catch(Exception exception)
        {
            ShowActionToast(GetErrorMessage(exception), TeamActionSeverity.Error);
            return;
        }

        _selectedTeamId = createdTeam.Id;

        if(createResult.Logo is null)
        {
            await RefreshSummaryAsync();
            _isCreateTeamDialogOpen = false;
            ToastService.ShowSuccess(Localization["General.TeamManage.TeamCreated"]);
            return;
        }

        try
        {
            await using var stream = new MemoryStream(createResult.Logo.Content);
            await TeamService.UploadLogoAsync(createdTeam.Id, stream, createResult.Logo.ContentType, createResult.Logo.FileName);
            await RefreshSummaryAsync();
            ToastService.ShowSuccess(Localization["General.TeamManage.TeamCreated"]);
        }
        catch(Exception exception)
        {
            await RefreshSummaryAsync();
            ShowActionToast(Localization.Get("General.TeamManage.TeamCreatedLogoFailed", GetErrorMessage(exception)), TeamActionSeverity.Error);
        }
        finally
        {
            _isCreateTeamDialogOpen = false;
        }
    }

    private Task OpenInviteDialogAsync(Guid teamId)
    {
        var team = ManageableTeams.FirstOrDefault(candidate => candidate.Id == teamId);
        _inviteTeamId = teamId;
        _inviteTeamName = team?.Name ?? string.Empty;
        _isInviteDialogOpen = true;
        return Task.CompletedTask;
    }

    private void CloseInviteDialog()
    {
        _isInviteDialogOpen = false;
        _inviteTeamId = null;
        _inviteTeamName = string.Empty;
    }

    private async Task InviteUserAsync(InviteUserDialogResult inviteResult)
    {
        if(!_inviteTeamId.HasValue)
            return;

        var teamId = _inviteTeamId.Value;
        CloseInviteDialog();
        await MutateAsync(async () =>
        {
            await TeamService.InviteUserAsync(teamId, inviteResult.UserId);
            await RefreshSummaryAsync();
            ShowActionToast(Localization["General.TeamManage.InviteSent"], TeamActionSeverity.Success);
        });
    }

    private async Task CancelInviteAsync(Guid teamId, Guid inviteId)
    {
        await MutateAsync(async () =>
        {
            await TeamService.CancelInviteAsync(teamId, inviteId);
            await RefreshSummaryAsync();
            ShowActionToast(Localization["General.TeamManage.InviteCanceled"], TeamActionSeverity.Success);
        });
    }

    private async Task RespondInviteAsync(Guid inviteId, bool accept)
    {
        await MutateAsync(async () =>
        {
            await TeamService.RespondToInviteAsync(inviteId, accept);
            await RefreshSummaryAsync();
            ShowActionToast(accept ? Localization["General.TeamManage.InviteAccepted"] : Localization["General.TeamManage.InviteDeclined"], TeamActionSeverity.Success);
        });
    }

    private async Task ConfirmLeaveAsync(Guid teamId, string teamName)
    {
        _confirmation = new TeamConfirmation(
            Localization["General.TeamManage.Membership"],
            Localization["team.leave"],
            Localization.Get("General.TeamManage.LeaveConfirmation", teamName),
            Localization["General.TeamManage.Leave"],
            async () =>
            {
                await MutateAsync(async () =>
                {
                    await TeamService.LeaveTeamAsync(teamId);
                    if(_selectedTeamId == teamId)
                        _selectedTeamId = null;

                    await RefreshSummaryAsync();
                    ShowActionToast(Localization["General.TeamManage.LeftTeam"], TeamActionSeverity.Success);
                });
            });
        await Task.CompletedTask;
    }

    private async Task ConfirmDeleteTeamAsync(Guid teamId, string teamName)
    {
        _confirmation = new TeamConfirmation(
            Localization["General.TeamManage.DangerZone"],
            Localization["General.TeamManage.DeleteTeam"],
            Localization.Get("General.TeamManage.DeleteConfirmation", teamName),
            Localization["General.TeamManage.DeleteTeam"],
            async () =>
            {
                await MutateAsync(async () =>
                {
                    await TeamService.DeleteTeamAsync(teamId);
                    _selectedLogos.Remove(teamId);
                    _transferSelections.Remove(teamId);
                    if(_selectedTeamId == teamId)
                        _selectedTeamId = null;

                    await RefreshSummaryAsync();
                    ShowActionToast(Localization["General.TeamManage.TeamDeleted"], TeamActionSeverity.Success);
                });
            });
        await Task.CompletedTask;
    }

    private async Task ConfirmRemoveMemberAsync(Guid teamId, string teamName, Guid userId, string memberName)
    {
        _confirmation = new TeamConfirmation(
            Localization["team.members"],
            Localization["General.TeamManage.RemoveMember"],
            Localization.Get("General.TeamManage.RemoveMemberConfirmation", memberName, teamName),
            Localization["General.TeamManage.RemoveMember"],
            async () =>
            {
                await MutateAsync(async () =>
                {
                    await TeamService.RemoveMemberAsync(teamId, userId);
                    if(GetTransferSelection(teamId) == userId)
                        _transferSelections.Remove(teamId);

                    await RefreshSummaryAsync();
                    ShowActionToast(Localization.Get("General.TeamManage.MemberRemoved", memberName), TeamActionSeverity.Success);
                });
            });
        await Task.CompletedTask;
    }

    private void CancelConfirmation()
    {
        _confirmation = null;
    }

    private Task HandleConfirmationKeyDown(KeyboardEventArgs args)
    {
        if(string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            CancelConfirmation();

        return Task.CompletedTask;
    }

    private async Task ConfirmPendingActionAsync()
    {
        if(_confirmation is not { } confirmation)
            return;

        _confirmation = null;
        await confirmation.Action();
    }

    private async Task TransferCaptainAsync(Guid teamId)
    {
        var newCaptainUserId = GetTransferSelection(teamId);
        if(!newCaptainUserId.HasValue)
            return;

        await MutateAsync(async () =>
        {
            await TeamService.TransferCaptainAsync(teamId, newCaptainUserId.Value);
            _transferSelections.Remove(teamId);
            await RefreshSummaryAsync();
            ShowActionToast(Localization["General.TeamManage.CaptainTransferred"], TeamActionSeverity.Success);
        });
    }

    private async Task SelectLogoAsync(Guid teamId, InputFileChangeEventArgs args)
    {
        var file = args.File;
        if(file.Size > MaximumLogoBytes)
        {
            ShowActionToast(Localization["General.Team.LogoTooLarge"], TeamActionSeverity.Warning);
            return;
        }

        if(!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ShowActionToast(Localization["General.Team.ImageFileRequired"], TeamActionSeverity.Warning);
            return;
        }

        var buffer = new byte[file.Size];
        await using var stream = file.OpenReadStream(MaximumLogoBytes);
        await stream.ReadExactlyAsync(buffer);
        var preview = $"data:{file.ContentType};base64,{Convert.ToBase64String(buffer)}";
        _selectedLogos[teamId] = new TeamLogoSelection(file.Name, file.ContentType, buffer, preview);
    }

    private async Task UploadLogoAsync(Guid teamId)
    {
        if(!_selectedLogos.TryGetValue(teamId, out var logo))
            return;

        await MutateAsync(async () =>
        {
            await using var stream = new MemoryStream(logo.Content);
            await TeamService.UploadLogoAsync(teamId, stream, logo.ContentType, logo.FileName);
            _selectedLogos.Remove(teamId);
            await RefreshSummaryAsync();
            ShowActionToast(Localization["General.TeamManage.LogoSaved"], TeamActionSeverity.Success);
        });
    }

    private async Task RemoveLogoAsync(Guid teamId)
    {
        await MutateAsync(async () =>
        {
            await TeamService.RemoveLogoAsync(teamId);
            _selectedLogos.Remove(teamId);
            await RefreshSummaryAsync();
            ShowActionToast(Localization["General.TeamManage.LogoRemoved"], TeamActionSeverity.Success);
        });
    }

    private async Task MutateAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch(Exception exception)
        {
            ShowActionToast(GetErrorMessage(exception), TeamActionSeverity.Error);
        }
    }

    private void SelectTeam(Guid teamId)
    {
        _selectedTeamId = teamId;
        _activeTab = TeamManagementTab.Members;
    }

    private void SelectTab(TeamManagementTab tab) => _activeTab = tab;

    private void EnsureSelectedTeam()
    {
        var teams = ManageableTeams;
        if(teams.Count == 0)
        {
            _selectedTeamId = null;
            return;
        }

        if(!_selectedTeamId.HasValue || teams.All(team => team.Id != _selectedTeamId.Value))
            _selectedTeamId = teams[0].Id;

        if(SelectedTeam is { } selectedTeam && !IsCaptain(selectedTeam) && _activeTab == TeamManagementTab.Management)
            _activeTab = TeamManagementTab.Members;
    }

    private bool IsCaptain(TeamManagementSummaryDTO team) =>
        _summary.CaptainedTeams.Any(candidate => candidate.Id == team.Id);

    private string GetTeamSelectorClass(Guid teamId)
    {
        var classes = "team-selector-item";
        return _selectedTeamId == teamId ? $"{classes} team-selector-item--active" : classes;
    }

    private void SelectMemberParticipant(ParticipantViewModel participant)
    {
        if(participant.User is { } user)
            _selectedUser = user;
    }

    private void CloseUserInfoDialog() => _selectedUser = null;

    private string GetTabClass(TeamManagementTab tab)
    {
        var classes = "team-tab-button";
        return _activeTab == tab ? $"{classes} team-tab-button--active" : classes;
    }

    private string GetMemberCountLabel(int count) =>
        Localization.Get(count == 1 ? "General.TeamManage.MemberCountOne" : "General.TeamManage.MemberCountMany", count);

    private string GetTeamSummary(TeamManagementSummaryDTO team, bool isCaptain)
    {
        var memberCount = GetMemberCountLabel(team.Members.Count);
        return isCaptain
            ? Localization.Get("General.TeamManage.TeamSummaryCaptain", memberCount)
            : memberCount;
    }

    private static string BuildTeamProfileHref(string teamName) =>
        string.IsNullOrWhiteSpace(teamName)
            ? string.Empty
            : $"/teams/{Uri.EscapeDataString(teamName.Trim())}";

    private IReadOnlyList<TeamInviteSummaryDTO> GetSentInvites(Guid teamId) =>
        _summary.SentPendingInvites.Where(invite => invite.TeamId == teamId).ToList();

    private IReadOnlySet<Guid> GetInviteDisabledUserIds() =>
        _inviteTeamId.HasValue
            ? ManageableTeams
                .FirstOrDefault(team => team.Id == _inviteTeamId.Value)?
                .Members
                .Select(member => member.Id)
                .ToHashSet() ?? new HashSet<Guid>()
            : new HashSet<Guid>();

    private Guid? GetTransferSelection(Guid teamId) =>
        _transferSelections.TryGetValue(teamId, out var value) ? value : null;

    private void SetTransferSelection(Guid teamId, string? value) =>
        _transferSelections[teamId] = Guid.TryParse(value, out var parsed) ? parsed : null;

    private bool HasSelectedLogo(Guid teamId) => _selectedLogos.ContainsKey(teamId);

    private string? GetLogoPreview(Guid teamId) =>
        _selectedLogos.TryGetValue(teamId, out var logo) ? logo.PreviewDataUrl : null;

    private string GetLogoFileName(Guid teamId) =>
        _selectedLogos.TryGetValue(teamId, out var logo) ? logo.FileName : Localization["form.noFile"];

    private void ShowActionToast(string message, TeamActionSeverity severity)
    {
        switch(severity)
        {
            case TeamActionSeverity.Success:
                ToastService.ShowSuccess(message);
                break;
            case TeamActionSeverity.Warning:
                ToastService.ShowWarning(message);
                break;
            case TeamActionSeverity.Error:
                ToastService.ShowError(message);
                break;
            default:
                ToastService.ShowInfo(message);
                break;
        }
    }

    private string GetMemberName(PublicUserDTO member)
    {
        if(!string.IsNullOrWhiteSpace(member.Username))
            return member.Username.Trim();

        if(!string.IsNullOrWhiteSpace(member.DisplayName))
            return member.DisplayName.Trim();

        return Localization["General.TeamManage.Player"];
    }

    private static string GetInitials(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private string GetErrorMessage(Exception exception) =>
        exception is TeamServiceException serviceException
            ? serviceException.Message
            : Localization["General.TeamManage.ActionFailed"];

    private async ValueTask DisposeConfirmationFocusTrapAsync()
    {
        var focusTrap = _confirmationFocusTrap;
        _confirmationFocusTrap = null;
        if(focusTrap is null)
            return;

        try
        {
            await focusTrap.InvokeVoidAsync("dispose");
            await focusTrap.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetimeCancellationTokenSource.Cancel();
        RealtimeService.TeamStateInvalidated -= RefreshFromSignalAsync;

        if(_initializationTask is not null)
        {
            try
            {
                await _initializationTask;
            }
            catch(OperationCanceledException)
            {
            }
        }

        await DisposeConfirmationFocusTrapAsync();
        _lifetimeCancellationTokenSource.Dispose();
    }

    private enum TeamManagementTab
    {
        Members,
        Management
    }

    private enum TeamActionSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    private sealed record TeamConfirmation(
        string Eyebrow,
        string Title,
        string Message,
        string ConfirmLabel,
        Func<Task> Action);
}
