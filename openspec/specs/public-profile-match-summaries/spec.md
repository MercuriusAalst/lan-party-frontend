# public-profile-match-summaries Specification

## Purpose
TBD - created by archiving change issue-47-profile-match-summaries. Update Purpose after archive.
## Requirements
### Requirement: Public profile match-summary endpoints

The API MUST expose anonymous match-summary reads only for public team profiles at
`GET /v1/lan/public/teams/{teamName}/match-summaries`. User match summaries MUST be available only
to admins at `GET /v1/lan/users/{username}/match-summaries`.

#### Scenario: Admin requests player summaries
- **WHEN** an admin requests summaries for a complete, non-deleted player profile
- **THEN** the response MUST contain at most one previous and at most one upcoming match for each
  tournament in which that player has an active individual registration or an active team
  registration whose confirmed roster or captain snapshot includes that player

#### Scenario: Non-admin requests player summaries
- **WHEN** an anonymous or non-admin client requests user match summaries
- **THEN** the request MUST be rejected without disclosing profile or match data

#### Scenario: Public team summaries
- **WHEN** an anonymous client requests summaries for an existing, non-deleted team profile
- **THEN** the response MUST contain at most one previous and at most one upcoming match for each
  tournament in which that team has an active team registration

#### Scenario: Missing public profile
- **WHEN** the requested username or team name is missing, incomplete, or deleted
- **THEN** the endpoint MUST return 404 and MUST NOT disclose profile or match data

#### Scenario: Blank route parameter
- **WHEN** a route parameter is empty or decodes to only whitespace
- **THEN** the page MUST stop loading, show the existing not-found state, and MUST NOT request
  profile or match-summary data

### Requirement: Match summary selection and state

The API MUST select summaries using the participant's active registration and MUST apply the
authoritative match lifecycle state.

#### Scenario: Most recent previous match
- **WHEN** a participant has multiple qualifying matches in one tournament
- **THEN** `previousMatches` MUST contain only the most recently completed match, where completed
  includes an official score result or forfeit, ordered by result/completion time descending and
  deterministic round, match, and match-id tie-breakers

#### Scenario: Next upcoming match
- **WHEN** a participant has multiple unplayed matches in one tournament
- **THEN** `upcomingMatches` MUST contain only the earliest unplayed non-BYE match, ordered by
  estimated/scheduled start ascending with deterministic round, match, and match-id tie-breakers

#### Scenario: Delayed or unscheduled upcoming match
- **WHEN** an unplayed match has no actual start, including when its estimate is overdue or absent
- **THEN** it MUST remain eligible for `upcomingMatches`, with overdue status or confirmed-time
  messaging in the UI; a match with an actual start MUST NOT be shown as upcoming

#### Scenario: Lifecycle exclusions
- **WHEN** a match belongs to a canceled tournament, is reversed, is already in progress, or is in
  an unresolved/disputed/admin-resolution state
- **THEN** the match MUST NOT be returned as a previous or upcoming summary

#### Scenario: Historical opponent snapshot
- **WHEN** a selected previous match's opponent registration is no longer active but its public
  registration snapshot remains available
- **THEN** the summary MUST retain the safe snapshot label without exposing private opponent data

#### Scenario: BYE and unresolved opponent
- **WHEN** a candidate match has a BYE slot
- **THEN** it MUST be excluded from profile summaries because it is not a playable opponent
- **WHEN** an upcoming candidate has the profile participant assigned but its opposing slot is not
  assigned
- **THEN** the summary MUST be returned with a public TBD opponent indication

### Requirement: Public summary contract

Each summary MUST expose only the fields needed to display and navigate the match: tournament ID
and name, match ID, opponent display name or TBD indication, public lifecycle/result state,
participant-relative scores when available, estimated and scheduled times when available, and safe
round/bracket metadata. `EstimatedStartTime` MUST take precedence over `ScheduledStartTime` for
upcoming ordering. `StartTime` and `EndTime` are actual lifecycle timestamps and MUST NOT be
presented as a scheduled start; an upcoming match with no actual start has no scheduled time unless
a separate scheduled value exists. A missing or `DateTime.MinValue` time MUST be represented as
absent rather than serialized as a sentinel. It MUST NOT expose email, Auth0 identifiers, private
reports, admin assignment, deletion state, or other private account metadata.

#### Scenario: Stable navigation data
- **WHEN** a summary is returned
- **THEN** it MUST include a non-empty tournament ID and match ID suitable for stable navigation

#### Scenario: Empty category
- **WHEN** a valid profile has no qualifying previous or upcoming match
- **THEN** the corresponding response array MUST be empty rather than omitted or synthesized

### Requirement: Efficient deterministic reads

The API MUST perform participant membership and previous/upcoming selection through bounded,
set-based persistence queries. It MUST NOT load all matches for a profile and filter them in
memory, and MUST NOT issue one query per tournament, match, or opponent.

#### Scenario: Many tournaments
- **WHEN** a profile participates in many tournaments
- **THEN** the summary read MUST use a constant bounded number of set-based queries with stable
  tournament and match ordering
