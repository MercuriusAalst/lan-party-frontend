## ADDED Requirements

### Requirement: Create tournament form initializes bound participation before validation

The frontend SHALL initialize the create tournament form's bound participation value to the individual (solo) option before the first render and the first validation pass, so that the option the form presents as selected is the value that is validated and submitted.

#### Scenario: Admin submits an untouched create form

- **WHEN** an admin opens the create tournament form and submits it without changing the participation control
- **THEN** the bound participation value MUST already be individual (solo)
- **AND** submission MUST NOT be rejected for a missing participation mode
- **AND** the admin MUST NOT have to toggle the participation control away from and back to its visible value

#### Scenario: Participation is bound before the first render

- **WHEN** the create tournament form is first rendered
- **THEN** the bound create request MUST report individual participation before any user interaction
- **AND** the visible selection and the bound value MUST agree

### Requirement: Create tournament failures show safe localized feedback

When a create tournament request fails, the frontend SHALL show a localized, user-facing message and MUST NOT render the raw API response body, including `ProblemDetails` JSON. Specific, safe validation feedback MAY be shown for a client error only when the backend supplies it in a recognized structured field such as `message`, `detail`, or a validation-error entry; all other bodies, server errors, and internal implementation details MUST be replaced by a localized generic message.

#### Scenario: Backend returns an internal server error

- **WHEN** creating a tournament fails with a server error whose response body is `ProblemDetails` or another JSON payload
- **THEN** the dialog MUST show a localized generic failure message
- **AND** the raw response body MUST NOT be rendered in the dialog or in any notification

#### Scenario: Backend returns specific validation feedback

- **WHEN** creating a tournament fails with a client error that carries a specific, safe validation message in a recognized structured field such as `message`, `detail`, or a validation-error entry
- **THEN** that validation message MAY be shown to the admin
- **AND** the shown text MUST be user-facing copy rather than a serialized payload

#### Scenario: Client error body is unstructured

- **WHEN** creating a tournament fails with a client error whose body is plain text, a JSON string, a JSON array, or a JSON object without a recognized message, detail, or validation-error field
- **THEN** the dialog MUST show the localized generic failure message
- **AND** the response body MUST NOT be rendered in the dialog or in any notification

#### Scenario: Failure is visible and accessible

- **WHEN** a create failure message is shown
- **THEN** it MUST be rendered as a visible alert near the top of the create form
- **AND** it MUST be exposed to assistive technology as an alert
- **AND** it MUST be visually distinct from ordinary helper text

#### Scenario: Form state survives a create failure

- **WHEN** a create failure message is shown
- **THEN** the values the admin entered MUST remain in the form
- **AND** the admin MUST be able to correct the input and retry without re-entering the whole tournament
