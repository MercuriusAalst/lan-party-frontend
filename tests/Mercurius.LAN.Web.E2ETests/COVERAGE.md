# E2E Coverage Ledger

Source-gap audit of the Playwright suite in this folder against the frontend
`src/Mercurius.LAN.Web` and the backend checkout the fixture starts. The audited baseline is the
main-line frontend and backend source, including the repairs listed under "Included repairs" below.

- 232 executable cases across 30 test classes: 224 `[Fact]` plus 3 `[Theory]` carrying 8
  `[InlineData]` (`AuthenticationFlowTests.CancellingSignInFromAProtectedRouteReturnsHomeAsAnonymousVisitor`
  x2, `MatchScoringFormatTests.DialogShowsTheConfiguredMatchFormat` x3, and
  `PublicUserProfileTests.AuthenticatedHeaderKeepsSearchUsableWithoutHorizontalOverflow` x3 at
  1025/1100/1440). The 229-case count in the evidence table below labels the earlier Windows and
  Ubuntu runs and is intentionally not rewritten.
- Three access levels (anonymous, member, admin) plus the ownership states (captain, invitee,
  non-member/stranger) are covered in the roles table below.
- Every class joins the `Playwright E2E` xUnit collection (`DisableParallelization`). `E2ETestBase`
  truncates the fixture-owned `mercurius_e2e_*` database before each test, refusing any database
  without that prefix, and `DisposeAsync` closes leftover browser contexts and API clients so tests
  never depend on execution order or on development data.

Paths: `F` = `src/Mercurius.LAN.Web`, `B` = backend `src`, `T` = this project. Component and
service claims use `file:line`; test references use the stable `Class.Method` name.

## Included repairs

| Repair | Source | Spec / capability | Tests |
| --- | --- | --- | --- |
| Server-rendered branded recovery pages for direct 403/404 responses, kept interactive after the response | `F/Components/Pages/Status/StatusCodePage.razor:1-28`, `F/Program.cs:67` | `responsive-accessible-states` (direct HTTP status responses) | `AuthenticationFlowTests.MemberHasNoOrganizerToolsAndIsForbiddenOnAdminRoute`, `MemberCannotOpenTheAdminOnlyUserProfileEvenThoughThePublicApiListsTheUsername`, `AuthenticatedMemberVisitingUnknownRouteSeesNotFoundPage`, `SponsorManagementPlaywrightTests.NonAdminCannotOpenSponsorManagement`, `PublicSiteTests.UnknownRouteShowsNotFoundPageForAnonymousVisitor` |
| Forbidden page uses the framework default layout instead of a duplicate layout provider | `F/Components/Routes.razor:5-17` (`NotAuthorized` renders `PageTitle` + `StatusPage` only; `AuthorizeRouteView.DefaultLayout` supplies the shell) | `responsive-accessible-states` (forbidden recovery page stays interactive) | `AuthenticationFlowTests.ForbiddenPageFromDirectRequestStaysInteractiveThroughInCircuitNavigation` |
| Same-route status-page retry reruns the initial data load in place | `F/Components/Shared/StatusPage.razor:25-35,61,91-101` (`OnRetry` callback button; `PrimaryForceLoad` link fallback kept), `F/Components/Pages/Users/Profile.razor:11-16` + `Profile.razor.cs:56`, `F/Components/Pages/Teams/ManageTeams.razor:15-21` + `ManageTeams.razor.cs:123`, `F/Components/Pages/Tournaments/TournamentDetail.razor:32-38`, `F/Components/Pages/Teams/PublicTeamProfile.razor:14-20` + `PublicTeamProfile.razor.cs:88`, `F/Components/Pages/Users/PublicUserProfile.razor:15-21` + `PublicUserProfile.razor.cs:97` | `api-unavailable-resilience` (in-place retry without a forced reload) | `ProfileAndOnboardingTests.ProfileLoadFailureShowsUnavailableStateAndRecovers`, `TeamManagementPageTests.TeamManagementLoadFailureShowsUnavailableStateAndRecovers`, `TournamentDetailLifecyclePlaywrightTests.TournamentDetailLoadFailureShowsRetryAndRecoversInPlace`, `TeamPublicProfileTests.PublicTeamProfileLoadFailureShowsUnavailableStateAndRecovers`, `PublicUserProfileTests.PublicUserProfileLoadFailureShowsUnavailableStateAndRecoversInPlace` |
| One shared status surface for full-page failure, unavailable, forbidden, not-found and server-failure states | `F/Components/Shared/StatusPage.razor:4-44` + `.razor.css`, `F/Components/Pages/Tournaments/TournamentDetail.razor:19-38`, `F/Components/Pages/Tournaments/TournamentsOverview.razor:14-22`, `F/Components/Pages/Users/CompleteProfile.razor:9-17`, `F/Components/Routes.razor:10,25`, `F/Components/Pages/Status/StatusCodePage.razor:5-25` | `error-status-surfaces` (new capability) | contract rows `StatusPageMarkupContractTests.*`; runtime rows `TournamentDetailLifecyclePlaywrightTests.TournamentDetailLoadFailureShowsRetryAndRecoversInPlace`, `UnknownTournamentIdShowsNotFoundState`, `PublicSiteTests.TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry`, `TeamPublicProfileTests.UnknownTeamNameShowsNotFoundStatusPage`, `PublicUserProfileTests.UnknownUsernameShowsTheUnavailableStateRatherThanNotFound` |
| Authentication failure does not retry the protected destination | `F/Components/Auth/RedirectToLogin.razor:7` | `account-access` | `AuthenticationFlowTests.CancellingSignInFromAProtectedRouteReturnsHomeAsAnonymousVisitor` (x2), `DeepLinkedProtectedRouteReturnsToTheRequestedPageAfterSignIn` |
| Invite-dialog actions stay reachable during the empty search state | `F/Components/Pages/Teams/InviteUserDialog.razor:47-75`, `.razor.css:132-145,200-210` | `user-owned-team-management` | `TeamInviteFlowTests.InviteDialogShowsEmptyResultMessageAndCanBeCanceled` |
| Global search stays usable with authenticated desktop navigation | `F/Components/Layout/NavMenu.razor.css:582-587` (`.brand-nav-search` `min-width: 10rem`); CSS unchanged for the P2 regression | `site-navigation` capability change `keep-authenticated-header-search-usable` | `PublicUserProfileTests.AdminOpensUserResultFromGlobalSearch`, `PublicUserProfileTests.AuthenticatedHeaderKeepsSearchUsableWithoutHorizontalOverflow` (x3 at 1025/1100/1440, uncommitted regression rows) |
| Personal-group fanout on member removal and team deletion | `B/Modules/Teams/Mercurius.Modules.Teams/Application/Services/TeamEventPublishingDecorator.cs` | removal/deletion live-refresh behavior | `TeamRealtimeTests.RemovingMemberUpdatesCaptainAndRemovedMemberWithoutReload`, `DeletingTeamRefreshesConnectedMembersAndInviteesWithoutReload` |

### Contact form email transport (test-only sink)

The fixture starts a loopback `LocalSmtpSink` on an ephemeral port and points the app's
`ContactEmail` options at it (`T/Infrastructure/LocalSmtpSink.cs:13-30`,
`T/Infrastructure/PlaywrightE2EFixture.cs:61,106-112`). It uses the .NET standard library only and
needs no external credentials, packages, or production changes. Covered on the real UI:

- Success envelope and headers: envelope sender and recipients plus the `From`, `To`, `Reply-To`
  and subject headers (`PublicSiteTests.ContactFormSendsEmailWithReplyToAndClearsAfterSuccess`).
