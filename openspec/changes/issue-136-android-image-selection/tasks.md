## 1. Specification

- [x] Create and strictly validate the cross-picker recovery and diagnostics change.

## 2. Recovery

- [x] Add behavioral tests for interrupted profile, team and shared image reads and successful retry.
- [x] Return all image pickers to a usable state after a recoverable read failure without retaining stale pending bytes.
- [x] Run the focused recovery tests and a Release build.

## 3. Diagnostics

- [x] Add the disabled-by-default browser diagnostic switch and component stage logging.
- [x] Add the focused JavaScript self-check and validate the diagnostics artifacts.

## 4. Device Evidence

- [ ] Verify Android Chrome with Google Gallery and Files, short and 30+ second selections, all upload forms and a visible native input experiment.
- [ ] Verify Samsung Internet when available, desktop Chrome/Firefox and mobile Safari/iOS.
- [ ] Record confirmed root cause and device/browser evidence before archiving or claiming the Android issue is fixed.
