# container-security-gates Specification

## Purpose
TBD - created by archiving change enforce-container-security-gates. Update Purpose after archive.
## Requirements
### Requirement: Remediated runtime container

The deployable runtime image MUST install vendor-provided security fixes for known HIGH or CRITICAL vulnerabilities in packages inherited from its base image while preserving the application's supported .NET runtime major version and startup behavior.

#### Scenario: A fixed vendor package is available

- **WHEN** the configured Azure Linux repository provides a fixed version of a vulnerable runtime package
- **THEN** the final image MUST contain the fixed package version
- **AND** the image MUST NOT suppress or ignore the vulnerability finding

### Requirement: Enforced pull-request image scan

The pull-request image workflow MUST fail when Trivy finds a HIGH or CRITICAL vulnerability in the built image. The workflow MUST retain human-readable scan feedback even when the security gate fails.

#### Scenario: Pull-request image contains a blocking vulnerability

- **WHEN** Trivy reports a HIGH or CRITICAL vulnerability in the pull-request image
- **THEN** the image job MUST fail
- **AND** the workflow MUST still attempt to publish its scan feedback

### Requirement: Scan the release artifact before publication

The delivery workflow MUST build the tagged release image once, scan that exact image for HIGH and CRITICAL vulnerabilities, and push that same image only after the scan succeeds. The workflow MUST retain the scan report when the scan fails.

#### Scenario: Release image passes the gate

- **WHEN** Trivy reports no HIGH or CRITICAL vulnerabilities in the locally built release image
- **THEN** the workflow MUST push that same tagged image without rebuilding it

#### Scenario: Release image fails the gate

- **WHEN** Trivy reports a HIGH or CRITICAL vulnerability in the locally built release image
- **THEN** the workflow MUST retain the scan report
- **AND** the workflow MUST NOT push the image

### Requirement: Least-privilege pipeline credentials

Container workflows MUST default to read-only repository access. Repository or pull-request write access MUST be limited to the jobs that publish release metadata or scan feedback. Docker registry credentials MUST NOT be exposed to image build or vulnerability scan steps and MUST be acquired only after the release image passes the vulnerability gate.

#### Scenario: Release image is built and scanned

- **WHEN** the delivery workflow builds and scans a release image
- **THEN** those steps MUST run without Docker Hub credentials
- **AND** the image job MUST have read-only repository access

#### Scenario: Release image passes the vulnerability gate

- **WHEN** the release image scan succeeds
- **THEN** the workflow MAY authenticate to Docker Hub immediately before pushing the already-scanned image

