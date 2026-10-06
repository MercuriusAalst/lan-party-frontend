## ADDED Requirements

### Requirement: Global search remains usable with authenticated desktop navigation
The site navigation MUST keep the global search input visible and editable on desktop widths when authenticated navigation controls are present.

#### Scenario: Admin searches from the desktop header
- **WHEN** an authenticated admin opens the site at a desktop viewport
- **THEN** the global search input remains visible and accepts a query
- **AND** the header controls remain within the viewport without horizontal overflow
