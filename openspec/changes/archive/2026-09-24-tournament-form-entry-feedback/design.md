## Context

The create tournament dialog binds an `EditContext` over `CreateTournamentDTO`. `ParticipationMode` is a nullable, required property with no initializer, while the participation `<select>` renders the enum in declaration order, so `Individual` appears selected without ever being written to the bound DTO. The same dialog renders `ApiException.Content` verbatim on a failed create, which surfaces backend `ProblemDetails` JSON to the admin. On leaderboard tournaments, `TournamentLeaderboardTab` disables the result-entry button unless the tournament status is `InProgress`, with no visible reason.

## Goals / Non-Goals

**Goals:**

- Make the create form's bound participation value match the option the form presents as selected before validation runs.
- Keep client-error validation feedback that is safe to show, replace server-error and internal payloads with a localized generic message, and never render a raw response body.
- Make the failure alert prominent and accessible.
- Make the disabled result-entry affordance self-explanatory and accessible without changing when it is enabled.

**Non-Goals:**

- No change to backend validation, authorization, lifecycle transitions, or the mock backend contract.
- No change to the leaderboard result-entry dialog, attempt history, or ranking behavior.
- No new dependency, no broad restyle of the tournament surfaces, and no change to the tournament edit form.

## Decisions

- **Default the DTO property rather than special-casing the form.** `CreateTournamentDTO.ParticipationMode` gets an `Individual` initializer so the bound value exists before `EditContext` validation. Alternative considered: setting the field in the dialog's `OnInitialized`; rejected because the default belongs to the request contract the form binds to, and any other consumer of the DTO would keep hitting the same trap.
- **Classify create failures instead of printing content.** Reuse the existing `ApiExceptionExtensions.GetApiError()` helper to read a safe message from the response. Show that message only for client errors (4xx) that carry one; otherwise fall back to the existing localized generic key. Alternative considered: always show the generic message; rejected because it drops actionable validation feedback such as a rejected name or image.
- **Reuse the shared alert styling.** The submit error uses the existing global `error-message` alert style so it reads as a failure banner instead of a field hint, and keeps `role="alert"` for assistive technology.
- **Explain the disabled state, do not enable it.** `CanManageResults` keeps requiring the in-progress status because the backend and mock backend reject recorded attempts in other states. The tab adds localized copy that adapts to whether the tournament has not started yet or no longer accepts results, and wires it to the button with `aria-describedby`.

## Risks / Trade-offs

- [A client error could carry an unsafe or verbose body] -> Only client-error responses are considered, the body is parsed rather than printed, and any parse failure falls back to the localized generic message.
- [Extra copy could crowd the leaderboard header] -> The explanation renders under the header as a small, localized line and only while the control is unavailable.
- [An existing admin flow could depend on the previous default] -> The change only sets a default that the form already displayed, and the create form's leaderboard branch already forced individual participation.

## Migration Plan

Front end only, no data migration. Rollback is reverting the dialog, DTO, tab, and resource changes together.

## Open Questions

None.
