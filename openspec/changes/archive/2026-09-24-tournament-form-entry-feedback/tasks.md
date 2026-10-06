## 1. Create form participation default

- [x] 1.1 Initialize the create tournament request's bound participation value to individual (solo) in `CreateTournamentDTO`.
- [x] 1.2 Confirm the create dialog's participation control and validation agree on that default on first render.

## 2. Safe, prominent create failure feedback

- [x] 2.1 Replace the raw `ApiException.Content` submit error with a localized message that keeps safe client-error validation text and maps server errors to the localized generic message.
- [x] 2.2 Render the submit error as a visible alert near the top of the create form using the shared alert styling, keeping `role="alert"`.
- [x] 2.3 Keep the admin's entered values intact after a failure so the submission can be corrected and retried.

## 3. Disabled result-entry explanation

- [x] 3.1 Keep `CanManageResults` limited to in-progress leaderboard tournaments.
- [x] 3.2 Add localized copy that explains why result entry is unavailable and how to proceed, distinguishing a tournament that has not started from one that no longer accepts results.
- [x] 3.3 Associate the explanation with the disabled control for assistive technology and make the disabled affordance visually unambiguous.

## 4. Localization

- [x] 4.1 Add the new create-form and result-entry strings to the en-US and nl-BE resources.

## 5. Tests and validation

- [x] 5.1 Add focused regression coverage for the create default participation value, the sanitized failure message, and the disabled result-entry explanation.
- [x] 5.2 Validate the OpenSpec change strictly after implementation and task updates.
- [x] 5.3 Run the focused contract test project and a frontend project build for the affected front end.
