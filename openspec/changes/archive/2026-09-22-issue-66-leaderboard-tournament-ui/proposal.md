# Leaderboard tournament UI

Issue #66 adds front-end support for leaderboard tournaments where participants are ranked by their best recorded score or best recorded time instead of generated head-to-head matches. Admins configure `Leaderboard` with a required ranking metric, and results are entered through an admin-only hybrid flow that accepts an existing leaderboard participant, an existing LAN user, or a free-text guest.

The tournament detail page renders a live leaderboard in the existing competition area with metric-aware value formatting, competition ranks for ties, guest-capable final placements, and an intentional empty state before the first result. Registration is not part of a leaderboard tournament, so registration actions are suppressed rather than shown as required or closed. Existing bracket types keep their current configuration, bracket, match, registration, placement, prize, and sponsor behavior.

Front-end DTOs, `ILANClient`, services, and mock data stay aligned with the backend contract added by mercurius-aalst/mercurius-aalst-back-end#49: a nullable ranking metric on tournament create/update/details, an anonymous leaderboard read, an admin leaderboard history read, and admin attempt create/correct/remove.

## Non-goals

- No public or self-service leaderboard registration.
- No head-to-head matches, brackets, or match scheduling for leaderboard tournaments.
- No redesign of existing tournament surfaces and no new visual language.
- No client-side ranking, validation, lifecycle, or placement authority.
