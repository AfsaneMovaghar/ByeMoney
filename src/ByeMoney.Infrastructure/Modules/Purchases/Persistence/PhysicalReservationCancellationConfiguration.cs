using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public class PhysicalReservationCancellationConfiguration : IEntityTypeConfiguration<PhysicalReservationCancellation>
{
    public void Configure(EntityTypeBuilder<PhysicalReservationCancellation> builder)
    {
        builder.ToTable("PhysicalReservationCancellations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PhysicalReservationCancellationId(value))
            .IsRequired();

        builder.Property(x => x.BuyerId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.CartId);

        builder.Property(x => x.OperationId).IsRequired();
        builder.Property(x => x.CartId).IsRequired();
        builder.Property(x => x.ReservationId).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAt);
    }
}
