using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class RelayLeaseConfiguration : IEntityTypeConfiguration<RelayLease>
{
    public void Configure(EntityTypeBuilder<RelayLease> builder)
    {
        builder.ToTable("RelayLeases");
        builder.HasKey(l => l.Name);
        builder.UseConcurrencyToken();
        builder.Property(l => l.Name).HasMaxLength(100);
        builder.Property(l => l.Owner).HasMaxLength(200).IsRequired();
        builder.Property(l => l.ExpiresAt);
    }
}
