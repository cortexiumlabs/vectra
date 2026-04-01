using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vectra.Core.Entities;

namespace Vectra.Infrastructure.Persistence.Sqlite.EntityConfigurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.TargetUrl).IsRequired();
        builder.Property(e => e.Status).IsRequired();
    }
}