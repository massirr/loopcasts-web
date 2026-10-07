using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Data;

// IdentityDbContext = our DbContext + the login tables (AspNetUsers, AspNetRoles, AspNetUserRoles, ...)
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // REQUIRED: configures the Identity tables

        modelBuilder.Entity<Course>()
            .Property(c => c.Level)
            .HasConversion<string>();

        // The database itself refuses duplicates: no invoice number twice, max. one invoice per order
        modelBuilder.Entity<Invoice>().HasIndex(i => i.Number).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(i => i.OrderId).IsUnique();
    }
}
