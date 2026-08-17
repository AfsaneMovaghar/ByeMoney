using ByeMoney.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.StrapiUserId)
            .IsRequired();

        // هر کاربر Strapi فقط یک بار در ByeMoney رکورد دارد
        builder.HasIndex(u => u.StrapiUserId)
            .IsUnique();

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.ProfileSyncedAt)
            .IsRequired();

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()   // ذخیره enum به‌صورت متن، خواناتر در دیتابیس
            .HasMaxLength(20);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt);

    }
}