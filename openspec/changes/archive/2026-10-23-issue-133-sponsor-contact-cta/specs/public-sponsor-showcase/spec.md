## MODIFIED Requirements

### Requirement: Public sponsors page introduces partners with clear actions
The public sponsors page SHALL present a polished partner-focused intro that uses the site's existing brand styling primitives and provides clear visitor actions for sponsor discovery and sponsor inquiries.

#### Scenario: Visitor opens sponsors page with sponsors available
- **WHEN** a visitor opens the public sponsors page and sponsor data is available
- **THEN** the page presents a sponsor-focused intro with stronger visual hierarchy than a plain heading and paragraph
- **AND** the intro includes a call to action for browsing current partners
- **AND** the intro includes a call to action for becoming a sponsor
- **AND** the intro remains consistent with the existing redesign system and shared brand/button classes

#### Scenario: Visitor follows sponsor inquiry action
- **WHEN** the visitor uses the become-a-sponsor call to action from populated or empty sponsor content
- **THEN** the action MUST navigate to `/info#contact-info`
- **AND** the existing contact form MUST be in the browser viewport after navigation
- **AND** the page does not require a new backend form, API endpoint, or sponsor data field
