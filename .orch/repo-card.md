# repo-card: lan-party updated: 2026-10-13 by: scout/architect approved: 2026-10-13
STACK: .NET 9, Blazor InteractiveServer, Refit, Bootstrap, Tailwind 3, xUnit
BUILD: dotnet build src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj
CHECK_FAST: dotnet test tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj
VERIFY: dotnet build src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj && dotnet test tests/Mercurius.LAN.Web.ContractTests/Mercurius.LAN.Web.ContractTests.csproj
UI_SHOTS:
SPEC_WORKFLOW: openspec
SPEC_PROFILE: mercurius-lan
PR_TEMPLATE: none
PR_FORMAT: openspec-workflow/profiles/mercurius-lan/pr-format.md
PROTECTED: main
RUNTIME: Blazor server; assume containers, multiple replicas and rolling deployments; backend owns durable match state
HOT_SPOTS: APIClients/ILANClient.cs; Mock/MockBackendStore.cs; authentication; match lifecycle
RELATED_REPOS: backend=D:/Github Repositories/mercurius-aalst-back-end; helm=D:/Github Repositories/lan-party-helmchart
CONVENTIONS:
- Reuse component .razor/.razor.cs/.razor.css patterns and shared app.css classes.
- Backend action capabilities authorize UI actions; refresh after mutation.
- Maintain mock parity and both en-US/nl-BE translations.
- No new packages, projects or Shared types without architect review.

## STRUCTURE (approved 2026-10-13; existing layout)
SHAPE: mixed; single presentation project, kinds grouped by feature
CORE: single Web project
PROJECTS: tests/Mercurius.LAN.Web.ContractTests -> src/Mercurius.LAN.Web; Web has no repository project references
FOLDERS: src/Mercurius.LAN.Web/Components/Pages/<Feature>/; DTOs/<Feature>/; Models/<Feature>/; Services/; APIClients/; Extensions/
MODULE-WIDE: Mock/ and MockData.Local/ for mock implementation and fixtures
CROSS-CUTTING: Components/Shared/; Localization/; wwwroot/app.css; wwwroot/locales/
SHARED: Components/Shared/; reuse existing components, additions need architect review
NAMESPACE: matches project and folder path
TESTS: tests/Mercurius.LAN.Web.ContractTests/; existing flat contract test files
NEW PROJECT OR MODULE: STOP IF; architect decides
EXAMPLES:
- Components/Pages/Tournaments/Matches/DetailView/TournamentMatchDetailsDialog.razor
- DTOs/Matches/SubmitMatchScoreDTO.cs
- Services/TournamentService.cs
- tests/Mercurius.LAN.Web.ContractTests/MockMatchLifecycleTests.cs
EXCEPTIONS:
- Existing flat Services and contract test layouts: leave as is.
