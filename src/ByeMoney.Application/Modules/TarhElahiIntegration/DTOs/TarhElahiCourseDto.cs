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
    /// Authoritative Noor price from the external catalog.
    /// </summary>
    public decimal PriceNoor { get; init; }

    public bool Published { get; init; }

    public bool Available { get; init; }

    public DateTime UpdatedAt { get; init; }
}

