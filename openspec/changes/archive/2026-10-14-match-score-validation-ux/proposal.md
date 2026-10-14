# Validate match series scores in the UI

## Why

The backend accepts only decisive series scores that match the configured Best-of format. The match dialog currently allows incomplete, tied, and out-of-range reports to reach the API, where the user receives a generic failure.

## What Changes

- Explain the required series wins and allowed losing score in the match score editor.
- Block invalid and incomplete player submissions and administrator resolutions in the UI, while retaining backend validation as the authority.
- Show a safe, actionable backend validation message for HTTP 400 responses and preserve generic handling for server failures.

## Impact

Updates the match result lifecycle UI requirement and its player/admin score editor behavior. No backend contract changes.
