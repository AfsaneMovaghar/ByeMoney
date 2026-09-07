using ByeMoney.Domain.Modules.Identity.Roles;
using ByeMoney.Domain.Modules.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.User;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value));

        builder.Property(ur => ur.RoleId)
            .HasConversion(
                id => id.Value,
                value => new RoleId(value));

        builder.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(ur => ur.CreatedAt)
            .IsRequired();
    }
}

