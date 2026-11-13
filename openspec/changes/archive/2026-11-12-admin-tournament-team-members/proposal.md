# Let administrators open team rosters from the tournament participants list

## Why

On a team tournament the Participants section only shows each team's name and logo. Administrators
running the event need to see who is actually on each registered roster (for check-in, seating and
disputes) without going through the registration administration list or leaving the page. Regular
visitors do not need this detail, and showing every roster inline would clutter the participants grid.

## What Changes

- On the tournament detail page, under Participants, team cards become clickable for
  administrators. Activating a card opens a popup with the team's registered roster: username,
  captain marker and roster confirmation state, captain first.
- This follows the existing pattern in the application: participant cards open a details dialog
  (`ParticipantCardComponent.OnParticipantSelected` + `UserInfoDialog`). The application does not
  use hover popovers for content, and hover does not work on touch or keyboard.
- The new `TournamentAdminRosterDialog` mirrors `UserInfoDialog`: modal, focus trap, Escape,
  close button, click outside to close, same styling tokens. Both use a small `ModalFocusTrap`
  helper for the focus-trap lifecycle, so keyboard fixes apply in one place.
- Administrators see player details without leaving the page: activating a roster member, or a
  player card in an individual tournament, loads the player from the admin user endpoint (the same
  data as `/users/{username}`) and shows it in the existing `UserInfoDialog`. From a roster member,
  a "back to team members" control reopens that team's roster. Only one dialog is open at a time.
- One component, `TournamentAdminParticipantDetails`, owns both dialogs and their lookups. It is
  used by the Participants section and by match details (`TournamentMatchDetailsDialog`), so both
  entry points behave the same. Match details already list each team's members, so there
  administrators open player details directly from any listed member or individual participant;
  team names keep linking to `/teams/{teamName}` and no roster popup is offered. Other viewers keep
  today's match details: only the authorized opponent captain opens the opponent profile.
- The roster comes from the admin registration list the Participants section already loads. When
  the selected team is not on that page, only
  that team is looked up on demand by paging the existing admin registrations endpoint. Nothing is
  fetched per participant up front, and no new endpoint is added.
- Visitors, signed-in players and team captains who are not administrators see the participants
  list exactly as today: non-interactive cards with team name and logo only.
- Cards are not interactive while the Participants admin registrations are loading or failed to
  load; an open dialog closes in that case and when its team leaves the list. The existing admin
  error message stays the only failure surface for that list.
- In the roster dialog the team name links to `/teams/{teamName}`. On the public team page
  (`/teams/{teamName}`, also the target of a team result in the global search) each member links to
  `/users/{username}` for administrators. Visitors without the administrator role keep plain
  usernames, because `/users/{username}` is admin-only.

## Impact

Modifies `tournament-registration-ui` and `public-participant-profiles` (administrator exception to
the "no user profile links" rules). Touches `TournamentParticipantsTab`, `TournamentMatchDetailsDialog`,
`ParticipantComponent`, `UserInfoDialog` and `PublicTeamProfile`; adds `TournamentAdminParticipantDetails`,
`TournamentAdminRosterDialog` and `ModalFocusTrap`; updates the English and Dutch localization.
No backend or DTO change: the admin registrations and admin user endpoints already require the
administrator role on every request. Usernames only in the roster, consistent with
`privacy-safe-participant-data`.
