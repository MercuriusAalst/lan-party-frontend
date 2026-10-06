## Frontend

- [x] 1.1 Restrict `/users/{username}` to admins and load details and match summaries from the admin user resources.
- [x] 1.2 Render only usernames in public user, team, search, and tournament participant surfaces; remove public user-profile links and participant-list user popups.
- [x] 1.3 Add the match opponent-profile DTO/client call and use it for detailed opponent information in match context only.
- [x] 1.4 Keep mock mode aligned with the admin and match-context profile contracts.

## Validation

- [x] 2.1 Run `openspec validate restrict-public-user-data --strict`.
- [x] 2.2 Build the frontend project, run focused contract tests, and inspect the changed paths for unintended edits.
