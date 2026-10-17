using Mercurius.LAN.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Shared;

public partial class UserAvatar
{
    [Inject] private IConfiguration Configuration { get; set; } = null!;

    /// <summary>Player label used for the initials fallback and the accessible name.</summary>
    [Parameter] public string? Name { get; set; }
    [Parameter] public string? PictureUrl { get; set; }
    [Parameter] public string Size { get; set; } = "regular";
    [Parameter] public string? AdditionalClass { get; set; }

    private bool _imageFailed;
    private string _resolvedPictureUrl = string.Empty;

    protected override void OnParametersSet()
    {
        var resolvedPictureUrl = AssetUrlResolver.Resolve(Configuration, PictureUrl);
        if(string.Equals(resolvedPictureUrl, _resolvedPictureUrl, StringComparison.Ordinal))
            return;

        // A replaced reference must be allowed to load even after the previous image failed.
        _resolvedPictureUrl = resolvedPictureUrl;
        _imageFailed = false;
    }

    private string DisplayName => Name?.Trim() ?? string.Empty;
    private bool IsDecorative => string.IsNullOrWhiteSpace(DisplayName);
    private string? AriaLabel => IsDecorative ? null : DisplayName;
    private bool HasImage => !_imageFailed && !string.IsNullOrWhiteSpace(_resolvedPictureUrl);
    private string ResolvedUrl => _resolvedPictureUrl;

    private string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length == 0)
                return "?";

            return parts.Length == 1
                ? parts[0].Length == 1 ? parts[0].ToUpperInvariant() : parts[0][..2].ToUpperInvariant()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }

    private string ContainerClass
    {
        get
        {
            var classes = $"user-avatar user-avatar--{Size}";
            return string.IsNullOrWhiteSpace(AdditionalClass) ? classes : $"{classes} {AdditionalClass}";
        }
    }

    private void HandleImageError() => _imageFailed = true;
}
