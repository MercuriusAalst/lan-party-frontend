# Design

The final Azure Linux runtime stage updates only the vulnerable `pcre2` package through the image's configured vendor repository and removes package-manager metadata. This keeps the existing .NET major version, runtime family, and application startup behavior.

CI runs Trivy with an enforced HIGH/CRITICAL exit code. The scan step records its outcome so report/comment steps can still run before a final explicit failure.

Delivery loads one tagged Buildx result into the local Docker daemon, scans that exact tag, uploads the JSON report even on failure, and pushes the same tag only after a successful scan. No rebuild occurs between scan and push.

Workflow permissions default to read-only repository contents. Only the release calculation job receives repository and pull-request write access, and only the front-end image job receives pull-request write access for scan feedback. Docker Hub authentication occurs after a successful scan, immediately before the push, so build and scan steps do not receive registry credentials.
