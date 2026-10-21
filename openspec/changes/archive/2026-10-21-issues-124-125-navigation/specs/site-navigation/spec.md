## ADDED Requirements

### Requirement: Responsive shell navigation remains usable

The shared header MUST collapse its primary navigation before supported
navigation, search, and account controls overlap or become clipped. The theme
preference control MUST appear beside the language selector in the footer.

#### Scenario: Header reaches a constrained desktop width

- **WHEN** the viewport is too narrow for the complete header controls
- **THEN** the header MUST show its navigation toggle
- **AND** the header MUST NOT horizontally overflow or overlap its controls
- **AND** opening the navigation MUST preserve search, menus, and keyboard
  dismissal

#### Scenario: Visitor changes shell preferences

- **WHEN** the visitor uses the footer theme or language control
- **THEN** the existing theme and language behavior MUST persist
- **AND** both controls MUST remain accessible and usable at narrow widths

### Requirement: Info menu dismisses without blocking outside actions

The Info menu MUST close when pointer input occurs outside its trigger and
dropdown, without preventing the outside target's normal action. Escape MUST
close the menu and restore focus to its trigger. Interaction inside the menu
MUST remain available, and opening another navigation menu MUST work on the
first activation.

#### Scenario: Visitor activates another control outside Info

- **WHEN** the Info menu is open and the visitor clicks or taps another
  navigation control or page target
- **THEN** the Info menu MUST close
- **AND** the targeted control's action MUST run once

#### Scenario: Visitor dismisses Info with Escape

- **WHEN** the Info menu is open and the visitor presses Escape
- **THEN** the Info menu MUST close
- **AND** focus MUST return to the Info trigger
- **AND** the trigger's expanded state MUST be false
