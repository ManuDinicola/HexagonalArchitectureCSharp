using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Infrastructure.Persistence;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.UseConcurrencyToken();
        builder.Property(o => o.CustomerId).HasMaxLength(Order.CustomerIdMaxLength).IsRequired();
        builder.Property(o => o.PlacedAt);
        builder.Ignore(o => o.Total);

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("OrderLines");
            lines.WithOwner().HasForeignKey("OrderId");
            lines.Property<int>("Id");
            lines.HasKey("Id");
            lines.Property(l => l.Sku).HasMaxLength(OrderLine.SkuMaxLength).IsRequired();
            lines.Property(l => l.Quantity);
            lines.Property(l => l.UnitPrice).HasPrecision(18, OrderLine.UnitPriceScale);
            lines.Ignore(l => l.Total);
        });
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
