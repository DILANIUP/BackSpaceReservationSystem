using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.Property(a => a.Type).IsRequired();

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.ResolutionObservation)
            .HasMaxLength(1000);

        builder.HasOne(a => a.Resource)
            .WithMany(r => r.Alerts)
            .HasForeignKey(a => a.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Space)
            .WithMany(s => s.Alerts)
            .HasForeignKey(a => a.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}