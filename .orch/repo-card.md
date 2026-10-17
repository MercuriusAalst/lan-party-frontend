# repo-card: lan-party-frontend   updated: 2026-10-13   by: scout   approved: 2026-10-13
STACK: .NET 10, Blazor, xUnit, Playwright
BUILD: dotnet build Mercurius.LAN.sln -c Release
CHECK_FAST: dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --filter FullyQualifiedName~DeleteAccountAnonymizesTheProfileAndEndsTheSession
VERIFY: dotnet build Mercurius.LAN.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false && dotnet test tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj -c Release --no-restore && bash .orch/verify-e2e.sh
UI_SHOTS: none
SPEC_WORKFLOW: openspec
SPEC_PROFILE: mercurius-lan
PR_TEMPLATE: none
PR_FORMAT: openspec-workflow/profiles/mercurius-lan/pr-format.md
PROTECTED: main
RUNTIME: unknown (assume multi-instance); E2E uses isolated PostgreSQL databases and a backend checkout
HOT_SPOTS: authentication, account deletion, API contracts
RELATED_REPOS: backend=D:/Github Repositories/mercurius-aalst-back-end
CONVENTIONS:
- Preserve MudBlazor UI patterns and existing feature folders.
- Keep API clients, services and mock implementations aligned.
- E2E tests require PostgreSQL, Chromium and the companion backend revision from CI.
- Test helpers must preserve genuine circuit failures, cancellation and bounded waits.
- The E2E runner executes every discovered case once in a fresh per-class process to contain long-run Chromium buffer pressure; failures and skipped or missing cases remain gate failures.

## STRUCTURE (approved 2026-10-13)
SHAPE: mixed (single web app with feature folders and separate test projects)
CORE: single Web project
PROJECTS: ContractTests -> Web; E2ETests -> Web + backend API
FOLDERS: Components/Pages/<Feature>; E2E Infrastructure helpers; test classes at test-project root
CROSS-CUTTING: Components/Layout, APIClients, Services
SHARED: no new shared types permitted for this task
NAMESPACE: follows existing project and folder conventions
TESTS: tests/Mercurius.LAN.Web.ContractTests; tests/Mercurius.LAN.Web.E2ETests
NEW PROJECT OR MODULE: STOP IF; architect decision required
EXAMPLES:
- src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor.cs
- src/Mercurius.LAN.Web/APIClients/ILANClient.cs
- tests/Mercurius.LAN.Web.E2ETests/Infrastructure/E2EPageExtensions.cs
- tests/Mercurius.LAN.Web.E2ETests/ProfileAndOnboardingTests.cs
EXCEPTIONS:
- E2ETests is outside Mercurius.LAN.sln and is verified separately; leave as is.
