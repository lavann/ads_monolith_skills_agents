using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure the Order entity to match the monolith's schema
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.CreatedUtc).IsRequired();
                entity.Property(o => o.CustomerId).IsRequired().HasMaxLength(100);
                entity.Property(o => o.Status).IsRequired().HasMaxLength(50);
                entity.Property(o => o.Total).HasColumnType("decimal(18,2)");
            });

            // Configure the OrderLine entity to match the monolith's schema
            modelBuilder.Entity<OrderLine>(entity =>
            {
                entity.ToTable("OrderLines");
                entity.HasKey(ol => ol.Id);
                entity.Property(ol => ol.Sku).IsRequired();
                entity.Property(ol => ol.Name).IsRequired();
                entity.Property(ol => ol.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(ol => ol.Quantity).IsRequired();

                // Configure relationship
                entity.HasOne(ol => ol.Order)
                      .WithMany(o => o.Lines)
                      .HasForeignKey(ol => ol.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
