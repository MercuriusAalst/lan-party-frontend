# Verification record

Command-level evidence for the E2E suite and the repository checks run alongside it. Each result
below comes from a recorded run, and the artifacts are referenced by absolute path. This file records
the commands and outcomes; it is not a re-execution.

The E2E and frontend commands ran from the frontend repository root and the backend commands from the
backend repository root. The `--results-directory` arguments below are absolute and point at the
isolated verification checkouts used for these runs.

## Frontend solution (final)

```powershell
dotnet build Mercurius.LAN.sln -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Exit 0 with 4 NU1900 warnings and 0 errors.

```powershell
dotnet test Mercurius.LAN.sln --no-build --no-restore --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.ContractTests\TestResults\frontend-final-expanded" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **364 passed, 0 failed, 0 skipped**
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.ContractTests\TestResults\frontend-final-expanded\svenp_MEAN_MACHINE_2026-10-03_02_41_28_net10.0.trx`).

An earlier 364-case run under
`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.ContractTests\TestResults\frontend-final`
predates the expanded source and is superseded by the run above.

## CI runs (GitHub Actions)

| Run | Commit | Result |
| --- | --- | --- |
| `37111821272` | frontend `ddc9b43`, backend pin `ca3b...` | **229 passed, 0 failed, 0 skipped** |
| `37110243446` | frontend `1e84604`, backend pin `ca3b...` | 228 passed, 1 failed |
| `37114424083` | frontend `e9d58c1`, backend pin `b98f197...` | 232 total, 231 passed, 1 failed, 0 skipped |
| `37146174575` | frontend `caea4a50`, backend pin `3cfc07f...` | **232 passed, 0 failed, 0 skipped** |

- `37146174575` is the first paired frontend/backend Ubuntu run
  (`https://github.com/MercuriusAalst/lan-party-frontend/actions/runs/37146174575`). It completed
  SUCCESS `2026-10-03T18:58:06Z` → `19:08:07Z`; checkout, .NET setup, certificate trust, restore,
  build (0 errors), Playwright Chromium install, and the test step all passed. The test step printed
  `Passed! - Failed: 0, Passed: 232, Skipped: 0, Total: 232, Duration: 7 m 41 s` at `19:08:04Z`.
- Exact executed CI test command (no serial flags):

  ```bash
  dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=e2e.trx"
  ```

  The trust step exported `SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"` into
  `$GITHUB_ENV` and reported "Successfully trusted the existing HTTPS certificate." with a
  `CN=localhost` trusted certificate found. No test-results artifact was uploaded because the upload
  step runs only on failure; that is a skipped workflow step, not skipped tests (`Skipped: 0`). The
  sibling Application workflow `37146174550` also succeeded (both jobs). The full job log is at
  `D:\Github Repositories\lan-party\.tmp\e2e-37146174575.log` (parent temp, outside this repo).
- `37111821272` ran the real `dotnet test` job in 7 m 31 s, finishing `2026-10-03T09:14:18Z`; the job
  log is kept at `D:\Github Repositories\lan-party\.tmp\e2e-37111821272.log`. Trust, restore, build
  (13 warnings, 0 errors), and the Playwright Chromium install all passed. The workflow uploads
  test-results artifacts only on failure, so this green run has no uploaded artifact.
- `37110243446` failed on the raw quoted-printable LF-marker wrap in the SMTP MIME assertion;
  `ddc9b43` replaced that with the semantic MIME-decoded assertion.
- `37114424083` failed one case on Ubuntu:
  `TeamInviteFlowTests.InviteNotificationBadgeLetsInviteeDecline` (assertion line 305) expected the
  invitee's "Notifications with 1 unread" button and saw an unread count of 0. The workflow's SSL
  trust and check, restore, build, and Playwright Chromium install all passed, so the records do not
  support an SSL cause. The failure artifact is unpacked at
  `D:\Github Repositories\lan-party\.tmp\ci-artifacts-37114424083` (`e2e.trx` plus the fixture
  traces).

## Backend solution

