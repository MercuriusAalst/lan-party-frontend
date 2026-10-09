## Why

The contact-administrator picker on the tournament create and edit forms is a free-text search
field. It only reveals administrators after at least three typed characters, hides the full set of
valid administrators, and makes an already assigned contact hard to find or verify. The back end
administrator list resource already supports paging, so the front end can present a complete,
bounded list instead of a typed search.

## What Changes

- The contact-administrator picker becomes a dropdown that loads the administrator list once when
  the form opens, instead of querying as the administrator types.
- The dropdown always offers a "no contact administrator" choice for clearing an existing contact.
- The picker pages through the administrator list until the last (short) page, sends no `query`
  filter, and shows a loading, empty, or error state instead of a silently partial list.
- An already assigned contact administrator stays selectable even when the loaded list does not
  contain it, and the selection is preserved through loading and error states.
- The administrator list call gains an optional `page` parameter and the mock backend mirrors the
  paging semantics.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `organizer-workflows`: the admin tournament create/edit contact-administrator picker presents a
  complete, paged administrator dropdown with a clear choice instead of a typed search.

## Impact

- Front end only: `ILANClient` administrator list call, tournament service, mock backend
  store/services, the shared `AdminPicker` component and its styles, localization, and focused
  contract/parity tests.
- No back-end route or DTO change is introduced here; the back-end optional `page` parameter is
  authoritative for validation and paging bounds.
