using Microsoft.EntityFrameworkCore;

namespace RestaurantDelivery.Payment.Payments;

/// <summary>
/// EF Core context for the Payment service's own PostgreSQL database (ADR-006). The unique index on
/// <see cref="PaymentRecord.IdempotencyKey"/> backs the at-most-one-charge guarantee at the storage layer,
/// in addition to the store's read-before-write check.
/// </summary>
public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<PaymentRecord>();
        payment.ToTable("payments");
        payment.HasKey(p => p.Id);
        payment.Property(p => p.IdempotencyKey).IsRequired();
        payment.Property(p => p.CorrelationId).IsRequired();
        payment.Property(p => p.Amount).HasColumnType("numeric(18,2)");
        payment.Property(p => p.Status).HasConversion<string>();
        payment.Property(p => p.Plan).HasConversion<string>();
        payment.HasIndex(p => p.IdempotencyKey).IsUnique();
    }
}
