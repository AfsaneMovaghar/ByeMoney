using ByeMoney.Domain.Modules.Identity.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.Permission;

public class PermissionConfiguration : IEntityTypeConfiguration<Domain.Modules.Identity.Permissions.Permission>
{
    public static readonly PermissionId NoorInjectPermissionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    public void Configure(EntityTypeBuilder<Domain.Modules.Identity.Permissions.Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PermissionId(value));

        builder.Property(p => p.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(250);

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasData(new
        {
            Id = NoorInjectPermissionId,
            Code = Permissions.Noor.Inject,
            Description = "Permission to inject Noor currency",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = (DateTime?)null
        });
    }
}

