# Restore Tournament Panel Scroll Chaining

## Why

Tournament participant and schedule lists currently block vertical wheel input
at their boundaries, leaving the page unable to scroll while the pointer is over
the inline tournament detail panels.

## What Changes

- Preserve internal scrolling for overflowing participant and schedule lists.
- Allow vertical wheel input to continue to the document at each list boundary.
- Keep registration dialogs and their scroll containment unchanged.
- Cover empty participants, populated participants, and schedule panels at
  desktop, tablet, and mobile widths.
