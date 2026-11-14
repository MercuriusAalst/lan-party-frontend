## MODIFIED Requirements

### Requirement: Info page presents catering as a restaurant-style menu

The LAN information page SHALL present catering options and current prices in a restaurant food-menu style without making specific menu items part of the permanent spec contract. A menu section MAY list the sauces that are included with its items; when it does, the page SHALL show those sauces and state that sauce is included, without showing a sauce price.

#### Scenario: Visitor reviews current drinks
- **WHEN** a visitor views the catering section
- **THEN** the page shows the currently configured drinks with visible prices
- **AND** the drinks are presented as part of a restaurant-style menu rather than a plain data list

#### Scenario: Visitor reviews current snacks
- **WHEN** a visitor views the catering section
- **THEN** the page shows the currently configured snacks with visible prices
- **AND** the snacks are presented as part of a restaurant-style menu rather than a plain data list

#### Scenario: Visitor compares catering categories
- **WHEN** drinks and snacks are both available on the page
- **THEN** the page groups drinks separately from snacks
- **AND** every listed menu item includes a visible price
- **AND** Euro prices place `EUR` after the numeric value

#### Scenario: Visitor checks which sauces come with snacks
- **WHEN** a menu section is configured with included sauces
- **THEN** that section lists every configured sauce by its localized name
- **AND** the section states that a sauce is included with the items in English and Dutch
- **AND** no separate price or surcharge is shown for the sauces
- **AND** the sauce information stays inside the menu column on narrow and wide viewports
