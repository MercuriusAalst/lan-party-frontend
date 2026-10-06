using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using Mercurius.LAN.API.Data;
using Mercurius.Modules.Identity.Domain;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using Npgsql;

namespace Mercurius.LAN.Web.E2ETests;

public sealed class PlaywrightE2EFixture : IAsyncLifetime
{
    private readonly List<ContextArtifacts> _contexts = [];
    private readonly ConcurrentBag<HttpClient> _createdApiClients = new();
    private string _frontendRoot = string.Empty;
    private string _backendRoot = string.Empty;
    private string _runDirectory = string.Empty;
    private string _traceDirectory = string.Empty;
    private IsolatedPostgresDatabase? _database;
    private LocalOidcServer? _oidc;
    private LocalSmtpSink? _smtp;
    private X509Certificate2? _serverCertificate;
    private ManagedProcess? _apiProcess;
    private ManagedProcess? _frontendProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _personaNumber;
    private int _traceNumber;

    public string BaseUrl { get; private set; } = string.Empty;
    public string ApiBaseUrl { get; private set; } = string.Empty;
    public string DatabaseConnectionString => _database?.ConnectionString ?? throw new InvalidOperationException("The E2E database has not started.");
    public string ArtifactDirectory => _runDirectory;
    public string ApiDiagnostics => _apiProcess?.RecentOutput ?? string.Empty;
    public string FrontendDiagnostics => _frontendProcess?.RecentOutput ?? string.Empty;
    public string OidcDiagnostics => _oidc?.RecentRequests ?? string.Empty;
    public HttpClient Api { get; private set; } = null!;
    internal LocalSmtpSink ContactEmailSink => _smtp ?? throw new InvalidOperationException("The E2E SMTP sink has not started.");

