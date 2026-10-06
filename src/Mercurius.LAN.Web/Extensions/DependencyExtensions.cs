using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Mercurius.LAN.Web.APIClients;
#if INCLUDE_MOCK_BACKEND
using Mercurius.LAN.Web.Mock;
#endif
using Mercurius.LAN.Web.Middleware;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Mercurius.LAN.Web.Localization;
using Refit;
using System.Text.Json;
using System.Web;
#if INCLUDE_MOCK_BACKEND
using System.Security.Claims;
#endif

namespace Mercurius.LAN.Web.Extensions;

public static class DependencyExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool mockModeEnabled)
    {
#if INCLUDE_MOCK_BACKEND
        if(mockModeEnabled)
        {
            services.AddAuthorization();
            services.AddCascadingAuthenticationState();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/account/login";
                    options.LogoutPath = "/account/logout";
                    options.AccessDeniedPath = "/";
                    options.ClaimsIssuer = "MercuriusMock";
                    options.Cookie.Name = "mercurius-mock-auth";
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                });

            return services;
        }
#endif
        var auth0Options = GetAuth0Options(configuration);

        services.AddAuthorization();
        services.AddCascadingAuthenticationState();

        services.AddAuth0WebAppAuthentication(options =>
        {
            options.Domain = auth0Options.Domain;
            options.ClientId = auth0Options.ClientId;
            options.ClientSecret = auth0Options.ClientSecret;
            options.Scope = auth0Options.Scope;
            options.ResponseType = "code";
        })
        .WithAccessToken(options =>
        {
            options.Audience = auth0Options.Audience;
            options.Scope = auth0Options.Scope;
            options.UseRefreshTokens = auth0Options.UseRefreshTokens;
        });

        services.Configure<OpenIdConnectOptions>(Auth0Constants.AuthenticationScheme, options =>
        {
            options.SaveTokens = true;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = auth0Options.RoleClaimType;
            options.Events ??= new OpenIdConnectEvents();
            options.Events.OnAccessDenied = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(BuildLoginFailureRedirectUri(context.Properties?.RedirectUri, "cancelled"));
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(BuildLoginFailureRedirectUri(context.Properties?.RedirectUri, "failed"));
                return Task.CompletedTask;
            };
        });

        services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            });

        return services;
    }

    public static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        JsonSerializerOptions jsonOptions,
        IConfiguration configuration,
        bool mockModeEnabled)
    {
#if INCLUDE_MOCK_BACKEND
        if(mockModeEnabled)
            return services;
#endif

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions)
        };

        services.AddTransient<AccessTokenHandler>();
        services.AddTransient<ApiReadTimeoutHandler>();

        var configuredBaseAddress = configuration.GetValue<string>("MercuriusAPI:BaseAddress");
        var baseAddress = BuildApiBaseAddress(configuredBaseAddress);

        services.AddRefitClient<ILANClient>(refitSettings)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
            {
                UseCookies = false
            })
            .AddHttpMessageHandler<ApiReadTimeoutHandler>()
            .AddHttpMessageHandler<AccessTokenHandler>();

        services.AddRefitClient<IUserClient>(refitSettings)
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
            {
                UseCookies = false
            })
            .AddHttpMessageHandler<ApiReadTimeoutHandler>()
            .AddHttpMessageHandler<AccessTokenHandler>();

        return services;
    }

    internal static string BuildApiBaseAddress(string? configuredBaseAddress)
    {
        if(string.IsNullOrWhiteSpace(configuredBaseAddress))
            throw new InvalidOperationException("Missing required MercuriusAPI:BaseAddress configuration.");

        var normalizedAddress = configuredBaseAddress.TrimEnd('/');
        if(normalizedAddress.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            normalizedAddress = normalizedAddress[..^3];

        return $"{normalizedAddress}/";
    }

    public static IServiceCollection AddCustomServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool mockModeEnabled)
    {
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IContactEmailService, SmtpContactEmailService>();

#if INCLUDE_MOCK_BACKEND
        if(mockModeEnabled)
        {
            services.AddSingleton<MockBackendStore>();
            services.AddScoped<ITournamentService, MockTournamentService>();
            services.AddScoped<ITeamService, MockTeamService>();
            services.AddScoped<ISponsorService, MockSponsorService>();
            services.AddScoped<IGlobalSearchService, MockGlobalSearchService>();
            services.AddScoped<IUserSearchService, MockUserSearchService>();
            services.AddScoped<IPublicProfileService, MockPublicProfileService>();
            services.AddScoped<IUserClient, MockUserClient>();
            services.AddScoped<ITeamNotificationService, TeamNotificationService>();
            services.AddScoped<ITeamRealtimeService, NoopTeamRealtimeService>();
            services.AddHttpContextAccessor();
            return services;
        }
#endif

        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ISponsorService, SponsorService>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddScoped<IUserSearchService, UserSearchService>();
        services.AddScoped<IPublicProfileService, PublicProfileService>();
        services.AddScoped<ITeamNotificationService, TeamNotificationService>();
        services.AddScoped<ITeamRealtimeService, TeamRealtimeService>();
        services.AddHttpContextAccessor();

        return services;
    }

