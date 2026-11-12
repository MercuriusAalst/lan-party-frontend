# Let administrators open team rosters from the tournament participants list

## Why

On a team tournament the Participants section only shows each team's name and logo. Administrators
running the event need to see who is actually on each registered roster (for check-in, seating and
disputes) without going through the registration administration list. Regular visitors do not need
this detail, and showing every roster inline would clutter the participants grid.

## What Changes

- On the tournament detail page, under Participants, team cards become clickable for
  administrators. Activating a card opens a popup with the team's registered roster: username,
  captain marker and roster confirmation state, captain first.
- This follows the existing pattern in the application: participant cards open a details dialog
  (`ParticipantCardComponent.OnParticipantSelected` + `UserInfoDialog`). The application does not
  use hover popovers for content, and hover does not work on touch or keyboard.
- The new `TournamentAdminRosterDialog` mirrors `UserInfoDialog`: modal, focus trap, Escape,
  close button, click outside to close, same styling tokens.
- The roster is built from the admin registration list the tab already loads for administrators,
  so no new endpoint or per-participant request is added.
- Visitors, signed-in players and team captains who are not administrators see the participants
  list exactly as today: non-interactive cards with team name and logo only.
- Cards are not interactive while the admin registrations are loading or failed to load; an open
  dialog closes in that case and when its team leaves the list. The existing admin error message
  stays the only failure surface.
- Administrators see player details without leaving the tournament page: activating a roster
  member, or a player card in an individual tournament, loads the player from the admin user
  endpoint (the same data as `/users/{username}`) and shows it in the existing `UserInfoDialog`.
  From a roster member, a "back to team members" control reopens that team's roster. Only one
  dialog is open at a time, so the existing focus trap keeps working. Other viewers keep the
  plain, non-interactive cards.
- In the roster dialog the team name links to `/teams/{teamName}`. On the public team
  page (`/teams/{teamName}`, also the target of a team result in the global search) each member
  links to `/users/{username}` for administrators. Visitors without the administrator role keep
  plain usernames, because `/users/{username}` is admin-only.
- A small `ProfileRoutes` helper builds both routes with escaping and skips missing identifiers.

## Impact

Modifies `tournament-registration-ui` and `public-participant-profiles` (administrator exception to
the "no user profile links" rules). Touches `TournamentParticipantsTab`, `ParticipantCardComponent` and `PublicTeamProfile`,
adds `TournamentAdminRosterDialog` and `ProfileRoutes`, and updates the English and Dutch
localization. No backend or DTO change. Usernames only, consistent with
`privacy-safe-participant-data`.
