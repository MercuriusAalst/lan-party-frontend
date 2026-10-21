## MODIFIED Requirements

### Requirement: Fastest-time attempts use elapsed duration input
Fastest-time leaderboard entry and correction SHALL use one native time input (`<input type="time">`) with a step of one millisecond, labeled with the localized elapsed-time label, in both the add form and the inline edit form. The control MUST NOT be split into separate unit fields or replaced by free text. The value MUST be positive and MUST NOT exceed `23:59:59.999`; the minimum is `00:00:00.001`. The dialog MUST accept the browser value forms `HH:mm`, `HH:mm:ss`, and `HH:mm:ss.f`, `HH:mm:ss.ff`, or `HH:mm:ss.fff` (a fraction of one to three digits is a decimal fraction of a second) and MUST send the exact positive integer number of milliseconds in the existing `durationMilliseconds` API field, without floating-point rounding. Editing MUST prefill the control from the stored integer milliseconds exactly; a stored value that a time input cannot represent (24 hours or more) MUST NOT be silently truncated and MUST be preserved if the control is left untouched. Blank, zero, malformed, or out-of-range values MUST show localized inline validation and MUST NOT be submitted. Display formatting MUST remain `m:ss.fff` below one hour and `h:mm:ss.fff` at or above one hour. Score entry and validation MUST remain unchanged.

#### Scenario: Record a duration with the time input
- **WHEN** an administrator enters `00:01:23.456` in the elapsed-time input
- **THEN** the request MUST carry `durationMilliseconds: 83456`

#### Scenario: Browser omits zero parts
- **WHEN** the input value is `00:01:23` or `00:01`
- **THEN** the request MUST carry `durationMilliseconds: 83000` or `60000` respectively

#### Scenario: Short fractions are decimal
- **WHEN** the input value is `00:00:01.4`
- **THEN** the request MUST carry `durationMilliseconds: 1400`

#### Scenario: Correct an attempt without changing its duration
- **WHEN** an administrator edits an existing duration and saves the prefilled value
- **THEN** the request MUST preserve the original integer milliseconds exactly

#### Scenario: Reject invalid time values
- **WHEN** an administrator leaves the input blank, enters zero, or the value is malformed or above `23:59:59.999`
- **THEN** the form MUST show duration validation
- **AND** it MUST NOT submit a request

#### Scenario: Localize elapsed-time validation
- **WHEN** the result dialog is shown in en-US or nl-BE
- **THEN** the elapsed-time label and validation MUST use that locale
