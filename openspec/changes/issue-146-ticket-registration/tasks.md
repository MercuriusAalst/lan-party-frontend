## 1. Specification

- [x] 1.1 Update the Info page requirement for configured ticket links and profile prefill

## 2. Configuration and interaction

- [x] 2.1 Add optional registration URLs and configurable Google Forms field mappings
- [x] 2.2 Implement accessible ticket links and click-time current-profile prefill with ticket-only fallback
- [x] 2.3 Add localized disclosure and focus styling

## 3. Verification

- [x] 3.1 Add contract and public-site coverage for destinations, accessibility, encoding, mapping failures, current-user changes, and profile fallback
- [x] 3.2 Validate OpenSpec and run targeted build and tests

Validation note: `openspec validate issue-146-ticket-registration --strict` passed. Release web and E2E project builds passed. Focused `TicketRegistrationUrlBuilderTests` passed 7/7. The focused ticket-registration E2E command passed 6/6; the full `PublicSiteTests` class command passed 45/45:

```powershell
$env:MERCURIUS_BACKEND_ROOT='D:\Github Repositories\mercurius-aalst-back-end'; dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~PublicSiteTests.AnonymousTicketCards|FullyQualifiedName~PublicSiteTests.SignedInTicketCard|FullyQualifiedName~PublicSiteTests.TicketLink|FullyQualifiedName~PublicSiteTests.TicketRegistrationConfirmation" --logger 'console;verbosity=minimal' -m:1 -nr:false -p:UseSharedCompilation=false
$env:MERCURIUS_BACKEND_ROOT='D:\Github Repositories\mercurius-aalst-back-end'; dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Mercurius.LAN.Web.E2ETests.PublicSiteTests" --logger 'console;verbosity=minimal' -m:1 -nr:false -p:UseSharedCompilation=false
```

The full E2E suite command, `$env:MERCURIUS_BACKEND_ROOT='D:\Github Repositories\mercurius-aalst-back-end'; dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj -c Release --no-build --no-restore --logger 'console;verbosity=minimal' -m:1 -nr:false -p:UseSharedCompilation=false`, completed in 7m37s with 274 passed and 2 failed. The failures were `ProfileAndOnboardingTests.DeleteAccountAnonymizesTheProfileAndEndsTheSession` (expected replay rejection but received HTTP 400) and `TournamentScrollChainingTests.EmptyParticipantAndRegistrationCardsScrollThePage(width: 1440, height: 900)` (3-second Playwright timeout in `AssertPageWheelChainsAsync`).