- In-flight state: the held 250 acceptance keeps `Sending...` disabled with the draft intact, and
  the form clears once the acceptance is released (same test).
- Discord fallback: a non-email contact value makes `Reply-To` fall back to the configured sender
  (`ContactFormUsesSenderAsReplyToForDiscordContact`).
- Transport failure and retry: the rejected delivery surfaces the Discord fallback message, the
  draft stays filled, the button re-enables, and the retry succeeds
  (`ContactFormSurfacesMailTransportFailureForValidMessage`).

## Executed evidence (232-case suite current; earlier 229-case runs retained)

| Artifact | Result |
| --- | --- |
| `T/TestResults/full-round10/full-round10.trx` | 229 total, 229 executed, 229 passed, 0 failed, 0 notExecuted, 30 classes; finished 02:34:57 (+02:00) |
| `T/TestResults/full-round11/full-round11.trx` | 229 total, 229 executed, 229 passed, 0 failed, 0 notExecuted, 30 classes; finished 02:40:56 (+02:00) |
| `T/TestResults/full-round8/full-round8.trx` | 229 total, 229 executed, 229 passed, 0 failed, 0 notExecuted, 30 classes; finished 23:59:21 UTC |
| `T/TestResults/full-round9/full-round9.trx` | 228 passed, 1 failed; historical failure, cause fixed by the test-only synchronization |
| `T/TestResults/full-round7/full-round7.trx` | 227 total, 227 passed, 0 failed; historical, valid prior coverage (pre-SMTP expansion) |
| `T/TestResults/full-round6/full-round6.trx` | 227 total, 227 passed, 0 failed; historical, valid prior coverage (pre-SMTP expansion) |
| `T/TestResults/full-round5/full-round5.trx` | 226 total, 226 passed, 0 failed; historical, pre-forbidden-circuit fix |
| `T/TestResults/full-round4/full-round4.trx` | 226 total, 226 passed, 0 failed; historical, pre-forbidden-circuit fix |
| `T/TestResults/probe3/probe3.trx`, `probe4/probe4.trx`, `probe5/probe5.trx` | 4 total, 4 passed, 0 failed each (12 cases) |
| `T/TestResults/contact-assertion-run1/contact-assertion-run1.trx` and `T/TestResults/contact-assertion-run2/contact-assertion-run2.trx` | 2 total, 2 passed, 0 failed each |
| `T/TestResults/solo-failcheck9/svenp_MEAN_MACHINE_2026-10-03_02_06_13_net10.0.trx` | 1 total, 1 failed; same cancellation case |
| `T/TestResults/fix-probes/final-classes/final-classes.trx` | 17 total, 17 passed, 0 failed (affected registration tests) |
| `T/TestResults/fix-probes/final-captain1/final-captain1.trx`, `final-captain2/final-captain2.trx`, `final-captain3/final-captain3.trx` | 1 total, 1 passed, 0 failed each (captain cancellation) |
| `T/TestResults/fix-probes/lane1/lane-probe1.trx`, `lane2/lane-probe2.trx`, `lane3/lane-probe3.trx` | 4 total, 4 passed, 0 failed each |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.ContractTests\TestResults\frontend-final-expanded\svenp_MEAN_MACHINE_2026-10-03_02_41_28_net10.0.trx` | 364 total, 364 passed, 0 failed, 0 skipped (final frontend contracts) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-forbidden-final\svenp_MEAN_MACHINE_2026-10-03_02_41_54_net10.0.trx` | 1 total, 1 passed, 0 failed (forbidden-page circuit) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-smtp-final\svenp_MEAN_MACHINE_2026-10-03_02_42_14_net10.0.trx` | 1 total, 1 passed, 0 failed (SMTP success) |
| `PublicSiteTests` across three fresh fixtures | 33 of 33 passed |
| GitHub Actions `37111821272` (frontend `ddc9b43`, backend pin `ca3b...`) | 229 passed, 0 failed, 0 skipped; `dotnet test` job 7 m 31 s, finished 09:14:18Z; log at `D:\Github Repositories\lan-party\.tmp\e2e-37111821272.log` |
| GitHub Actions `37110243446` (frontend `1e84604`) | 228 passed, 1 failed; raw quoted-printable LF-marker wrap in the SMTP MIME assertion, fixed by `ddc9b43` |
| `T/TestResults/public-user-profile-green/public-user-profile-green.trx` | 9 total, 9 passed, 0 failed (`PublicUserProfileTests`, including all three header widths) |
| `T/TestResults/full-232/full-232.trx` | 231 passed, 1 failed; historical, 1025 header case hit the not-yet-indexed search read model |
| `T/TestResults/full-232-final/full-232-final.trx` | 232 total, 232 passed, 0 failed, on backend `b98f197...`; finished 11:42:42 (+02:00) |
| GitHub Actions `37114424083` (frontend `e9d58c1`, backend pin `b98f197...`) | 232 total, 231 passed, 1 failed, 0 skipped; historical failure, artifact at `D:\Github Repositories\lan-party\.tmp\ci-artifacts-37114424083` |
| GitHub Actions `37146174575` (frontend `caea4a50`, backend pin `3cfc07f...`) | 232 passed, 0 failed, 0 skipped; first paired Ubuntu green, `dotnet test` stdout duration 7 m 41 s |
| `T/TestResults/nfv-badge-focus-{1,2,3}/badge-focus-{1,2,3}.trx` | 1 total, 1 passed, 0 failed each; historical probe; `TeamInviteFlowTests.InviteNotificationBadgeLetsInviteeDecline` after the test-only preseed reorder |
| `T/TestResults/nfv-team-invite-realtime/team-invite-realtime.trx` | 20 total, 20 passed, 0 failed; historical probe; 16 `TeamInviteFlowTests` + 4 `TeamRealtimeTests` |
| `T/TestResults/nfv-realtime-1/realtime-1.trx` | 4 total, 3 passed, 1 failed, pre-repair; `TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload` |
| `T/TestResults/nfv-realtime-leave-2/realtime-leave-2.trx` | 1 total, 0 passed, 1 failed, pre-repair; same leave case, repeat |
| `T/TestResults/realtime-gate-final/leave-{1,2,3}/leave-{1,2,3}.trx` | 1 total, 1 passed, 0 failed each; HISTORICAL, superseded source shape (see the repair notes below) |
| `T/TestResults/realtime-gate-final/combined-20/combined-20.trx` | 20 total, 20 passed, 0 failed; HISTORICAL, 16 `TeamInviteFlowTests` + 4 `TeamRealtimeTests` |
| `T/TestResults/realtime-gate-final/contracts/contracts.trx` | 364 total, 364 passed, 0 failed; HISTORICAL (`Mercurius.LAN.Web.ContractTests`) |
| `T/TestResults/realtime-gate-final/full-232/full-232.trx` | 232 total, 232 passed, 0 failed (`dotnet test` stdout duration 5 m 37 s); HISTORICAL, superseded source shape |
| `T/TestResults/realtime-rest-first-final/new-case/new-case.trx` | 1 total, 1 passed, 0 failed (`ComponentLifecycleBehaviorTests.ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending`) |
| `T/TestResults/realtime-rest-first-final/leave-{1,2,3}/leave-{1,2,3}.trx` | 1 total, 1 passed, 0 failed each; leave case on the current frozen source |
| `T/TestResults/realtime-rest-first-final/combined-20/combined-20.trx` | 20 total, 20 passed, 0 failed; 16 `TeamInviteFlowTests` + 4 `TeamRealtimeTests` |
| `T/TestResults/realtime-rest-first-final/contracts/contracts.trx` | 365 total, 365 passed, 0 failed (`Mercurius.LAN.Web.ContractTests`) |
| `T/TestResults/realtime-rest-first-final/full-232/full-232.trx` | 232 total, 232 executed, 232 passed, 0 failed, 0 skipped across 30 classes; `dotnet test` stdout duration 5 m 34 s |

The 229-case revision is green on two consecutive Windows full runs:
`full-round10` and `full-round11` each pass all 229 cases with no failures and no unexecuted cases.
`full-round9` is the historical failure: its one case,
`RegistrationTeamAdministrationPlaywrightTests.CaptainCanKeepThenCycleTeamRegistrationCancellation`,
saw the `#registration` panel still offering "Manage registration" and "Cancel registration" with 0
participants after a reload that raced the cancellation, because the success toast can appear before
the frontend refresh completes. The fix is test-only and synchronizes on the observable completed
panel state by awaiting the exact `#registration` "Register now" button before the reload and
asserting it again after the reload, in both the captain team-registration and individual
registration tests.

