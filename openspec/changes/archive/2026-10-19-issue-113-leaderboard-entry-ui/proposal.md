# Improve leaderboard result entry

## Why

The administrator result-entry dialog can lose the selected participant context when changing tabs or selecting an existing LAN user. Its labels and input contrast also need to make the flow clearer in both themes.

## What Changes

LAN-user selection identifies the selected user in the control, loads the linked participant's attempt history when one exists, and clears stale selections when switching participant modes. Result-entry wording and theme contrast improve, and mock deletion behavior stays aligned with the API contract.

The backend contract does not change. Removing a participant's final attempt removes the participant; other attempts and revision checks remain authoritative.
