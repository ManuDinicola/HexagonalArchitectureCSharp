using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Application.Idempotency;

namespace MyApp.Infrastructure.Persistence;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => new { r.Scope, r.Key }); // the unique key is what makes two concurrent first calls fail instead of both winning
        builder.UseConcurrencyToken();
        builder.Property(r => r.Scope).HasMaxLength(200);
        builder.Property(r => r.Key).HasMaxLength(IdempotencyKeys.MaxLength);
        builder.Property(r => r.Fingerprint).HasMaxLength(64).IsRequired(); // SHA-256 as hex
        builder.Property(r => r.Response).IsRequired();
        builder.Property(r => r.CreatedAt).HasConversion<UtcTicksConverter>(); // filtered by retention
    }
}