Independent `final_code_review` of both final runs passed with minor findings, and the publication
gate passed. The review is read-only and no rerun is claimed: it independently parsed 229 of 229
passed with 30 classes for each run and scanned all 242 traces per run. Each run holds the 6 expected
403/404 console errors, 0 browser page errors, and 0 Mud duplicate-provider errors; all 27 API
exceptions per run are the expected `42P01` database-fault records. Accepted `CustomAutocomplete`
JS-disconnected teardown records were 2 in `full-round10` and 1 in `full-round11`, and the five-flow
mutations were checked to complete (204/200) before the refresh with state-specific pre- and
post-reload assertions.

The 227-case runs postdate the forbidden-page circuit fix but predate the SMTP expansion, and the
226-case runs predate the forbidden-page circuit fix, so those four are historical prior coverage
rather than evidence for this revision. The contact-assertion runs repeat the two SMTP-focused cases,
and `PublicSiteTests` passes 33 of 33 across three fresh fixtures at the current source.

The working tree has grown to **232 cases across 30 classes** with the test-only P2 theory
`PublicUserProfileTests.AuthenticatedHeaderKeepsSearchUsableWithoutHorizontalOverflow` at
1025/1100/1440. The overflow was not reproducible with a real admin at 1100/1440 and the CSS is
unchanged, so the three rows are regression guards rather than a product fix; the change is test-only
(one file). Real header geometry at 1025/1100/1440 is asserted directly (no horizontal overflow and
real search-field width). The first 232 run failed only the 1025 case because the search read model
was not yet indexed, so the UI showed no matches; it was fixed by reusing the existing
`PublicSiteTests.WaitForSearchResultAsync` deterministic API precondition without weakening any
assertion. `full-232-final` then passed 232/232 on backend `b98f197...`.

Header independent review is FINAL PASS: current diff + 9/9 `PublicUserProfileTests` + 232/232 full +
the three header traces showing no overflow. The 229 labels above stay as the historical Windows and
Ubuntu revision counts.

The cancellation fix synchronizes five flows across three files: individual registration
cancellation (`RegistrationPlaywrightTests.cs`), roster accept and roster decline
(`RegistrationTeamPlaywrightTests.cs`), and the captain cancellation cycle plus roster swap
(`RegistrationTeamAdministrationPlaywrightTests.cs`). `fix-probes/final-classes` passes all 17
affected registration tests, and the three `final-captain*` runs each pass the captain case 1/1.
These probes preceded the green `full-round10`/`full-round11` pair and are consistent with it.

Command-level evidence, including the exact commands and raw result artifacts for the solution and
solo runs, is recorded in [`VERIFICATION.md`](VERIFICATION.md).

Traces and final-page screenshots live under `T/TestResults/PlaywrightE2E/<run-id>`, and
`show-trace` is documented in `README.md`.

## Routes (14 `@page`)

| Route | Source | Primary tests |
| --- | --- | --- |
| `/` | `F/Components/Pages/Home.razor:1` | `PublicSiteTests.AnonymousHomePagePresentsEventFactsAndEntryPoints` (asserts the seeded-empty sponsor default, `Home.razor:93`), `HomeTicketCallToActionNavigatesToTicketSection`, `HomeFeaturedTournamentsMirrorThePublishedOrderAndOpenTheirDetailPage`, `HomePageShowsTheEmptyTournamentStateWhenNoTournamentExists`, `HomeTournamentLoadFailureShowsTheRetryStateAndRecoversAfterTheApiIsRestored`, `HomeSponsorFailureDegradesOnlyTheSponsorSection` |
| `/info` | `F/Components/Pages/Info.razor:1` | `PublicSiteTests.InfoPageRendersPackingTicketsMenuAndContactSections`, `InfoNavigationDropdownJumpsToRequestedSection`, `InfoMenuClosesWhenTheInteractionOverlayIsClicked`, `ContactFormRejectsBlankSubmissionWithoutSending`, `ContactFormRejectsMessageShorterThanTenCharacters`, `ContactFormSendsEmailWithReplyToAndClearsAfterSuccess`, `ContactFormUsesSenderAsReplyToForDiscordContact`, `ContactFormSurfacesMailTransportFailureForValidMessage` |
| `/sponsors` | `F/Components/Pages/Sponsors.razor:1` | `PublicSiteTests.SponsorsPageRendersSeededTiersInOrderWithTierSpecificDescriptions`, `SponsorsPageShowsTheEmptyStateWhenNoSponsorExists`, `SponsorsPageLoadFailureReportsFailureToastAndRecoversOnReload` |
| `/privacy-policy` | `F/Components/Pages/PrivacyPolicy.razor:1` | `PublicSiteTests.PrivacyPolicyPageDescribesCollectedDataAndRights`, `FooterQuickLinksNavigateToTheLegalPage` |
| `/Error` | `F/Components/Pages/Error.razor:1` | `PublicSiteTests.ErrorPageRendersSystemFailureState` |
| `/status/{StatusCode:int}` | `F/Components/Pages/Status/StatusCodePage.razor:1` | 403 and 404 render their dedicated copy and are asserted through the router rows below; the remaining codes fall to the generic error branch (see the re-execution note) |
| `/complete-profile` | `F/Components/Pages/Users/CompleteProfile.razor:1` | `ProfileAndOnboardingTests.*` |
| `/profile` | `F/Components/Pages/Users/Profile.razor:1` | `ProfileAndOnboardingTests.ProfileEditPersistsEveryFieldAndTheNewUsername`, `ProfileSaveRequiresAValidUsernameAndDoesNotPersistInvalidEdits`, `DeleteAccountStaysDisabledUntilTheExactUsernameIsTyped`, `AbandoningDeleteConfirmationLeavesTheAccountIntact`, `DeleteAccountAnonymizesTheProfileAndEndsTheSession`, `UnverifiedProfileOffersResendVerificationAndReportsGenericOutcome`, `ProfilePasswordResetReportsGenericOutcome`, `ProfileLoadFailureShowsUnavailableStateAndRecovers` |
| `/users/{Username}` | `F/Components/Pages/Users/PublicUserProfile.razor:1` | `PublicUserProfileTests.*`, `AuthenticationFlowTests.MemberCannotOpenTheAdminOnlyUserProfileEvenThoughThePublicApiListsTheUsername` |
| `/teams/manage` | `F/Components/Pages/Teams/ManageTeams.razor:1` | `TeamManagementPageTests.*`, `TeamMembershipTests.*`, `TeamInviteFlowTests.*`, `TeamDeleteTests.*`, `TeamRealtimeTests.*`, `TeamAuthorizationTests.*` |
| `/teams/{TeamName}` | `F/Components/Pages/Teams/PublicTeamProfile.razor:1` | `TeamPublicProfileTests.*` |
| `/tournaments` | `F/Components/Pages/Tournaments/TournamentsOverview.razor:1` | `TournamentOverviewPlaywrightTests.*`, `PublicSiteTests.TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry`, `TournamentsOverviewSponsorFailureDegradesOnlyTheSponsorSection` |
| `/tournaments/{TournamentId:guid}` | `F/Components/Pages/Tournaments/TournamentDetail.razor:1` | `TournamentDetailLifecyclePlaywrightTests.*`, `Match*` tests, `Leaderboard*` tests, `Registration*` tests, `TournamentPartnerAndSwissPlaywrightTests.*` |
| `/admin/sponsors` | `F/Components/Pages/Admin/SponsorManagement.razor:1` | `SponsorManagementPlaywrightTests.*`, `AuthenticationFlowTests.AdminSeesOrganizerToolsAndCanOpenSponsorManagement` |

