# Tasks

- [x] Build an admin-only lookup from active team admin registrations to their roster members in `TournamentParticipantsTab`.
- [x] Make team participant cards interactive for administrators only and open `TournamentAdminRosterDialog` (username, captain marker, confirmation state), following the existing `UserInfoDialog` pattern; non-interactive while loading, on failure, for non-admins and in popup-only renders.
- [x] Close the dialog when the roster is no longer available (team removed, admin data reloading, role lost, tournament changed).
- [x] Link the roster dialog team name to `/teams/{teamName}`.
- [x] Show player details in `UserInfoDialog` (admin user endpoint) when an admin activates a roster member, with a "back to team members" control; the roster dialog stays open with an error when loading fails.
- [x] Link public team page members to `/users/{username}` for administrators only (`AuthorizeView Roles="admin"`); visitors keep plain text.
- [x] Open the same player details dialog from individual-tournament player cards for administrators only (no return control).
- [x] Add English and Dutch localization keys and theme-aware, responsive dialog styles.
- [x] Add contract tests: admin can open the roster, non-admin and anonymous cannot, failed/loading admin state, individual tournaments unchanged, dialog content without private fields.
- [x] Run the contract tests (`dotnet test tests/Mercurius.LAN.Web.ContractTests`).
- [x] Validate the change with `openspec validate admin-tournament-team-members --strict`.
- [ ] Archive the specification delta after merge.
