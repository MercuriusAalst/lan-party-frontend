## ADDED Requirements

### Requirement: Image picker diagnostics are opt-in and privacy-safe

The frontend MUST provide an explicitly enabled, disabled-by-default diagnostic mode for native image input events and observable browser lifecycle states. The mode MUST install its listeners idempotently, MUST send no telemetry, and MUST NOT record filenames, file contents, object URLs or secrets. It MAY record event stages, counts, MIME types, byte sizes and observable reconnect or error DOM states.

#### Scenario: Diagnostics stay silent by default

- **WHEN** a user opens an image picker without enabling diagnostics
- **THEN** the diagnostic mode MUST NOT install or emit image-selection stage logs

#### Scenario: An operator enables diagnostics

- **WHEN** an operator enables the documented session switch and reloads the page
- **THEN** image-picker click, change and subsequent lifecycle observations MUST be written only to the browser console
- **AND** the observations MUST contain no filename, file content or secret
- **AND** repeated script initialization MUST NOT duplicate listeners

#### Scenario: Observable state is distinguished from transport cause

- **WHEN** a diagnostic session records visibility, network or reconnect/error DOM changes around the native picker
- **THEN** the procedure MUST treat those records as observable frontend stages
- **AND** it MUST NOT claim the SignalR circuit closed or reconnected unless existing server logs or a direct transport observation confirm that stage
