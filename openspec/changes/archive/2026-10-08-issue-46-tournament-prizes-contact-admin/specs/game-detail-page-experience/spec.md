## ADDED Requirements

### Requirement: Tournament detail presents contact administrator and prizes

The public tournament detail page SHALL present the tournament's configured contact
administrator and top-three prizes using only public-safe response data. Prize presentation MUST
remain coherent both before and after placements are known, and unconfigured values MUST be
omitted without placeholder noise.

#### Scenario: Visitor views the tournament contact administrator

- **WHEN** a public tournament detail response includes a contact administrator
- **THEN** the page MUST display the contact administrator's public identity
- **AND** it MUST NOT display or request private account fields such as email, linked identities,
  roles, or deletion state

#### Scenario: Tournament has no contact administrator

- **WHEN** a public tournament detail response includes no contact administrator
- **THEN** the page MUST omit the contact-administrator presentation
- **AND** it MUST NOT render an empty contact placeholder

#### Scenario: Prizes are configured before placements are known

- **WHEN** a tournament has one or more configured prizes and no placements yet
- **THEN** the placements area MUST still present each configured prize with its matching place
- **AND** it MUST NOT present an empty placements placeholder in place of the configured prizes

#### Scenario: Prizes appear under the matching placement

- **WHEN** placements are known and a prize is configured for a top-three place
- **THEN** the prize MUST be displayed underneath the placement for that same place
- **AND** a place without a configured prize MUST render without a prize line

#### Scenario: No prizes are configured

- **WHEN** a tournament has no configured prizes
- **THEN** the page MUST omit prize presentation entirely
- **AND** existing placement or empty-state presentation MUST remain unchanged