#if INCLUDE_MOCK_BACKEND
    public static string NormalizeMockPersona(string? persona, string fallbackPersona)
    {
        var candidate = string.IsNullOrWhiteSpace(persona) ? fallbackPersona : persona;

        return candidate.Trim().ToLowerInvariant() switch
        {
            "admin" => "admin",
            "anonymous" => "anonymous",
            _ => "user"
        };
    }

    internal static ClaimsPrincipal BuildMockPrincipal(string persona, MockBackendStore store)
    {
        var normalizedPersona = NormalizeMockPersona(persona, "user");
        var claims = new List<Claim>
        {
            new("mock_persona", normalizedPersona)
        };

        if(!string.Equals(normalizedPersona, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            var currentProfile = store.GetCurrentProfile(normalizedPersona);
            var profileUser = currentProfile.User;
            claims.Add(new Claim(ClaimTypes.Name, profileUser?.DisplayName ?? normalizedPersona));

            if(!string.IsNullOrWhiteSpace(profileUser?.Username))
            {
                claims.Add(new Claim("preferred_username", profileUser.Username));
                claims.Add(new Claim("nickname", profileUser.Username));
                claims.Add(new Claim("username", profileUser.Username));
            }

            if(!string.IsNullOrWhiteSpace(currentProfile.Email))
                claims.Add(new Claim(ClaimTypes.Email, currentProfile.Email));

            if(profileUser != null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, profileUser.Id.ToString()));

            if(string.Equals(normalizedPersona, "admin", StringComparison.OrdinalIgnoreCase))
                claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var identity = string.Equals(normalizedPersona, "anonymous", StringComparison.OrdinalIgnoreCase)
            ? new ClaimsIdentity(claims)
            : new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
#endif

    private static Auth0Options GetAuth0Options(IConfiguration configuration)
    {
        var auth0Options = configuration
            .GetRequiredSection(Auth0Options.SectionName)
            .Get<Auth0Options>() ?? new Auth0Options();

        var missingKeys = new List<string>();

        if(string.IsNullOrWhiteSpace(auth0Options.Domain))
            missingKeys.Add("Auth0:Domain");
        if(string.IsNullOrWhiteSpace(auth0Options.ClientId))
            missingKeys.Add("Auth0:ClientId");
        if(string.IsNullOrWhiteSpace(auth0Options.ClientSecret))
            missingKeys.Add("Auth0:ClientSecret");
        if(string.IsNullOrWhiteSpace(auth0Options.Audience))
            missingKeys.Add("Auth0:Audience");
        if(string.IsNullOrWhiteSpace(auth0Options.Scope))
            missingKeys.Add("Auth0:Scope");
        if(string.IsNullOrWhiteSpace(auth0Options.RoleClaimType))
            missingKeys.Add("Auth0:RoleClaimType");

        if(missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required Auth0 configuration: {string.Join(", ", missingKeys)}. Store Auth0:ClientSecret in user-secrets or an environment variable.");
        }

        return auth0Options;
    }

    private static string BuildLoginFailureRedirectUri(string? redirectUri, string reason)
    {
        var safeReturnUrl = LocalReturnUrlHelper.GetSafeLogoutReturnUrl(redirectUri);
        var uriBuilder = new UriBuilder($"http://localhost{safeReturnUrl}");
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["login"] = reason;
        uriBuilder.Query = query.ToString() ?? string.Empty;

        var failureUri = string.IsNullOrWhiteSpace(uriBuilder.Query)
            ? uriBuilder.Path
            : $"{uriBuilder.Path}?{uriBuilder.Query.TrimStart('?')}";

        return failureUri + uriBuilder.Fragment;
    }

}