    public async Task InitializeAsync()
    {
        _frontendRoot = FindFrontendRoot();
        _backendRoot = FindBackendRoot(_frontendRoot);
        var runId = $"{DateTime.UtcNow:yyyyMMddTHHmmss}_{Environment.ProcessId}_{Guid.NewGuid():N}";
        var configuredArtifactRoot = Environment.GetEnvironmentVariable("MERCURIUS_E2E_ARTIFACTS");
        var artifactRoot = string.IsNullOrWhiteSpace(configuredArtifactRoot)
            ? Path.Combine(_frontendRoot, "tests", "Mercurius.LAN.Web.E2ETests", "TestResults", "PlaywrightE2E")
            : Path.GetFullPath(configuredArtifactRoot);
        _runDirectory = Path.Combine(artifactRoot, runId);
        _traceDirectory = Path.Combine(_runDirectory, "traces");
        Directory.CreateDirectory(_traceDirectory);

        try
        {
            _serverCertificate = GetTrustedDevelopmentCertificate();
            _oidc = await LocalOidcServer.StartAsync(_serverCertificate);
            await VerifyLocalOidcAsync();
            _database = await IsolatedPostgresDatabase.CreateAsync();
            _smtp = LocalSmtpSink.Start();

            var apiEnvironment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Test",
                ["DOTNET_ENVIRONMENT"] = "Test",
                ["Mercurius.LAN.API_ConnectionStrings__MercuriusDB"] = _database.ConnectionString,
                ["Mercurius.LAN.API_FileStorage__Location"] = Path.Combine(_runDirectory, "api-media"),
                ["Mercurius.LAN.API_FileStorage__MaxFileSizeInMB"] = "5",
                ["Mercurius.LAN.API_Auth0__Authority"] = _oidc.Authority,
                ["Mercurius.LAN.API_Auth0__Audience"] = _oidc.Audience,
                ["Mercurius.LAN.API_Auth0__RoleClaimType"] = LocalOidcServer.RoleClaimType,
                ["Mercurius.LAN.API_Auth0__ManagementAudience"] = new Uri(_oidc.Issuer, "api/v2/").ToString(),
                ["Mercurius.LAN.API_Auth0__ManagementClientId"] = LocalOidcServer.ManagementClientId,
                ["Mercurius.LAN.API_Auth0__ManagementClientSecret"] = LocalOidcServer.ManagementClientSecret,
                ["Mercurius.LAN.API_Auth0__DatabaseConnection"] = LocalOidcServer.DatabaseConnection,
                ["Mercurius.LAN.API_Auth0__PasswordResetClientId"] = LocalOidcServer.ClientId,
                ["Mercurius.LAN.API_Logging__EventLog__LogLevel__Default"] = "None",
                ["Mercurius.LAN.API_Logging__LogLevel__Microsoft.AspNetCore.Authentication"] = "Debug",
                ["Mercurius.LAN.API_Logging__LogLevel__Microsoft.IdentityModel"] = "Debug",
                ["Mercurius.LAN.API_RateLimiting__GlobalPermitLimit"] = "10000",
                ["Mercurius.LAN.API_RateLimiting__SearchPermitLimit"] = "10000",
                ["Mercurius.LAN.API_TeamInvite__MaintenanceIntervalSeconds"] = "3600"
            };
            Directory.CreateDirectory(apiEnvironment["Mercurius.LAN.API_FileStorage__Location"]);
            _apiProcess = await ManagedProcess.StartWebProjectAsync(
                Path.Combine(_backendRoot, "src", "MercuriusAPI", "Mercurius.LAN.API.csproj"),
                apiEnvironment,
                Path.Combine(_runDirectory, "api.log"));
            ApiBaseUrl = EnsureTrailingSlash(_apiProcess.BaseUri.ToString());

            var frontendEnvironment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_CONTENTROOT"] = GetFrontendOutputDirectory(_frontendRoot),
                ["ASPNETCORE_WEBROOT"] = Path.Combine(_frontendRoot, "src", "Mercurius.LAN.Web", "wwwroot"),
                ["Auth0__Domain"] = _oidc.Domain,
                ["Auth0__ClientId"] = LocalOidcServer.ClientId,
                ["Auth0__ClientSecret"] = LocalOidcServer.ClientSecret,
                ["Auth0__Audience"] = LocalOidcServer.ApiAudience,
                ["Auth0__Scope"] = "openid profile email",
                ["Auth0__RoleClaimType"] = LocalOidcServer.RoleClaimType,
                ["MercuriusAPI__BaseAddress"] = ApiBaseUrl,
                ["MockBackend__Enabled"] = "false",
                ["ContactEmail__Enabled"] = "true",
                ["ContactEmail__Host"] = "127.0.0.1",
                ["ContactEmail__Port"] = _smtp.Port.ToString(CultureInfo.InvariantCulture),
                ["ContactEmail__EnableSsl"] = "false",
                ["ContactEmail__SenderEmail"] = "lan-contact@sender.test",
                ["ContactEmail__SenderName"] = "Mercurius LAN E2E",
                ["ContactEmail__RecipientEmail"] = "lan-contact@recipient.test",
                ["Logging__LogLevel__Microsoft.AspNetCore.Authentication"] = "Debug",
                ["Logging__LogLevel__Microsoft.IdentityModel"] = "Debug"
            };
            _frontendProcess = await ManagedProcess.StartWebProjectAsync(
                Path.Combine(_frontendRoot, "src", "Mercurius.LAN.Web", "Mercurius.LAN.Web.csproj"),
                frontendEnvironment,
                Path.Combine(_runDirectory, "frontend.log"));
            BaseUrl = EnsureTrailingSlash(_frontendProcess.BaseUri.ToString());

            Api = new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
            await AssertHttpReadyAsync(Api, new Uri(new Uri(ApiBaseUrl), "v1/lan/tournaments"));

