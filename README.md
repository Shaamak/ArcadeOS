# 🕹️ ArcadeOS.net

> **High-Throughput Arcade Management, Real-Time Telemetry & Payment Gateway Engine** built on **.NET 8**, **Entity Framework Core**, and **PostgreSQL**.

---

## 📖 Overview

**ArcadeOS.net** is an enterprise-grade backend management system designed for physical amusement centers, arcade venues, and IoT-connected gaming hardware. It handles real-time NFC/RFID card taps, cashless wallet transactions, machine telemetry monitoring, recurring customer memberships, prize redemptions, and real-time operator analytics dashboards.

Built using **Clean Architecture** principles, the platform guarantees zero financial race conditions through **optimistic concurrency control** and **idempotency checks**, ensuring zero double-spends even during peak venue traffic.

---

## ✨ Key Features & Technical Highlights

### ⚡ Cashless Wallet & Ledger Engine
* **Decimal Financial Precision**: Exact base-10 balance tracking preventing floating-point calculation drift.
* **Append-Only Financial Ledger**: Every credit load, game play, and refund is recorded in an immutable `transactions` table with audit metadata.
* **Optimistic Concurrency Control**: Leverages PostgreSQL `xmin` row versioning to detect simultaneous wallet taps and prevent double-spending without expensive table locking.
* **Idempotency Guarantee**: Unique reference IDs (`ReferenceId`) prevent duplicate billing from network retries or repeated RFID scans.

### 🕹️ Machine Heartbeats & IoT Telemetry
* **IoT Device Management**: Arcade machines authenticate via dedicated JWT service credentials and emit periodic heartbeats.
* **Background Health Monitor**: `MachineStatusMonitorService` (`IHostedService`) periodically scans for missed heartbeats and marks unresponsive cabinets as `Offline` or `Maintenance`.
* **Real-Time Telemetry**: `ArcadeHub` (SignalR WebSocket engine) streams live machine status changes, gameplay taps, and high-value prize redemptions to operator dashboards.

### 💳 Memberships & Reward Center
* **Tiered Subscription Plans**: Define custom plans with monthly fees, daily bonus ticket grants, and gameplay credit discounts.
* **Automated Expiration Worker**: `MembershipExpirationService` automatically transitions expired subscriptions and grants daily bonus tickets.
* **Prize Redemption Engine**: Reward catalog with stock management and concurrency-safe ticket deductions.

### 📊 Executive Analytics & OpenAPI Swagger
* **Aggregated Dashboard Metrics**: Real-time revenue summaries, credits spent, active cards, ticket payout ratios, and hourly heatmap breakdowns via `AnalyticsService`.
* **OpenAPI / Swagger UI**: Embedded interactive documentation at `/swagger` pre-configured with JWT Bearer authentication.

---

## 🏗️ Architecture & Project Structure

```text
arcadeOS_net/
├── src/
│   └── ArcadeOS.Api/
│       ├── Application/
│       │   ├── DTOs/            # Request/Response contracts
│       │   ├── Interfaces/      # Service abstractions
│       │   ├── Services/        # Core business logic handlers
│       │   └── Validators/      # FluentValidation rules
│       ├── Controllers/         # REST API Endpoints
│       ├── Domain/
│       │   ├── Entities/        # EF Core domain entities
│       │   ├── Enums/           # System enumerations
│       │   └── Exceptions/      # Custom domain exceptions
│       ├── Infrastructure/
│       │   ├── Auth/            # JWT Token creation & validation
│       │   ├── BackgroundJobs/  # Hosted background workers (IHostedService)
│       │   ├── Hubs/            # SignalR WebSocket hubs
│       │   └── Persistence/     # ArcadeDbContext & EF Core configurations
│       ├── Middleware/          # Centralized Exception Handling Middleware
│       └── Program.cs           # Dependency Injection root & HTTP pipeline
└── tests/
    └── ArcadeOS.UnitTests/      # xUnit test suite (54 test cases)
```

---

## 🛠️ Technology Stack

* **Framework**: .NET 8 Web API
* **Database**: PostgreSQL 18 via Npgsql
* **ORM**: Entity Framework Core 8.0
* **Authentication**: JWT Bearer Tokens with Role-Based Authorization (`Admin`, `Staff`, `Operator`)
* **Real-Time WebSockets**: ASP.NET Core SignalR
* **Validation**: FluentValidation
* **Password Hashing**: BCrypt.Net
* **Testing**: xUnit with InMemory EF Core provider

---

## 🚀 Getting Started

### Prerequisites
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [PostgreSQL 18](https://www.postgresql.org/download/) running on `localhost:5432`

### Database Setup
1. Create a PostgreSQL database named `arcadeos`.
2. Configure your connection string in `src/ArcadeOS.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=arcadeos;Username=arcadeos_user;Password=arcadeos_pass"
}
```

3. Apply Entity Framework Core database migrations:

```bash
dotnet ef database update --project src/ArcadeOS.Api
```

### Running the API

```bash
dotnet run --project src/ArcadeOS.Api
```

Once running, navigate to:
* **Interactive OpenAPI Swagger Docs**: `https://localhost:7045/swagger`
* **SignalR Telemetry Endpoint**: `https://localhost:7045/hubs/arcade`

---

## 🧪 Running Unit Tests

Execute the full xUnit test suite (54 test cases covering Wallet concurrency, Gameplay, Memberships, Rewards, and Analytics):

```bash
dotnet test
```

---

## 🔌 Core API Endpoints Overview

| Category | Endpoint | Method | Role | Description |
|---|---|---|---|---|
| **Auth** | `/api/auth/login` | `POST` | Public | Authenticates user and returns JWT Bearer token |
| **Auth** | `/api/auth/register` | `POST` | Admin | Registers staff or operator accounts |
| **Customers**| `/api/customers` | `GET / POST` | Staff/Admin | Customer profile management |
| **Wallet** | `/api/wallet/topup` | `POST` | Staff/Admin | Loads credits onto customer card ledger |
| **Wallet** | `/api/wallet/balance/{id}`| `GET` | Staff/Admin | Retrieves live credit & ticket balances |
| **Machines** | `/api/machines` | `GET / POST` | Admin | Registers and manages arcade hardware |
| **Machines** | `/api/machines/heartbeat` | `POST` | Machine | IoT telemetry heartbeat submission |
| **Gameplay** | `/api/gameplay/play` | `POST` | Machine/Staff| Taps card to play game, debits wallet & awards tickets |
| **Memberships**| `/api/memberships/plans` | `GET / POST` | Admin | Configures subscription tiers |
| **Memberships**| `/api/memberships/subscribe`| `POST` | Staff | Enrolls customer into a membership plan |
| **Rewards** | `/api/rewards/redeem` | `POST` | Staff | Deducts tickets and redeems prize from catalog |
| **Kiosk** | `/api/kiosk/tap` | `POST` | Staff/Public| Fast NFC kiosk scan endpoint |
| **Analytics** | `/api/analytics/summary` | `GET` | Admin/Operator| Retrieves executive dashboard metrics |

---

## 📝 License

Distributed under the MIT License. See `LICENSE` for details.