```powershell
dotnet restore LAN.API.sln -m:1 -nr:false -p:UseSharedCompilation=false --nologo
dotnet build LAN.API.sln -m:1 -nr:false -p:UseSharedCompilation=false --nologo
dotnet test LAN.API.sln --no-build --no-restore --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\backend-playwright-e2e\tests\TestResults\backend-final" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Restore and build exited 0 with 0 warnings.
- Test result: **705 passed, 0 failed, 0 skipped across 8 projects**
  (`D:\Github Repositories\lan-party\.tmp\backend-playwright-e2e\tests\TestResults\backend-final`).
  This is an earlier backend revision.

The current backend revision is `3cfc07f76cd6cb66b0ac17595e01359d3d966606` (published;
`refs/pull/139/head`), which makes deleted-team notifications best-effort and cancellation-independent
on top of the earlier serialization work. PR #139 is checked clean and Sol and DSE source review
passed.

```powershell
dotnet restore LAN.API.sln -m:1 --nologo
dotnet build LAN.API.sln -m:1 -nr:false -p:UseSharedCompilation=false --nologo --no-restore
dotnet format --verify-no-changes --no-restore
```

- Restore exited 0 with all projects already up to date; the build exited 0 with **0 warnings and
  0 errors**.
- `dotnet format --verify-no-changes --no-restore` passed on two runs, each with an empty log.

Post-commit test evidence on that revision:

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\backend-playwright-e2e\tests\Mercurius.Modules.Teams.Tests\TestResults\backend-postcommit-fanout-verify1\verify-run1.trx` | 4 passed, 0 failed (new fan-out cases) |
| `D:\Github Repositories\lan-party\.tmp\backend-playwright-e2e\tests\Mercurius.Modules.Teams.Tests\TestResults\backend-postcommit-fanout-verify2\verify-run2.trx` | 4 passed, 0 failed (repeat) |
| `D:\Github Repositories\lan-party\.tmp\backend-playwright-e2e\tests\Mercurius.Modules.Teams.Tests\TestResults\backend-postcommit-fanout-verify-teams-full\verify-teams-full.trx` | 104 passed, 0 failed |
| `dotnet test LAN.API.sln --no-build` (`%TEMP%\backend-postcommit-sln-full.log`) | **715 passed, 0 failed, 0 skipped** across 8 projects |

- Per project: Api 194, Identity 53, Teams 104, Tournament 204, Sponsorship 18, Platform 117,
  Discovery 16, Media 9 — 715 total.
- Invocation logs: `%TEMP%\backend-postcommit-{restore,build,run1,run2,teams-full,sln-full,format}.log`.

The earlier backend revision `b98f197f8622f3108b56053a7bc97f7ddfd6a6b0` (published) serialized team
deletion with invite maintenance across 8 paths: concurrency/advisory-lock handling for all writers
and maintenance, logo, captured mutation recipients, and `None` post-commit on both deletes.

Backend verification on that revision:

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\be-verify\full` (8 project TRXs) | 710 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\be-verify\focused-1`, `focused-2`, `focused-3` | 5 passed, 0 failed each |
| `D:\Github Repositories\lan-party\.tmp\be-verify\pg-only-1` | 3 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\be-verify-2\maint-solo-1`, `maint-solo-2` | 1 passed, 0 failed each |
| `D:\Github Repositories\lan-party\.tmp\be-verify-2\new-6` | 6 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\be-verify-2\full-teams` | 100 passed, 0 failed |

- Independent restore/build of `LAN.API` with `-m:1 -nr:false -p:UseSharedCompilation=false` reported
  0 warnings and 0 errors.
- The full solution stood at 710/710 before the new test-only G1 addition; the full 711-case run at
  that revision was never executed. The executed full-solution result is the 715-case run above.

## E2E project

```powershell
dotnet restore tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -m:1 -nr:false -p:UseSharedCompilation=false --nologo
dotnet build tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Restore exited 0 with one NU1900 advisory (service index unreachable).
- Build exited 0 with 2 NU1900 warnings and 0 errors.

## E2E suite (229-case revision, green)

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=full-round10.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round10" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **229 passed, 0 failed, 0 skipped** across 30 classes
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round10\full-round10.trx`,
  finished 02:34:57 (+02:00)). Exit 0.
