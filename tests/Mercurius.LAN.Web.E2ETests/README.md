# Blazor UI / E2E tests

These tests run Chromium against the real Blazor Server frontend and ASP.NET Core API. Each fixture run creates a uniquely named PostgreSQL database, starts the API and frontend on loopback ports, and uses a local HTTPS OIDC provider to exercise the application's normal cookie and JWT authentication. No production Auth0 account or pre-existing application data is used.

Command-level evidence for the recorded verification runs, including the exact commands and their results, is in [VERIFICATION.md](VERIFICATION.md).

## Prerequisites

- .NET 10 SDK
- PostgreSQL reachable from the test process; the configured database role must be able to create and drop databases
- A trusted localhost development certificate for the in-process OIDC provider
- NuGet access on the first restore

No SMTP server is required. The fixture starts its own loopback SMTP sink on an ephemeral port for
the contact-form tests, so those tests exercise a real SMTP conversation without external
credentials, extra packages, or production configuration.

Trust the ASP.NET Core development certificate once on the machine running the tests:

```powershell
dotnet dev-certs https --trust
```

On Linux, OpenSSL only honors that trust when the dev-certs certificate directory is listed in
`SSL_CERT_DIR`, so export it before trusting; without it the command exits with code 4 and reports
the certificate as trusted by some clients only.

```bash
export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"
dotnet dev-certs https --trust
```

By default, the fixture connects to `Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres`. Override that connection with `TEST_POSTGRES_CONNECTION` if needed. The fixture always creates and drops only its own `mercurius_e2e_*` database; the configured database is used only for administrative `CREATE DATABASE` / `DROP DATABASE` operations.

The E2E project discovers a sibling backend checkout named `backend-playwright-e2e` or `mercurius-aalst-back-end`. If the backend is elsewhere, set `MERCURIUS_BACKEND_ROOT` to its repository root, or pass `-p:BackendProjectRoot=<path>` to `dotnet`.

## Run the suite

From the frontend repository root, restore, build and install the matching Playwright Chromium
browser once:

```powershell
dotnet restore tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj
dotnet build tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj
pwsh tests/Mercurius.LAN.Web.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Then run the whole suite:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj
```

Later runs only need `dotnet test`. For a Release build, build with `-c Release` and use
`bin/Release/net10.0/playwright.ps1` instead. The test project fails during build with an
actionable error if it cannot locate the backend checkout.

Run a focused test or class with the standard VSTest filter:

```powershell
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --filter "FullyQualifiedName~TeamManagementPageTests"
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --filter "FullyQualifiedName~CaptainCanRemoveMemberFromTeam"
```

To launch Chromium visibly, set `E2E_HEADED=1` for the test process:

```powershell
$env:E2E_HEADED = "1"
dotnet test tests/Mercurius.LAN.Web.E2ETests/Mercurius.LAN.Web.E2ETests.csproj --filter "FullyQualifiedName~InfrastructureSmokeTests"
Remove-Item Env:E2E_HEADED
```

For step-by-step debugging, add `PWDEBUG=1` alongside `E2E_HEADED=1`; the Playwright inspector
then pauses before each action.

## Isolation and diagnostics

Tests in the `Playwright E2E` xUnit collection run serially against the fixture's isolated database. Classes deriving from `E2ETestBase` truncate that database before each test and close any remaining browser contexts before the next reset. The database prefix guard refuses to reset anything except a fixture-owned `mercurius_e2e_*` database.

The fixture also starts the loopback SMTP sink, points the application's `ContactEmail` options at
it, and can hold or reject a single acceptance so the contact tests can assert the in-flight,
success, and transport-failure states. The sink is created with the fixture and disposed with it.

Each fixture run writes complete API and frontend child-process output to `api.log` and `frontend.log`, concise recent output tails to `process-output.log`, and browser traces plus final-page screenshots beneath `tests/Mercurius.LAN.Web.E2ETests/TestResults/PlaywrightE2E/<run-id>`. To place artifacts elsewhere, set `MERCURIUS_E2E_ARTIFACTS` to the desired directory. Traces can be opened with the installed Playwright CLI:

```powershell
pwsh tests/Mercurius.LAN.Web.E2ETests/bin/Debug/net10.0/playwright.ps1 show-trace <path-to-trace.zip>
```

The tests start and stop the API, frontend, OIDC provider, browser, and isolated database automatically. If startup fails, inspect the fixture exception and the matching run's `process-output.log`, then use `api.log` or `frontend.log` for full child-process output; common setup issues are PostgreSQL connectivity/permissions, the localhost certificate, a missing Chromium install, or an incorrect backend path.

## Continuous integration

`.github/workflows/ci-e2e.yml` runs this suite on `ubuntu-24.04` for pull requests that target `main` and change `src/**`, `tests/**`, or the workflow itself. Both repositories are public, so the job reads the backend with the default token and needs no secrets.

The job starts a disposable `postgres:17` service, checks the frontend repository out at the workspace root and the backend repository into `backend-playwright-e2e`, points `MERCURIUS_BACKEND_ROOT` at that checkout, trusts the ASP.NET Core development certificate, installs Playwright Chromium with its OS dependencies, then restores and builds the E2E project and runs it with `--no-build --no-restore`.

The automatic run checks the backend out at the commit pinned in the workflow's `E2E_BACKEND_REF` value, so a frontend pull request that consumes unmerged backend work stays reproducible and can pass before that backend change lands. Once the paired backend change is on `main`, update `E2E_BACKEND_REF` to the merged `main` commit, or remove the pin and the `env.E2E_BACKEND_REF` fallback from the checkout step to restore the plain `main` default. A manual `workflow_dispatch` run can point at any other backend ref through the `backend_ref` input, which overrides the pin.

The `workflow_dispatch` trigger only becomes usable once this workflow is registered on the default branch, so it cannot be dispatched from an unmerged branch.

On failure the job uploads `tests/Mercurius.LAN.Web.E2ETests/TestResults` - the `.trx` log plus the per-run Playwright traces, screenshots, and process logs - as the `playwright-e2e-test-results` artifact, retained for seven days.
