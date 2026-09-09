using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;
namespace ByeMoney.Domain.Modules.Identity.Users;

public class User : BaseEntity<UserId>
{
    public string ExternalUserId { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ProfileSyncedAt { get; private set; }
    public UserType UserType { get; private set; }

    public string? DisplayName =>
        string.IsNullOrWhiteSpace($"{FirstName} {LastName}")
            ? null
            : $"{FirstName} {LastName}".Trim();

    private User() { }

    public static User CreateFromStrapi(
        string externalUserId,
        string? phone = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        bool confirmed = false,
        bool blocked = false,
        UserType userType = UserType.Normal)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
            throw new DomainException(DomainErrors.User_ExternalUserIdRequired);

        return new User
        {
            Id = UserId.New(),
            ExternalUserId = externalUserId,
            Phone = phone,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = confirmed && !blocked,
            ProfileSyncedAt = DateTime.MinValue,
            UserType = userType
        };
    }

    public void SyncProfile(
        string? phone,
        string? email,
        string? firstName,
        string? lastName,
        bool confirmed,
        bool blocked)
    {
        Phone = phone;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        IsActive = confirmed && !blocked;
        ProfileSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProfileSynced()
    {
        ProfileSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool NeedsProfileSync()
        => (DateTime.UtcNow - ProfileSyncedAt).TotalHours > 24;
}