- Fixture artifacts are under
  `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\PlaywrightE2E\20261003T002927_162276_e07634dc96ed4233b8cccaedaa465674`
  (242 traces, 0 page errors, 0 `ObjectDisposedException`, 0 Mud duplicate-provider errors, 2 accepted
  `CustomAutocomplete` cleanup records).

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=full-round11.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round11" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **229 passed, 0 failed, 0 skipped** across 30 classes
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round11\full-round11.trx`,
  finished 02:40:56 (+02:00)). Exit 0.
- Fixture artifacts are under
  `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\PlaywrightE2E\20261003T003528_137568_253b25e926014f4192a680a0206401b7`.
  The independent trace review passed; see "Final review" below.

Earlier 229-case green run, before the synchronization repair (coverage count only):

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=full-round8.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round8" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **229 passed, 0 failed, 0 skipped** across 30 classes
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round8\full-round8.trx`,
  finished 23:59:21 UTC).

Historical failure, cause fixed by the test-only synchronization:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --logger "trx;LogFileName=full-round9.trx" --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round9" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **228 passed, 1 failed, 0 skipped** across 30 classes
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round9\full-round9.trx`,
  `ResultSummary outcome="Failed"`).
- Failing case:
  `RegistrationTeamAdministrationPlaywrightTests.CaptainCanKeepThenCycleTeamRegistrationCancellation`.
  After a reload that raced the cancellation, the `#registration` panel still offered "Manage
  registration" and "Cancel registration" with 0 participants, so the expected "Register now" button
  was absent. The fix is test-only and synchronizes on the observable completed panel state by
  awaiting the exact `#registration` "Register now" button before the reload and asserting it again
  after the reload, in both the captain team-registration and individual registration tests.
- Fixture artifacts are under
  `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\PlaywrightE2E\20261002T235953_80204_2ab1292db1c2479e9e9b718ce44ebd0f`,
  with trace 146 and the frontend log.

Historical runs at earlier revisions:

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round7\full-round7.trx` | 227 passed, 0 failed, 0 skipped; finished 23:26:01 UTC |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round6\full-round6.trx` | 227 passed, 0 failed, 0 skipped; finished 23:20:20 UTC |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round5\full-round5.trx` | 226 passed, 0 failed, 0 skipped |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-round4\full-round4.trx` | 226 passed, 0 failed, 0 skipped; exact invocation not recorded |

The 227-case runs postdate the forbidden-page circuit fix but predate the SMTP expansion, and the
226-case runs predate the forbidden-page circuit fix.

### 232-case revision (header regression)

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\public-user-profile-green\public-user-profile-green.trx` | 9 passed, 0 failed (`PublicUserProfileTests`, all three header widths) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-232\full-232.trx` | 231 passed, 1 failed; historical 1025 failure from the not-yet-indexed search read model |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\full-232-final\full-232-final.trx` | 232 passed, 0 failed, on backend `b98f197...`; finished 11:42:42 (+02:00) |

- The fixer reported only a project `dotnet build`; that exact command line is not captured here.
  The class and full-suite invocations were not recorded, so no flags are invented for them.
- The 1025 failure was fixed by reusing the existing `PublicSiteTests.WaitForSearchResultAsync`
  deterministic API precondition; no assertion was weakened.
- Header independent review is FINAL PASS: current diff + 9/9 `PublicUserProfileTests` + 232/232 full
  + the three header traces showing no overflow.

## Cancellation fix probes

The fix synchronizes five flows across three files: individual registration cancellation
(`RegistrationPlaywrightTests.cs`), roster accept and roster decline
(`RegistrationTeamPlaywrightTests.cs`), and the captain cancellation cycle plus roster swap
(`RegistrationTeamAdministrationPlaywrightTests.cs`). Probe invocations were not recorded.

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\final-classes\final-classes.trx` | 17 passed, 0 failed (affected registration tests) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\final-captain1\final-captain1.trx` | 1 passed, 0 failed (captain cancellation) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\final-captain2\final-captain2.trx` | 1 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\final-captain3\final-captain3.trx` | 1 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\lane1\lane-probe1.trx` | 4 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\lane2\lane-probe2.trx` | 4 passed, 0 failed |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\fix-probes\lane3\lane-probe3.trx` | 4 passed, 0 failed |

These probes preceded the green `full-round10`/`full-round11` pair and are consistent with it.

## SMTP coverage

