using ByeMoney.Domain.Modules.Identity.Permissions;
using ByeMoney.Domain.Modules.Identity.Roles;
using ByeMoney.Infrastructure.Modules.Identity.Persistence.Permission;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.Role;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.RoleId)
            .HasConversion(
                id => id.Value,
                value => new RoleId(value));

        builder.Property(rp => rp.PermissionId)
            .HasConversion(
                id => id.Value,
                value => new PermissionId(value));

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rp => rp.CreatedAt)
            .IsRequired();

        builder.HasData(new
        {
            RoleId = RoleConfiguration.AdminRoleId,
            PermissionId = PermissionConfiguration.NoorInjectPermissionId,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