            var tokenProbe = await CreatePersonaAsync("fixture-token-probe");
            using (var authenticatedApi = CreateApiClient(tokenProbe))
            using (var response = await authenticatedApi.GetAsync("v1/lan/users/me"))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"The API rejected a local OIDC access token: {(int)response.StatusCode} {response.StatusCode}. {responseBody}{Environment.NewLine}Expected issuer: {_oidc.Issuer}{Environment.NewLine}Configured API authority: {_oidc.Authority}{Environment.NewLine}API audience: {_oidc.Audience}{Environment.NewLine}Token subject: {tokenProbe.Subject}{Environment.NewLine}Local OIDC requests:{Environment.NewLine}{_oidc.RecentRequests}{Environment.NewLine}API output:{Environment.NewLine}{_apiProcess.RecentOutput}");
                }
            }

            _playwright = await Playwright.CreateAsync();
            try
            {
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = !string.Equals(Environment.GetEnvironmentVariable("E2E_HEADED"), "1", StringComparison.Ordinal)
                });
            }
            catch (PlaywrightException exception)
            {
                throw new InvalidOperationException(
                    "Playwright Chromium is not installed for the pinned Microsoft.Playwright package. Build the E2E project, then run `pwsh tests/Mercurius.LAN.Web.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium`.",
                    exception);
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public MercuriusDBContext CreateDbContext() =>
        _database?.CreateDbContext() ?? throw new InvalidOperationException("The E2E database has not started.");

    public async Task<E2EPersona> CreatePersonaAsync(
        string label,
        bool isAdmin = false,
        bool createProfile = true,
        bool emailVerified = false,
        bool hasPasswordResetIdentity = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var uniqueId = Guid.NewGuid();
        var number = Interlocked.Increment(ref _personaNumber);
        var slug = new string(label.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).Take(10).ToArray());
        if (slug.Length == 0)
            slug = "member";
        var username = $"e2e{slug}{uniqueId:N}";
        username = username[..Math.Min(username.Length, 32)];
        var name = $"{label.Trim()} {number}";
        var email = $"e2e-{uniqueId:N}@example.test";
        var subject = $"auth0|e2e-{uniqueId:N}";
        var roles = isAdmin ? new[] { "admin" } : [];
        var userId = createProfile ? uniqueId : (Guid?)null;
        var persona = new E2EPersona(userId, subject, username, name, email, isAdmin, emailVerified, hasPasswordResetIdentity, roles);

        _oidc?.Register(persona);
        if (createProfile)
        {
            await using var db = CreateDbContext();
            db.Users.Add(new User
            {
                Id = userId!.Value,
                Auth0UserId = subject,
                Username = username,
                NormalizedUsername = username,
                Firstname = label.Trim()[..Math.Min(label.Trim().Length, 100)],
                Lastname = "E2E",
                Email = email,
                EmailVerified = emailVerified,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        return persona;
    }

    public HttpClient CreateApiClient(E2EPersona? persona = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        if (persona is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _oidc!.CreateAccessToken(persona));
        _createdApiClients.Add(client);
        return client;
    }

    internal void DisposeCreatedApiClients()
    {
        List<Exception>? failures = null;
        while (_createdApiClients.TryTake(out var client))
        {
            try
            {
                client.Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more test API clients could not be disposed.", failures);
    }

    public async Task<IBrowserContext> NewContextAsync()
    {
        var browser = _browser ?? throw new InvalidOperationException("Playwright has not started.");
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
            Locale = "en-US",
            TimezoneId = "Europe/Brussels"
        });
        var tracePath = Path.Combine(_traceDirectory, $"{Interlocked.Increment(ref _traceNumber):D3}.zip");
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });
        var artifacts = new ContextArtifacts(context, tracePath);
        artifacts.TrackedContext = TracingBrowserContextProxy.Create(context, () => CloseContextAsync(artifacts));
        context.Page += (_, page) => E2EPageExtensions.Observe(page);
        lock (_contexts)
            _contexts.Add(artifacts);
        return artifacts.TrackedContext;
    }

    public async Task<IBrowserContext> NewAuthenticatedContextAsync(E2EPersona persona)
    {
        var context = await NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await LoginAsync(page, persona);
            return context;
        }
        catch
        {
            await CloseContextAsync(context);
            throw;
        }
    }

    public async Task LoginAsync(IPage page, E2EPersona persona)
    {
        var appBase = new Uri(BaseUrl, UriKind.Absolute);
        var returnUrl = "/";
        if (Uri.TryCreate(page.Url, UriKind.Absolute, out var current) &&
            current.Authority == appBase.Authority &&
            current.Scheme == appBase.Scheme &&
            current.AbsolutePath is not "/account/login" and not "/account/register")
        {
            returnUrl = current.PathAndQuery + current.Fragment;
        }

        returnUrl = GetSafeLocalReturnUrl(returnUrl);
        var expectedReturnUri = GetExpectedLandingUri(appBase, returnUrl, persona);
        var loginUri = new Uri(appBase, $"account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        await page.GotoAsync(loginUri.ToString());
        await SelectPersonaAsync(page, persona);
        await page.WaitForURLAsync(url => IsSameUri(url, expectedReturnUri));

        // Reaching the app origin alone is not enough: the OIDC callback itself is served from
        // this origin before it redirects to the requested page. Wait for the authenticated UI
        // as well so callers cannot interrupt the callback while its cookie is being issued.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new()
        {
            Name = $"{persona.Username} account menu",
            Exact = true
        })).ToBeVisibleAsync();
    }

    private static string GetSafeLocalReturnUrl(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            returnUrl.Any(char.IsControl) ||
            !Uri.TryCreate(returnUrl, UriKind.Relative, out _) ||
            !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    private static Uri GetExpectedLandingUri(Uri appBase, string returnUrl, E2EPersona persona)
    {
        var requestedReturnUri = new Uri(appBase, returnUrl);
        var returnPath = requestedReturnUri.AbsolutePath.Trim('/');
        if (string.Equals(returnPath, "complete-profile", StringComparison.OrdinalIgnoreCase) ||
            returnPath.StartsWith("account/", StringComparison.OrdinalIgnoreCase))
        {
            return requestedReturnUri;
        }

        var query = QueryHelpers.ParseQuery(requestedReturnUri.Query);
        if (query.TryGetValue("registration", out var registrationValues) &&
            registrationValues.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)))
        {
            var profileReturnQuery = query
                .Where(entry => !string.Equals(entry.Key, "registration", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(entry => entry.Key, entry => (string?)entry.Value.ToString());
            var profileReturnUrl = QueryHelpers.AddQueryString(requestedReturnUri.AbsolutePath, profileReturnQuery) + requestedReturnUri.Fragment;
            return new Uri(appBase, $"/complete-profile?registration=true&returnUrl={Uri.EscapeDataString(profileReturnUrl)}");
        }

        return persona.UserId is null
            ? new Uri(appBase, $"/complete-profile?returnUrl={Uri.EscapeDataString(returnUrl)}")
            : requestedReturnUri;
    }

    private static bool IsSameUri(string actualUrl, Uri expectedUri) =>
        Uri.TryCreate(actualUrl, UriKind.Absolute, out var actualUri) &&
        actualUri.Scheme == expectedUri.Scheme &&
        actualUri.Authority == expectedUri.Authority &&
        actualUri.AbsolutePath == expectedUri.AbsolutePath &&
        actualUri.Query == expectedUri.Query &&
        actualUri.Fragment == expectedUri.Fragment;

    public async Task SelectPersonaAsync(IPage page, E2EPersona persona)
    {
        var buttonName = $"Continue as {persona.Name} ({persona.Username})";
        await page.GetByRole(AriaRole.Button, new() { Name = buttonName }).ClickAsync();
    }

    public async Task CloseContextAsync(IBrowserContext context)
    {
        ContextArtifacts? artifacts;
        lock (_contexts)
        {
            artifacts = _contexts.FirstOrDefault(entry => ReferenceEquals(entry.TrackedContext, context));
        }

        if (artifacts is not null)
            await CloseContextAsync(artifacts);
        else
            await context.CloseAsync();
    }

    public async Task CloseAllContextsAsync()
    {
        ContextArtifacts[] contexts;
        lock (_contexts)
            contexts = _contexts.ToArray();

        List<Exception>? failures = null;
        foreach (var context in contexts)
        {
            try
            {
                await CloseContextAsync(context);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more Playwright contexts could not be closed before database reset.", failures);
    }

    private async Task CloseContextAsync(ContextArtifacts artifacts)
    {
        Task closing;
        lock (_contexts)
        {
            if (artifacts.Closing is not null)
            {
                closing = artifacts.Closing;
            }
            else
            {
                _contexts.Remove(artifacts);
                artifacts.Closing = CloseContextCoreAsync(artifacts);
                closing = artifacts.Closing;
            }
        }

        await closing;
    }

    private static async Task CloseContextCoreAsync(ContextArtifacts artifacts)
    {
        try
        {
            var lastOpenPage = artifacts.Context.Pages.LastOrDefault(page => !page.IsClosed);
            if (lastOpenPage is not null)
            {
                await lastOpenPage.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.ChangeExtension(artifacts.TracePath, ".png"),
                    FullPage = true
                });
            }
        }
        catch (PlaywrightException)
        {
            // A page can close while its final screenshot is being captured.
        }

        try
        {
            await artifacts.Context.Tracing.StopAsync(new TracingStopOptions { Path = artifacts.TracePath });
        }
        finally
        {
            await artifacts.Context.CloseAsync();
        }
    }

    private static async Task AssertHttpReadyAsync(HttpClient client, Uri address)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        Exception? lastError = null;
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(address, timeout.Token);
                if ((int)response.StatusCode < 500)
                    return;
                lastError = new HttpRequestException($"The API readiness request returned {(int)response.StatusCode}.");
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
            }
            await Task.Delay(100, timeout.Token);
        }
        throw new InvalidOperationException($"The API did not become ready at {address}.", lastError);
    }

    private async Task VerifyLocalOidcAsync()
    {
        using var identityProvider = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            using var metadata = await identityProvider.GetAsync(new Uri(_oidc!.Issuer, ".well-known/openid-configuration"));
            metadata.EnsureSuccessStatusCode();
            using var document = System.Text.Json.JsonDocument.Parse(await metadata.Content.ReadAsStringAsync());
            if (document.RootElement.GetProperty("issuer").GetString() != _oidc.Issuer.ToString())
                throw new InvalidOperationException("The local OIDC metadata issuer does not match the configured test issuer.");

            using var keys = await identityProvider.GetAsync(document.RootElement.GetProperty("jwks_uri").GetString()!);
            keys.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"The local OIDC HTTPS endpoints are not reachable at {_oidc!.Issuer}. The trusted development certificate must include localhost and have a private key (subject={_serverCertificate?.Subject}, privateKey={_serverCertificate?.HasPrivateKey}).",
                exception);
        }
    }

    private static string FindFrontendRoot()
    {
        var configured = Environment.GetEnvironmentVariable("MERCURIUS_FRONTEND_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "src", "Mercurius.LAN.Web", "Mercurius.LAN.Web.csproj")))
            return Path.GetFullPath(configured);

        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "Mercurius.LAN.Web.csproj")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Set MERCURIUS_FRONTEND_ROOT to the frontend repository checkout.");
    }

    private static string FindBackendRoot(string frontendRoot)
    {
        var configured = Environment.GetEnvironmentVariable("MERCURIUS_BACKEND_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "src", "MercuriusAPI", "Mercurius.LAN.API.csproj")))
            return Path.GetFullPath(configured);

        var candidates = new[]
        {
            Path.Combine(frontendRoot, ".tmp", "backend-playwright-e2e"),
            Path.GetFullPath(Path.Combine(frontendRoot, "..", "backend-playwright-e2e")),
            Path.GetFullPath(Path.Combine(frontendRoot, "..", "mercurius-aalst-back-end"))
        };
        var match = candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "src", "MercuriusAPI", "Mercurius.LAN.API.csproj")));
        return match is not null
            ? Path.GetFullPath(match)
            : throw new DirectoryNotFoundException("Set MERCURIUS_BACKEND_ROOT to the backend repository checkout.");
    }

    private static X509Certificate2 GetTrustedDevelopmentCertificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var now = DateTime.UtcNow;
        var certificate = store.Certificates
            .OfType<X509Certificate2>()
            .FirstOrDefault(candidate =>
                candidate.HasPrivateKey &&
                candidate.NotBefore.ToUniversalTime() <= now &&
                candidate.NotAfter.ToUniversalTime() > now &&
                string.Equals(candidate.GetNameInfo(X509NameType.DnsName, forIssuer: false), "localhost", StringComparison.OrdinalIgnoreCase));
        if (certificate is null)
        {
            throw new InvalidOperationException(
                "A trusted ASP.NET Core HTTPS development certificate with a localhost DNS name and private key is required. Run `dotnet dev-certs https --trust` and retry.");
        }

        return certificate;
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";

    private static string GetFrontendOutputDirectory(string frontendRoot)
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = outputDirectory.Parent?.Name ?? "Debug";
        var targetFramework = outputDirectory.Name;
        return Path.Combine(frontendRoot, "src", "Mercurius.LAN.Web", "bin", configuration, targetFramework);
    }

    public async Task DisposeAsync()
    {
        var cleanupFailures = new List<Exception>();

        void TryCleanup(string resource, Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException($"Could not dispose {resource}.", exception));
            }
        }

        async Task TryCleanupAsync(string resource, Func<Task> cleanup)
        {
            try
            {
                await cleanup();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException($"Could not dispose {resource}.", exception));
            }
        }

        async Task TryCleanupValueTaskAsync(string resource, Func<ValueTask> cleanup)
        {
            try
            {
                await cleanup();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException($"Could not dispose {resource}.", exception));
            }
        }

        TryCleanup("test API clients", DisposeCreatedApiClients);
        TryCleanup("shared API client", () => Api?.Dispose());

        var browser = _browser;
        if (browser is not null)
        {
            await TryCleanupAsync("browser contexts", CloseAllContextsAsync);
            await TryCleanupAsync("browser", () => browser.CloseAsync());
        }

        // Capture the child-process output before stopping the processes, even when browser
        // teardown failed. The first test failure and its diagnostics should remain available.
        await TryCleanupAsync("process diagnostics", WriteDiagnosticsAsync);

        if (_playwright is not null)
            TryCleanup("Playwright", _playwright.Dispose);

        if (_frontendProcess is not null)
            await TryCleanupValueTaskAsync("frontend process", _frontendProcess.DisposeAsync);

        if (_smtp is not null)
            await TryCleanupValueTaskAsync("local SMTP sink", _smtp.DisposeAsync);

        if (_apiProcess is not null)
            await TryCleanupValueTaskAsync("API process", _apiProcess.DisposeAsync);

        if (_oidc is not null)
            await TryCleanupValueTaskAsync("local OIDC server", _oidc.DisposeAsync);

        if (_serverCertificate is not null)
            TryCleanup("development certificate", _serverCertificate.Dispose);

        if (_database is not null)
            await TryCleanupValueTaskAsync("isolated database", _database.DisposeAsync);

        if (cleanupFailures.Count > 0)
        {
            if (!string.IsNullOrEmpty(_runDirectory))
            {
                try
                {
                    await File.WriteAllLinesAsync(
                        Path.Combine(_runDirectory, "cleanup-errors.log"),
                        cleanupFailures.Select(failure => failure.ToString()));
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(new InvalidOperationException("Could not write the cleanup error report.", exception));
                }
            }

            throw new AggregateException("One or more E2E fixture resources failed to shut down cleanly.", cleanupFailures);
        }
    }

    private async Task WriteDiagnosticsAsync()
    {
        if (string.IsNullOrEmpty(_runDirectory))
            return;

        var diagnostics = $"OIDC requests:{Environment.NewLine}{OidcDiagnostics}{Environment.NewLine}{Environment.NewLine}Frontend output tail (full log: frontend.log):{Environment.NewLine}{FrontendDiagnostics}{Environment.NewLine}{Environment.NewLine}API output tail (full log: api.log):{Environment.NewLine}{ApiDiagnostics}";
        try
        {
            await File.WriteAllTextAsync(Path.Combine(_runDirectory, "process-output.log"), diagnostics);
        }
        catch (IOException)
        {
            // A failing test should retain its Playwright trace even if the diagnostic file cannot be written.
        }
    }

    private sealed class ContextArtifacts(IBrowserContext context, string tracePath)
    {
        public IBrowserContext Context { get; } = context;
        public string TracePath { get; } = tracePath;
        public IBrowserContext TrackedContext { get; set; } = null!;
        public Task? Closing { get; set; }
    }
}
