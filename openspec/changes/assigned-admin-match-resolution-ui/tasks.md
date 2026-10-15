# Tasks

- [x] Make the assigned administrator mandatory in the create and update DTOs, the multipart request, the mock store, the admin picker (`Required`, no "none" choice) and the create and edit forms, with a prompt for legacy tournaments that have none.
- [x] Add `GET /v1/lan/matches/pending-resolution`, `PendingMatchResolutionDTO` and the reason enum to the client, the tournament service and the mock.
- [x] Make the mock stop accepting a lone report automatically and list admin-resolution matches for the admin persona only.
- [x] Add the admin navigation entry with a quiet 30 second count badge that stops on disposal and never runs for non-admins or while prerendering.
- [x] Add the `/admin/match-resolutions` page with loading, empty, error and unauthorized states, and open the existing match details dialog from each item.
- [x] State the reason in the admin-resolution description of the match details dialog and remove the automatic-acceptance wording from English and Dutch copy.
- [x] Add contract tests (DTOs, client route and JSON, mock lifecycle and parity, picker, nav polling, markup) and browser tests (queue page, dialog with a mandatory admin, deadline texts); make the browser helpers create tournaments with an assigned admin and teach the local OIDC server the Auth0 role endpoints.
- [x] Validate the change with `openspec validate assigned-admin-match-resolution-ui --strict`.
- [ ] Verify the pending-resolution DTO against the backend change before shipping and archive the specification deltas after merge.
