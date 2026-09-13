using ByeMoney.Domain.Modules.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Modules.Settings.Persistence;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public static readonly DateTime SeedTimestampUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable(SystemSettingConstants.Tables.SystemSettings);

        builder.HasKey(s => s.Key);

        builder.Property(s => s.Key)
            .HasMaxLength(SystemSettingConstants.Limits.KeyMaxLength)
            .IsRequired();

        builder.Property(s => s.Value)
            .HasMaxLength(SystemSettingConstants.Limits.ValueMaxLength)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(SystemSettingConstants.Limits.DescriptionMaxLength);

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        builder.HasData(SystemSetting.Create(
            SystemSettingConstants.Keys.RialToNoor,
            SystemSettingConstants.Defaults.RialToNoorRateString,
            SystemSettingConstants.Defaults.RialToNoorDescription,
            SeedTimestampUtc));
    }
}