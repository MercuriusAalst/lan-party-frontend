using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mercurius.LAN.Web.E2ETests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Mercurius.LAN.Web.E2ETests.Infrastructure;

internal sealed class LocalOidcServer : IAsyncDisposable
{
    public const string ClientId = "mercurius-e2e-client";
    public const string ClientSecret = "mercurius-e2e-client-secret";
    public const string ApiAudience = "https://api.mercurius-e2e.test";
    public const string RoleClaimType = "https://mercurius-aalst.be/roles";
    public const string ManagementClientId = "mercurius-e2e-management";
    public const string ManagementClientSecret = "mercurius-e2e-management-secret";
    public const string DatabaseConnection = "e2e-users";

    private readonly WebApplication _app;
    private readonly RSA _signingKey;
    private readonly string _keyId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, E2EPersona> _personas = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AuthorizationRequest> _requests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _recentRequests = new();

    private LocalOidcServer(WebApplication app, RSA signingKey, Uri issuer)
    {
        _app = app;
        _signingKey = signingKey;
        Issuer = issuer;
    }

    public Uri Issuer { get; }
    public string Authority => Issuer.ToString().TrimEnd('/');
    public string Audience => ApiAudience;
    public string Domain => Issuer.Authority;
    public string RecentRequests => string.Join(Environment.NewLine, _recentRequests.ToArray());