The detail route parameter is `{TournamentId:guid}`, not `{id:guid}`
(`F/Components/Pages/Tournaments/TournamentDetail.razor:1`). The create dialog offers only
`SingleElimination`, `DoubleElimination` and `Leaderboard`
(`F/Components/Pages/Tournaments/AddTournamentDialog.razor.cs:36-41`), so tests that need
`RoundRobin`/`Swiss` seed through the API.

### Router and SSR states (no `@page`)

| State | Source | Tests |
| --- | --- | --- |
| 403 forbidden | `F/Components/Routes.razor:5-17` (`AuthorizeRouteView.NotAuthorized`) | `AuthenticationFlowTests.MemberHasNoOrganizerToolsAndIsForbiddenOnAdminRoute`, `ForbiddenPageFromDirectRequestStaysInteractiveThroughInCircuitNavigation`, `MemberCannotOpenTheAdminOnlyUserProfileEvenThoughThePublicApiListsTheUsername`, `SponsorManagementPlaywrightTests.NonAdminCannotOpenSponsorManagement` |
| 404 not found | `F/Components/Routes.razor:22-27` (`Router.NotFound`) | `PublicSiteTests.UnknownRouteShowsNotFoundPageForAnonymousVisitor`, `AuthenticationFlowTests.AuthenticatedMemberVisitingUnknownRouteSeesNotFoundPage` |
| anonymous challenge | `F/Components/Routes.razor:12-15` (`RedirectToLogin`) to `/account/login` | `AuthenticationFlowTests.AnonymousVisitorIsChallengedByTheIdentityProviderForProtectedRoutes`, `CancellingSignInFromAProtectedRouteReturnsHomeAsAnonymousVisitor` (x2), `TeamManagementPageTests.AnonymousVisitorIsSentToSignInFromTeamManagement`, `RegistrationPlaywrightTests.AnonymousVisitorIsAskedToSignInOnOpenTournament` |
| HTTP re-execution | `F/Program.cs:67` `UseStatusCodePagesWithReExecute("/status/{0}")` | the two status rows above |

`StatusCodePage` switches on the status code: `403` renders the forbidden copy, `404` the not-found
copy, and the default branch (`_ =>`) renders the generic error copy
(`F/Components/Pages/Status/StatusCodePage.razor:16-28`). The default branch is reached by a direct
`/status/{code}` request for any code other than 403/404; ordinary server errors are handled earlier
by `UseExceptionHandler("/Error", ...)` (`F/Program.cs:62`) before the status-code re-execution
(`F/Program.cs:67`), so the router rows below exercise only the two dedicated branches. The 403 and
404 branches are asserted through the router rows. A direct-request case for a non-403/404 code is
deliberately not duplicated: the default branch renders the same generic error `StatusPage` copy as
`/Error` (`F/Components/Pages/Error.razor:8` uses `common.error` and `common.unexpectedError`, the
same keys as `StatusCodePage.razor:20,27`), and that behavior already has its own case in
`PublicSiteTests.ErrorPageRendersSystemFailureState`. The new behavior exercised here is the
dedicated 403/404 recovery page, not the default branch, and the default branch is not claimed as
tested.

Shell auth routes `/account/login`, `/account/register`, `/account/logout` and the logout callback
are mapped in `F/Program.cs:76-141` and exercised through `AuthenticationFlowTests` and
`PlaywrightE2EFixture.LoginAsync`. `AuthenticationFlowTests.DeepLinkedProtectedRouteReturnsToTheRequestedPageAfterSignIn`
covers the protected-return after sign-in.

## Roles and ownership

| Role / ownership | Covered behavior | Tests |
| --- | --- | --- |
| anonymous | protected routes challenge; public shell theme/language/mobile/search; public team and user profiles | `AuthenticationFlowTests.AnonymousVisitorIsChallengedByTheIdentityProviderForProtectedRoutes`, `PublicSiteTests.LanguageSelectorSwitchesCultureToDutchAndItSurvivesReload`, `ThemeToggleFlipsTheLayoutThemeAndItSurvivesReload`, `MobileNavigationOverlayOpensAndClosesFromButtonBackdropAndEscape`, `TeamPublicProfileTests.AnonymousVisitorSeesRosterWithCaptainBadge`, `PublicUserProfileTests.AnonymousVisitorIsChallengedBeforeReachingTheParticipantProfile`, `SponsorManagementPlaywrightTests.AnonymousVisitorIsChallengedWhenOpeningSponsorManagement`, `TeamAuthorizationTests.AnonymousCallerCannotReadTeamManagementData` |
| member | sign-in/out, onboarding, profile edit/delete, individual registration, match participation | `AuthenticationFlowTests.MemberSignsInThroughTheLoginUiAndSignsOutAgain`, `ProfileAndOnboardingTests.*`, `RegistrationPlaywrightTests.MemberCanRegisterForIndividualTournamentAndRegistrationPersists`, `MatchLifecycleTests.*` |
| admin | organizer tools, sponsor management, tournament lifecycle, admin registration/leaderboard/resolution, global-search user results | `AuthenticationFlowTests.AdminSeesOrganizerToolsAndCanOpenSponsorManagement`, `SponsorManagementPlaywrightTests.*`, `TournamentDetailLifecyclePlaywrightTests.*`, `MatchAdminActionTests.*`, `RegistrationPlaywrightTests.AdminCanRemoveIndividualRegistrationFromAdministrationPanel`, `RegistrationTeamAdministrationPlaywrightTests.AdminCanRemoveTeamRegistrationWithReason`, `LeaderboardPlaywrightTests.AdminRecordsGuestAttemptThroughDialogAndStandingsPersist`, `MatchBracketProgressionTests.AdminReversalClearsTheAdvancedDownstreamSlot`, `PublicUserProfileTests.AdminOpensParticipantProfileWithLinkedIdentitiesAndEmptyMatchHistory`, `AdminOpensUserResultFromGlobalSearch` |
| captain | create team, invite/remove/transfer/logo/delete, roster submit and roster-edit gates | `TeamManagementPageTests.Captain*`, `TeamMembershipTests.Captain*`, `TeamDeleteTests.Captain*`, `TeamInviteFlowTests.Captain*`, `RegistrationTeamPlaywrightTests.Captain*`, `RegistrationTeamAdministrationPlaywrightTests.Captain*`, `MatchLifecycleTests.TeamCaptainSeesCaptainActionsAndCanConfirmTheEnd` |
| invitee | accept/decline invite and roster selection from the page and notification bell; expired/canceled/re-sent invites | `TeamInviteFlowTests.Invitee*`, `TeamInviteFlowTests.InviteNotification*`, `RegistrationNotificationPlaywrightTests.*` |
| non-member / stranger | captain-only API mutations and other members' invites rejected | `TeamAuthorizationTests.MemberCannotUseCaptainMutationsThroughTheApi`, `MemberCannotCancelSomeoneElsesInviteThroughTheApi`, `StrangerCannotReplyToSomeoneElsesInvite`, `MemberCannotLeaveOnBehalfOfAnotherMember`, `MatchAdminActionTests.MatchMutationEndpointsRejectAnonymousNonParticipantAndNonAdminCallers` |

