## 1. Specification

- [x] 1.1 Update the Info page requirement for configured ticket links and profile prefill

## 2. Configuration and interaction

- [x] 2.1 Add optional registration URLs and configurable Google Forms field mappings
- [x] 2.2 Implement accessible ticket links and page-load current-profile prefill with ticket-only fallback
- [x] 2.3 Add visible keyboard focus styling

## 3. Verification

- [x] 3.1 Add contract and public-site coverage for destinations, accessibility, encoding, mapping failures, logout refresh, and profile fallback
- [x] 3.2 Validate OpenSpec and run targeted build and tests

Validation note: `openspec validate issue-146-ticket-registration --strict` passed. The Release E2E project build passed. Focused `TicketRegistrationUrlBuilderTests` passed 7/7, and the full `PublicSiteTests` class passed 42/42 after switching to server-side profile loading. After removing the disclosure text and keys, the focused Info page E2E passed 1/1. The full E2E suite was not rerun after these adjustments.

```powershell
$env:MERCURIUS_BACKEND_ROOT='D:\Github Repositories\mercurius-aalst-back-end'; dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~InfoPageRendersPackingTicketsMenuAndContactSections --logger 'console;verbosity=minimal' -m:1 -nr:false -p:UseSharedCompilation=false
$env:MERCURIUS_BACKEND_ROOT='D:\Github Repositories\mercurius-aalst-back-end'; dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Mercurius.LAN.Web.E2ETests.PublicSiteTests" --logger 'console;verbosity=minimal' -m:1 -nr:false -p:UseSharedCompilation=false
```