    public static async Task<LocalOidcServer> StartAsync(X509Certificate2 certificate, CancellationToken cancellationToken = default)
    {
        var signingKey = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listenOptions => listenOptions.UseHttps(certificate));
        });

        var app = builder.Build();
        LocalOidcServer? server = null;
        app.Use(async (context, next) =>
        {
            server?._recentRequests.Enqueue($"{context.Request.Method} {context.Request.Path}");
            await next(context);
        });
        app.MapGet("/.well-known/openid-configuration", () =>
        {
            var issuer = server!.Issuer.ToString();
            return Results.Json(new
            {
                issuer,
                authorization_endpoint = new Uri(server.Issuer, "authorize").ToString(),
                token_endpoint = new Uri(server.Issuer, "oauth/token").ToString(),
                userinfo_endpoint = new Uri(server.Issuer, "userinfo").ToString(),
                end_session_endpoint = new Uri(server.Issuer, "v2/logout").ToString(),
                jwks_uri = new Uri(server.Issuer, ".well-known/jwks.json").ToString(),
                response_types_supported = new[] { "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
                scopes_supported = new[] { "openid", "profile", "email" },
                token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
                grant_types_supported = new[] { "authorization_code", "client_credentials" },
                code_challenge_methods_supported = new[] { "S256" }
            });
        });
        app.MapGet("/.well-known/jwks.json", () => Results.Json(new { keys = new[] { server!.PublicKey() } }));
        app.MapGet("/authorize", (HttpRequest request) => server!.ShowAuthorizationPage(request));
        app.MapPost("/authorize/continue", (HttpRequest request) => server!.ContinueAuthorizationAsync(request));
        app.MapPost("/authorize/cancel", (HttpRequest request) => server!.CancelAuthorizationAsync(request));
        app.MapPost("/oauth/token", (HttpRequest request) => server!.IssueTokensAsync(request));
        app.MapGet("/userinfo", (HttpRequest request) => server!.GetUserInfo(request));
        app.MapGet("/api/v2/users/{**subject}", (string? subject) => server!.GetManagementProfile(subject));
        app.MapPost("/api/v2/jobs/verification-email", () => Results.Json(new { id = $"job_{Guid.NewGuid():N}" }, statusCode: StatusCodes.Status201Created));
        app.MapPost("/dbconnections/change_password", () => Results.Text("We've just sent you an email to reset your password."));
        app.MapGet("/v2/logout", (HttpRequest request) => server!.EndSession(request));

        await app.StartAsync(cancellationToken);
        var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault()
            ?? throw new InvalidOperationException("The local OIDC server did not publish its HTTPS address.");
        var port = new Uri(address).Port;
        server = new LocalOidcServer(app, signingKey, new Uri($"https://localhost:{port}/"));
        return server;
    }

    public void Register(E2EPersona persona) => _personas[persona.Subject] = persona;

    public string CreateAccessToken(E2EPersona persona) => WriteToken(persona, Audience, nonce: null, TimeSpan.FromMinutes(15));

    private object PublicKey()
    {
        var parameters = _signingKey.ExportParameters(false);
        return new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = _keyId,
            n = Base64UrlEncoder.Encode(parameters.Modulus!),
            e = Base64UrlEncoder.Encode(parameters.Exponent!)
        };
    }

    private IResult ShowAuthorizationPage(HttpRequest request)
    {
        var query = request.Query;
        var ticket = Guid.NewGuid().ToString("N");
        _requests[ticket] = new AuthorizationRequest(
            query["client_id"].ToString(),
            query["redirect_uri"].ToString(),
            query["state"].ToString(),
            query["nonce"].ToString(),
            query["audience"].ToString(),
            query["code_challenge"].ToString(),
            query["code_challenge_method"].ToString(),
            query["response_mode"].ToString(),
            string.Equals(query["screen_hint"], "signup", StringComparison.OrdinalIgnoreCase));

        var buttons = new StringBuilder();
        foreach (var persona in _personas.Values.OrderBy(persona => persona.Username, StringComparer.Ordinal))
        {
            buttons.Append("<form method=\"post\" action=\"/authorize/continue\"><input type=\"hidden\" name=\"ticket\" value=\"")
                .Append(HtmlEncoder.Default.Encode(ticket))
                .Append("\"><button type=\"submit\" name=\"subject\" value=\"")
                .Append(HtmlEncoder.Default.Encode(persona.Subject))
                .Append("\">Continue as ")
                .Append(HtmlEncoder.Default.Encode(persona.Name))
                .Append(" (")
                .Append(HtmlEncoder.Default.Encode(persona.Username))
                .Append(")</button></form>");
        }

        var title = _requests[ticket].IsRegistration ? "Mercurius E2E sign up" : "Mercurius E2E sign in";
        var html = $"<!doctype html><html><head><title>{title}</title></head><body><main><h1>{title}</h1><p>Choose a deterministic test identity.</p>{buttons}<form method=\"post\" action=\"/authorize/cancel\"><input type=\"hidden\" name=\"ticket\" value=\"{HtmlEncoder.Default.Encode(ticket)}\"><button type=\"submit\">Cancel sign in</button></form></main></body></html>";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private async Task<IResult> ContinueAuthorizationAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        if (!_requests.TryRemove(form["ticket"].ToString(), out var authorization) ||
            !_personas.TryGetValue(form["subject"].ToString(), out var persona) ||
            !IsAllowedCallback(authorization.RedirectUri))
            return Results.BadRequest("The test authorization request is invalid.");

        var code = Guid.NewGuid().ToString("N");
        _codes[code] = new AuthorizationCode(authorization, persona);
        var callback = QueryHelpers.AddQueryString(authorization.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = authorization.State
        });
        if (string.Equals(authorization.ResponseMode, "form_post", StringComparison.OrdinalIgnoreCase))
        {
            var html = $"<!doctype html><html><body><form method=\"post\" action=\"{HtmlEncoder.Default.Encode(authorization.RedirectUri)}\"><input type=\"hidden\" name=\"code\" value=\"{HtmlEncoder.Default.Encode(code)}\"><input type=\"hidden\" name=\"state\" value=\"{HtmlEncoder.Default.Encode(authorization.State)}\"></form><script>document.forms[0].submit()</script></body></html>";
            return Results.Content(html, "text/html; charset=utf-8");
        }
        return Results.Redirect(callback);
    }

    private async Task<IResult> CancelAuthorizationAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        if (!_requests.TryRemove(form["ticket"].ToString(), out var authorization) || !IsAllowedCallback(authorization.RedirectUri))
            return Results.BadRequest("The test authorization request is invalid.");

        var callback = QueryHelpers.AddQueryString(authorization.RedirectUri, new Dictionary<string, string?>
        {
            ["error"] = "access_denied",
            ["error_description"] = "The test user cancelled sign in.",
            ["state"] = authorization.State
        });
        return Results.Redirect(callback);
    }

    private async Task<IResult> IssueTokensAsync(HttpRequest request)
    {
        var values = await ReadValuesAsync(request);
        values.TryGetValue("client_id", out var clientId);
        values.TryGetValue("client_secret", out var clientSecret);
        if (values.TryGetValue("grant_type", out var grant) && grant == "client_credentials")
        {
            if (clientId != ManagementClientId || clientSecret != ManagementClientSecret)
                return Results.Unauthorized();

            return Results.Json(new { access_token = "e2e-management-token", token_type = "Bearer", expires_in = 3600 });
        }

        if (!values.TryGetValue("grant_type", out grant) || grant != "authorization_code" ||
            clientId != ClientId || clientSecret != ClientSecret ||
            !values.TryGetValue("code", out var code) || !_codes.TryRemove(code, out var authorizationCode) ||
            !values.TryGetValue("redirect_uri", out var redirectUri) || redirectUri != authorizationCode.Request.RedirectUri ||
            !IsValidCodeVerifier(values.GetValueOrDefault("code_verifier"), authorizationCode.Request.CodeChallenge))
            return Results.BadRequest(new { error = "invalid_grant" });

        var accessToken = WriteToken(authorizationCode.Persona, authorizationCode.Request.Audience is { Length: > 0 } audience ? audience : Audience, null, TimeSpan.FromMinutes(15));
        var idToken = WriteToken(authorizationCode.Persona, ClientId, authorizationCode.Request.Nonce, TimeSpan.FromMinutes(15));
        return Results.Json(new
        {
            access_token = accessToken,
            id_token = idToken,
            token_type = "Bearer",
            expires_in = 900,
            scope = "openid profile email"
        });
    }

    private IResult GetUserInfo(HttpRequest request)
    {
        if (!TryGetSubject(request, out var persona))
            return Results.Unauthorized();

        return Results.Json(new
        {
            sub = persona.Subject,
            name = persona.Name,
            preferred_username = persona.Username,
            nickname = persona.Username,
            email = persona.Email,
            email_verified = persona.EmailVerified,
            roles = persona.Roles
        });
    }

    private IResult GetManagementProfile(string? subject)
    {
        var unescapedSubject = Uri.UnescapeDataString(subject ?? string.Empty);
        if (!_personas.TryGetValue(unescapedSubject, out var persona))
            return Results.NotFound();

        var identities = persona.HasPasswordResetIdentity
            ? new[] { new { connection = DatabaseConnection, provider = "auth0", user_id = persona.Username } }
            : Array.Empty<object>();
        return Results.Json(new
        {
            user_id = persona.Subject,
            email = persona.Email,
            email_verified = persona.EmailVerified,
            identities
        });
    }

    private IResult EndSession(HttpRequest request)
    {
        var returnTo = request.Query["returnTo"].ToString();
        if (!Uri.TryCreate(returnTo, UriKind.Absolute, out var uri) ||
            !IPAddress.TryParse(uri.Host, out var ip) || !IPAddress.IsLoopback(ip) ||
            uri.AbsolutePath != "/account/logout/callback")
            return Results.BadRequest("The test logout return URL is invalid.");

        return Results.Redirect(uri.ToString());
    }

    private string WriteToken(E2EPersona persona, string audience, string? nonce, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, persona.Subject),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Name, persona.Name),
            new("preferred_username", persona.Username),
            new("nickname", persona.Username),
            new(JwtRegisteredClaimNames.Email, persona.Email),
            new(JwtRegisteredClaimNames.EmailVerified, persona.EmailVerified ? "true" : "false", ClaimValueTypes.Boolean)
        };
        claims.AddRange(persona.Roles.Select(role => new Claim(RoleClaimType, role)));
        if (!string.IsNullOrWhiteSpace(nonce))
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));

        var token = new JwtSecurityToken(
            issuer: Issuer.ToString(),
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-10),
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = _keyId }, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool TryGetSubject(HttpRequest request, out E2EPersona persona)
    {
        persona = null!;
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(authorization[7..]);
            return _personas.TryGetValue(token.Subject, out persona!);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, string>> ReadValuesAsync(HttpRequest request)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            return form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
        }

        using var document = await JsonDocument.ParseAsync(request.Body);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.Ordinal);
    }

    private static bool IsValidCodeVerifier(string? verifier, string expectedChallenge)
    {
        if (string.IsNullOrWhiteSpace(verifier) || string.IsNullOrWhiteSpace(expectedChallenge))
            return string.IsNullOrWhiteSpace(expectedChallenge);
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(challenge), Encoding.ASCII.GetBytes(expectedChallenge));
    }

    private static bool IsAllowedCallback(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttp &&
        IPAddress.TryParse(uri.Host, out var ip) &&
        IPAddress.IsLoopback(ip) &&
        uri.AbsolutePath == "/callback";

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _signingKey.Dispose();
    }

    private sealed record AuthorizationRequest(
        string ClientId,
        string RedirectUri,
        string State,
        string Nonce,
        string Audience,
        string CodeChallenge,
        string CodeChallengeMethod,
        string ResponseMode,
        bool IsRegistration);

    private sealed record AuthorizationCode(AuthorizationRequest Request, E2EPersona Persona);
}
