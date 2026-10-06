## Implementation

- [x] Update the final runtime image to install the vendor-fixed `pcre2` package and remove package-manager metadata.
- [x] Enforce HIGH/CRITICAL pull-request image scan failure while preserving scan feedback.
- [x] Build the delivery image once, scan the exact tagged artifact, retain its report, and push only after a successful scan.
- [x] Scope write permissions to release metadata and scan-feedback jobs, and defer Docker Hub authentication until the scan passes.

## Validation

- [x] Strictly validate this OpenSpec change before and after implementation.
- [x] Build the final container and verify its installed `pcre2` version.
- [x] Scan the fixed final image and confirm no HIGH/CRITICAL findings.
- [x] Scan the original vulnerable runtime image as a negative control and confirm the gate exits nonzero.
- [x] Validate the changed workflow syntax and inspect the build-scan-push dependency order.
- [x] Verify build and scan steps have read-only repository access and no Docker Hub credentials.