| Artifact | Result |
| --- | --- |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\contact-assertion-run1\contact-assertion-run1.trx` | 2 passed, 0 failed (`ContactFormSurfacesMailTransportFailureForValidMessage`, `ContactFormSendsEmailWithReplyToAndClearsAfterSuccess`) |
| `D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\contact-assertion-run2\contact-assertion-run2.trx` | 2 passed, 0 failed, same two cases |
| `PublicSiteTests` across three fresh fixtures | 33 of 33 passed |

The contact-assertion runs were invoked without a recorded command line. The contact form talks to
the fixture's loopback SMTP sink, so no external server or credentials are involved. The independent
SMTP source review passed with no findings.

## Solo runs

Final forbidden-page circuit check:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName=Mercurius.LAN.Web.E2ETests.AuthenticationFlowTests.ForbiddenPageFromDirectRequestStaysInteractiveThroughInCircuitNavigation" --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-forbidden-final" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **1 passed, 0 failed**
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-forbidden-final\svenp_MEAN_MACHINE_2026-10-03_02_41_54_net10.0.trx`).

Final SMTP success check:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName=Mercurius.LAN.Web.E2ETests.PublicSiteTests.ContactFormSendsEmailWithReplyToAndClearsAfterSuccess" --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-smtp-final" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Result: **1 passed, 0 failed**
  (`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-smtp-final\svenp_MEAN_MACHINE_2026-10-03_02_42_14_net10.0.trx`).

Earlier solo checks:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName=Mercurius.LAN.Web.E2ETests.PublicUserProfileTests.AdminOpensUserResultFromGlobalSearch" --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-search" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --no-build --no-restore --filter "FullyQualifiedName=Mercurius.LAN.Web.E2ETests.RegistrationNotificationPlaywrightTests.InvitedMemberCanAcceptRosterSelectionFromNotificationBell" --logger trx --results-directory "D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults\solo-roster" -m:1 -nr:false -p:UseSharedCompilation=false --nologo
```

- Results: **1 passed, 0 failed** each (`solo-search`, `solo-roster`).

## Frontend notification and realtime probes (historical, pre-repair)

These runs exercised the 232-case working tree with the notification reorder and the earlier realtime
work. Each invocation was launched without a recorded command line; the TRX files are the
evidence, and the relative paths below are under
`D:\Github Repositories\lan-party\.tmp\playwright-e2e\tests\Mercurius.LAN.Web.E2ETests\TestResults`.

| Artifact | Result |
| --- | --- |
| `nfv-badge-focus-1\badge-focus-1.trx` | 1 passed, 0 failed (`TeamInviteFlowTests.InviteNotificationBadgeLetsInviteeDecline`) |
| `nfv-badge-focus-2\badge-focus-2.trx` | 1 passed, 0 failed (repeat) |
| `nfv-badge-focus-3\badge-focus-3.trx` | 1 passed, 0 failed (repeat) |
| `nfv-team-invite-realtime\team-invite-realtime.trx` | 20 passed, 0 failed (16 `TeamInviteFlowTests` + 4 `TeamRealtimeTests`) |
| `nfv-realtime-1\realtime-1.trx` | 4 total, 3 passed, 1 failed, pre-repair (`TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload`) |
| `nfv-realtime-leave-2\realtime-leave-2.trx` | 1 total, 0 passed, 1 failed, pre-repair (same leave case) |

- The badge case that failed once on Ubuntu in `37114424083` now passes focused three times and again
  inside the combined 20-case run. The change is test-only: it seeds the pending invite before the
  invitee page initialises its notification summary, matching the mark-read and canceled siblings
  (`TeamInviteFlowTests.cs:300-303`). Independent review of the reorder passed.
- `CaptainRosterRefreshesWhenMemberLeavesWithoutReload` flaked here: the leaving member's participant
  card was still visible after the leave (Playwright `ToBeVisibleAsync` expected not-visible; the card
  resolved visible). The same case also passed in the 20/20 combined run, so the failure was
  intermittent. The shared-`Connecting`/`Join` and REST-first repair for this path is frozen; see
  "Realtime startup repair" below for the current gate and the superseded earlier evidence.

## Realtime startup repair (frozen source, local gate PASS)

The accepted source renders the first REST summary promptly and reconciles realtime state afterwards.
It covers two production paths plus the E2E readiness marker:

- `F/Services/TeamRealtimeService.cs` (unchanged, `8790B519...`) — `StartAsync` serialises connection
  startup behind a gate and shares one in-flight start task, so concurrent callers share a single
  `Connecting` start instead of silently returning; `JoinTeamsAsync` awaits that startup before
  invoking `JoinTeam`.
- `F/Components/Pages/Teams/ManageTeams.razor.cs` — `InitializeAsync` loads the REST summary and then
  clears the loading state and renders it *before* `StartAsync` is called. Realtime startup, the join
  of the displayed team ids, and `ReconcileSummaryAfterRealtimeStartupAsync` all run in that
  non-loading state. The reconcile joins only the team ids missing from the displayed snapshot
  *before* it assigns the reconciled summary, and it keeps the displayed snapshot when the reconcile
  GET or the delta join fails. `_isRealtimeReady` is set once the initially displayed team ids have
  joined and the reconcile attempt has run while the connection is still up; the page shows the
  `Live updates unavailable` warning when the hub is not connected. That guarantees the displayed
  snapshot is on screen and its ids joined, not that a latest atomic domain snapshot was fetched.
- `F/Components/Pages/Teams/ManageTeams.razor` — the `#team-workspace` section carries
  `data-live-updates-ready="@(_isRealtimeReady ? "true" : "false")"`, and the E2E helper waits on that
  attribute; the marker is used only on `/teams/manage`.

