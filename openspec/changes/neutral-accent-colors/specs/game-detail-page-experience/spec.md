## ADDED Requirements

### Requirement: Bracket view selector uses neutral accents

The double-elimination bracket view selector MUST use neutral tokens for its tab fills, edges, hover, active tab and meta chip treatments in both light and dark themes, and MUST NOT use the brand green for fills, labels or chips. The brand green MUST appear only as a small accent: the active tab's leading accent bar and the keyboard focus ring. In the light theme the selector MUST get its contrast from soft grey fills and edges: inactive tabs MUST use a light grey fill with a visible grey edge against the container, hover MUST use a slightly darker grey fill, and the active tab MUST use a still darker grey fill with a green leading accent bar and a bold ink label. In the light theme ink MUST be used only for text, and the selector MUST NOT use ink-filled chips, ink borders or heavy ink outlines. The dark theme MAY keep its light-on-dark treatment. The active view MUST remain visually obvious without relying on colour alone. Label and chip text MUST meet WCAG AA contrast, the active accent bar and focus outline MUST have at least 3:1 contrast against the adjacent surface, and the active tab MUST be distinguishable from inactive and hover tabs by both fill and accent bar. Hovering the selected tab MUST NOT change its active fill or edge.

#### Scenario: Visitor switches the bracket view

- **WHEN** a visitor selects the upper bracket, lower bracket or grand final view
- **THEN** the selected tab MUST be marked with `aria-selected="true"` and a neutral active treatment
- **AND** the other tabs MUST be marked `aria-selected="false"`
