# Correct Info Navigation and Ticket Guidance

## Why

The Info navigation control keeps stale active state after client-side route
changes, and the Info page does not clearly direct visitors to the ticket
purchase options requested for the event.

## What Changes

- Keep Info highlighted while `/info` and its section fragments are current.
- Close an open Info menu after header navigation reaches another route.
- Show the requested localized page heading and ticket purchase guidance.
- Link visitors to the event Google Forms ticket page while retaining the
  venue purchase option.
