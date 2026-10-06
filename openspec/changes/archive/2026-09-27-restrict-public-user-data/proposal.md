## Why

Public user pages and participant surfaces currently expose more account information than people
need to identify one another. Detailed identity fields should be limited to admins and the actual
opponents in a tournament match.

## What Changes

- Restrict `/users/{username}` to admins and load its full profile through the admin user endpoint.
- Keep public user and participant identity displays to usernames only.
- Remove user popups from the tournament participant list.
- Load detailed opponent identity through the match-authorized opponent-profile endpoint.

## Non-goals

- No changes to account self-service profile data or tournament match authorization rules.
- No new frontend dependency or general profile-view abstraction.
