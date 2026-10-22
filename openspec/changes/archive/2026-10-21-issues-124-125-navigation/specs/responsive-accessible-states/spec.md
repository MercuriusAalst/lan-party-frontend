## ADDED Requirements

### Requirement: Responsive navigation controls retain their usable size

The header MUST switch to its collapsed navigation before the full supported
navigation and account actions cease to fit. Authentication actions and their
labels MUST remain readable and individually operable without horizontal page
overflow.

#### Scenario: Visitor opens the header at a constrained width

- **WHEN** the viewport is at or below the responsive navigation breakpoint
- **THEN** the navigation toggle MUST be available
- **AND** menu, search, authentication, and dismissal actions MUST remain
  reachable and keyboard accessible