## Backend domain states

| State / contract | Source | Frontend surface | Tests |
| --- | --- | --- | --- |
| `TournamentStatus` (Scheduled, InProgress, Completed, Canceled) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/TournamentStatus.cs` | status chips, filters, lifecycle buttons | `TournamentOverviewPlaywrightTests.AnonymousVisitorSeesTournamentCardWithStatusAndNoAdminControls`, `StatusFilterChipHidesNonMatchingStatusesAndExposesPressedState`, `NonScheduledTournamentCardClosesRegistration`, `TournamentDetailLifecyclePlaywrightTests.AdminCanCancelScheduledAndResetCanceledTournament`, `InProgressTournamentCannotBeDeletedAndFinishCompletesIt` |
| `MatchLifecycleState` (8 values) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/MatchLifecycleState.cs` | match dialog actions and states | `MatchLifecycleTests.BothSidesConfirmingTheEndUnlocksScoreReportingInSeparateSessions`, `OpponentReportingADifferentScoreOpensTheCorrectionWindow`, `DisputedMatchAllowsExactlyOneCorrectionPerSideAndCompletesWhenTheyAgree`, `MatchDeadlineTests.DisputeEscalatesToAdminResolutionWhenTheCorrectionWindowExpires`, `MatchAdminActionTests.*` |
| `BracketType` (5 values) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/BracketType.cs` | bracket rendering and create-dialog choices | `MatchBracketViewTests.StartedSingleEliminationRendersEveryBracketMatchWithItsParticipants`, `DoubleEliminationBracketExposesUpperLowerAndGrandFinalViews`, `RoundRobinTournamentRendersTheUnsupportedBracketNotice`, `LeaderboardTournamentReplacesTheBracketWithTheLeaderboard`, `TournamentAdminDialogPlaywrightTests.SelectingLeaderboardBracketSwapsParticipationForRankingMetric` |
| `TournamentFormat` (BestOf1/3/5) | `F/Models/Tournaments` and backend contracts | format label and score validation | `MatchScoringFormatTests.DialogShowsTheConfiguredMatchFormat`, `BestOfOneRejectsADrawReportAndKeepsTheMatchUnresolved`, `BestOfThreeRejectsAScoreBeyondTheDecisiveWin`, `BestOfFiveCompletesWhenBothSidesReportThreeToOne` |
| `TournamentRegistrationStatus` (PendingConfirmation, Active) and `TournamentRegistrationKind` (Individual, Team) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/TournamentRegistrationStatus.cs`, `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/TournamentRegistrationKind.cs` | registration panels and admin removal | `RegistrationPlaywrightTests.MemberCanRegisterForIndividualTournamentAndRegistrationPersists`, `MemberCanCancelIndividualRegistrationThroughConfirmation`, `RegistrationIsClosedForNonScheduledTournament`, `RegistrationTeamPlaywrightTests.CaptainSubmitsRosterAndInvitedMemberAcceptingActivatesTheRegistration`, `CaptainCannotConfirmTeamRegistrationUntilTheRosterIsComplete` |
| `RosterMemberConfirmationStatus` (AutoConfirmed, Pending, Confirmed) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/RosterMemberConfirmationStatus.cs` | roster chips and notification decisions | `RegistrationTeamPlaywrightTests.InvitedMemberDecliningRemovesThemFromTheRoster`, `RegistrationNotificationPlaywrightTests.InvitedMemberCanAcceptRosterSelectionFromNotificationBell`, `InvitedMemberCanDeclineRosterSelectionFromNotificationBell`, `RegistrationTeamAdministrationPlaywrightTests.CaptainSwapsAnActiveRosterMemberAndTheReplacementConfirmsToReactivate` |
| `TeamInviteStatus` (Pending, Accepted, Declined, Cancelled, Expired) | `B/Modules/Teams/Mercurius.Modules.Teams.Contracts/TeamInviteStatus.cs` | invite lists and notifications | `TeamInviteFlowTests.CaptainInvitesPlayerThenCancelsThePendingInvite`, `CaptainCannotReinviteAfterThreeDeclinesInsideCooldown`, `ExpiredPendingInviteIsHiddenFromTheInvitee`, `ExpiredPendingInviteIsSupersededWhenCaptainInvitesAgain`, `CanceledInviteCanBeSentAgain`, `CanceledInviteDisappearsForTheInviteeWithoutReload` |
| `LeaderboardParticipantKind` (LinkedUser, Guest) and the ranking metric | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/LeaderboardParticipantKind.cs` | guest badge, metric copy, ordering | `LeaderboardPlaywrightTests.AdminRecordsGuestAttemptThroughDialogAndStandingsPersist`, `AdminRecordsExistingLanUserAttemptWithoutGuestBadge`, `FastestTimeLeaderboardRanksShortestDurationFirst`, `LeaderboardValidationPlaywrightTests.GuestEntryRequiresDisplayNameAndResultValue` |
| `MatchResultKind` (Score, Forfeit, AdminResolution) | `B/Modules/Tournament/Mercurius.Modules.Tournament.Contracts/MatchResultKind.cs` | resolution, forfeit and reversal controls | `MatchLifecycleTests.ParticipantCanCancelAndThenConfirmAForfeit`, `MatchAdminActionTests.AdminResolvesADisputedMatchWithTheVerifiedScore`, `AdminReversalClearsTheOfficialResult`, `AdminForfeitConfirmationCanBeCancelledThenRecordsTheResult` |
| `SponsorTier` (Presenting, Gold, Silver, Bronze) | `B/Modules/Sponsorship/Mercurius.Modules.Sponsorship.Contracts/SponsorTier.cs` | tier sections and descriptions | `PublicSiteTests.SponsorsPageRendersSeededTiersInOrderWithTierSpecificDescriptions` |
| tournament finish gate on a ranked leaderboard result and on an unresolved final | backend service | disabled Finish plus reason | `LeaderboardPlaywrightTests.LeaderboardCannotBeFinishedUntilAResultExists`, `MatchBracketProgressionTests.FinishingATournamentWithAnUnresolvedFinalIsRefusedAndStaysOngoing`, `WinningTheFinalThenFinishingTheTournamentPublishesPlacements` (BestOf1 final won 1-0, then placements published) |
| realtime team invalidation | `F/Services/TeamRealtimeService.cs:70-71` | second-browser roster/team refresh without reload | `TeamRealtimeTests.CaptainRosterRefreshesWhenInviteeAcceptsInviteFromTheirOwnBrowser`, `CaptainRosterRefreshesWhenMemberLeavesWithoutReload`, `RemovingMemberUpdatesCaptainAndRemovedMemberWithoutReload`, `DeletingTeamRefreshesConnectedMembersAndInviteesWithoutReload` |

