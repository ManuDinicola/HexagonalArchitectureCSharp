using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// Optimistic concurrency for aggregate roots, kept out of the Domain: a shadow "Version" column that changes on every save of the aggregate,
/// including when only one of its owned children changed. Two writers holding the same version cannot both win.
/// </summary>
internal static class ConcurrencyToken
{
    public const string PropertyName = "Version";

    /// <summary>Call this in the configuration of every aggregate root. An Infrastructure test fails if one forgets.</summary>
    public static EntityTypeBuilder<T> UseConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<Guid>(PropertyName).IsConcurrencyToken();
        return builder;
    }

    /// <summary>Gives every added or changed aggregate root a fresh version. Call right before saving.</summary>
    public static void StampVersions(this ChangeTracker tracker, Guid version)
    {
        tracker.DetectChanges();
        var entries = tracker.Entries().ToList();

        foreach (var root in entries.Where(e => !e.Metadata.IsOwned() && e.Metadata.FindProperty(PropertyName) is not null))
        {
            var changed = root.State is EntityState.Added or EntityState.Modified
                || (root.State == EntityState.Unchanged && entries.Any(e => e.State != EntityState.Unchanged && IsOwnedBy(e, root)));

            if (changed)
            {
                root.Property(PropertyName).CurrentValue = version;
            }
        }
    }

    private static bool IsOwnedBy(EntityEntry child, EntityEntry root)
    {
        var ownership = child.Metadata.FindOwnership();
        return ownership is not null
            && ownership.PrincipalEntityType == root.Metadata
            && Equals(
                child.Property(ownership.Properties[0].Name).CurrentValue,
                root.Property(ownership.PrincipalKey.Properties[0].Name).CurrentValue);
    }
}
