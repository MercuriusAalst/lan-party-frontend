## ADDED Requirements

### Requirement: Bracket view selector uses neutral accents

The double-elimination bracket view selector MUST use neutral tokens for its hover border, keyboard focus outline, active tab and active meta chip in both light and dark themes, and MUST NOT use the brand green. The active view MUST remain visually obvious through a neutral ink border, a leading ink indicator bar, a muted fill and an ink label, without relying on colour alone. Label text and meta chip text MUST meet WCAG AA contrast, and the active indicator and focus outline MUST have at least 3:1 contrast against the adjacent surface.

#### Scenario: Visitor switches the bracket view

- **WHEN** a visitor selects the upper bracket, lower bracket or grand final view
- **THEN** the selected tab MUST be marked with `aria-selected="true"` and a neutral active treatment
- **AND** the other tabs MUST be marked `aria-selected="false"`
