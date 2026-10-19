# Player profile pictures

## Why

Player identity on the LAN site is a username or an initial. The profile editor offers no picture, and every participant, roster, leaderboard and match surface falls back to initials, so a player is hard to recognise across pages even once the back end starts returning a picture reference.

## What Changes

- The existing authenticated profile editor gains upload, replacement and removal of the player's own picture, with a local preview, pending and disabled states, and feedback that matches the existing media limits.
- A shared circular user avatar component renders the returned picture with display-only cropping and an initials fallback, in compact, regular and prominent sizes.
- The existing player identity surfaces consume the picture URL already present in the responses they load, and the signed-in header reflects a confirmed change without a manual reload.

## Impact

Adds the `user-profile-pictures` frontend capability. It consumes the committed back-end contract (`PUT`/`DELETE /v1/lan/users/me/picture` and the nullable `profilePictureUrl` on existing user, participant and search projections). Team identity, privacy rules and the existing routes are unchanged.
