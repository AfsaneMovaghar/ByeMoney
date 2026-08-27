using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common._Resources;
using ByeMoney.Domain.Common.Exceptions;
namespace ByeMoney.Domain.Modules.Identity.Users;

public class User : BaseEntity<UserId>
{
    public int StrapiUserId { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Phone { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public DateTime ProfileSyncedAt { get; private set; }
    public UserStatus Status { get; private set; }
    public UserType UserType { get; private set; }


    private User() { }

    public static User Create(int strapiUserId, string displayName, string phone, string role,UserType userType)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException(DomainMessages.UserDisplayNameRequired);
        return new User
        {
            Id = UserId.New(),
            StrapiUserId = strapiUserId,
            DisplayName = displayName,
            Phone = phone,
            Role = role,
            ProfileSyncedAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            UserType = userType
        };
    }

    public void SyncProfile(string displayName, string phone, string role)
    {
        DisplayName = displayName;
        Phone = phone;
        Role = role;
        ProfileSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow; 
    }

    public void MarkProfileSynced()
    {
        ProfileSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static User CreateFromStrapi(int strapiUserId)
    {
        return new User
        {
            Id = UserId.New(),
            StrapiUserId = strapiUserId,
            DisplayName = null,
            Phone = null,
            Role = string.Empty,
            ProfileSyncedAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            UserType = UserType.Normal
        };
    }

    public void Suspend() => Status = UserStatus.Suspended;
    public void Activate() => Status = UserStatus.Active;

    public bool NeedsProfileSync()
        => (DateTime.UtcNow - ProfileSyncedAt).TotalHours > 24;
}

