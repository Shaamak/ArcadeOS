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

    // Machines & Heartbeats (IoT pattern)
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineHeartbeat> MachineHeartbeats => Set<MachineHeartbeat>();

    // Memberships & Rewards
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<RewardItem> RewardItems => Set<RewardItem>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();

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

        // --- Machine table configuration ---
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.ToTable("machines");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(m => m.Model).HasColumnName("model").HasMaxLength(100);
            entity.Property(m => m.SerialNumber).HasColumnName("serial_number").HasMaxLength(100);
            entity.HasIndex(m => m.SerialNumber).IsUnique(); // Cannot have two machines with same serial
            
            entity.Property(m => m.CreditCost).HasColumnName("credit_cost").HasColumnType("numeric(6,2)").HasDefaultValue(1.0m);
            entity.Property(m => m.TicketPayout).HasColumnName("ticket_payout").HasDefaultValue(0);
            
            entity.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(m => m.QrCode).HasColumnName("qr_code").HasMaxLength(200);
            entity.HasIndex(m => m.QrCode).IsUnique(); // QR code should be uniquely identifiable
            
            entity.Property(m => m.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        });

        // --- MachineHeartbeat table configuration ---
        modelBuilder.Entity<MachineHeartbeat>(entity =>
        {
            entity.ToTable("machine_heartbeats");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasColumnName("id");
            
            entity.Property(h => h.MachineId).HasColumnName("machine_id");
            entity.Property(h => h.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(h => h.CreditBalance).HasColumnName("credit_balance").HasColumnType("numeric(6,2)");
            entity.Property(h => h.PlayCount).HasColumnName("play_count");
            entity.Property(h => h.ErrorCode).HasColumnName("error_code").HasMaxLength(50);
            
            // PostgreSQL specific: JSONB column type for efficient JSON storage/querying
            entity.Property(h => h.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
            
            entity.Property(h => h.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasOne(h => h.Machine)
                .WithMany(m => m.Heartbeats)
                .HasForeignKey(h => h.MachineId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a machine deletes its heartbeats (optional, depends on retention policy)

            // Very important for time-series querying: composite index on MachineId + ReceivedAt DESC
            entity.HasIndex(h => new { h.MachineId, h.ReceivedAt }).HasDatabaseName("idx_heartbeats_machine_received");
        });

        // --- MembershipPlan table configuration ---
        modelBuilder.Entity<MembershipPlan>(entity =>
        {
            entity.ToTable("membership_plans");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(p => p.MonthlyFee).HasColumnName("monthly_fee").HasColumnType("numeric(10,2)").IsRequired();
            entity.Property(p => p.DailyBonusTickets).HasColumnName("daily_bonus_tickets").HasDefaultValue(0);
            entity.Property(p => p.GameplayDiscountPercent).HasColumnName("gameplay_discount_percent").HasColumnType("numeric(5,2)").HasDefaultValue(0m);
            entity.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        });

        // --- Membership table configuration ---
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(m => m.PlanId).HasColumnName("plan_id").IsRequired();
            entity.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(m => m.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(m => m.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(m => m.AutoRenew).HasColumnName("auto_renew").HasDefaultValue(true);
            entity.Property(m => m.LastBonusClaimedAt).HasColumnName("last_bonus_claimed_at");
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(m => m.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            entity.HasOne(m => m.Customer)
                .WithMany()
                .HasForeignKey(m => m.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Plan)
                .WithMany()
                .HasForeignKey(m => m.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => m.CustomerId);
        });

        // --- RewardItem table configuration ---
        modelBuilder.Entity<RewardItem>(entity =>
        {
            entity.ToTable("reward_items");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(r => r.TicketCost).HasColumnName("ticket_cost").IsRequired();
            entity.Property(r => r.StockQuantity).HasColumnName("stock_quantity").IsRequired();
            entity.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            // Optimistic concurrency token via PostgreSQL xmin system column
            entity.Property(r => r.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion()
                .ValueGeneratedOnAddOrUpdate();

            entity.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        });

        // --- Redemption table configuration ---
        modelBuilder.Entity<Redemption>(entity =>
        {
            entity.ToTable("redemptions");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(r => r.RewardItemId).HasColumnName("reward_item_id").IsRequired();
            entity.Property(r => r.TicketsSpent).HasColumnName("tickets_spent").IsRequired();
            entity.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            
            entity.Property(r => r.ReferenceId).HasColumnName("reference_id").HasMaxLength(100);
            entity.HasIndex(r => r.ReferenceId).IsUnique().HasFilter("reference_id IS NOT NULL");

            entity.Property(r => r.RedeemedAtUtc).HasColumnName("redeemed_at_utc").HasDefaultValueSql("NOW()");
            entity.Property(r => r.ClaimedAtUtc).HasColumnName("claimed_at_utc");

            entity.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.RewardItem)
                .WithMany()
                .HasForeignKey(r => r.RewardItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.CustomerId);
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