`TeamMembershipChangeKind` (Joined, Removed, Left) has no UI representation: the frontend only
receives `TeamMembershipChangedEvent` as an invalidation signal
(`F/DTOs/Participants/Teams/TeamManagementDTOs.cs:49`, `F/Services/TeamRealtimeService.cs:71`) and
refetches. `SearchIndexRebuildJobStatus` (Pending, Running, Completed, Failed) belongs to the
internal discovery jobs, which no client calls.

### Load-failure and error states

| Surface | Source | Tests |
| --- | --- | --- |
| `/profile` load failure | `F/Components/Pages/Users/Profile.razor:11-16` (shared `StatusPage`, in-place `OnRetry` -> `Profile.razor.cs:56`) | `ProfileAndOnboardingTests.ProfileLoadFailureShowsUnavailableStateAndRecovers` |
| `/teams/manage` load failure | `F/Components/Pages/Teams/ManageTeams.razor:15-21` (shared `StatusPage`, in-place `OnRetry` -> `ManageTeams.razor.cs:123`) | `TeamManagementPageTests.TeamManagementLoadFailureShowsUnavailableStateAndRecovers` |
| `/teams/{TeamName}` load failure | `F/Components/Pages/Teams/PublicTeamProfile.razor:14-20` (shared `StatusPage`, in-place `OnRetry` -> `PublicTeamProfile.razor.cs:88`) | `TeamPublicProfileTests.PublicTeamProfileLoadFailureShowsUnavailableStateAndRecovers` |
| `/users/{username}` load failure | `F/Components/Pages/Users/PublicUserProfile.razor:15-21` (shared `StatusPage`, in-place `OnRetry` -> `PublicUserProfile.razor.cs:97`) | `PublicUserProfileTests.PublicUserProfileLoadFailureShowsUnavailableStateAndRecoversInPlace` |
| `/tournaments/{id}` load failure | `F/Components/Pages/Tournaments/TournamentDetail.razor:32-38` (shared `StatusPage`, in-place retry plus a back-to-tournaments link) | `TournamentDetailLifecyclePlaywrightTests.TournamentDetailLoadFailureShowsRetryAndRecoversInPlace` |
| `/tournaments/{id}` not found | `F/Components/Pages/Tournaments/TournamentDetail.razor:19-27` (shared `StatusPage`, info tone) | `TournamentDetailLifecyclePlaywrightTests.UnknownTournamentIdShowsNotFoundState` |
| `/complete-profile` load failure | `F/Components/Pages/Users/CompleteProfile.razor:9-17` (shared `StatusPage`, in-place retry; the duplicate status markup was removed) | no dedicated E2E case in this suite; the shared-surface markup is covered by `StatusPageMarkupContractTests` in `Mercurius.LAN.Web.ContractTests` |
| `/` tournament/sponsor failure | `F/Components/Pages/Home.razor:79-93` | `PublicSiteTests.HomeTournamentLoadFailureShowsTheRetryStateAndRecoversAfterTheApiIsRestored`, `HomeSponsorFailureDegradesOnlyTheSponsorSection` |
| `/tournaments` load/sponsor failure | `F/Components/Pages/Tournaments/TournamentsOverview.razor:14-22` (shared `StatusPage`, in-place `OnRetry`), `TournamentsOverview.razor.css` (obsolete `tournaments-load-error` rules removed) | `PublicSiteTests.TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry`, `TournamentsOverviewSponsorFailureDegradesOnlyTheSponsorSection` |
| unknown username | `F/Components/Pages/Users/PublicUserProfile.razor.cs:77`, `.razor:15-31` (shares the unavailable state; the localized not-found branch is unreachable from the UI) | `PublicUserProfileTests.UnknownUsernameShowsTheUnavailableStateRatherThanNotFound` |
| unknown team | `F/Components/Pages/Teams/PublicTeamProfile.razor:24-32` (shared `StatusPage`, info tone) | `TeamPublicProfileTests.UnknownTeamNameShowsNotFoundStatusPage` |

## Per-class inventory (232 cases)

| Class | Cases |
| --- | --- |
| `AuthenticationFlowTests` | 15 |
| `InfrastructureSmokeTests` | 1 |
| `LeaderboardPlaywrightTests` | 7 |
| `LeaderboardValidationPlaywrightTests` | 3 |
| `MatchAdminActionTests` | 8 |
| `MatchBracketProgressionTests` | 5 |
| `MatchBracketViewTests` | 7 |
| `MatchDeadlineTests` | 3 |
| `MatchLifecycleTests` | 11 |
| `MatchScheduleFilterTests` | 3 |
| `MatchScoringFormatTests` | 8 |
| `ProfileAndOnboardingTests` | 15 |
| `PublicSiteTests` | 33 |
| `PublicUserProfileTests` | 9 |
| `RegistrationNotificationPlaywrightTests` | 2 |
| `RegistrationPlaywrightTests` | 7 |
| `RegistrationTeamAdministrationPlaywrightTests` | 5 |
| `RegistrationTeamPlaywrightTests` | 5 |
| `SponsorManagementPlaywrightTests` | 7 |
| `TeamAuthorizationTests` | 6 |
| `TeamDeleteTests` | 4 |
| `TeamInviteFlowTests` | 16 |
| `TeamManagementPageTests` | 10 |
| `TeamMembershipTests` | 7 |
| `TeamPublicProfileTests` | 7 |
| `TeamRealtimeTests` | 4 |
| `TournamentAdminDialogPlaywrightTests` | 4 |
| `TournamentDetailLifecyclePlaywrightTests` | 9 |
| `TournamentOverviewPlaywrightTests` | 8 |
| `TournamentPartnerAndSwissPlaywrightTests` | 3 |

## Source-backed discrepancies

1. **Swiss is creatable only through the API and never startable from the UI.** The create dialog
   omits Swiss (`F/Components/Pages/Tournaments/AddTournamentDialog.razor.cs:36-41`), the detail
   page blocks Start with `unsupportedSwissBracket`
   (`F/Components/Pages/Tournaments/TournamentDetail.razor.cs:315-316`), and the backend factory has
   no Swiss case, so it falls to the `NotSupportedException` default
   (`B/Modules/Tournament/Mercurius.Modules.Tournament/Application/Services/MatchModeratorFactory.cs:22`).
   UI path asserted by `TournamentPartnerAndSwissPlaywrightTests.SwissTournamentCannotBeStartedAndShowsUnsupportedReason`.
2. **RoundRobin is backend-supported but UI-unsupported.** The factory resolves
   `RoundRobinMatchModerator`
   (`B/Modules/Tournament/Mercurius.Modules.Tournament/Application/Services/MatchModeratorFactory.cs:20`)
   and the API can create and start a
   RoundRobin tournament, but the matches tab renders `bracketUnsupported` for it and the create
   dialog cannot select it. Asserted by
   `MatchBracketViewTests.RoundRobinTournamentRendersTheUnsupportedBracketNotice`.
3. **`SponsorContext` has four values and one producer.** The only placement writer hardcodes
   `SponsorContext.TournamentPartner`
   (`F/Components/Pages/Tournaments/TournamentDetail.razor.cs:845`), and the admin UI offers a
   single sponsor select. The label, eyebrow and display-order switches for `CateringPartner`,
   `InfrastructurePartner` and `PrizePartner` (`F/Extensions/SponsorExtensions.cs:43-74`) are
   therefore unreachable.
