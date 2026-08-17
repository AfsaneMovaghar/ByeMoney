using ByeMoney.Domain.Common;
using ByeMoney.Domain.Enums;

namespace ByeMoney.Domain.Entities;

public class User : BaseEntity<Guid>
{
    public int StrapiUserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public DateTime ProfileSyncedAt { get; private set; }
    public UserStatus Status { get; private set; }
    public UserType UserType { get; private set; }

    // public Account? Account { get; private set; }

    private User() { }

    public static User Create(int strapiUserId, string displayName, string phone, string role,UserType userType)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            StrapiUserId = strapiUserId,
            DisplayName = displayName,
            Phone = phone,
            Role = role,
            ProfileSyncedAt = DateTime.Now,
            Status = UserStatus.Active,
            UserType= userType
        };
    }

    public void SyncProfile(string displayName, string phone, string role)
    {
        DisplayName = displayName;
        Phone = phone;
        Role = role;
        ProfileSyncedAt = DateTime.Now;
        UpdatedAt = DateTime.Now; // اگه UpdatedAt رو protected set داری
    }

    public void Suspend() => Status = UserStatus.Suspended;
    public void Activate() => Status = UserStatus.Active;

    public bool NeedsProfileSync()
        => (DateTime.Now - ProfileSyncedAt).TotalHours > 24;
}



    

