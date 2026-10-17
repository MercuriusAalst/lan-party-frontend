## ADDED Requirements

### Requirement: Manage the own profile picture in the existing profile editor

The authenticated profile editor MUST let the player select a local image, preview it before uploading, and then upload it, replace it or remove it through the existing current-user picture resource. The editor MUST keep the persisted and displayed picture unchanged until the backend confirms success, so a failed upload or replacement leaves the previous picture visible. While a picture action is pending, the editor MUST disable the conflicting picture actions, MUST release a selected or previewed image when it is abandoned, and MUST announce progress, success and failure to assistive technology. The client MUST reject a file whose type or size is outside the existing media limits with a localised message before uploading, while the backend stays authoritative.

#### Scenario: Player uploads a picture

- **WHEN** an authenticated player selects a local image within the accepted types and size and confirms the upload
- **THEN** the editor MUST send the image as multipart field `picture` to `PUT /v1/lan/users/me/picture`
- **AND** it MUST show the returned picture only after the backend confirms success

#### Scenario: Player replaces or removes a picture

- **WHEN** a player with a saved picture uploads another image or removes the current one
- **THEN** the displayed picture MUST follow the confirmed backend response
- **AND** removal MUST send `DELETE /v1/lan/users/me/picture` and show the initials fallback on success

#### Scenario: Failed upload preserves the saved picture

- **WHEN** an upload or replacement fails or is interrupted
- **THEN** the editor MUST keep the previously saved picture
- **AND** it MUST report the failure without leaving the pending state active

#### Scenario: Rejected local file

- **WHEN** the selected file is not one of the accepted image types or exceeds the existing media size limit
- **THEN** the editor MUST reject it locally with a localised message
- **AND** it MUST NOT call the backend

### Requirement: Shared circular user avatar

Player identity MUST be rendered through one reusable user avatar that shows the returned picture as a circular image cropped with `object-fit: cover` and, when no picture is available or the image fails to load, falls back to the player's initials. The component MUST offer compact, regular and prominent sizes of stable dimensions, MUST resolve backend-relative picture references through the existing asset URL resolver, and MUST keep the identity readable to assistive technology.

#### Scenario: Picture is available

- **WHEN** a player identity has a picture reference
- **THEN** the avatar MUST render it as a circular image cropped to fill the frame
- **AND** the rendered URL MUST be resolved through the existing asset URL resolver

#### Scenario: No picture or a broken image

- **WHEN** a player identity has no picture reference, or the image fails to load
- **THEN** the avatar MUST show the initials fallback for the same size
- **AND** a later picture change MUST reset the failure so the new image can render

#### Scenario: Identity size variants

- **WHEN** a surface needs a smaller or larger identity
- **THEN** the avatar MUST accept a compact, regular or prominent size without changing the surrounding layout

### Requirement: Player identity surfaces show the returned picture

The existing surfaces that render an individual player's identity MUST show that player's picture from the response they already load, and MUST NOT request a picture per identity. Team identity, including team logos, MUST stay unchanged. Incidental text-only mentions of a name MAY stay text.

#### Scenario: Surfaces render a player identity

- **WHEN** the own profile, the signed-in header, a public player profile, a team roster or member card, a tournament participant or player summary, a match participant view, a leaderboard entry or a user search result renders a player
- **THEN** it MUST show the picture returned for that player
- **AND** it MUST NOT issue an extra request to resolve a missing picture

#### Scenario: Confirmed change reaches shared current-user state

- **WHEN** a picture upload or removal succeeds in the profile editor
- **THEN** the signed-in header identity MUST show the confirmed picture without a manual page reload
