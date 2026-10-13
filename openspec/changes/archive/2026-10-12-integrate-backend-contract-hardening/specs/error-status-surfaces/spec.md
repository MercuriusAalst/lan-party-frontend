## ADDED Requirements

### Requirement: API failures render a readable backend message

The profile, profile-completion, sponsor-management, and leaderboard-attempt surfaces MUST render a
readable, user-facing message for an API failure. The message MUST be derived from the shared API
error parser: for a problem-details body the `message`, else the `detail`, else the first validation
`errors` entry, else the `title`; for a legacy JSON string or a plain-text body the body content
itself. The parser's error `code` MUST remain available to the calling surface. Raw JSON, raw
exception text, or an empty message MUST NOT be shown.

#### Scenario: Problem-details body is shown readably

- **WHEN** a profile, profile-completion, sponsor, or leaderboard request fails with an
  `application/problem+json` body
- **THEN** the UI MUST display the parsed `message`, or its `detail`, or its first validation
  `errors` entry, or its `title`
- **AND** the UI MUST NOT display the raw JSON document

#### Scenario: Validation problem details are readable

- **WHEN** the failure body carries an `errors` object without a top-level `message` or `detail`
- **THEN** the UI MUST display the first validation error entry as the readable message

#### Scenario: Legacy or plain-text body falls back

- **WHEN** the failure body is a legacy JSON string or a plain-text message rather than a
  problem-details object
- **THEN** the UI MUST display that body content as the readable message

#### Scenario: No readable message is available

- **WHEN** the failure body is empty or cannot be parsed
- **THEN** the UI MUST fall back to the surface's localized generic failure copy
- **AND** it MUST NOT show an empty or exception-derived message
