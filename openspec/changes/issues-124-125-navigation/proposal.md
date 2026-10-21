# Repair responsive navigation and Info dismissal

The shared header can overlap at intermediate desktop widths, the theme control
belongs with the footer language selector, and the Info menu currently relies
on a full-screen overlay for outside dismissal. This change makes the header
collapse before its controls collide, places the existing theme control in the
footer, and dismisses Info on outside pointer input or Escape without blocking
the action under the pointer.

## Scope

- Synchronize the responsive navigation breakpoint and preserve usable controls.
- Place the existing theme preference control alongside the footer language
  selector.
- Dismiss the Info menu on outside pointer input and Escape, restoring trigger
  focus for Escape while leaving pointer targets actionable.
