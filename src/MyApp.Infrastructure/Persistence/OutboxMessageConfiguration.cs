using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Sequence);
        builder.Property(m => m.Sequence).ValueGeneratedOnAdd(); // the database orders delivery: ids are random, time can tie
        builder.UseConcurrencyToken();
        builder.HasIndex(m => m.MessageId).IsUnique();
        builder.HasIndex(m => m.ProcessedAt);
        builder.Property(m => m.Type).HasMaxLength(OutboxMessage.TypeMaxLength).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAt);
        builder.Property(m => m.CreatedAt);
        builder.Property(m => m.ProcessedAt).HasConversion<UtcTicksConverter>(); // filtered by retention
        builder.Property(m => m.Attempts);
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);
        builder.Ignore(m => m.IsDeadLettered);
    }
}
