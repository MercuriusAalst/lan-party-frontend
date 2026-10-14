## Why

Players currently confirm that a match ended, wait for their opponent to confirm the same, and only then submit a score. The backend removed the confirm-ended command, its route and its `canConfirmEnded` capability by explicit product decision: a submitted score records that side's ended confirmation and starts the existing five-minute window. The front end still calls the removed route and still shows a separate confirmation step, so the deployed client fails against the deployed back end.

## What Changes

- Submit a score directly through the existing score route; there is no separate match-end confirmation step and no both-sides-confirmed prerequisite.
- Remove the `ConfirmMatchEndedAsync` client, service, interface and mock methods, the `CanConfirmEnded` capability, the confirm-ended action and the per-side ended-confirmation status display.
- Keep the persisted ended-confirmation projection fields and lifecycle states for schema compatibility; the UI no longer gates or prompts on them.
- Mirror the backend's direct-score semantics in the mock: the first report records that side's ending, enters score confirmation and starts the five-minute deadline; matching reports complete, differing reports open the correction window, and the existing duplicate, correction, BYE, terminal, forfeit and reversal guards are preserved.
- Keep the English and Dutch lifecycle copy accurate for the states that remain.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `match-result-lifecycle-ui`: the score form replaces the ended-confirmation action and status, and the mock no longer exposes a match-end confirmation command.

## Impact

- `Components/Pages/Tournaments/Matches/DetailView/TournamentMatchDetailsDialog.razor` and `.razor.cs`: direct score entry, removed confirm-ended action and status, corrected localized copy keys.
- `APIClients/ILANClient.cs`, `Services/ITournamentService.cs`, `Services/TournamentService.cs`, `Mock/MockServices.cs`, `Mock/MockBackendStore.cs`, `DTOs/Matches/MatchActionStateDTO.cs`: removed confirm-ended command and capability, direct-score mock transitions.
- `wwwroot/locales/translations.en-US.json` and `translations.nl-BE.json`: removed confirm-ended copy, corrected awaiting-score descriptions.
- `tests/Mercurius.LAN.Web.ContractTests` and `tests/Mercurius.LAN.Web.E2ETests`: route, capability and lifecycle coverage.
- Front end only; the backend `direct-match-score` contract is authoritative and already committed on its branch. The removed route requires a coordinated back-end and front-end deployment.
