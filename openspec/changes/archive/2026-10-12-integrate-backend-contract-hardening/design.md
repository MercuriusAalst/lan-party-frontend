## Context

The front-end Blazor Server app talks to the sibling back end through Refit clients in
`APIClients/`, service wrappers in `Services/`, and page components under `Components/Pages/`. The
back end is authoritative for authorization, validation, privacy projections, timers/deadlines,
state transitions, and concurrency safety. Back-end PRs #146-#149 hardened that contract; this
change catches the front end up without editing the back end or adding packages. The match read
endpoints (`GET /v1/lan/matches/{id}` and `/me`) compute effective deadline state in memory; the
tournament aggregate read returns the persisted snapshot.

## Goals / Non-Goals

**Goals:**

- Show one readable message for API failures on the four affected surfaces, reusing the existing
  `ApiExceptionExtensions.GetApiError` parser.
- Recover from the exact backend `team_changed` `409` conflict without losing the user's place or
  resending a mutation.
- Converge the tournament page on the backend's effective projection after a refreshed backend match
  projection confirms the deadline expired, without computing results locally.
- Drop the removed legacy match-update route, method, DTO, and mock path so the boundary stays clean.

**Non-Goals:**

- Changing the back end, adding HTTP compatibility aliases, or reintroducing the removed route.
- Adding a new package, a generated client, or a second HTTP path.
- Reworking the match dialog's existing protected/public projection logic, its loading states, or its
  authorization behavior.
- Emulating the back end's ~30 second persistence/bracket-advancement polling or its batch-of-100
  advancement; the front end only reads the effective projection.
- Prescribing a new notification-area UI; notification-refresh failures only need to be contained and
  recoverable with the existing UI.

## Decisions

### 1. Route every readable error through the shared parser

The four surfaces will call `ApiExceptionExtensions.GetApiError`, which already resolves problem
`message`, `detail`, first validation `errors` entry, or `title`, exposes `code`, and falls back to a
legacy JSON string or plain text. Pages stop reading `exception.Content` or
`GetContentAsAsync<string>` directly and stop trimming quotes themselves.

**Alternative considered:** keep per-page readers. They diverge, hide problem details, and leak raw
JSON, so the shared parser wins.

### 2. Treat the exact `team_changed` conflict as recoverable

The team service will surface the parsed `code` so the page can act only on the exact `team_changed`
`409` conflict: show localized feedback, reload the confirmed team summary, and refresh
notifications. The page will not replay the mutation. Summary and notification failures are each
contained in the same durable inline recovery notice so neither hides the conflict feedback or breaks
the page.

**Alternative considered:** retry the mutation automatically. The back end rejected it because the
team moved on, so a replay could act on stale intent; it is rejected.

### 3. Own the deadline refresh at the page, triggered by a refreshed backend match

The advisory client countdown never changes state; the refresh only starts once a refreshed backend
match projection confirms the deadline expired. The tournament page then owns a bounded, cancellable
refresh that reloads the authoritative tournament aggregate, which maps the persisted match
collection. Per the verified #149 behavior (the deadline processor applies the source deadline and
its direct downstream in one tracked context and saves once), a persisted source match `ResultVersion`
at or above the expected effective version proves persistence and bracket convergence, and a strictly
higher version supersedes the expected one. The refresh keeps that strictly newer authoritative
same-ID projection and guards against out-of-order responses with a single generation counter, with
no defensive direct downstream-slot comparisons. The page stops the refresh on navigation or
disposal. The match dialog and wrappers may stay unchanged when the page can own the fix.

**Alternative considered:** let the dialog poll the aggregate. The dialog closes, so the page would
lose the post-deadline state; the page-owned refresh survives close and is bounded.

### 4. Remove the dead route in one slice

No front-end page called `PUT /v1/lan/matches/{matchId}`, so the route, `UpdateMatchAsync`,
`UpdateMatchScoresAsync`, the update DTO, and the mock update path are deleted together to prevent
stale boundary usage. Score changes use the supported lifecycle commands only.

**Alternative considered:** leave the method as a deprecated wrapper. It would keep declaring a route
the back end removed, so it is deleted.

## Risks / Trade-offs

- [Risk] Known back-end follow-up (not fixed by this change): backend `ReverseAsync` can return HTTP
  `400` for a read of an effective-completed match whose deadline has just expired while persistence
  catches up. -> The front end must treat that as a recoverable failure and keep retrying within its
  bound.
- [Risk] Removing the match-update method could strand a caller. -> Grep every caller before deletion;
  none is expected to exist, and remaining score changes use the lifecycle commands.
- [Risk] A page-owned refresh could fight the dialog's own refresh or a stale aggregate reload. ->
  Use one generation guard and preserve the strictly newer same-ID projection on both paths.
- [Risk] Conflict recovery could resend a mutation by accident. -> Assert the no-resend behavior in a
  focused test.

## Migration Plan

1. Route the four surfaces through the shared parser and add the fallback tests.
2. Add `team_changed` recovery and its tests, including the no-resend and contained-failure cases.
3. Add the page-owned deadline-convergence refresh and its tests, then remove the legacy route,
   method, DTO, and mock path and update fixtures.
4. Run `openspec validate integrate-backend-contract-hardening --strict`, the front-end build, and
   the focused contract tests; then hand the change to review and archive.

Rollback is a normal source revert on the feature branch; no data migration is involved.

## Resolved Questions

- The exact back-end conflict contract is confirmed against the pinned PR #146-#149 heads: a team
  conflict is HTTP `409` with a stable top-level `code` field (`team_changed`) on the problem-details
  body. `TeamServiceException.Code` surfaces it and the focused tests pin the exact-code matching.
- The deadline refresh bound stays an implementation detail: 40 attempts at a 2 second interval, and
  the retry action resets the bound. It is deliberately not fixed in the spec.
- The separate `ReverseAsync` effective-completed `400` read risk above remains an open back-end
  follow-up; this change only records it and keeps the front end resilient to it.
