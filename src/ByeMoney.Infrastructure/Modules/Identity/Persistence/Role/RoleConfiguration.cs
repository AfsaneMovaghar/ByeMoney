using ByeMoney.Domain.Modules.Identity.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.Role;

public class RoleConfiguration : IEntityTypeConfiguration<Domain.Modules.Identity.Roles.Role>
{
    public static readonly RoleId AdminRoleId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    public void Configure(EntityTypeBuilder<Domain.Modules.Identity.Roles.Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RoleId(value));

        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.HasData(new
        {
            Id = AdminRoleId,
            Name = "Admin",
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = (DateTime?)null
        });
    }
}

