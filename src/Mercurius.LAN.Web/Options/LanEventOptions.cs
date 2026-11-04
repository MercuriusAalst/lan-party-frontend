using System.ComponentModel.DataAnnotations;

namespace Mercurius.LAN.Web.Options;

public sealed class LanEventOptions
{
    public const string SectionName = "LanEvent";

    [Required]
    public string Name { get; set; } = "Mercurius LAN";

    [Required]
    public string EventWindow { get; set; } = "Date To Be Announced";

    [Required]
    public string VenueName { get; set; } = "Odisee Aalst";

    [Required]
    public string Address { get; set; } = "Kwalestraat 154, 9320 Aalst, Belgium";

    [Required]
    public string MapEmbedUrl { get; set; } = string.Empty;

    [Required]
    public string DirectionsUrl { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string ContactEmail { get; set; } = "mercuriusaalst.studentenclub@gmail.com";

    public List<PackingItemOptions> PackingItems { get; set; } = [];

    public List<TicketOption> Tickets { get; set; } = [];

    public RegistrationFormOptions Registration { get; set; } = new();

    public List<MenuSectionOptions> MenuSections { get; set; } = [];

    public List<SocialLinkOptions> SocialLinks { get; set; } = [];
}

public sealed class PackingItemOptions
{
    public int Index { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Group { get; set; } = string.Empty;

    [Required]
    public string GroupKey { get; set; } = string.Empty;

    [Required]
    public string Icon { get; set; } = string.Empty;
}

public sealed class TicketOption
{
    [Required]
    public string Kind { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Price { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? RegistrationUrl { get; set; }
}

public sealed class RegistrationFormOptions
{
    public string? GeneralUrl { get; set; }
    public string? FirstNameEntryId { get; set; }
    public string? LastNameEntryId { get; set; }
    public string? UsernameEntryId { get; set; }
}

public sealed class MenuSectionOptions
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Icon { get; set; } = string.Empty;

    public List<MenuItemOptions> Items { get; set; } = [];
}

public sealed class MenuItemOptions
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
}

public sealed class SocialLinkOptions
{
    [Required]
    public string Label { get; set; } = string.Empty;

    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string Icon { get; set; } = string.Empty;

    [Required]
    public string AriaLabel { get; set; } = string.Empty;
}
