## MODIFIED Requirements

### Requirement: Public profile match-summary endpoints
The API MUST expose anonymous match-summary reads only for public team profiles at
`GET /v1/lan/public/teams/{teamName}/match-summaries`. User match summaries MUST be available only
to admins at `GET /v1/lan/users/{username}/match-summaries`.

#### Scenario: Admin requests player summaries
- **WHEN** an admin requests summaries for a complete, non-deleted player profile
- **THEN** the admin user resource MUST return at most one previous and at most one upcoming match
  for each tournament in which that player has an active individual registration or an active team
  registration whose confirmed roster or captain snapshot includes that player

#### Scenario: Non-admin requests player summaries
- **WHEN** an anonymous or non-admin client requests user match summaries
- **THEN** the request MUST be rejected without disclosing profile or match data

#### Scenario: Public team summaries
- **WHEN** an anonymous client requests summaries for an existing, non-deleted team profile
- **THEN** the public team resource MUST return at most one previous and at most one upcoming match
  for each tournament in which that team has an active team registration

#### Scenario: Missing profile
- **WHEN** the requested username or team name is missing, incomplete, or deleted
- **THEN** the endpoint MUST return 404 and MUST NOT disclose profile or match data
