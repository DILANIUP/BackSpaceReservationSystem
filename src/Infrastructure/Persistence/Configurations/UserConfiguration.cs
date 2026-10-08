using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Name).IsRequired().HasMaxLength(150);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.IdentificationNumber).HasMaxLength(10);
        builder.HasIndex(u => u.IdentificationNumber).IsUnique();

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(256);

            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Career)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CareerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}