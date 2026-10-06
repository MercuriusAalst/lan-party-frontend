## 1. Team eligibility and step navigation
- [x] 1.1 Filter captained team choices by required roster size and apply the filter to restored selections and existing registrations.
- [x] 1.2 Add localized guidance that undersized teams are hidden.
- [x] 1.3 Make step headers navigable with keyboard and focus affordances; guard forward navigation with current team and roster validation.

## 2. Popup and roster presentation
- [x] 2.1 Remove redundant registration eyebrow, intro sentence, selected-team block, roster-size help, and status messages while retaining accessible dialog naming and meaningful disabled/error states.
- [x] 2.2 Show roster and summary titles with inline selected/required counts; omit the captain from candidates while keeping the captain selected automatically.
- [x] 2.3 Restyle roster members as confirmation-state cards and place cancellation beside the save action.
- [x] 2.4 Use a borderless close control and correct disabled primary button styling.

- [x] 2.5 Keep captain-managed cancellation reachable before Summary when registration teams are filtered out or roster eligibility blocks progression; add a focused regression check.

## 3. Localization and validation
- [x] 3.1 Keep en-US and nl-BE resources in parity for changed copy.
- [x] 3.2 Add focused tests for eligibility filtering and guarded step navigation; verify changed locale key parity.
- [x] 3.3 Build the Blazor project and strictly validate this OpenSpec change.
