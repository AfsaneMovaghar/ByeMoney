using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.User;

using ByeMoney.Domain.Modules.Identity.Users;
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(
                id => id.Value,
                value => new UserId(value));

        builder.Property(u => u.ExternalUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(u => u.ExternalUserId)
            .IsUnique();

        builder.Property(u => u.FirstName)
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .HasMaxLength(255);

        builder.Property(u => u.Phone)
            .HasMaxLength(15);

        builder.HasIndex(u => u.Phone)
            .IsUnique()
            .HasFilter("\"Phone\" IS NOT NULL");

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.ProfileSyncedAt)
            .IsRequired();

        builder.Property(u => u.UserType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Ignore(u => u.DisplayName);
    }
}