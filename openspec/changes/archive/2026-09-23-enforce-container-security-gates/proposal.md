## Why

The deployable container can retain known HIGH vulnerabilities from its base image, and the current delivery workflow publishes without checking the built artifact. A release must be blocked before publication when its exact image contains a HIGH or CRITICAL vulnerability, while preserving useful scan evidence.

## What Changes

- Refresh vulnerable vendor packages in the final runtime image from the configured Azure Linux repositories.
- Make pull-request image scanning an enforced HIGH/CRITICAL gate while retaining the scan report and PR feedback when findings exist.
- Build the delivery image once, scan that exact local image, retain the report, and push only after the scan succeeds.
- Limit write permissions and Docker Hub credentials to the jobs and post-scan steps that require them.

## Capabilities

### New Capabilities

- `container-security-gates`: Defines runtime package remediation and enforced pre-publication vulnerability scanning.

### Modified Capabilities

None.

## Impact

The Docker runtime stage and GitHub Actions application CI and delivery workflows are affected. Application contracts and runtime configuration are unchanged.
