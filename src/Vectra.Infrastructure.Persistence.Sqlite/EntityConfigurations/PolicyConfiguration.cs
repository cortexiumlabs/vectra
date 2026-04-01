using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vectra.Core.Entities;

namespace Vectra.Infrastructure.Persistence.Sqlite.EntityConfigurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TargetApi).IsRequired();
        builder.Property(e => e.AllowedMethods).HasConversion(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}