Frozen SHA-256 of the accepted sources:

- `src/Mercurius.LAN.Web/Services/TeamRealtimeService.cs`:
  `8790B519ECC52B6FBDFFCCD94FB10928947B6226E3F2CFD7CE353FD5B7F69D2C` (unchanged)
- `src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor.cs`:
  `BEF49C148912D270577F755ED8AEC1D1717D54566D0B1C81E656802B26153B48`
- `src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor`:
  `6ED0DD3677994BEBB87880DE104CE71227B6C207660406E6923614C71415B33D`
- `tests/Mercurius.LAN.Web.E2ETests/TeamE2EHelpers.cs`:
  `454783DB8D207D4C087B1F891617BAABDF1A69E5036865D2E0BA48C08B996E89`
- `tests/Mercurius.LAN.Web.ContractTests/ComponentLifecycleBehaviorTests.cs`:
  `94543E7030B912756579AF964340725B6C3EDF125D316130AD667C52D7FF1E6F`

All five hashes were re-computed from the working tree. Sol's final source review passed with minor
findings and DSE's final review passed on these sources; nothing blocking.

New contract precondition:
`ComponentLifecycleBehaviorTests.ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending` holds
realtime startup open with a pending `TaskCompletionSource` plus a recorded render, then asserts the
REST summary rendered with `IsLoading == false`. It protects the initial-render requirement as a
non-E2E lifecycle/exception precondition; the normal flows stay real E2E.

Historical (superseded, not the current proof): the earlier
`...\TestResults\realtime-gate-final` run passed leave 1/2/3 (1 each), combined-20 (20), contracts
(364), and the full suite (232 passed; the `dotnet test` stdout reports 5 m 37 s), but it was produced
on the earlier `_isLoading`-held-through-join shape that was rejected on spec and then fixed. It is
retained as historical evidence only.

Current local gate (complete, all PASS) on the frozen source above, under
`...\TestResults\realtime-rest-first-final`:

| Artifact | Result |
| --- | --- |
| `new-case\new-case.trx` | 1 total, 1 passed, 0 failed (`ComponentLifecycleBehaviorTests.ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending`) |
| `leave-1\leave-1.trx`, `leave-2\leave-2.trx`, `leave-3\leave-3.trx` | 1 total, 1 passed, 0 failed each (`TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload`) |
| `combined-20\combined-20.trx` | 20 total, 20 passed, 0 failed (16 `TeamInviteFlowTests` + 4 `TeamRealtimeTests`) |
| `contracts\contracts.trx` | 365 total, 365 passed, 0 failed (`Mercurius.LAN.Web.ContractTests`) |
| `full-232\full-232.trx` | 232 total, 232 executed, 232 passed, 0 failed, 0 skipped across 30 classes; `dotnet test` stdout duration 5 m 34 s |

