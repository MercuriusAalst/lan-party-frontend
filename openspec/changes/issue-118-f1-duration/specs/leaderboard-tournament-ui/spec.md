## ADDED Requirements

### Requirement: Fastest-time attempts use elapsed duration input
Fastest-time leaderboard entry and correction MUST accept elapsed durations in `m:ss.fff` or `h:mm:ss.fff` form and MUST send the exact positive integer number of milliseconds in the existing `durationMilliseconds` API field. Input MUST use ASCII digits, exactly two second digits and three fractional digits; hour-form minutes MUST be 00-59. Outer whitespace MAY be trimmed. The interface MUST describe the elapsed-time format and report format-specific validation, including for empty duration input. Editing MUST prefill the formatted elapsed duration. Score entry and validation MUST remain unchanged.

#### Scenario: Record a minute duration
- **WHEN** an administrator records `1:23.456`
- **THEN** the request MUST carry `durationMilliseconds: 83456`

#### Scenario: Record a long duration
- **WHEN** an administrator records `1:02:03.004`
- **THEN** the request MUST carry `durationMilliseconds: 3723004`

#### Scenario: Reject invalid duration text
- **WHEN** an administrator enters zero, raw milliseconds, a sign, malformed fields, non-ASCII digits, an out-of-range seconds/minute field, extra precision, or an overflowing duration
- **THEN** the form MUST show duration-format validation
- **AND** it MUST NOT submit a request

#### Scenario: Correct an attempt without changing its duration
- **WHEN** an administrator edits an existing duration and saves the prefilled value
- **THEN** the interface MUST show an elapsed duration
- **AND** the request MUST preserve the original integer milliseconds exactly

#### Scenario: Localize elapsed-time guidance
- **WHEN** the result dialog is shown in en-US or nl-BE
- **THEN** the elapsed-time label, example, help, and validation MUST use that locale
