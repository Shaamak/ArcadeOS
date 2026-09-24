using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Infrastructure.Persistence;

/// <summary>
/// The EF Core database context — the single gateway between your C# code and PostgreSQL.
/// Think of it as a "unit of work": all operations within a request share one DbContext instance.
///
/// HOW IT WORKS:
/// 1. You define DbSet&lt;T&gt; properties — one per table you want EF to manage.
/// 2. OnModelCreating() is called once at startup to configure column names, constraints, indexes, seed data.
/// 3. Migrations translate your C# model changes into SQL ALTER TABLE / CREATE TABLE statements.
/// </summary>
public class ArcadeDbContext : DbContext
{
    // Constructor: ASP.NET Core DI passes DbContextOptions (connection string, provider, etc.)
    public ArcadeDbContext(DbContextOptions<ArcadeDbContext> options) : base(options) { }

    // DbSet = represents the "app_users" table in PostgreSQL.
    // You can query it like: _db.Users.Where(u => u.IsActive).ToListAsync()
    public DbSet<AppUser> Users => Set<AppUser>();

    // DbSet = represents the "customers" table in PostgreSQL.
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Customer table configuration ---
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .HasColumnName("id");

            entity.Property(c => c.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.Email)
                .HasColumnName("email")
                .HasMaxLength(100)
                .IsRequired();

            // Unique constraint on Email
            entity.HasIndex(c => c.Email)
                .IsUnique();

            entity.Property(c => c.Phone)
                .HasColumnName("phone")
                .HasMaxLength(20);

            entity.Property(c => c.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(c => c.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");
        });

        // --- AppUser table configuration ---
        modelBuilder.Entity<AppUser>(entity =>
        {
            // Map the C# class to a snake_case table name (PostgreSQL convention)
            entity.ToTable("app_users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                .HasColumnName("id");

            entity.Property(u => u.Username)
                .HasColumnName("username")
                .HasMaxLength(50)
                .IsRequired();

            // Unique index: two users cannot share the same username
            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .HasColumnName("password_hash")
                .IsRequired();

            // Store the enum as a string ("Admin", "Staff") rather than an integer (0, 1)
            // Reason: string values are readable in the DB and survive enum reordering
            entity.Property(u => u.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(u => u.LocationId)
                .HasColumnName("location_id");

            entity.Property(u => u.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(u => u.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(u => u.RefreshToken)
                .HasColumnName("refresh_token");

            entity.Property(u => u.RefreshTokenExpiresAt)
                .HasColumnName("refresh_token_expires_at");
        });

        // --- Seed Data: create default admin and staff users ---
        // BCrypt hash of "Admin@123"
        const string adminHash = "$2a$12$qlRqQ6H.t8b.SuBrpIK37ekByVcbHuqXJQvHgXaJG4nDL.JGCWv2u";
        // BCrypt hash of "Staff@123"
        const string staffHash = "$2a$12$TQX3bEJ7kKqE3P5Hp6LqB.kW45v7UhvJl1JpJEVbCjFqFc8wqF1pO";

        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "admin",
                PasswordHash = adminHash,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new AppUser
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Username = "staff1",
                PasswordHash = staffHash,
                Role = UserRole.Staff,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
