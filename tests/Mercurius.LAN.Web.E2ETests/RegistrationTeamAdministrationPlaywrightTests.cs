using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class RegistrationTeamAdministrationPlaywrightTests : TournamentE2ETestBase
{
    public RegistrationTeamAdministrationPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task CaptainCanKeepThenCycleTeamRegistrationCancellation()
    {
        var (captain, _, tournamentId, _, teamId) = await ArrangeRegisteredTeamAsync("keep-cancel");

        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToBeVisibleAsync();

        // Keeping the registration leaves it untouched.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel registration" }).ClickAsync();
        var confirmation = dialog.Locator("[role=\"alertdialog\"]");
        await Expect(confirmation.GetByRole(AriaRole.Button, new() { Name = "Keep registration" })).ToBeVisibleAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Keep registration" }).ClickAsync();
        await Expect(dialog.Locator("[role=\"alertdialog\"]")).ToHaveCountAsync(0);

        // Confirming the cancellation removes the team registration.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel registration" }).ClickAsync();
        await dialog.Locator("[role=\"alertdialog\"]")
            .GetByRole(AriaRole.Button, new() { Name = "Cancel registration" })
            .ClickAsync();

        await Expect(page.Locator(".participants-state-card")).ToContainTextAsync("No participants yet");

        // The delete and the current-user refresh run server-side after the click returns, so wait
        // for the refreshed registration panel; reloading earlier reads pre-cancel state.
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();
        Assert.NotEqual(Guid.Empty, teamId);
    }

    [Fact]
    public async Task AdminCanRemoveTeamRegistrationWithReason()
    {
        var admin = await App.CreatePersonaAsync("team-remove-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var (_, _, tournamentId, teamName, _) = await ArrangeRegisteredTeamAsync("team-remove", adminApi);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        var adminPanel = page.Locator("#admin-registration");
        await Expect(adminPanel).ToContainTextAsync(teamName);

        await page.Locator("#admin-removal-reason").FillAsync("Removed by the E2E suite");
        await adminPanel.GetByRole(AriaRole.Button, new() { Name = "Remove" }).ClickAsync();

        await Expect(adminPanel.GetByText("No registrations are available to administer.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainSwapsAnActiveRosterMemberAndTheReplacementConfirmsToReactivate()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("roster-swap-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Roster Swap"), teamSize: 2);

        var captain = await App.CreatePersonaAsync("roster-swap-captain");
        var first = await App.CreatePersonaAsync("roster-swap-first");
        var replacement = await App.CreatePersonaAsync("roster-swap-second");
        var captainApi = App.CreateApiClient(captain);
        var teamName = TournamentE2E.Unique("E2E Swap Squad");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, App.CreateApiClient(first), teamId, first.UserId!.Value);
        await TournamentE2E.InviteAndAcceptAsync(
            captainApi, App.CreateApiClient(replacement), teamId, replacement.UserId!.Value);

        // Active baseline: captain + first submit, then the invited member confirms through the UI.
        await TournamentE2E.SubmitTeamRosterAsync(
            captainApi, tournamentId, teamId, captain.UserId!.Value, first.UserId!.Value);
        var firstContext = await App.NewAuthenticatedContextAsync(first);
        var firstPage = await firstContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(firstPage, $"/tournaments/{tournamentId}");
        await firstPage.Locator("#registration .registration-invitation")
            .GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.AcceptRosterPlace })
            .ClickAsync();
        await Expect(firstPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();

        // The captain swaps the confirmed member for the replacement through the real roster editor.
        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await Expect(page.Locator(".participants-overview-grid")).ToContainTextAsync(teamName);
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }).ClickAsync();

        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        var teamOption = dialog.GetByRole(AriaRole.Option).Filter(new() { HasText = teamName });
        if (await teamOption.CountAsync() > 0)
            await teamOption.ClickAsync();

        var editRoster = dialog.GetByRole(AriaRole.Button, new() { Name = "Edit roster" });
        await Expect(editRoster).ToBeEnabledAsync();
        await editRoster.ClickAsync();

        var firstCheckbox = dialog.Locator($"input[type=\"checkbox\"][aria-label=\"{first.Username}\"]");
        var replacementCheckbox = dialog.Locator($"input[type=\"checkbox\"][aria-label=\"{replacement.Username}\"]");
        await Expect(firstCheckbox).ToBeCheckedAsync();
        await firstCheckbox.UncheckAsync();
        await replacementCheckbox.CheckAsync();

        var reviewRoster = dialog.GetByRole(AriaRole.Button, new() { Name = "Review roster" });
        await Expect(reviewRoster).ToBeEnabledAsync();
        await reviewRoster.ClickAsync();

        var save = dialog.GetByRole(AriaRole.Button, new() { Name = "Save roster changes" });
        await Expect(save).ToBeEnabledAsync();
        await save.ClickAsync();

        // Swapping in a fresh member returns the registration to pending confirmation: the
        // replacement is invited, and the swapped-out member no longer holds a registration.
        var replacementContext = await App.NewAuthenticatedContextAsync(replacement);
        var replacementPage = await replacementContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(replacementPage, $"/tournaments/{tournamentId}");
        var invitation = replacementPage.Locator("#registration .registration-invitation");
        await Expect(invitation).ToContainTextAsync(TournamentE2E.Text.YouWereSelected);
        await invitation.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.AcceptRosterPlace }).ClickAsync();

        // The accept and the current-user refresh run server-side after the click returns; wait for
        // the replacement's refreshed panel so the other pages never read the pre-accept roster.
        await Expect(replacementPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();

        await TournamentE2E.ReloadInteractiveAsync(firstPage);
        await Expect(firstPage.Locator("#registration .registration-invitation")).ToHaveCountAsync(0);
        await Expect(firstPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();

        // Reloading the captain proves the saved roster now lists the replacement and not the first member.
        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.Locator(".participants-overview-grid")).ToContainTextAsync(teamName);
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }).ClickAsync();
        var reopened = page.Locator("div.registration-dialog");
        await Expect(reopened).ToBeVisibleAsync();
        var reopenedTeamOption = reopened.GetByRole(AriaRole.Option).Filter(new() { HasText = teamName });
        if (await reopenedTeamOption.CountAsync() > 0)
            await reopenedTeamOption.ClickAsync();
        var reopenEdit = reopened.GetByRole(AriaRole.Button, new() { Name = "Edit roster" });
        await Expect(reopenEdit).ToBeEnabledAsync();
        await reopenEdit.ClickAsync();

        // A is still a team member, so the candidate list still shows A - but unchecked. Only the
        // selected roster changed, which is exactly the persisted edit.
        var reopenedReplacement = reopened.Locator($"input[type=\"checkbox\"][aria-label=\"{replacement.Username}\"]");
        var reopenedFirst = reopened.Locator($"input[type=\"checkbox\"][aria-label=\"{first.Username}\"]");
        await Expect(reopenedReplacement).ToBeCheckedAsync();
        await Expect(reopenedFirst).ToBeVisibleAsync();
        await Expect(reopenedFirst).Not.ToBeCheckedAsync();
    }

    [Fact]
    public async Task UndersizedCaptainedTeamIsHiddenFromRegistration()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("undersized-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Undersized"), teamSize: 3);

        var captain = await App.CreatePersonaAsync("undersized-captain");
        var member = await App.CreatePersonaAsync("undersized-member");
        var captainApi = App.CreateApiClient(captain);
        var teamName = TournamentE2E.Unique("E2E Small Squad");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, App.CreateApiClient(member), teamId, member.UserId!.Value);

        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();

        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog.GetByText("Teams with fewer than 3 members are hidden.")).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Option)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MemberAlreadyEnteredInAnotherTeamIsShownUnavailable()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("conflict-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Conflict"), teamSize: 2);

        var sharingMember = await App.CreatePersonaAsync("conflict-shared-member");
        var firstCaptain = await App.CreatePersonaAsync("conflict-first-captain");
        var secondCaptain = await App.CreatePersonaAsync("conflict-second-captain");

        var firstCaptainApi = App.CreateApiClient(firstCaptain);
        var firstTeamId = await TournamentE2E.CreateTeamAsync(firstCaptainApi, firstCaptain.UserId!.Value, TournamentE2E.Unique("E2E First Squad"));
        await TournamentE2E.InviteAndAcceptAsync(
            firstCaptainApi, App.CreateApiClient(sharingMember), firstTeamId, sharingMember.UserId!.Value);
        await TournamentE2E.SubmitTeamRosterAsync(
            firstCaptainApi, tournamentId, firstTeamId, firstCaptain.UserId!.Value, sharingMember.UserId!.Value);

        var secondCaptainApi = App.CreateApiClient(secondCaptain);
        var secondTeamName = TournamentE2E.Unique("E2E Second Squad");
        var secondTeamId = await TournamentE2E.CreateTeamAsync(secondCaptainApi, secondCaptain.UserId!.Value, secondTeamName);
        await TournamentE2E.InviteAndAcceptAsync(
            secondCaptainApi, App.CreateApiClient(sharingMember), secondTeamId, sharingMember.UserId!.Value);

        var context = await App.NewAuthenticatedContextAsync(secondCaptain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();

        var dialog = page.Locator("div.registration-dialog");
        await dialog.GetByRole(AriaRole.Option).Filter(new() { HasText = secondTeamName }).ClickAsync();
        var chooseRoster = dialog.GetByRole(AriaRole.Button, new() { Name = "Choose roster" });
        await Expect(chooseRoster).ToBeEnabledAsync();
        await chooseRoster.ClickAsync();

        var sharedMemberCheckbox = dialog.Locator("input[type=\"checkbox\"]").First;
        await Expect(sharedMemberCheckbox).ToBeDisabledAsync();
        await Expect(sharedMemberCheckbox)
            .ToHaveAttributeAsync("aria-label", $"{sharingMember.Username}, unavailable");
    }

    private async Task<(E2EPersona Captain, E2EPersona Member, Guid TournamentId, string TeamName, Guid TeamId)> ArrangeRegisteredTeamAsync(
        string label,
        HttpClient? adminApiOverride = null)
    {
        var adminApi = adminApiOverride ?? App.CreateApiClient(await App.CreatePersonaAsync($"{label}-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique($"E2E {label}"), teamSize: 2);

        var captain = await App.CreatePersonaAsync($"{label}-captain");
        var member = await App.CreatePersonaAsync($"{label}-member");
        var captainApi = App.CreateApiClient(captain);
        var memberApi = App.CreateApiClient(member);
        var teamName = TournamentE2E.Unique($"E2E {label} Squad");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, memberApi, teamId, member.UserId!.Value);
        await TournamentE2E.SubmitTeamRosterAsync(
            captainApi, tournamentId, teamId, captain.UserId!.Value, member.UserId!.Value);

        return (captain, member, tournamentId, teamName, teamId);
    }
}
