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

    // Wallets: 1:1 with Customer
    public DbSet<Wallet> Wallets => Set<Wallet>();

    // Transactions: the append-only financial ledger
    public DbSet<Transaction> Transactions => Set<Transaction>();

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

        // --- Wallet table configuration ---
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("wallets");

            entity.HasKey(w => w.Id);
            entity.Property(w => w.Id).HasColumnName("id");

            // UNIQUE constraint: one wallet per customer (enforced at DB level)
            entity.Property(w => w.CustomerId).HasColumnName("customer_id");
            entity.HasIndex(w => w.CustomerId).IsUnique();

            // NUMERIC(12,2) — up to 9,999,999,999.99 credits, 2 decimal places
            entity.Property(w => w.Balance)
                .HasColumnName("balance")
                .HasColumnType("numeric(12,2)")
                .HasDefaultValue(0m);

            entity.Property(w => w.LifetimeCredits)
                .HasColumnName("lifetime_credits")
                .HasColumnType("numeric(12,2)")
                .HasDefaultValue(0m);

            entity.Property(w => w.TicketBalance)
                .HasColumnName("ticket_balance")
                .HasDefaultValue(0);

            // xmin is a PostgreSQL system column that auto-increments on every row update.
            // EF Core uses it as the optimistic concurrency token — no extra column needed!
            entity.Property(w => w.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion()
                .ValueGeneratedOnAddOrUpdate();

            entity.Property(w => w.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            // EF Core relationship: Wallet has one Customer, Customer has one Wallet
            entity.HasOne(w => w.Customer)
                .WithOne()
                .HasForeignKey<Wallet>(w => w.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Don't cascade-delete wallet if customer soft-deleted
        });

        // --- Transaction table configuration ---
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");

            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id");

            entity.Property(t => t.WalletId).HasColumnName("wallet_id");

            // Store enum as string for DB readability ("TopUp", "Debit", etc.)
            entity.Property(t => t.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(12,2)")
                .IsRequired();

            entity.Property(t => t.BalanceBefore)
                .HasColumnName("balance_before")
                .HasColumnType("numeric(12,2)")
                .IsRequired();

            entity.Property(t => t.BalanceAfter)
                .HasColumnName("balance_after")
                .HasColumnType("numeric(12,2)")
                .IsRequired();

            // UNIQUE constraint on ReferenceId — core of idempotency guarantee
            entity.Property(t => t.ReferenceId)
                .HasColumnName("reference_id")
                .HasMaxLength(100);
            entity.HasIndex(t => t.ReferenceId)
                .IsUnique()
                .HasFilter("reference_id IS NOT NULL"); // Partial index — only index non-null values

            entity.Property(t => t.Description).HasColumnName("description");
            entity.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");

            entity.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            // EF Core relationship: Transaction belongs to Wallet
            entity.HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for fast retrieval of all transactions for a wallet
            entity.HasIndex(t => t.WalletId).HasDatabaseName("idx_transactions_wallet_id");
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
