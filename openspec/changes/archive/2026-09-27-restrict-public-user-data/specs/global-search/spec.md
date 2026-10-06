## MODIFIED Requirements

### Requirement: Global search returns normalized safe result records
Global search MUST return bounded normalized records with only the data needed to render a result.
User results MUST display usernames only and MUST NOT navigate non-admin visitors to the admin-only
user-detail route.

#### Scenario: Search returns a user result
- **WHEN** a public search response includes a user match
- **THEN** the user result MUST display only the username
- **AND** a non-admin visitor MUST NOT be navigated to `/users/{username}`
- **AND** an admin MAY open the admin-only user-detail page