4. **`/users/{Username}` is admin-only while search is offered to everyone.** The page carries
   `@attribute [Authorize(Roles = "admin")]` (`F/Components/Pages/Users/PublicUserProfile.razor:2`)
   while the anonymous public API resolves any username
   (`B/Modules/Identity/Mercurius.Modules.Identity/Endpoints/UserEndpoints.cs:27-36`), and global
   search disables and ignores user results for non-admins
   (`F/Components/Layout/NavMenu.razor.cs:446,458-459`). Asserted by
   `AuthenticationFlowTests.MemberSearchResultForAnotherUserIsDisabledInGlobalSearch` and
   `MemberCannotOpenTheAdminOnlyUserProfileEvenThoughThePublicApiListsTheUsername`.
5. **Unknown username renders "unavailable", not "not found".** Every API failure, including the
   404, sets `_hasError` (`F/Components/Pages/Users/PublicUserProfile.razor.cs:77`), so the
   `_hasError` branch (`F/Components/Pages/Users/PublicUserProfile.razor:13-22`) wins over the
   `_profile is null` branch (`F/Components/Pages/Users/PublicUserProfile.razor:23-31`),
   which is unreachable from the UI. Asserted by
   `PublicUserProfileTests.UnknownUsernameShowsTheUnavailableStateRatherThanNotFound`. The
   equivalent team-profile not-found state is reachable
   (`TeamPublicProfileTests.UnknownTeamNameShowsNotFoundStatusPage`).
6. **Overview search/status/participation filters are client-side over one page.** `PageSize = 24`
   and only the fetched page is filtered
   (`F/Components/Pages/Tournaments/TournamentsOverview.razor.cs:63,139`), so a match on page two is
   invisible to the filters. Pagination itself is covered by
   `TournamentOverviewPlaywrightTests.PaginationShowsNextPageWhenMoreThanOnePageOfTournamentsExists`.
7. **Some destructive actions skip confirmation.** Sponsor delete and tournament
   cancel/reset/finish/delete run straight from `@onclick`
   (`F/Components/Pages/Admin/SponsorManagement.razor:76` and `.razor.cs:170`;
   `F/Components/Pages/Tournaments/TournamentDetail.razor:109,118,128,134` and
   `.razor.cs:372,390,399,408`), while team removal, leave and delete do confirm
   (`TeamMembershipTests.CaptainCanCancelTeammateRemoval`, `MemberCanCancelLeavingTeam`,
   `TeamDeleteTests.CaptainCanCancelTeamDeletion`). The lifecycle actions themselves are exercised
   by `TournamentDetailLifecyclePlaywrightTests.AdminCanCancelScheduledAndResetCanceledTournament`
   and `AdminCanDeleteScheduledTournamentAndReturnsToOverview`.

## Not E2E-applicable

- **Auth0-hosted flows.** Social connections, MFA and email verification/reset delivery: the
  fixture swaps in a local OIDC stub (`T/Infrastructure/LocalOidcServer.cs`) and the profile tests
  assert the generic toast only
  (`ProfileAndOnboardingTests.UnverifiedProfileOffersResendVerificationAndReportsGenericOutcome`,
  `ProfilePasswordResetReportsGenericOutcome`). The application under test still uses the real OIDC
  cookie and JWT chain against that local provider.
- **External email delivery.** The contact form now runs a real SMTP conversation against the
  fixture's loopback sink, so the success path is covered; delivery to an external recipient and a
  real mail provider remain outside the fixture.
- **Admin user CRUD** (`B/Modules/Identity/Mercurius.Modules.Identity/Endpoints/UserEndpoints.cs:108-144`)
  and **discovery rebuild jobs**
  (`B/Modules/Discovery/Mercurius.Modules.Discovery/Endpoints/DiscoveryEndpoints.cs:42-61`): no
  route, component or client caller reaches them; only `IUserClient` wrapper methods exist.
- **`PUT /v1/lan/matches/{matchId}`** (`F/APIClients/ILANClient.cs:238-239`): wrapped by
  `TournamentService.UpdateMatchScoresAsync` (`F/Services/TournamentService.cs:155-159`) but no
  component calls it; score entry uses `PUT /v1/lan/matches/{matchId}/score` instead.

## Gap scan

A source-level re-audit covering every route, page action, role and domain enum found no untested
flow beyond the cases already listed. The contact-form SMTP success path, previously listed as not
E2E-applicable because no SMTP server was configured, is now covered by the test-only loopback sink
and is no longer a gap. The remaining genuine gaps are the external ones above: Auth0-hosted
MFA/social flows, delivery to external mail recipients, and the backend-only surfaces with no UI
caller.

## Remaining product findings

Recorded as found, not fixed. These are lifecycle findings, not clean-log claims.

1. **`ManageTeams` prerender/HTTP-scope disposal race (non-fatal, intermittent).** In the
   `full-round6` fixture
   (`TestResults/PlaywrightE2E/20261002T231452_144388_cc8f6b4a18104f01bb7deb1643a5ce01`),
   `frontend.log` records at `23:18:54.328` UTC (lines 31959-31985,
   `fail: Microsoft.AspNetCore.Server.Kestrel[13]`) a `System.ObjectDisposedException` for
   `IServiceProvider`, with the stack
   `ManageTeams.RequestRenderAsync` -> `RenderIfActiveAsync` -> `LoadSummaryAsync` -> `InitializeAsync`
   -> `DisposeAsync`. Source anchors: `F/Components/Pages/Teams/ManageTeams.razor.cs:64,135,182,184-195,621-626`.
   The race surfaces during the first render for
   `TeamAuthorizationTests.MemberOnlySeesRosterAndMembershipControls` on `/teams/manage` (trace
   `163`). It recurs in the `full-round9` fixture
   (`TestResults/PlaywrightE2E/20261002T235953_80204_2ab1292db1c2479e9e9b718ce44ebd0f`) at
   `00:04:06.056` UTC (`frontend.log` lines 32083-32109, `fail: Kestrel[13]`) with the same stack,
   while the interactive page stayed healthy. The page's roster and control assertions passed, and
   the same log line is absent from the `full-round7` fixture
   (`TestResults/PlaywrightE2E/20261002T232042_180336_1613b11656d3497fbdcb373da090ec98`), so this is
   an intermittent SSR lifecycle race rather than browser teardown, and the frontend log is not
   clean. It did not recur in the final pair: both `full-round10` and `full-round11` frontend logs
   contain 0 `ObjectDisposedException` records.
2. **`CustomAutocomplete.DisposeListener` async-void teardown.** An explicit Sponsor-edit reload
   produces a `JSDisconnectedException` from `CustomAutocomplete<T>.DisposeListener`
   (`F/Components/Shared/CustomAutocomplete.razor.cs:95,98`). It appears in both fixtures
   (`frontend.log` `23:18:48.668`, lines 31069-31076, and `23:24:31.675`, lines 31109-31116;
   `fail: CircuitHost[111]`, trace `158`). The active page recovers, so it is a separate known
   teardown bug rather than a page regression.
3. **Mud provider active-circuit errors: 0** in the `full-round6` and `full-round7` logs
   (independent reviewer confirmation).

## Archive and review status

