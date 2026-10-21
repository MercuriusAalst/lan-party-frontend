## MODIFIED Requirements

### Requirement: Fastest-time attempts use elapsed duration input
Fastest-time leaderboard entry and correction MUST accept four labeled numeric fields for hours, minutes, seconds, and milliseconds. Each unit MAY be left blank and MUST be treated as zero; the total MUST be positive. Hours MUST be a non-negative integer representable as part of a positive Int64 millisecond total. Minutes and seconds MUST be integers from 0 through 59. Milliseconds MUST be an integer from 0 through 999. The form MUST send the exact positive integer number of milliseconds in the existing `durationMilliseconds` API field, without rounding. Editing MUST prefill the four fields from the stored integer milliseconds exactly. Display formatting MUST remain `m:ss.fff` below one hour and `h:mm:ss.fff` at or above one hour. Invalid or zero totals MUST show localized inline validation and MUST NOT be submitted. Score entry and validation MUST remain unchanged.

#### Scenario: Record a duration from unit fields
- **WHEN** an administrator records 0 hours, 1 minute, 23 seconds, and 456 milliseconds
- **THEN** the request MUST carry `durationMilliseconds: 83456`

#### Scenario: Hours may be omitted
- **WHEN** an administrator leaves hours blank and enters 1 minute, 23 seconds, and 456 milliseconds
- **THEN** the request MUST carry `durationMilliseconds: 83456`

#### Scenario: Other blank units count as zero
- **WHEN** an administrator enters 1 hour and leaves minutes, seconds, and milliseconds blank
- **THEN** the request MUST carry `durationMilliseconds: 3600000`

#### Scenario: Correct an attempt without changing its duration
- **WHEN** an administrator edits an existing duration and saves the prefilled unit fields
- **THEN** the request MUST preserve the original integer milliseconds exactly

#### Scenario: Reject invalid unit values
- **WHEN** an administrator enters a negative, fractional, malformed, overflowing, or out-of-range unit, or a total of zero
- **THEN** the form MUST show duration validation
- **AND** it MUST NOT submit a request

#### Scenario: Localize elapsed-time fields
- **WHEN** the result dialog is shown in en-US or nl-BE
- **THEN** the elapsed-time field labels and validation MUST use that locale
