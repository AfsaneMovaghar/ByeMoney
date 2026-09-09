namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

public class TarhElahiUserDto
{
    /// <summary>
    /// Strapi User's stable documentId (NOT numeric DB id, NOT phone number).
    /// </summary>
    public string ExternalUserId { get; init; } = default!;

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public bool Confirmed { get; init; }

    public bool Blocked { get; init; }

    public bool IsMobileVerified { get; init; }

    public DateTime? CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public bool IsActive => Confirmed && !Blocked;
}

