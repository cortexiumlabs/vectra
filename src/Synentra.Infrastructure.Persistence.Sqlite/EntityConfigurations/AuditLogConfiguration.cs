using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Synentra.Domain.AuditTrails;

namespace Synentra.Infrastructure.Persistence.Sqlite.EntityConfigurations;

public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
{
    public void Configure(EntityTypeBuilder<AuditTrail> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.TargetUrl).IsRequired();
        builder.Property(e => e.Status).IsRequired();
    }
}