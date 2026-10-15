## 1. Contract and service

- [x] 1.1 Add an optional `page` parameter (alias `page`, default 1) to the `ILANClient` administrator list call after the existing cancellation token, keeping every current caller source-compatible.
- [x] 1.2 Add a tournament-service helper that fetches administrators page by page with `pageSize=50`, sends no `query` filter, stops after the first short page, and propagates failures instead of returning a partial list.
- [x] 1.3 Mirror the paging semantics in the mock backend store and services.

## 2. Admin picker

- [x] 2.1 Replace the typed search in `AdminPicker` with a dropdown that loads the administrator list once on mount, offers a "no contact administrator" choice, and reports the loading, empty, and error states accessibly.
- [x] 2.2 Keep an assigned contact administrator selectable when it is absent from the loaded list or while the list is loading or errored, and clear selection through the empty choice.
- [x] 2.3 Remove the debounce/search styling and localization keys the dropdown no longer uses.

## 3. Validation

- [x] 3.1 Strictly validate the OpenSpec change before code and again after task synchronization.
- [x] 3.2 Add focused contract tests for the `page` parameter, the omitted `query` key, and multi-page assembly, plus mock paging parity.
- [x] 3.3 Run the focused contract tests and build the web project.
