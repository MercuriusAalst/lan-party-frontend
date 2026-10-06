using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamMembershipTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamMembershipTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task CaptainRemovesTeammateAfterConfirming()
    {
        var (captain, member, captainApi, team) = await CreateTeamWithMemberAsync("captain-remove");
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await Expect(TeamE2E.MemberCard(page, member.Username)).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = $"Remove {member.Username} from {team.Name}", Exact = true }));

        var dialog = TeamE2E.ConfirmationDialog(page, "Remove member");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText($"Remove {member.Username} from {team.Name}?")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Remove member", Exact = true }));

        await Expect(page.GetByText($"{member.Username} removed from the team.")).ToBeVisibleAsync();
        await Expect(TeamE2E.MemberCard(page, member.Username)).Not.ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Single(summary.CaptainedTeams.Single(t => t.Id == team.Id).Members);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(TeamE2E.MemberCard(page, member.Username)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainCanCancelTeammateRemoval()
    {
        var (captain, member, captainApi, team) = await CreateTeamWithMemberAsync("captain-remove-cancel");
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = $"Remove {member.Username} from {team.Name}", Exact = true }));
        var dialog = TeamE2E.ConfirmationDialog(page, "Remove member");
        await Expect(dialog).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(TeamE2E.MemberCard(page, member.Username)).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Equal(2, summary.CaptainedTeams.Single(t => t.Id == team.Id).Members.Count);
    }

    [Fact]
    public async Task MemberLeavesTeamAfterConfirming()
    {
        var (_, member, _, team) = await CreateTeamWithMemberAsync("member-leave");
        using var memberApi = _app.CreateApiClient(member);
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, member, "/teams/manage");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Leave team", Exact = true }));

        var dialog = TeamE2E.ConfirmationDialog(page, "Leave team");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText($"Leave {team.Name}? You can join again if the captain invites you.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Leave", Exact = true }));

        await Expect(page.GetByText("You left the team.")).ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(memberApi);
        Assert.Empty(summary.MemberTeams);
        Assert.Empty(summary.CaptainedTeams);
    }

    [Fact]
    public async Task MemberCanCancelLeavingTeam()
    {
        var (_, member, _, team) = await CreateTeamWithMemberAsync("member-leave-cancel");
        using var memberApi = _app.CreateApiClient(member);
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, member, "/teams/manage");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Leave team", Exact = true }));
        var dialog = TeamE2E.ConfirmationDialog(page, "Leave team");
        await Expect(dialog).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(memberApi);
        Assert.Single(summary.MemberTeams);
    }

    [Fact]
    public async Task CaptainTransfersCaptaincyToTeammate()
    {
        var (captain, member, captainApi, team) = await CreateTeamWithMemberAsync("captain-transfer");
        var memberId = TeamE2E.RequireUserId(member);
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));

        var captainSelect = page.Locator($"#team-captain-select-{team.Id}");
        await Expect(captainSelect).ToBeVisibleAsync();
        var transferButton = page.GetByRole(AriaRole.Button, new() { Name = "Transfer captainship", Exact = true });
        await Expect(transferButton).ToBeDisabledAsync();
        await captainSelect.SelectOptionAsync(new SelectOptionValue { Label = member.Username });
        await Expect(transferButton).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(transferButton);

        await Expect(page.GetByText("Captainship transferred.")).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamToolsTab(page)).Not.ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your membership" })).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Empty(summary.CaptainedTeams);
        var memberTeam = Assert.Single(summary.MemberTeams);
        Assert.Equal(team.Id, memberTeam.Id);
        Assert.Equal(memberId, memberTeam.CaptainUserId);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(TeamE2E.TeamToolsTab(page)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainUploadsLogoThenRemovesIt()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-logo"));
        using var api = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));

        var saveLogo = page.GetByRole(AriaRole.Button, new() { Name = "Save logo", Exact = true });
        var removeLogo = page.GetByRole(AriaRole.Button, new() { Name = "Remove logo", Exact = true });
        await Expect(saveLogo).ToBeDisabledAsync();
        await Expect(removeLogo).ToBeDisabledAsync();

        await page.Locator(".team-tools-panel input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "team-logo.png",
            MimeType = "image/png",
            Buffer = TeamE2E.OnePixelPngBytes()
        });

        await Expect(page.GetByAltText("Selected logo preview")).ToBeVisibleAsync();
        await Expect(saveLogo).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(saveLogo);

        await Expect(page.GetByText("Team logo saved.")).ToBeVisibleAsync();
        await Expect(page.GetByAltText($"{team.Name} saved logo")).ToBeVisibleAsync();
        await Expect(removeLogo).ToBeEnabledAsync();

        var savedSummary = await TeamE2E.GetSummaryAsync(api);
        Assert.False(string.IsNullOrWhiteSpace(savedSummary.CaptainedTeams.Single(t => t.Id == team.Id).LogoUrl));

        await page.ClickWhenInteractiveAsync(removeLogo);

        await Expect(page.GetByText("Team logo removed.")).ToBeVisibleAsync();
        await Expect(page.GetByAltText($"{team.Name} saved logo")).Not.ToBeVisibleAsync();
        await Expect(removeLogo).ToBeDisabledAsync();

        var clearedSummary = await TeamE2E.GetSummaryAsync(api);
        Assert.True(string.IsNullOrWhiteSpace(clearedSummary.CaptainedTeams.Single(t => t.Id == team.Id).LogoUrl));
    }

    [Fact]
    public async Task LogoPickerRejectsNonImageAndOversizedFiles()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-logo-invalid"));
        using var api = _app.CreateApiClient(captain);
        await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        var filePicker = page.Locator(".team-tools-panel input[type=file]");
        var saveLogo = page.GetByRole(AriaRole.Button, new() { Name = "Save logo", Exact = true });

        await filePicker.SetInputFilesAsync(new FilePayload
        {
            Name = "notes.txt",
            MimeType = "text/plain",
            Buffer = "not an image"u8.ToArray()
        });
        await Expect(page.GetByText("Choose an image file for the team logo.")).ToBeVisibleAsync();
        await Expect(saveLogo).ToBeDisabledAsync();

        await filePicker.SetInputFilesAsync(new FilePayload
        {
            Name = "huge.png",
            MimeType = "image/png",
            Buffer = new byte[5 * 1024 * 1024 + 1]
        });
        await Expect(page.GetByText("Choose a logo no larger than 5 MB.")).ToBeVisibleAsync();
        await Expect(saveLogo).ToBeDisabledAsync();
        await Expect(page.GetByAltText("Selected logo preview")).Not.ToBeVisibleAsync();
    }

    private async Task<(E2EPersona Captain, E2EPersona Member, HttpClient CaptainApi, TeamE2E.TeamManagementSummary Team)>
        CreateTeamWithMemberAsync(string captainLabel)
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel(captainLabel));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel(captainLabel.Replace("captain", "member")));
        var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));
        return (captain, member, captainApi, team);
    }
}