Four archived OpenSpec changes cover this work: `2026-10-02-fix-forbidden-page-circuit`,
`2026-10-02-fix-server-status-and-aria-states`,
`2026-10-02-keep-authenticated-header-search-usable`, and the SMTP expansion, which modifies an
existing capability rather than adding one. Strict validation passes with 32 checks on the frontend
and 31 on the backend, so the frontend count stays 32 rather than 33. The independent SMTP source
review passed with no findings, and the receipt-bound, draft-preserved, retry, and hold-cleanup items
were resolved against the real two-case TRXs. The runtime fatal-log review for `full-round8` and
`full-round9` is closed: full9 carried the `ManageTeams` disposal recurrence and the
`CustomAutocomplete` teardown record noted above. The five-flow synchronization review passed. The
final frontend contracts pass 364/364, both final solo checks pass 1/1, and the tester finished with
0 hosts left and PostgreSQL untouched.

The backend revision moved to the published `3cfc07f76cd6cb66b0ac17595e01359d3d966606`
(`refs/pull/139/head`), which makes deleted-team notifications best-effort and
cancellation-independent on top of the earlier serialization work. PR #139 is checked clean and Sol
and DSE source review passed. Fresh independent post-commit validation on that revision: `LAN.API.sln`
restore (`dotnet restore LAN.API.sln -m:1 --nologo`) exited 0; the build
(`--no-restore -m:1 -nr:false -p:UseSharedCompilation=false --nologo`) reported 0 warnings and
0 errors; the new fan-out tests passed 4/4 twice; Teams passed 104/104; the full solution
(`dotnet test LAN.API.sln --no-build`) passed **715/715** across 8 projects; and
`dotnet format --verify-no-changes --no-restore` passed twice. TRXs are under
`tests/Mercurius.Modules.Teams.Tests/TestResults/backend-postcommit-fanout-verify{1,2}/verify-run{1,2}.trx`
and `.../backend-postcommit-fanout-verify-teams-full/verify-teams-full.trx`, with invocation logs at
`%TEMP%/backend-postcommit-{restore,build,run1,run2,teams-full,sln-full,format}.log`.

Historical: the earlier published pin `b98f197f8622f3108b56053a7bc97f7ddfd6a6b0` serialized team
deletion with invite maintenance across 8 paths (concurrency/advisory lock for all writers and
maintenance, logo, captured mutation recipients, and `None` post-commit on both deletes), with an
independent `LAN.API` restore/build at 0 warnings and 0 errors, and focused 5x3, maintenance-G1 solos
2x1, new 6/6, and full Teams 100/100 all passing. Its full solution stood at 710/710 before the new
test-only G1 addition, and the full 711-case run at that revision was never executed; the executed
full-solution number is the 715 above.

The workflow is repinned from `b98f197...` to `3cfc07f...`. The last published frontend tip
(`e9d58c1`, pin `b98f197...`) ran the paired Ubuntu suite as GitHub Actions `37114424083` and
finished 231/232: `TeamInviteFlowTests.InviteNotificationBadgeLetsInviteeDecline` (assertion line 305)
expected the invitee's "Notifications with 1 unread" button and saw an unread count of 0. The
workflow's SSL trust and check, restore, build, and Playwright install all passed, so the records do
not support an SSL cause. The test-only preseed reorder (seed the pending invite before the invitee
page loads its notification summary) now passes focused 3/3 and inside the combined 20-case run, so
that case has a proven focused runtime, and it also passes on the paired Ubuntu run `37146174575`. The
Windows 232 suite at `b98f197...` (`full-232-final`) stays green, and no CSS or production file is
involved.
Two backend review threads raised on `b98f197...` (`PRRT_kwDOOwmpHc6ol8HO` post-commit `None` and
other `MembershipChanged` calls; `PRRT_kwDOOwmpHc6ol8HQ` attempt all deleted recipients after the
first failure) are implemented by `3cfc07f...`.

The `TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload` flake (1 of 4, then 1 of 1
in the repeats above) now has a REST-first startup repair: `TeamRealtimeService.StartAsync` shares one
in-flight start task behind a gate and `JoinTeamsAsync` awaits it; `ManageTeams` renders the first
REST summary promptly before realtime startup begins, reconciles in that non-loading state, joins
only the team ids missing from the displayed snapshot before assigning the reconciled summary, and
keeps the displayed snapshot when the reconcile GET or the delta join fails. The `#team-workspace`
section carries `data-live-updates-ready`, which the E2E helper waits on. Frozen SHA-256: service
`8790B519ECC52B6FBDFFCCD94FB10928947B6226E3F2CFD7CE353FD5B7F69D2C` (unchanged), code-behind
`BEF49C148912D270577F755ED8AEC1D1717D54566D0B1C81E656802B26153B48`, Razor
`6ED0DD3677994BEBB87880DE104CE71227B6C207660406E6923614C71415B33D`, helper
`454783DB8D207D4C087B1F891617BAABDF1A69E5036865D2E0BA48C08B996E89`, contract test
`94543E7030B912756579AF964340725B6C3EDF125D316130AD667C52D7FF1E6F`. Sol's final source review
passed with minor findings and DSE's final review passed on these sources; nothing blocking. The new
precondition is
`ComponentLifecycleBehaviorTests.ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending` (pending
TCS plus recorded render; non-E2E lifecycle precondition, normal flows stay real E2E). The earlier
`T/TestResults/realtime-gate-final` results (3 x leave, 20-case combined, 364 contracts, and a 232/232
full run whose `dotnet test` stdout reports 5 m 37 s) were produced on the superseded
`_isLoading`-held-through-join shape and are historical only.

The current local gate `T/TestResults/realtime-rest-first-final` is complete and all green: the new
lifecycle case (1), the three leave repeats (1 each), the combined 20 (20), the contracts suite (365),
and the full suite (`full-232/full-232.trx`, 232 total, 232 passed, 0 failed, 0 skipped across 30
classes, `dotnet test` stdout duration 5 m 34 s). The tester confirmed the exact argv: builds run as
`dotnet build <project> --no-restore --nologo` and tests as
`dotnet test <project> --no-build --no-restore [--filter <filter>] --logger ... --results-directory ...`,
with no `-m:1`, `-nr:false`, or `-p:UseSharedCompilation` flags.
`MERCURIUS_E2E_ARTIFACTS=<base>\PlaywrightE2E` was set for the new-case, leave, combined, and full
runs.

```powershell
dotnet build tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-restore --nologo
dotnet build tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj --no-restore --nologo
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=full-232.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\realtime-rest-first-final\full-232"
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName~TeamRealtimeTests|FullyQualifiedName~TeamInviteFlowTests" --logger "trx;LogFileName=combined-20.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\realtime-rest-first-final\combined-20"
dotnet test tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending" --logger "trx;LogFileName=new-case.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\realtime-rest-first-final\new-case"
dotnet test tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj --no-build --no-restore --logger "trx;LogFileName=contracts.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\realtime-rest-first-final\contracts"
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName~TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload" --logger "trx;LogFileName=leave-N.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\realtime-rest-first-final\leave-N"
```

The last line runs three times with `N` = 1, 2, 3. Reported durations: full 5 m 34 s, contracts 5 s,
combined 34 s, new-case 21 ms, leave-1 7 s, leave-2 and leave-3 5 s.

REPRODUCTION only (not the commands that were executed): adding
`-m:1 -nr:false -p:UseSharedCompilation=false` is optional when re-running these parts serially.

The paired Ubuntu 232 run is green: GitHub Actions `37146174575` (frontend `caea4a50`, backend pin
`3cfc07f...`) passed 232/232 with 0 failed and 0 skipped in a `dotnet test` stdout duration of
7 m 41 s, closing the local-proof loop on the same product source. Its exact test command is
`dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=e2e.trx"`,
and no artifact was uploaded because the workflow's upload step runs only on failure.
