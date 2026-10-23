## Why

Android Chrome reports that choosing an image from the system Gallery returns without a usable file and that the same picker can then stop opening. Controlled browser tests can reproduce interrupted image reads, but they cannot establish whether Android loses the native `change` event, the Blazor callback, the circuit, or the file stream. The existing upload surfaces currently provide inconsistent recovery, and the shared picker reports every `IOException` as an oversized file.

## What Changes

- Recover from interrupted browser file reads across profile, create-team, manage-team and shared tournament/sponsor pickers. Clear the failed pending selection, keep the controls usable, and show localized retry feedback.
- Keep replacement selections unusable until they have been read successfully, so failed bytes are never uploaded in place of an earlier selection.
- Add an explicitly enabled browser-only diagnostic switch that records image-picker lifecycle stages and sanitized file metadata in the browser console. It sends no telemetry and records no filenames, file contents or secrets.
- Document a physical-device procedure that separates native input events, component callbacks, observable reconnect/error DOM states and existing server circuit logs. The procedure does not infer the circuit state from DOM markers alone.

## Impact

Modified capabilities: `image-upload-recovery` and `image-selection-diagnostics`. The change is frontend-only and preserves the existing upload routes, authorization, media limits and backend-confirmed success behavior. Android Gallery correctness remains pending physical-device evidence and this change is not eligible for archival until that validation is complete.
