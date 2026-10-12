## 1. Contract and models

- [x] 1.1 Add the admin-only administrator list call (`GET /v1/lan/users/admins` with optional `query` and `pageSize`) to `ILANClient` and reuse `PublicUserDTO` for the public-safe `{id, username, displayName}` projection.
- [x] 1.2 Carry `AssignedAdminUserId`, `FirstPlacePrize`, `SecondPlacePrize`, and `ThirdPlacePrize` on the create/update tournament DTOs and include them in the multipart builder, trimming prize whitespace and sending an explicit empty value when the contact or a prize is cleared.
- [x] 1.3 Expose the configured prizes and the public-safe `ContactAdmin` projection on the public tournament model.

## 2. Admin tournament form

- [x] 2.1 Add a contact-administrator picker to the create dialog and the inline edit form, listing only validated administrators from a debounced search with cancellation, preserving the current selection, and supporting clear.
- [x] 2.2 Add optional 1st/2nd/3rd prize inputs with length validation to both admin surfaces.
- [x] 2.3 Initialize the picker from the existing contact administrator on edit, and surface administrator-list and save failures without dropping the form state.

## 3. Public presentation

- [x] 3.1 Show the configured contact administrator on the public tournament detail without exposing private account fields.
- [x] 3.2 Show configured prizes under the matching top-three placement once placements are known.
- [x] 3.3 Show configured prizes before placements are known, and omit unconfigured prizes without placeholder copy.

## 4. Mock and localization

- [x] 4.1 Update mock backend data and services so administrator listing, prize configuration, and contact projection work in mock mode.
- [x] 4.2 Add the new user-facing copy in `en-US` and `nl-BE`.

## 5. Validation

- [x] 5.1 Strictly validate the OpenSpec change before code and again after task synchronization.
- [x] 5.2 Add focused contract tests for the administrator route, the multipart prize/admin fields, and mock parity.
- [x] 5.3 Run the focused contract tests and build the web project.
