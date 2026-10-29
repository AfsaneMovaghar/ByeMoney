using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Gifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Gifts;

public class GiftCampaignSettingConfiguration : IEntityTypeConfiguration<GiftCampaignSetting>
{
    public void Configure(EntityTypeBuilder<GiftCampaignSetting> builder)
    {
        builder.ToTable("GiftCampaignSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CampaignType)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(s => s.CampaignType)
            .IsUnique();

        builder.Property(s => s.AmountNoor)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(s => s.IsEnabled)
            .IsRequired();

        builder.Property(s => s.StartDateUtc);
        builder.Property(s => s.EndDateUtc);

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        builder.Property(s => s.UpdatedByAdminUserId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);

        builder.HasData(new
        {
            Id = new Guid("88888888-8888-8888-8888-888888888888"),
            CampaignType = GiftCampaignType.Welcome,
            AmountNoor = 50m,
            IsEnabled = true,
            StartDateUtc = (DateTime?)null,
            EndDateUtc = (DateTime?)null,
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