All five parts passed. The tester confirmed the exact argv: builds run as
`dotnet build <project> --no-restore --nologo` and tests as
`dotnet test <project> --no-build --no-restore [--filter <filter>] --logger ... --results-directory ...`,
with no `-m:1`, `-nr:false`, or `-p:UseSharedCompilation` flags. The console logs record only the
summary, not the invocation line. `MERCURIUS_E2E_ARTIFACTS=<base>\PlaywrightE2E` was set for the
new-case, leave, combined, and full runs.

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

- The repair is a frontend connection-startup fix. These records do not implicate SMTP outbox timing
  or SSL trust.
- Accepted limitation worth keeping explicit: the readiness marker covers the displayed initial
  summary and the successful join of its ids, not a latest atomic domain snapshot; when the reconcile
  refresh fails, the displayed summary is preserved.
- The realtime double reports `IsConnected == false`, so the page renders its accurate
  `Live updates unavailable` warning instead of a faked connection, and the pending-task contract
  stub used by the new precondition does not model a real connection.

This completes the local proof baseline for the repair; the change set is ready for publication, with
the paired Ubuntu run as the remaining runtime gate.

## Hosts and runtime

- The final tester finished with 0 hosts left running, and PostgreSQL was untouched.
- At that session the backend was unchanged since the 705-case run, so that result stood at the time;
  the backend has since moved on, and the current revision and its evidence are recorded under
  "Backend solution" above.

## Final review

- Independent `final_code_review` of `full-round10` and `full-round11` passed with minor findings, and
  the publication gate passed. The review is read-only and no rerun is claimed. It independently
  parsed 229 of 229 passed with 30 classes for each run and scanned all 242 traces per run: each run
  holds the 6 expected 403/404 console errors, 0 browser page errors, and 0 Mud duplicate-provider
  errors; all 27 API exceptions per run are the expected `42P01` database-fault records. Accepted
  `CustomAutocomplete` JS-disconnected teardown records were 2 in `full-round10` and 1 in
  `full-round11`. The `ManageTeams` SSR finding is absent from both final runs but is retained as a
  historical finding in `COVERAGE.md`.

## Tooling checks

| Check | Result | Invocation |
| --- | --- | --- |
| CI workflow lint (actionlint) | 0 findings | not recorded |
| Strict OpenSpec validation, frontend (post-archive) | 32 passed, 0 failed | not recorded |
| Strict OpenSpec validation, backend (post-archive) | 31 passed, 0 failed | not recorded |

Actionlint is a static lint of the workflow file. The workflow runtime has since executed remotely:
run `37111821272` passed 229/229 on frontend `ddc9b43` with the then-current backend pin
`ca3b...`, and run `37110243446` failed 228/229 on the SMTP MIME assertion that `ddc9b43` fixed.

## Runtime gates closed

- No runtime gate remains open. The paired Ubuntu run `37146174575` above closes the previous
  `b98f197...` 231/232 gap, and the P2 header-overflow rows,
  `TeamInviteFlowTests.InviteNotificationBadgeLetsInviteeDecline`, and
  `TeamRealtimeTests.CaptainRosterRefreshesWhenMemberLeavesWithoutReload` all pass on it.
- The two backend review threads raised on `b98f197...` (`PRRT_kwDOOwmpHc6ol8HO` post-commit `None`
  and other `MembershipChanged` calls; `PRRT_kwDOOwmpHc6ol8HQ` attempt all deleted recipients after
  the first failure) are implemented by `3cfc07f...` (caller `None` plus aggregate after attempts),
  and that revision is what the green paired run pinned - no stale `b98f197` token remains in the
  pinned workflow.
- The genuine external coverage gaps are unchanged and listed under "Not E2E-applicable" in
  `COVERAGE.md` (Auth0-hosted social/MFA flows, delivery to external mail recipients, and the
  backend-only surfaces with no UI caller). The runtime logs are not claimed all-clean: the accepted
  SSR `ObjectDisposedException` and `CustomAutocomplete` JS-disconnect teardown records remain
  historical findings in `COVERAGE.md`.

## Notes

- Serial flags (`-m:1 -nr:false -p:UseSharedCompilation=false`) appear only on the historical runs
  that recorded them. The current gate used the tester-confirmed argv above with no such flags.
- All restore and build commands exited 0.
