## ADDED Requirements

### Requirement: Info navigation follows client-side route changes

The site navigation SHALL update active-state feedback and transient menu state
from the current URL after client-side navigation.

#### Scenario: Visitor navigates away from and back to Info

- **WHEN** a visitor moves between Home, Info including an Info section fragment,
  and Tournaments without reloading the document
- **THEN** the Info navigation control MUST be selected only while the current
  URL is an Info route
- **AND** an open Info menu MUST close when the current URL changes
