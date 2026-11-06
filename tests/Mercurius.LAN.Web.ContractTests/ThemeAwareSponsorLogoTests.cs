using System.ComponentModel.DataAnnotations;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ThemeAwareSponsorLogoTests
{
    private static readonly Guid CounterStrike2TournamentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task BothLogoVariantsRenderForTheActiveTheme()
    {
        var html = await RenderLogoAsync("/images/default.svg", "/images/light.svg");

        Assert.Contains("theme-aware-sponsor-logo__default", html);
        Assert.Contains("src=\"/images/default.svg\"", html);
        Assert.Contains("theme-aware-sponsor-logo__light", html);
        Assert.Contains("src=\"/images/light.svg\"", html);
        Assert.Equal(2, CountOccurrences(html, "alt=\"Example logo\""));
    }

    [Fact]
    public async Task LegacySponsorRendersOnlyTheDefaultLogo()
    {
        var html = await RenderLogoAsync("/images/default.svg", null);

        Assert.Contains("theme-aware-sponsor-logo__default", html);
        Assert.DoesNotContain("theme-aware-sponsor-logo__light", html);
    }

    [Fact]
    public void ActiveThemeAndForcedPreviewsSelectContrastingArtwork()
    {
        var css = File.ReadAllText(FindRepositoryFile(
            Path.Combine("src", "Mercurius.LAN.Web", "Components", "Shared", "ThemeAwareSponsorLogo.razor.css")));

        Assert.Contains("[data-theme-state=\"dark\"] .theme-aware-sponsor-logo--has-light .theme-aware-sponsor-logo__default", css);
        Assert.Contains("[data-theme-state=\"dark\"] .theme-aware-sponsor-logo--has-light .theme-aware-sponsor-logo__light", css);
        Assert.Contains(".theme-aware-sponsor-logo--light .theme-aware-sponsor-logo__default", css);
        Assert.Contains(".theme-aware-sponsor-logo--dark.theme-aware-sponsor-logo--has-light .theme-aware-sponsor-logo__light", css);
    }

    [Fact]
    public void CreateRequiresTheDefaultLogo()
    {
        var dto = CreateSponsorDto();
        dto.Logo = null;
        dto.LightLogo = new FakeBrowserFile("light.png");
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SponsorManagementDTO.Logo)));
    }

    [Fact]
    public void MockSponsorMutationRetainsReplacesAndRemovesTheLightLogo()
    {
        var store = CreateStore();
        var createDto = CreateSponsorDto();
        createDto.Logo = new FakeBrowserFile("default.png");
        createDto.LightLogo = new FakeBrowserFile("light.png");

        var created = store.CreateSponsor(createDto);
        Assert.NotNull(created.LightLogoUrl);

        var retained = store.UpdateSponsor(created.Id, new SponsorManagementDTO
        {
            Id = created.Id,
            IsCreateMode = false,
            Name = $"{created.Name} renamed",
            SponsorTier = created.SponsorTier,
            InfoUrl = created.InfoUrl,
            Description = created.Description
        });
        Assert.Equal(created.LightLogoUrl, retained.LightLogoUrl);

        var placed = store.ReplaceTournamentSponsors(CounterStrike2TournamentId, new ReplaceTournamentSponsorsDTO
        {
            SponsorPlacements =
            [
                new TournamentSponsorPlacementInputDTO
                {
                    SponsorId = retained.Id,
                    Context = SponsorContext.TournamentPartner
                }
            ]
        });
        Assert.Equal(retained.LightLogoUrl, placed.SponsorPlacement?.SponsorLightLogoUrl);

        var removed = store.UpdateSponsor(created.Id, new SponsorManagementDTO
        {
            Id = created.Id,
            IsCreateMode = false,
            Name = retained.Name,
            SponsorTier = retained.SponsorTier,
            InfoUrl = retained.InfoUrl,
            Description = retained.Description,
            RemoveLightLogo = true
        });
        Assert.Null(removed.LightLogoUrl);
    }

    private static SponsorManagementDTO CreateSponsorDto() => new()
    {
        Name = "Theme-aware sponsor",
        SponsorTier = SponsorTier.Gold,
        InfoUrl = "https://example.test/sponsor",
        Description = "Test sponsor"
    };

    private static async Task<string> RenderLogoAsync(string logoUrl, string? lightLogoUrl)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ThemeAwareSponsorLogo>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ThemeAwareSponsorLogo.LogoUrl)] = logoUrl,
                    [nameof(ThemeAwareSponsorLogo.LightLogoUrl)] = lightLogoUrl,
                    [nameof(ThemeAwareSponsorLogo.AltText)] = "Example logo"
                }));
            return component.ToHtmlString();
        });
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static MockBackendStore CreateStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine("src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
    }

    private static string FindRepositoryRoot()
    {
        foreach(var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while(directory is not null)
            {
                if(File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for the mock fixture.");
    }

    private static string FindRepositoryFile(string relativePath) =>
        Path.Combine(FindRepositoryRoot(), relativePath);

    private sealed class FakeBrowserFile(string name) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public long Size => 0;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Stream.Null;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(ThemeAwareSponsorLogoTests);
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
