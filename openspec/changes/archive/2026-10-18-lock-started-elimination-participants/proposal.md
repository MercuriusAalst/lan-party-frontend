# Lock started elimination participants

## Why

Removing a registration after a single or double elimination tournament leaves Scheduled can invalidate its bracket.

## What changes

- Lock admin removal of individual and team registrations for single and double elimination tournaments after they leave Scheduled.
- Keep leaderboard and other bracket registration behavior unchanged.

## Impact

Admin registration UI and mock backend behavior. The production backend enforces the same rule; API routes and response shapes remain unchanged.
