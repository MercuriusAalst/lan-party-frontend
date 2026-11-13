# Tasks

- [x] Build an admin-only lookup from active team admin registrations to their roster members in `TournamentParticipantsTab`.
- [x] Make team participant cards interactive for administrators only and open `TournamentAdminRosterDialog` (username, captain marker, confirmation state), following the existing `UserInfoDialog` pattern; non-interactive while loading, on failure, for non-admins and in popup-only renders.
- [x] Close the dialog when the roster is no longer available (team removed, admin data reloading, role lost, tournament changed).
- [x] Link the roster dialog team name to `/teams/{teamName}`.
- [x] Show player details in `UserInfoDialog` (admin user endpoint) when an admin activates a roster member, with a "back to team members" control; the roster dialog stays open with an error when loading fails.
- [x] Link public team page members to `/users/{username}` for administrators only; visitors keep plain text.
- [x] Open the same player details dialog from individual-tournament player cards for administrators only (no return control).
- [x] Add English and Dutch localization keys and theme-aware, responsive dialog styles.
- [x] Add contract tests: admin can open the roster, non-admin and anonymous cannot, failed/loading admin state, individual tournaments unchanged, dialog content without private fields.
- [x] Review: move both dialogs and their lookups into `TournamentAdminParticipantDetails` and reuse it from `TournamentMatchDetailsDialog` (admins: team name opens the roster, member or individual participant opens player details; non-admin behavior unchanged).
- [x] Review: look up a team beyond the loaded admin page on demand (page through the admin registrations for the selected team only); team cards no longer depend on the loaded page.
- [x] Review: drop `ProfileRoutes`; build the two escaped routes at their call sites like the rest of the app.
- [x] Review: share the focus-trap lifecycle of `UserInfoDialog` and `TournamentAdminRosterDialog` through a small `ModalFocusTrap` helper.
- [x] Review: confirm server-side admin enforcement of `GET /v1/lan/users/{username}` and reply with the reference.
- [x] Review: in match details keep team names linking to `/teams/{teamName}` and drop the admin roster popup there; admins open player details directly from every listed member.
- [x] Run the contract tests (`dotnet test tests/Mercurius.LAN.Web.ContractTests`).
- [x] Validate the change with `openspec validate admin-tournament-team-members --strict`.
- [x] Archive the specification delta.
