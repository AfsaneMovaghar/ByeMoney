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

        builder.Property(u => u.StrapiUserId)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100);

        builder.Property(u => u.Phone)
            .HasMaxLength(15);

        builder.HasIndex(u => u.StrapiUserId)
            .IsUnique();

        builder.HasIndex(u => u.Phone)
            .IsUnique();
       
       builder.Property(u => u.Role)
                  .HasMaxLength(50)
                  .IsRequired();
       
       builder.Property(u => u.ProfileSyncedAt)
                  .IsRequired();
        //ذخیره به صورت متن، خواناتره توی دیتابیس
        builder.Property(u => u.Status)
                 .HasConversion<string>()     
                 .HasMaxLength(20)
                 .IsRequired();
       
        builder.Property(u => u.UserType)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();
    }
}