namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class TarhElahiCourseDto
{
    public string Source { get; init; } = default!;

    public string Type { get; init; } = default!;

    /// <summary>
    /// Strapi Course's stable documentId (NOT numeric DB id).
    /// </summary>
    public string ExternalId { get; init; } = default!;

    public string? ParentExternalId { get; init; }

    public string Title { get; init; } = default!;

    public string Slug { get; init; } = default!;

    /// <summary>
    /// Authoritative raw Rial price from Strapi. Conversion to Noor happens elsewhere in ByeMoney.
    /// </summary>
    public decimal PriceRial { get; init; }

    public bool Published { get; init; }

    public bool Available { get; init; }

    public DateTime UpdatedAt { get; init; }
}

