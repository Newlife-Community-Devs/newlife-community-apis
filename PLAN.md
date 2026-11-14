# Newlife Community APIs — Backend Build Plan

> **Audience:** an AI coding agent (or engineer) implementing this backend from scratch.
> **Status:** approved plan. Build milestones in order (Section 17). Do not make architectural
> decisions outside this document without recording the deviation in `DECISIONS.md`.
> **Verified on:** 2026-11-14. Every version, URL, and contract below was verified against
> live sources on that date (see Section 20 for provenance).

---

## 1. Mission

Build the REST API backend for the Newlife Community Baptist Church website
(`https://www.nlcbcgh.com`). It replaces the legacy external prayer/auth API and adds:

1. **Prayer Wall** — public submissions + wall listing, admin moderation, dashboard stats.
2. **CMS** — media library (Cloudflare R2), events, announcements, site settings.
3. **Sermons** — episode catalogue ingested from the church's RSS.com podcast feed.
4. **Ticketing** — paid event tickets with Hubtel payments (GHS), QR tickets, door check-in.
5. **Identity** — JWT auth for the website's admin console (NextAuth credentials provider).

The existing Next.js frontend (repo: `newlife-website`) will be repointed at this API module
by module. Section 16 defines the exact frontend integration contract.

### Legacy contract being replaced (for reference only)

| Legacy | Shape |
|---|---|
| `POST {BACKEND_URL}/auth/token` | OAuth2 password form → `{ access_token, token_type }` |
| `GET .../prayers/wall?skip=&limit=&sort_by=created_at&sort_order=desc` | `{ items: [{ id, title, content, created_at, is_answered, is_flagged, owner_id, submitter_name, phone_number, is_anonymous }], total, has_more }` |
| `POST .../prayers/submit` | `{ name, phoneNumber, prayerRequest, keepAnonymous }` |

The new API uses a clean v1 contract (camelCase, page/pageSize, ProblemDetails). Section 16.2
maps every legacy consumer to its replacement.

---

## 2. Locked decisions

| Decision | Value | Notes |
|---|---|---|
| Framework | ASP.NET Core Web API, **.NET 10 LTS** (`net10.0`) | SDK 10.0.400 installed locally; EOL 2028-11-14 |
| API style | **Controllers** (attribute routing) | Rationale: large CRUD surface, uniform validation/authorization filters, easiest for verbatim generation. Do not mix minimal APIs. |
| Architecture | Clean Architecture, 4 projects + 2 test projects | Dependency rule enforced by project references (Section 4) |
| ORM | EF Core 10 + Npgsql | Code-first migrations |
| Database | PostgreSQL (Neon free tier in prod; Docker Postgres 17 locally) | `timestamptz`, UTC everywhere |
| Auth | ASP.NET Core Identity + **JWT** (HS256), refresh-token rotation | Roles: `Admin`, `ContentEditor`, `EventManager` |
| Payments | **Hubtel Online Checkout** (hosted page), callback + status re-query | GHS decimal amounts; no client-side credentials |
| Media storage | Cloudflare R2 (S3-compatible, presigned PUT) with local-disk provider for dev | No persistent disk on Render |
| Sermons | RSS ingestion from `https://media.rss.com/the-newlife-podcast/feed.xml` | No Spotify API dependency |
| JSON | camelCase, enums as strings, UTC ISO-8601 | `JsonStringEnumConverter` |
| Errors | RFC 9457 ProblemDetails + `code` extension + `errors` dict | Section 6.4 |
| Pagination | `page` (1-based), `pageSize` (default 20, max 100) | Envelope Section 6.5 |
| Hosting | **Render free web service** (Docker) + **Neon** Postgres + uptime pinger | Section 15.3 |
| No MediatR, no AutoMapper | Manual mapping extension methods | Avoids 2025+ commercial licensing and reduces indirection |
| Commit policy | Feature-by-feature commits to `main`; build+tests green before each commit | Conventional commits: `feat:`, `fix:`, `chore:`, `docs:`, `test:` |

---

## 3. Tech stack and exact package versions

> **CRITICAL:** .NET 11 (STS) shipped 2026-11-10. NuGet now serves `11.0.0` of Microsoft.*
> packages that target `net11.0` only. **Never** let `dotnet add package` pick 11.x —
> it fails with NU1202 on `net10.0`. All versions below are pinned explicitly.

### 3.1 SDK and tools

- `global.json`: SDK `10.0.400`, `rollForward: latestPatch`.
- Local tool manifest: `dotnet-ef` **10.0.15**
  (`dotnet new tool-manifest && dotnet tool install dotnet-ef --version 10.0.15`).
- Optional: upgrade local SDK/runtime to 10.0.404/10.0.15 (current servicing patch).

### 3.2 Central Package Management (`Directory.Packages.props`)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- Data -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.15" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.15" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.15" />
    <PackageVersion Include="Microsoft.Extensions.Identity.Stores" Version="10.0.15" />
    <PackageVersion Include="Microsoft.Extensions.Identity.Core" Version="10.0.15" />
    <!-- Web / auth -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.15" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.15" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerUI" Version="11.0.1" />
    <!-- Validation / logging -->
    <PackageVersion Include="FluentValidation" Version="12.2.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.2.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <!-- Integrations -->
    <PackageVersion Include="AWSSDK.S3" Version="4.1.0" />
    <PackageVersion Include="QRCoder" Version="1.8.1" />
    <PackageVersion Include="System.ServiceModel.Syndication" Version="10.0.15" />
    <PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.0" />
    <!-- Tests -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.15" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.16.0" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

**Do NOT add:** `Microsoft.Extensions.Http` (in shared framework),
`Microsoft.AspNetCore.RateLimiting` (built-in), `MediatR`, `AutoMapper`,
`FluentAssertions` (v8 is paid; v7 is EOL — use Shouldly), `xunit.v3`
(requires `global.json` test-runner opt-in; v2 is the template default and is stable).

### 3.3 Known package gotchas (do not "fix" these)

1. **QRCoder 1.8.1** has no `net10.0` asset; it falls back to `net6.0` and pulls
   `System.Drawing.Common` (Windows-only). Use **`PngByteQRCode`** only
   (`new PngByteQRCode(qrData).GetGraphic(10)`). Never `QRCode`/`ArtQRCode` — they throw
   `PlatformNotSupportedException` on Linux/Docker.
2. **OpenAPI**: use built-in `builder.Services.AddOpenApi()` + `app.MapOpenApi()`.
   Add **only** `Swashbuckle.AspNetCore.SwaggerUI` for the dev UI. Do NOT add
   `Swashbuckle.AspNetCore` 11.x (NU1107 version conflict with Microsoft.OpenApi).
   `WithOpenApi()` is deprecated (ASPDEPR002) — use `AddOpenApiOperationTransformer`.
3. **Identity + JwtBearer are NOT in the shared framework** — both packages required.
4. **Npgsql 10**: `date`→`DateOnly`, `time`→`TimeOnly`, `cidr`→`IPNetwork` by default.
   Use `DateTime` (UTC) for timestamps, `DateOnly` for date-only values.

---

## 4. Repository layout

Create exactly this structure. Namespaces: `Newlife.Domain`, `Newlife.Application`,
`Newlife.Infrastructure`, `Newlife.Api`, `Newlife.UnitTests`, `Newlife.IntegrationTests`.

```
newlife-community-apis/
├── .github/workflows/ci.yml
├── .editorconfig
├── .gitignore
├── .env.example
├── Directory.Build.props
├── Directory.Packages.props
├── Dockerfile
├── README.md
├── PLAN.md
├── global.json
├── Newlife.slnx                        # `dotnet new sln` output, add all 6 projects
├── docker-compose.yml                  # api + postgres:17 for local dev
├── src/
│   ├── Newlife.Domain/
│   │   ├── Newlife.Domain.csproj       # refs: Microsoft.Extensions.Identity.Stores (pinned) — keeps EF Core out of Domain
│   │   ├── Common/BaseEntity.cs
│   │   ├── Entities/AdminUser.cs
│   │   ├── Entities/RefreshToken.cs
│   │   ├── Entities/PrayerRequest.cs
│   │   ├── Entities/MediaAsset.cs
│   │   ├── Entities/Event.cs
│   │   ├── Entities/TicketType.cs
│   │   ├── Entities/Announcement.cs
│   │   ├── Entities/SermonEpisode.cs
│   │   ├── Entities/SiteSetting.cs
│   │   ├── Entities/Order.cs
│   │   ├── Entities/OrderItem.cs
│   │   ├── Entities/OrderPaymentAttempt.cs
│   │   ├── Entities/Ticket.cs
│   │   ├── Entities/HubtelCallbackLog.cs
│   │   └── Enums/OrderStatus.cs, TicketStatus.cs, CheckInResult.cs
│   ├── Newlife.Application/
│   │   ├── Newlife.Application.csproj  # refs: Domain, EF Core, Identity.Core, FluentValidation
│   │   ├── Common/Interfaces/IAppDbContext.cs
│   │   ├── Common/Interfaces/IStorageProvider.cs
│   │   ├── Common/Interfaces/IPaymentGateway.cs
│   │   ├── Common/Interfaces/IPodcastFeedClient.cs
│   │   ├── Common/Interfaces/IEmailSender.cs
│   │   ├── Common/Interfaces/ICurrentUser.cs
│   │   ├── Common/Interfaces/IDateTimeProvider.cs
│   │   ├── Common/Interfaces/IJwtTokenService.cs
│   │   ├── Common/Models/PagedResult.cs, PaginationQuery.cs, PresignedUpload.cs
│   │   ├── Common/Exceptions/NotFoundException.cs, ConflictException.cs,
│   │   │   ForbiddenException.cs, UnauthorizedException.cs
│   │   ├── Auth/ (AuthService.cs, DTOs/, Validators/)
│   │   ├── Prayers/ (PrayerService.cs, DTOs/, Validators/)
│   │   ├── Media/ (MediaService.cs, DTOs/, Validators/)
│   │   ├── Events/ (EventService.cs, DTOs/, Validators/)
│   │   ├── Announcements/ (AnnouncementService.cs, DTOs/, Validators/)
│   │   ├── Sermons/ (SermonService.cs, DTOs/, Validators/)
│   │   ├── Settings/ (SettingService.cs, DTOs/, Validators/)
│   │   ├── Ticketing/ (OrderService.cs, TicketService.cs, CheckInService.cs,
│   │   │   TicketCodeGenerator.cs, PaymentFulfillmentService.cs, DTOs/, Validators/)
│   │   └── DependencyInjection.cs        # AddApplication()
│   ├── Newlife.Infrastructure/
│   │   ├── Newlife.Infrastructure.csproj
│   │   ├── Persistence/AppDbContext.cs
│   │   ├── Persistence/Configurations/  # one IEntityTypeConfiguration per entity
│   │   ├── Persistence/Migrations/
│   │   ├── Persistence/Seed/DbSeeder.cs
│   │   ├── Identity/JwtTokenService.cs
│   │   ├── Payments/HubtelClient.cs, HubtelOptions.cs, HubtelDtos.cs
│   │   ├── Storage/R2StorageProvider.cs, LocalStorageProvider.cs, StorageOptions.cs
│   │   ├── Podcasts/PodcastFeedClient.cs, RssSyncHostedService.cs, RssOptions.cs
│   │   ├── Email/SmtpEmailSender.cs, NullEmailSender.cs
│   │   ├── BackgroundJobs/PaymentReconciliationHostedService.cs,
│   │   │   PaymentFulfillmentHostedService.cs, OrderExpiryHostedService.cs
│   │   └── DependencyInjection.cs        # AddInfrastructure(IConfiguration)
│   └── Newlife.Api/
│       ├── Newlife.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json, appsettings.Development.json
│       ├── Middleware/ExceptionHandlingMiddleware.cs
│       ├── Filters/ValidationFilter.cs
│       ├── Extensions/ClaimsPrincipalExtensions.cs
│       ├── OpenApi/BearerSecuritySchemeTransformer.cs
│       └── Controllers/
│           ├── AuthController.cs
│           ├── PrayersController.cs, AdminPrayersController.cs
│           ├── MediaController.cs, AdminMediaController.cs
│           ├── EventsController.cs, AdminEventsController.cs
│           ├── AnnouncementsController.cs, AdminAnnouncementsController.cs
│           ├── SermonsController.cs, AdminSermonsController.cs
│           ├── SettingsController.cs, AdminSettingsController.cs
│           ├── OrdersController.cs, TicketsController.cs
│           ├── AdminOrdersController.cs, AdminCheckInController.cs
│           ├── AdminUsersController.cs
│           ├── DevUploadsController.cs
│           ├── HubtelCallbackController.cs
│           └── HubtelRefundCallbackController.cs
└── tests/
    ├── Newlife.UnitTests/
    │   ├── Newlife.UnitTests.csproj
    │   ├── Ticketing/TicketCodeGeneratorTests.cs, OrderPricingTests.cs,
    │   │   PaymentFulfillmentGuardTests.cs
    │   ├── Podcasts/RssParsingTests.cs
    │   ├── Payments/HubtelDtosTests.cs
    │   └── Validators/*.cs
    └── Newlife.IntegrationTests/
        ├── Newlife.IntegrationTests.csproj
        ├── Infrastructure/ApiFactory.cs, DatabaseFixture.cs, TestAuth.cs
        ├── AuthTests.cs, PrayersTests.cs, MediaTests.cs, EventsTests.cs,
        │   AnnouncementsTests.cs, SermonsTests.cs, OrdersTests.cs,
        │   HubtelCallbackTests.cs, CheckInTests.cs
        └── (test data builders as needed)
```

**Layer reference rules (do not violate):**

- `Domain` → no project references (may reference Identity stores package for `AdminUser`).
- `Application` → `Domain` + EF Core + Identity Core + FluentValidation. No ASP.NET types.
- `Infrastructure` → `Application` + `Domain` + Npgsql/EF, AWSSDK.S3, Syndication, Serilog.
  **Must add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`** — hosted services
  and `IHttpContextAccessor`/`LinkGenerator` live in `Microsoft.Extensions.Hosting.Abstractions`,
  which the plain `Microsoft.NET.Sdk` class library does not include.
- `Api` → `Application` + `Infrastructure` (+ ASP.NET Core). Controllers contain **no business logic**.
- Tests → `Api` (integration) / `Application`+`Domain`+`Infrastructure` (unit). The unit project
  needs `Infrastructure` because `HubtelClient` DTO mapping and `PodcastFeedClient` parsing are
  tested there. No in-memory EF provider anywhere.

---

## 5. Domain model

All entities derive from `BaseEntity`:

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();   // time-ordered IDs
    public DateTime CreatedAt { get; set; }                 // UTC, set by SaveChanges override
    public DateTime UpdatedAt { get; set; }                 // UTC, set by SaveChanges override
}
```

`AppDbContext.SaveChangesAsync` override: for `Added` entities set `CreatedAt`/`UpdatedAt`;
for `Modified` set `UpdatedAt`. All `DateTime` values are UTC (`DateTimeKind.Utc`).

### 5.1 Entities and columns

Types are PostgreSQL types. `NOT NULL` unless stated. Indexes listed explicitly.

#### `AdminUser` — `AspNetUsers` (Identity)

`IdentityUser<Guid>` subclass; add: `FullName string(120) NOT NULL`. No public registration.

#### `RefreshToken` — `refresh_tokens`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| UserId | uuid FK → AspNetUsers | cascade delete |
| TokenHash | varchar(64) | SHA-256 hex of the opaque token; **unique index** |
| ExpiresAt | timestamptz | |
| CreatedAt / UpdatedAt | timestamptz | |
| RevokedAt | timestamptz NULL | |
| ReplacedByTokenHash | varchar(64) NULL | rotation chain |
| CreatedByIp | varchar(64) NULL | |

Indexes: `(UserId)`, unique `(TokenHash)`.

#### `PrayerRequest` — `prayer_requests`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| Title | varchar(200) NULL | optional short title |
| Content | varchar(4000) NOT NULL | required |
| SubmitterName | varchar(120) NULL | null when anonymous |
| PhoneNumber | varchar(32) NULL | **never returned publicly** |
| IsAnonymous | boolean NOT NULL | default false |
| IsAnswered | boolean NOT NULL | default false |
| AnsweredAt | timestamptz NULL | |
| IsFlagged | boolean NOT NULL | default false; flagged prayers are hidden from the public wall |
| FlagReason | varchar(500) NULL | admin-only |
| DeletedAt | timestamptz NULL | soft delete |
| CreatedAt / UpdatedAt | timestamptz | |

Indexes: `(IsFlagged, DeletedAt, CreatedAt DESC)` for the wall;
`(IsAnswered)`, `(DeletedAt)`. GIN trigram index on `Content` is optional (add only if search
performance requires it; use `pg_trgm` extension).

#### `MediaAsset` — `media_assets`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| StorageKey | varchar(300) NOT NULL | unique index; e.g. `media/2026/11/{guid}.jpg` |
| Url | varchar(1000) NOT NULL | public URL |
| FileName | varchar(255) NOT NULL | original name |
| ContentType | varchar(100) NOT NULL | allowlist only |
| SizeBytes | bigint NULL | client-reported, optional |
| Title | varchar(200) NULL | |
| AltText | varchar(300) NULL | |
| CreatedAt / UpdatedAt | timestamptz | |

#### `Event` — `events`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| Slug | varchar(200) NOT NULL | unique index; lowercase-kebab, generated from title |
| Title | varchar(200) NOT NULL | |
| Description | text NULL | Markdown |
| Venue | varchar(300) NULL | |
| StartAt | timestamptz NOT NULL | |
| EndAt | timestamptz NULL | must be ≥ StartAt when set |
| BannerMediaId | uuid NULL FK → media_assets | `ON DELETE SET NULL` |
| BannerUrl | varchar(1000) NULL | denormalized for public reads |
| IsPublished | boolean NOT NULL | default false |
| IsFeatured | boolean NOT NULL | default false |
| TicketingEnabled | boolean NOT NULL | default false |
| CreatedAt / UpdatedAt | timestamptz | |

Indexes: unique `(Slug)`, `(IsPublished, StartAt)`.

#### `TicketType` — `ticket_types`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| EventId | uuid NOT NULL FK → events | cascade delete |
| Name | varchar(120) NOT NULL | e.g. "Regular", "VIP" |
| Description | varchar(500) NULL | |
| Price | numeric(12,2) NOT NULL | GHS; ≥ 0 |
| Quantity | integer NOT NULL | total inventory; > 0 |
| MaxPerOrder | integer NOT NULL | default 10; 1..50 |
| SalesStartAt | timestamptz NULL | |
| SalesEndAt | timestamptz NULL | |
| IsActive | boolean NOT NULL | default true |
| CreatedAt / UpdatedAt | timestamptz | |

Index: `(EventId, IsActive)`.

#### `Announcement` — `announcements`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| Title | varchar(200) NOT NULL | |
| Body | text NOT NULL | Markdown |
| IsPinned | boolean NOT NULL | default false |
| IsPublished | boolean NOT NULL | default false |
| PublishAt | timestamptz NULL | if set, not public before this instant |
| ExpiresAt | timestamptz NULL | if set, not public after |
| CreatedAt / UpdatedAt | timestamptz | |

Index: `(IsPublished, IsPinned DESC, PublishAt DESC)`.

#### `SermonEpisode` — `sermon_episodes`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| FeedGuid | varchar(300) NOT NULL | unique index; RSS `<guid>` |
| Title | varchar(500) NOT NULL | |
| DescriptionHtml | text NULL | raw feed description (sanitize on frontend) |
| Summary | varchar(1000) NULL | plain-text first ~400 chars |
| AudioUrl | varchar(1000) NOT NULL | enclosure URL |
| DurationSeconds | integer NULL | parsed from `itunes:duration` |
| ArtworkUrl | varchar(1000) NULL | episode or channel image |
| PublishedAt | timestamptz NOT NULL | |
| IsVisible | boolean NOT NULL | default true |
| CreatedAt / UpdatedAt | timestamptz | |

Index: `(IsVisible, PublishedAt DESC)`.

#### `SiteSetting` — `site_settings`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| Key | varchar(100) NOT NULL | unique index; dotted, e.g. `sermons.latestYoutubeUrl` |
| Value | text NOT NULL | |
| Group | varchar(50) NOT NULL | `sermons`, `home`, `general` |
| CreatedAt / UpdatedAt | timestamptz | |

#### `Order` — `orders`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| OrderNumber | varchar(20) NOT NULL | unique; `NL-` + 8 Crockford base32 chars (uppercase) |
| OrderToken | uuid NOT NULL | capability token for public order/ticket reads; unique |
| EventId | uuid NOT NULL FK → events | restrict delete |
| BuyerFullName | varchar(200) NOT NULL | |
| BuyerEmail | varchar(320) NOT NULL | |
| BuyerPhone | varchar(32) NOT NULL | E.164 without `+`, e.g. `233244685106` |
| Status | varchar(30) NOT NULL | `OrderStatus` enum as string |
| TotalAmount | numeric(12,2) NOT NULL | server-computed |
| Currency | varchar(3) NOT NULL | `GHS` |
| HubtelSalesInvoiceId | varchar(64) NULL | from callback `Data.SalesInvoiceId` (audit; not a refund key) |
| HubtelTransactionId | varchar(64) NULL | unique filtered index where not null; populated by status verification |
| HubtelRefundId | varchar(64) NULL | from refund request/callback |
| PaymentMethod | varchar(30) NULL | e.g. `mobilemoney`, `card`, `free` |
| PaidAt | timestamptz NULL | |
| RefundedAt | timestamptz NULL | |
| RequiresReview | boolean NOT NULL | default false; set when payment integrity is doubtful |
| ReviewReason | varchar(300) NULL | admin-only |
| HoldExpiresAt | timestamptz NULL | PendingPayment: CreatedAt + 30 min; AwaitingPayment: CreatedAt + 24 h |
| CreatedAt / UpdatedAt | timestamptz | |

Indexes: unique `(OrderNumber)`, unique `(OrderToken)`, `(EventId, Status)`,
`(Status, CreatedAt)`, unique filtered `(HubtelTransactionId)`.

`OrderStatus` enum: `PendingPayment`, `AwaitingPayment`, `Paid`, `RefundPending`, `Refunded`,
`RefundFailed`, `Cancelled`, `Expired` (reserved; no code path sets it — cancellation always
uses `Cancelled`).

#### `OrderPaymentAttempt` — `order_payment_attempts`

One row per checkout creation attempt (retries create new rows; the order's callback resolution
goes through this table, never through a mutable column).

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| OrderId | uuid NOT NULL FK → orders | cascade delete |
| ClientReference | varchar(32) NOT NULL | **unique index**; `NL{8charOrderSuffix}{2digitAttempt}` ≤ 32 |
| AttemptNumber | integer NOT NULL | 1..3 |
| HubtelCheckoutId | varchar(64) NULL | from create response |
| CheckoutUrl | varchar(1000) NULL | from create response; returned by order reads (latest attempt) |
| CreatedAt / UpdatedAt | timestamptz | |

Indexes: unique `(ClientReference)`, `(OrderId)`.

#### `OrderItem` — `order_items`

`Id`, `OrderId` FK cascade, `TicketTypeId` FK restrict, `Quantity int > 0`,
`UnitPrice numeric(12,2)`, `CreatedAt/UpdatedAt`. Unique `(OrderId, TicketTypeId)`.

#### `Ticket` — `tickets`

| Column | Type | Notes |
|---|---|---|
| Id | uuid PK | |
| Code | varchar(20) NOT NULL | unique; `NL-` + 10 Crockford base32 chars |
| OrderId | uuid NOT NULL FK → orders | restrict delete |
| OrderItemId | uuid NOT NULL FK → order_items | restrict |
| TicketTypeId | uuid NOT NULL FK → ticket_types | restrict |
| EventId | uuid NOT NULL FK → events | restrict |
| HolderName | varchar(200) NULL | defaults to buyer name |
| Status | varchar(20) NOT NULL | `TicketStatus` enum as string |
| CheckedInAt | timestamptz NULL | |
| CheckedInByUserId | uuid NULL FK → AspNetUsers | set null on delete |
| CreatedAt / UpdatedAt | timestamptz | |

`TicketStatus` enum: `Valid`, `CheckedIn`, `Cancelled`.
Indexes: unique `(Code)`, `(OrderId)`, `(EventId, Status)`.

#### `HubtelCallbackLog` — `hubtel_callback_logs`

`Id`, `ReceivedAt timestamptz`, `SourceIp varchar(64)`, `ClientReference varchar(64) NULL`,
`Payload jsonb NOT NULL` (raw body as string), `ProcessingResult varchar(100) NULL`,
`ProcessedAt timestamptz NULL`, `CreatedAt/UpdatedAt`. Index `(ClientReference)`.

### 5.2 Availability rule (ticket inventory)

```
available(ticketType) = Quantity
  - SUM(order_items.Quantity) over orders of the same event in status
    {PendingPayment (HoldExpiresAt > now), AwaitingPayment, Paid}
```

- `Cancelled`, `Expired`, `Refunded`, `RefundFailed` orders release inventory.
- Order creation runs in a **Serializable** transaction: re-check availability, insert order,
  then commit. On serialization failure retry up to 3 times, then return
  `409 order.capacity_exceeded`.
- **Single owner of cancellation: `OrderExpiryHostedService`** (every 5 min):
  - `PendingPayment` past `HoldExpiresAt` (30 min) → `Cancelled` (checkout never created).
  - `AwaitingPayment` past `HoldExpiresAt` (24 h): run one status check first — `Paid` →
    fulfil; otherwise → `Cancelled` and release inventory. This is a **hold release**, not a
    payment verdict: docs forbid marking a payment failed on a timer. A callback that arrives
    later for a cancelled order is logged (`late_payment_after_cancel`) and sets
    `RequiresReview=true`; the admin fulfils or refunds manually.
  - The reconciliation job (§9.6) never changes order status except via confirmed `Paid`
    or `Refunded` outcomes.

---

## 6. Cross-cutting conventions

### 6.1 JSON

- `System.Text.Json`, camelCase (ASP.NET default).
- Enums as strings: `options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())`.
- `DateTime` always UTC, serialized with `Z` suffix.

### 6.2 Routing

- All routes under `/api/v1`. Controllers use `[Route("api/v1/...")]`.
- Public vs admin split: `/api/v1/admin/...` requires `Admin` or module role (Section 8.2).
- Resource IDs are GUIDs in URLs; exceptions: public events are keyed by `slug`, public orders
  by `orderNumber`.

### 6.3 Validation

- FluentValidation validators per request DTO, auto-run via `ValidationFilter`
  (`IAsyncActionFilter`, registered globally; resolves `IValidator<T>` from DI; on failure
  returns `400` ProblemDetails with `errors` dictionary keyed by camelCase property name).
- Data annotations only for route/query binding (`[Required]`, `[Range]`).

### 6.4 Error contract

```json
{
  "type": "https://httpstatuses.io/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Prayer request was not found.",
  "code": "prayer.not_found",
  "traceId": "0HN7...:00000001",
  "errors": { "content": ["Content is required."] }
}
```

- `ExceptionHandlingMiddleware` maps:
  `NotFoundException`→404, `ConflictException`→409, `ForbiddenException`→403,
  `UnauthorizedException`→401, `ValidationException`→400, unhandled→500 (log with traceId,
  never leak stack traces).
- Controllers return `Problem(...)` for 400/401/403/404/409 in non-exception paths.
- Error `code` strings are stable: `prayer.not_found`, `order.capacity_exceeded`,
  `auth.invalid_credentials`, `media.in_use`, etc. (documented inline in controllers).

### 6.5 Pagination envelope

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0, "hasMore": false }
```

`PaginationQuery`: `page` ≥ 1 (default 1), `pageSize` 1–100 (default 20).
`PagedResult<T>.Create(items, page, pageSize, totalCount)` computes `totalPages`/`hasMore`.

### 6.6 Rate limiting (built-in, no package)

Named policies via `AddRateLimiter`; `RejectionStatusCode = 429`; fixed window per IP.
Behind Render's proxy, configure `ForwardedHeaders` with `ForwardLimit = 1` and
`ForwardedHeaders = XForwardedFor | XForwardedProto`. On Render the container is only
reachable through the edge proxy, so clear `KnownProxies`/`KnownNetworks` (Render's proxy IPs
are not static); the single-hop limit means only the last `X-Forwarded-For` entry is consumed.
After the middleware, read the client IP from `HttpContext.Connection.RemoteIpAddress` — do
**not** parse raw XFF headers in application code (the leftmost entry is attacker-controlled).
The same resolved IP is used by the rate limiter and the Hubtel callback IP check. In local
development the middleware is disabled (direct connections).

| Policy | Limit | Applied to |
|---|---|---|
| `auth` | 5 / minute | `POST /auth/login`, `/auth/refresh` |
| `prayer-submit` | 5 / 10 minutes | `POST /prayers` |
| `orders` | 10 / minute | `POST /orders` |
| `global` | 120 / minute | all other endpoints |

### 6.7 CORS

`Cors:AllowedOrigins` (string array from config). Dev defaults:
`http://localhost:4080`, `http://localhost:3000`. Prod:
`https://nlcbcgh.com`, `https://www.nlcbcgh.com`.
`Cors:AllowVercelPreviews` (bool, default false): when true, also allow any
`https://*.vercel.app` origin (preview deployments). Methods: all; headers: all;
`AllowCredentials` false (JWT in `Authorization` header, no cookies).

### 6.8 Logging

- Serilog: console sink; compact JSON formatter in Production, readable template in Development.
- Request logging: method, path, status, duration, `traceId`. **Never log** request bodies for
  prayers, auth, orders, or callbacks (PII). Log order numbers and client references only.
- **Redact secrets in URLs**: a Serilog enricher replaces any `token=` query value with
  `token=***` before logging (`Hubtel:CallbackToken`, `orderToken` are capability secrets).
- `UseSerilogRequestLogging()`.

### 6.9 OpenAPI

- `AddOpenApi()` with document name `v1`; `app.MapOpenApi()` at `/openapi/v1.json`.
- Bearer security scheme transformer (`BearerSecuritySchemeTransformer.cs`).
- Swagger UI (dev only) at `/swagger` via `Swashbuckle.AspNetCore.SwaggerUI` pointing at
  `/openapi/v1.json`.
- `Microsoft.OpenApi` 2.x moved `OpenApiOperation` out of `Microsoft.OpenApi.Models` — write the
  security transformer against the 2.x namespaces (`Microsoft.OpenApi`), not .NET 8-era snippets.

### 6.10 Health

- `GET /health` — liveness, always `200 {"status":"healthy"}` (no DB access).
- `GET /health/ready` — readiness, checks Npgsql connectivity via
  `AddHealthChecks().AddNpgSql(connectionString)` (package `AspNetCore.HealthChecks.NpgSql`
  9.0.0, referenced by `Newlife.Api`); `503` when DB unreachable.

### 6.11 Database conventions

- Snake_case table/column names (configured via `UseSnakeCaseNamingConvention()`? **No extra
  package** — instead name tables/columns explicitly in configurations as shown in Section 5).
- FK delete behaviors exactly as listed in Section 5.
- All migrations committed to `src/Newlife.Infrastructure/Persistence/Migrations/`.
- `Database:RunMigrationsOnStartup` (bool): when true, `await db.Database.MigrateAsync()` at
  startup inside a startup lock (EF 10 migration locking); log and exit non-zero on failure.

---

## 7. Authentication and authorization

### 7.1 Identity configuration

- `AdminUser : IdentityUser<Guid>` with `FullName`.
- Password: min 10 chars, requires digit + lower + upper; `RequireNonAlphanumeric = false`.
- Lockout: 5 failed attempts → 15 minutes.
- Roles seeded: `Admin`, `ContentEditor`, `EventManager`.
- No self-registration endpoint. Users created by `Admin` (Section 8.10) or by seeder.

### 7.2 JWT

- HS256, signing key `JWT:SigningKey` (**≥ 64 chars** random; startup validation enforces 64).
- Claims: `sub` (userId), `email`, `name` (FullName), `role` (one per role), `jti`.
- Issuer `JWT:Issuer`, audience `JWT:Audience`; `ClockSkew = 30s`.
- Access token lifetime: `JWT:AccessTokenMinutes` (default **60**).
- Refresh token: opaque 64-byte random (base64url), stored as SHA-256 hash; lifetime
  `JWT:RefreshTokenDays` (default **30**); **rotating**: each refresh revokes the old token and
  issues a new pair. Reuse of a revoked token → revoke the whole user's token chain + `401`.
- **60-second grace window** for rotation races: a token revoked less than 60 s ago that was
  replaced by a still-active successor is accepted once more and a fresh pair is issued
  (do not revoke the chain). This protects concurrent refresh calls (e.g. NextAuth parallel
  JWT callbacks). Outside the window, reuse detection applies.
- Password reset and role changes revoke all of the user's refresh tokens (Section 8.10).

### 7.3 Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/auth/login` | anonymous, `auth` rate limit | `{ email, password }` → `{ accessToken, refreshToken, accessTokenExpiresAt, user }` |
| POST | `/api/v1/auth/refresh` | anonymous, `auth` rate limit | `{ refreshToken }` → same pair shape |
| POST | `/api/v1/auth/logout` | `[AllowAnonymous]` (revokes the presented refresh token; bearer validated when present) | `{ refreshToken }` → `204`, revokes it |
| GET | `/api/v1/auth/me` | bearer | `{ id, email, fullName, roles }` |

Login failure: `401` `auth.invalid_credentials` (same message for unknown user / bad password;
do not reveal which). Locked out: `423` `auth.locked_out`.

`user` object shape:

```json
{ "id": "0193...", "email": "admin@nlcbcgh.com", "fullName": "Gerald Ofori", "roles": ["Admin"] }
```

### 7.4 Role matrix

| Area | Admin | ContentEditor | EventManager |
|---|---|---|---|
| Prayer moderation | ✓ | ✓ | — |
| Media library | ✓ | ✓ | — |
| Events & announcements & sermons & settings | ✓ | ✓ | events read only |
| Ticket types, orders, check-in, refunds | ✓ | — | ✓ |
| User management | ✓ | — | — |

Implementation: `[Authorize(Roles = "Admin,ContentEditor")]` etc. — exact attributes in
Section 8 endpoint tables.

---

## 8. API catalogue

All request/response bodies are camelCase JSON. Every list endpoint uses the pagination
envelope (6.5). Validation rules are enforced server-side (Section 6.3) and listed per endpoint.

### 8.1 Auth

See Section 7.3.

### 8.2 Prayer Wall

#### Public

**`POST /api/v1/prayers`** — anonymous, policy `prayer-submit`.

```json
{ "title": "Healing", "content": "Please pray for my mother...", "submitterName": "Ama",
  "phoneNumber": "233244000000", "isAnonymous": false }
```

Rules: `content` 5–4000 chars required; `title` ≤ 200; `submitterName` ≤ 120 required when
`isAnonymous=false`; `phoneNumber` optional, `^233[0-9]{9}$` when present; `isAnonymous=true`
→ `submitterName` stored as null, response hides it.

Response `201`:

```json
{ "id": "0193...", "title": "Healing", "content": "Please pray for my mother...",
  "submitterName": "Ama", "isAnonymous": false, "isAnswered": false, "createdAt": "2026-11-14T16:40:00Z" }
```

**`GET /api/v1/prayers/wall?page=1&pageSize=20&sort=createdAt&order=desc`** — anonymous.

`sort` ∈ `createdAt|answeredAt`; `order` ∈ `asc|desc` (default `createdAt desc`).
Only rows with `DeletedAt IS NULL AND IsFlagged = false`. Phone never included.

```json
{ "items": [{ "id": "0193...", "title": "Healing", "content": "...", "isAnonymous": false,
  "submitterName": "Ama", "isAnswered": true, "answeredAt": "2026-11-13T09:00:00Z",
  "createdAt": "2026-11-12T18:00:00Z" }],
  "page": 1, "pageSize": 20, "totalCount": 42, "totalPages": 3, "hasMore": true }
```

#### Admin — `[Authorize(Roles = "Admin,ContentEditor")]`

**`GET /api/v1/admin/prayers?page&pageSize&search&status=all|open|answered|flagged`**
`search` matches content/title/submitter (case-insensitive `ILIKE`). Includes
`phoneNumber`, `isFlagged`, `flagReason`. Excludes soft-deleted. Default `status=all`.

**`GET /api/v1/admin/prayers/stats`**

```json
{ "total": 242, "open": 169, "answered": 73, "flagged": 4, "anonymous": 31,
  "submissionsLast7Days": 12, "uniqueSubmitters": 151 }
```

(`total` excludes soft-deleted; `uniqueSubmitters` = distinct non-null `submitterName`.)

**`POST /api/v1/admin/prayers/{id}/answer`** — body `{ "note": "optional" }` (note is not
persisted in v1; accepted for forward compatibility) → `200` prayer (admin shape).
Sets `IsAnswered=true`, `AnsweredAt=now`. Idempotent.

**`POST /api/v1/admin/prayers/{id}/flag`** — `{ "reason": "optional" }` → sets `IsFlagged=true`.

**`POST /api/v1/admin/prayers/{id}/unflag`** → clears flag.

**`DELETE /api/v1/admin/prayers/{id}`** → soft delete (`DeletedAt=now`), `204`.

Errors: `404 prayer.not_found`.

### 8.3 Media library

#### Admin — `[Authorize(Roles = "Admin,ContentEditor")]`

**`POST /api/v1/admin/media/uploads`** — request presigned upload:

```json
{ "fileName": "youth-camp.jpg", "contentType": "image/jpeg" }
```

Allowed content types: `image/jpeg`, `image/png`, `image/webp` (no SVG). Max size 10 MB
(enforced client-side and by the R2 bucket upload policy; the API never proxies bytes).

Response `200`:

```json
{ "uploadUrl": "https://<account>.r2.cloudflarestorage.com/<bucket>/media/2026/11/<guid>.jpg?X-Amz-...",
  "method": "PUT",
  "headers": { "Content-Type": "image/jpeg" },
  "key": "media/2026/11/<guid>.jpg",
  "publicUrl": "https://media.nlcbcgh.com/media/2026/11/<guid>.jpg",
  "expiresAt": "2026-11-14T17:00:00Z" }
```

Client PUTs the file bytes to `uploadUrl` with `headers`, then registers:

**`POST /api/v1/admin/media`**

```json
{ "key": "media/2026/11/<guid>.jpg", "fileName": "youth-camp.jpg",
  "contentType": "image/jpeg", "sizeBytes": 204800, "title": "Youth Camp",
  "altText": "Youth camping trip" }
```

→ `201` MediaAsset `{ id, url, key, fileName, contentType, sizeBytes, title, altText, createdAt }`.
`key` must start with `media/`; duplicate key → `409 media.duplicate_key`.

**`GET /api/v1/admin/media?page&pageSize&search`** — paged, newest first.

**`DELETE /api/v1/admin/media/{id}`** — deletes R2 object then row. If referenced by an event
banner → `409 media.in_use`.

### 8.4 Events

#### Public

**`GET /api/v1/events?page&pageSize&upcoming=true|false&featured=true|false`**
Only `IsPublished=true`. `upcoming=true` → `EndAt ?? StartAt >= now`, ordered `StartAt asc`;
otherwise ordered `StartAt desc`.

**`GET /api/v1/events/{slug}`** → public event shape; `404 event.not_found`.

**`GET /api/v1/events/{slug}/ticket-types`** → array (not paged):

```json
[ { "id": "0193...", "name": "Regular", "description": null, "price": 50.00,
    "currency": "GHS", "remaining": 87, "maxPerOrder": 10,
    "salesStartAt": null, "salesEndAt": "2026-12-24T23:59:59Z", "isOnSale": true } ]
```

`isOnSale` = `IsActive && now within sales window && remaining > 0`. `remaining` per
Section 5.2. Only for events with `TicketingEnabled=true` (else `404 ticketing.disabled`).

Public event shape:

```json
{ "id": "0193...", "slug": "youth-camp-2026", "title": "Youth Camp 2026",
  "description": "## Markdown...", "venue": "Kpone-Katamanso",
  "startAt": "2026-12-27T09:00:00Z", "endAt": "2026-12-30T17:00:00Z",
  "bannerUrl": "https://media.nlcbcgh.com/media/...", "isFeatured": true,
  "ticketingEnabled": true }
```

#### Admin — events

| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/api/v1/admin/events?page&pageSize&search&published` | Admin, ContentEditor, EventManager | includes unpublished |
| POST | `/api/v1/admin/events` | Admin, ContentEditor | create (body below) |
| PUT | `/api/v1/admin/events/{id}` | Admin, ContentEditor | full update |
| DELETE | `/api/v1/admin/events/{id}` | Admin, ContentEditor | `409 event.has_orders` if orders exist |
| POST | `/api/v1/admin/events/{id}/publish` | Admin, ContentEditor | `{ "isPublished": true }` |
| POST | `/api/v1/admin/events/{eventId}/ticket-types` | Admin, EventManager | create ticket type |
| PUT | `/api/v1/admin/ticket-types/{id}` | Admin, EventManager | update |
| DELETE | `/api/v1/admin/ticket-types/{id}` | Admin, EventManager | `409 ticket_type.has_tickets` if sold |

Create/update body:

```json
{ "title": "Youth Camp 2026", "slug": "youth-camp-2026", "description": "## Markdown...",
  "venue": "Kpone-Katamanso", "startAt": "2026-12-27T09:00:00Z",
  "endAt": "2026-12-30T17:00:00Z", "bannerMediaId": null, "isPublished": false,
  "isFeatured": false, "ticketingEnabled": true }
```

Rules: `title` required ≤ 200; `slug` optional (auto-generated from title; lowercase-kebab,
unique, `409 event.slug_taken`); `startAt` required; `endAt` ≥ `startAt`;
`bannerMediaId` must exist (`404 media.not_found`).

Ticket type body:

```json
{ "name": "Regular", "description": null, "price": 50.00, "quantity": 100,
  "maxPerOrder": 10, "salesStartAt": null, "salesEndAt": "2026-12-24T23:59:59Z",
  "isActive": true }
```

Rules: `price` ≥ 0, 2dp; `quantity` 1–10000; `maxPerOrder` 1–50 and ≤ `quantity`;
`quantity` cannot be reduced below sold+held count (`409 ticket_type.quantity_below_sold`).

> **Milestone note:** `remaining`, `event.has_orders`, `ticket_type.has_tickets` and
> `quantity_below_sold` depend on order tables. In M4 they are inert (no orders exist:
> treat sold/held as 0, `remaining = quantity`); they become effective in M6.

### 8.5 Announcements

#### Public

**`GET /api/v1/announcements?page&pageSize&active=true`** — `IsPublished=true`,
`(PublishAt IS NULL OR PublishAt <= now)`, `(ExpiresAt IS NULL OR ExpiresAt > now)`,
ordered `IsPinned desc, PublishAt desc nulls last, CreatedAt desc`.

**`GET /api/v1/announcements/{id}`** — same visibility rules, else `404 announcement.not_found`.

Shape: `{ id, title, body, isPinned, publishAt, expiresAt, createdAt }`.

#### Admin — `[Authorize(Roles = "Admin,ContentEditor")]`

`GET /api/v1/admin/announcements?page&pageSize&search` · `POST /api/v1/admin/announcements` ·
`PUT /api/v1/admin/announcements/{id}` · `DELETE /api/v1/admin/announcements/{id}`.
Body: `{ "title", "body", "isPinned", "isPublished", "publishAt", "expiresAt" }`.
Rules: title required ≤ 200; body required ≤ 20000; `expiresAt > publishAt` when both set.

### 8.6 Sermons

#### Public

**`GET /api/v1/sermons?page&pageSize`** — `IsVisible=true`, `PublishedAt desc`.

Shape: `{ id, title, summary, audioUrl, durationSeconds, artworkUrl, publishedAt }`
(no `descriptionHtml` in list).

**`GET /api/v1/sermons/{id}`** — includes `descriptionHtml`.

**`GET /api/v1/sermons/latest`**

```json
{ "episode": { "id": "0193...", "title": "Faith That Moves Mountains",
    "summary": "...", "audioUrl": "https://...", "durationSeconds": 3120,
    "artworkUrl": "https://...", "publishedAt": "2026-11-09T06:00:00Z" },
  "youtubeUrl": "https://www.youtube.com/live/..." }
```

`episode` is `null` if none ingested. `youtubeUrl` from site setting
`sermons.latestYoutubeUrl` (may be `""`).

#### Admin — `[Authorize(Roles = "Admin,ContentEditor")]`

| Method | Path | Notes |
|---|---|---|
| GET | `/api/v1/admin/sermons?page&pageSize&search` | includes hidden |
| POST | `/api/v1/admin/sermons/sync` | run ingestion now → `{ "added": 2, "updated": 0, "total": 40 }` |
| PUT | `/api/v1/admin/sermons/{id}` | `{ "isVisible": false }` |

### 8.7 Site settings

**`GET /api/v1/settings/public`** — anonymous; returns only **whitelisted public keys**:

```json
{ "sermons.latestYoutubeUrl": "https://www.youtube.com/live/...",
  "home.heroAnnouncement": "" }
```

Whitelist is a code constant: `["sermons.latestYoutubeUrl", "home.heroAnnouncement"]`.

**Admin — `[Authorize(Roles = "Admin,ContentEditor")]`**

`GET /api/v1/admin/settings` → all settings grouped.
`PUT /api/v1/admin/settings/{key}` — `{ "value": "..." }` → upsert; `400` if key unknown
(keys must exist in the seed set; new keys require a code change).

Seeded keys: `sermons.latestYoutubeUrl` (group `sermons`, from env `SEED_YOUTUBE_URL` or `""`),
`home.heroAnnouncement` (group `home`, `""`).

### 8.8 Orders and payments (public)

**`POST /api/v1/orders`** — anonymous, policy `orders`.

```json
{ "eventId": "0193...", "buyerFullName": "Ama Mensah", "buyerEmail": "ama@example.com",
  "buyerPhone": "233244000000",
  "items": [ { "ticketTypeId": "0193...", "quantity": 2 } ] }
```

Rules: 1–5 distinct ticket types; quantity per item 1..`maxPerOrder`; event published,
ticketing enabled, sales window open; capacity available (Section 5.2).

Response `201`:

```json
{ "orderNumber": "NL-7K3M9Q2A", "orderToken": "6f1c...", "status": "AwaitingPayment",
  "totalAmount": 100.00, "currency": "GHS",
  "checkoutUrl": "https://pay.hubtel.com/7569a11e8b784f21baa9443b3fce31ed",
  "items": [ { "ticketTypeId": "0193...", "name": "Regular", "quantity": 2, "unitPrice": 50.00 } ] }
```

- Total = Σ(unitPrice × quantity) computed **server-side**; client never sends amounts.
- If total is **0** (free tickets): status `Paid`, `paymentMethod="free"`, `checkoutUrl=null`,
  tickets issued immediately; response also includes `tickets`.
- Else: create order `PendingPayment` → call Hubtel create checkout (Section 9.3) → insert an
  `OrderPaymentAttempt` row (`CheckoutId`, `CheckoutUrl`, attempt 1) → set `AwaitingPayment`
  and `HoldExpiresAt = CreatedAt + 24 h`, return `checkoutUrl`.
- Hubtel create failure: order stays `PendingPayment`, the 502 ProblemDetails body includes
  `orderNumber` and `orderToken` so the client can retry:
  `POST /api/v1/orders/{orderNumber}/retry-payment?token={orderToken}` → `{ orderNumber,
  orderToken, status, checkoutUrl }`. Creates the next attempt (`AttemptNumber` 2, 3 — max 3
  attempts, then `409 order.retry_limit_reached`), only while status `PendingPayment`; policy
  `orders`. Invalid token → `404 order.not_found`.

**`GET /api/v1/orders/{orderNumber}?token={orderToken}`** — token must equal `OrderToken`,
else `404 order.not_found` (do not reveal existence). Returns order with items, tickets
(`code`, `ticketTypeName`, `holderName`, `status`, `checkedInAt`), `paidAt`, and
`checkoutUrl` from the latest `OrderPaymentAttempt` while `AwaitingPayment`.

**`GET /api/v1/tickets/{code}/qr.png?token={orderToken}`** — `image/png` QR of the ticket code
string (PngByteQRCode, 10 px/module, dark `#001F3F` on white). Token must belong to the
ticket's order. `404 ticket.not_found` otherwise.

### 8.9 Orders, check-in, refunds (admin)

**`[Authorize(Roles = "Admin,EventManager")]`**

| Method | Path | Notes |
|---|---|---|
| GET | `/api/v1/admin/orders?page&pageSize&search&status&eventId` | search: orderNumber/email/phone/name |
| GET | `/api/v1/admin/orders/{orderNumber}` | full detail incl. callbacks log summary |
| POST | `/api/v1/admin/orders/{orderNumber}/refund` | mobile-money refund via Hubtel (Section 9.7); body `{}`; `409 order.not_refundable` when no transaction id |
| POST | `/api/v1/admin/check-in` | `{ "code": "NL-XXXXXXXXXX" }` |
| GET | `/api/v1/admin/check-in/stats?eventId=` | `{ "totalTickets": 200, "checkedIn": 87, "remaining": 113 }` |
| POST | `/api/v1/admin/orders/{orderNumber}/mark-reviewed` | clears `RequiresReview` after manual resolution |

Check-in response `200` (always 200 for a well-formed code so scanners show a clear screen):

```json
{ "result": "CheckedIn", "ticket": { "code": "NL-XXXXXXXXXX", "holderName": "Ama Mensah",
  "ticketTypeName": "Regular", "status": "CheckedIn" }, "checkedInAt": "2026-12-27T09:12:00Z" }
```

`result` ∈ `CheckedIn | AlreadyCheckedIn | Invalid | Cancelled`. `AlreadyCheckedIn` includes
the original `checkedInAt`. `Invalid` for unknown codes (no enumeration detail).

### 8.10 Admin users

**`[Authorize(Roles = "Admin")]`**

| Method | Path | Notes |
|---|---|---|
| GET | `/api/v1/admin/users?page&pageSize` | `{ id, email, fullName, roles, isLockedOut }` |
| POST | `/api/v1/admin/users` | `{ email, fullName, password, roles: ["ContentEditor"] }` → `201` |
| PUT | `/api/v1/admin/users/{id}/roles` | `{ "roles": ["Admin","EventManager"] }` |
| POST | `/api/v1/admin/users/{id}/reset-password` | `{ "newPassword": "..." }` |

Roles must be from the seeded set (`400 user.invalid_role`). Cannot remove the last `Admin`
(`409 user.last_admin`).

---

## 9. Hubtel payments integration (verified contract)

> Provenance: official docs at `https://code.hubtel.com/docs` (fetched 2026-11-14).
> `docs.hubtel.com` and `sandbox-pay.hubtel.com` **do not exist** — never use them.

### 9.1 Credentials and configuration

- HTTP **Basic** auth: `base64(API_ID:API_KEY)` — from Workbench → API keys.
- `merchantAccountNumber`: collection account number (body/path, not auth).
- Config keys: `Hubtel:ApiId`, `Hubtel:ApiKey`, `Hubtel:MerchantAccountNumber`,
  `Hubtel:CreateCheckoutUrl`, `Hubtel:StatusBaseUrl`, `Hubtel:RefundBaseUrl`,
  `Hubtel:CallbackBaseUrl` (public HTTPS base of this API, e.g.
  `https://newlife-community-apis.onrender.com`), `Hubtel:CallbackToken` (random secret),
  `Hubtel:EnforceCallbackIp` (bool, **default true in code**; `appsettings.Development.json`
  sets false for local testing), `Hubtel:Sandbox` (bool).
- Startup validation: in `Production`, `EnforceCallbackIp=false` **fails startup** (must be an
  explicit, logged decision to disable); missing `Hubtel:CallbackToken` in Production also fails.

### 9.2 Hosts

| Purpose | Production | Sandbox |
|---|---|---|
| Create checkout | `POST https://payproxyapi.hubtel.com/items/initiate` | `POST https://code.hubtel.com/sandbox/v1/payproxyapi/items/initiate` |
| Status check | `GET https://api-txnstatus.hubtel.com/transactions/{accountNumber}/status?clientReference={ref}` | `GET https://code.hubtel.com/sandbox/v1/api-txnstatus/transactions/{accountNumber}/status?clientReference={ref}` |
| Refund | `POST https://refund-api.hubtel.com/refund/{accountNumber}/order/{transactionId}` | `POST https://code.hubtel.com/sandbox/v1/refund-api/refund/{accountNumber}/order/{transactionId}` |

Store base URLs in config (above) with these production values as defaults; the sandbox
toggle rewrites them to the `code.hubtel.com/sandbox/v1/...` prefix.

`IPaymentGateway` (Application layer) — the only surface callers use:

```csharp
Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct);
Task<TransactionStatusResult> GetTransactionStatusAsync(string clientReference, CancellationToken ct);
Task<RefundResult> RefundAsync(string transactionId, string callbackUrl, CancellationToken ct);
```

`HubtelClient` implements it; integration tests replace it with a scripted fake (no network).
Create-checkout failures surface as `PaymentProviderUnavailableException` → `502`.

### 9.3 Create checkout (exact contract)

Request (camelCase — Hubtel expects camelCase here):

```json
{ "totalAmount": 100.00, "description": "Tickets NL-7K3M9Q2A",
  "callbackUrl": "https://api.example.com/api/v1/payments/hubtel/callback?token=SECRET",
  "returnUrl": "https://nlcbcgh.com/events/youth-camp-2026/order/NL-7K3M9Q2A",
  "cancellationUrl": "https://nlcbcgh.com/events/youth-camp-2026/order/NL-7K3M9Q2A",
  "merchantAccountNumber": "12345", "clientReference": "NL7K3M9Q2A01",
  "payeeName": "Ama Mensah", "payeeMobileNumber": "233244000000", "payeeEmail": "ama@example.com" }
```

Rules: `totalAmount` is **GHS decimal, max 2dp — not pesewas**; `description` must not contain
`& * ! % @` (sanitize: `Tickets {orderNumber}`); `clientReference` ≤ **32** alphanumeric chars,
unique forever (format `NL{8charOrderSuffix}{2digitAttempt}`, e.g. `NL7K3M9Q2A01`).

Response `200` (business codes arrive in-body, HTTP is 200):

```json
{ "responseCode": "0000", "status": "Success",
  "data": { "checkoutUrl": "https://pay.hubtel.com/<id>", "checkoutId": "<id>",
    "clientReference": "NL7K3M9Q2A01",
    "checkoutDirectUrl": "https://pay.hubtel.com/<id>/direct" } }
```

`responseCode "0000"` means **checkout created, not paid**. Other codes to map:
`4000` validation (log detail, return 502 to caller), `4070` fees not configured,
`4101` scope/business disabled, `2001` payment failed, `0005` partner unreachable.

HttpClient rules: 30s timeout, **no automatic retry of the create call** (retry with same
reference → `Duplicated client reference`; retry with new reference risks double charge).
Serialization: `System.Text.Json`, camelCase.

### 9.4 Callback webhook

**`POST /api/v1/payments/hubtel/callback?token={Hubtel:CallbackToken}`** — anonymous,
no CSRF, JSON body (**PascalCase**):

```json
{ "ResponseCode": "0000", "Status": "Success",
  "Data": { "CheckoutId": "...", "SalesInvoiceId": "...", "ClientReference": "NL7K3M9Q2A01",
    "Status": "Success", "Amount": 100.00, "CustomerPhoneNumber": "233244000000",
    "PaymentDetails": { "MobileMoneyNumber": "233244000000", "PaymentType": "mobilemoney",
      "Channel": "mtn-gh" },
    "Description": "..." } }
```

Handler protocol (order matters):

1. Validate `token` query param (constant-time compare). Invalid → `404`.
2. If `Hubtel:EnforceCallbackIp=true`: compare `HttpContext.Connection.RemoteIpAddress`
   (resolved by `ForwardedHeaders` middleware, Section 6.6 — **never** parse raw
   `X-Forwarded-For` here) against `108.129.40.25`. Mismatch → log warning, store log row with
   `ProcessingResult="ip_rejected"`, return `200` (never 4xx/5xx to Hubtel; avoids retry
   storms) but **do not fulfil**.
3. Persist raw body to `hubtel_callback_logs` (`SourceIp`, `ClientReference`, `Payload`).
4. Look up the `OrderPaymentAttempt` by `Data.ClientReference` → `OrderId`. Unknown → log
   `unknown_reference`, return `200`. Store `Data.SalesInvoiceId` on the order when present.
5. Return `200` immediately. **No fulfilment inline.**
6. Queue fulfilment: write `orderId` to the bounded `Channel<Guid>` consumed by
   `PaymentFulfillmentHostedService` (single background consumer, no scoped-service misuse in
   the request pipeline). Duplicate callback deliveries enqueue the same order id — the
   idempotency guard in 9.5 makes repeats no-ops.

### 9.5 Verification and fulfilment (exactly-once)

`PaymentFulfillmentService.TryFulfilAsync(orderId, CancellationToken ct)` — called only by
`PaymentFulfillmentHostedService` and the reconciliation job:

1. Load the order **with its payment attempts**:
   - `Cancelled` → log `late_payment_after_cancel`, set `RequiresReview=true`,
     `ReviewReason="late_payment"`, return `Rejected` (no tickets; admin resolves manually).
   - Any status other than `PendingPayment`/`AwaitingPayment` (e.g. `Paid` duplicate) →
     return `AlreadyProcessed`.
2. Query the status endpoint by the **latest attempt's** `ClientReference` (when reachable —
   see 9.8).
3. Require status `data.status == "Paid"` **and** `data.amount == order.TotalAmount`.
4. In one DB transaction:
   - Re-read the order row (`FOR UPDATE`) and re-check status ∈ {`PendingPayment`,
     `AwaitingPayment`} (idempotency guard under concurrency). If `HoldExpiresAt` has passed
     but the expiry job has not run yet, fulfil anyway (the payment is confirmed — never
     reject a real payment because of a hold timer).
   - Set `Paid`, `PaidAt`, `HubtelTransactionId` (from status `data.transactionId`),
     `PaymentMethod` (status `data.paymentMethod`), `RequiresReview=false`.
   - Insert `Ticket` rows (one per seat; unique `Code`), status `Valid`.
   - Commit. Unique filtered index on `HubtelTransactionId` makes duplicate fulfilment fail
     safely → catch and treat as already processed.
5. Post-commit: best-effort confirmation email (Section 15.4).

**Callback-only fallback** — when the status endpoint is unreachable/403 **but** the callback
passed token+IP checks and its `Data.Status == "Success"` with `Data.Amount ==
order.TotalAmount`:

- Set `Paid` with `HubtelTransactionId = NULL` and `RequiresReview=true`,
  `ReviewReason="callback_only_unverified"`; store `HubtelSalesInvoiceId` for audit.
- Issue tickets (buyer must not be blocked), `ProcessingResult="callback_only"`, log warning.
- **Reconciliation re-verifies** these orders every 5 minutes (9.6): on a confirmed status
  response, fill `HubtelTransactionId`/`PaymentMethod` and clear `RequiresReview`; if the
  status endpoint reports a non-Paid state, flag `ReviewReason="callback_status_mismatch"` for
  admin action (never auto-cancel issued tickets).
- Never fulfil twice (same transaction guard).

### 9.6 Reconciliation job

`PaymentReconciliationHostedService` every 60 seconds (status checks only — it never cancels
orders; cancellation is owned by `OrderExpiryHostedService`, Section 5.2):

- Orders `AwaitingPayment` with `UpdatedAt < now - 5 min` (limit 50/run) → status check.
  - `Paid` → fulfil (9.5).
  - `Unpaid` → leave `AwaitingPayment`; increment an in-memory backoff counter; log after 24h.
  - `Refunded` → set `Refunded`, cancel tickets, log alert.
- Orders `Paid` with `RequiresReview=true` and `HubtelTransactionId IS NULL` (callback-only
  fallback, 9.5) → re-verify via status: confirmed `Paid` → fill transaction id/payment method,
  clear `RequiresReview`; non-Paid → `ReviewReason="callback_status_mismatch"` (admin action,
  never auto-cancel).
- Skip entirely (log once at startup) when `Hubtel:ApiId` is empty (dev).

### 9.7 Refunds

State machine: `Paid` → `RefundPending` (request accepted by Hubtel) → `Refunded` (refund
callback confirms) or `RefundFailed` (Hubtel rejects / callback reports failure; tickets stay
`Valid`, admin retries).

1. `POST /refund/{accountNumber}/order/{transactionId}` with body
   `{ "callbackUrl": "<CallbackBaseUrl>/api/v1/payments/hubtel/refund-callback?token=..." }`.
   Requires `HubtelTransactionId` (from status check); when it is null (callback-only order)
   return `409 order.not_refundable` with detail "transaction not yet verified".
2. Mobile-money only (card refunds are not documented — return `422 refund.unsupported_channel`
   for card orders).
3. Request accepted (202 / `0001` / `4515` already in progress): set order `RefundPending`
   (tickets stay `Valid` until confirmed), store `HubtelRefundId`/reference from the response.
4. `POST /api/v1/payments/hubtel/refund-callback?token={Hubtel:CallbackToken}` (anonymous,
   same token + IP checks as 9.4; log raw payload): on success → order `Refunded`, tickets
   `Cancelled`, `RefundedAt` set; on failure → `RefundFailed`, log with Hubtel code.
5. Immediate rejections `4000` amount < GHS 1, `4509` not eligible → `409 refund.rejected` with
   Hubtel code (order stays `Paid`). `RefundFailed` orders can be retried by the admin.

### 9.8 Operational constraints (must be surfaced in README)

- **Status endpoint requires the calling server IP to be allowlisted by Hubtel.** Render free
  tier has **no static outbound IP**. Mitigation in code: callback-first fulfilment (token+IP
  checks) with status verification as best-effort; if Hubtel confirms allowlisting is required
  for this account, either (a) request allowlisting of Render's published egress ranges, or
  (b) move the API to a host with static egress. This is a **known launch risk** — record the
  decision in `DECISIONS.md` at implementation time.
- Callback source IP for Online Checkout: `108.129.40.25` (documented). Production callback
  signatures are **not** part of the published v1 contract — do not implement HMAC verification.
- Sandbox: test keys from Workbench → Test keys; sandbox callbacks only reach public URLs
  (use cloudflared/ngrok locally). Test numbers: `233240000001` success, `...02` insufficient
  funds, `...03` declined, `...04` timeout, `...05` lost callback (reconciliation drill),
  `...06` duplicate callback, `...07` out-of-order. Test cards: `4000000000000002` (approved),
  `4000000000000010` (declined), `5200000000000007` (3-D Secure, code `123456`).
- Amounts are GHS decimals with ≤ 2dp; fees are business-paid 1.95% (MTN/Telecel/cards).

---

## 10. Sermons RSS ingestion

- Feed: `Sermons:FeedUrl`, default
  `https://media.rss.com/the-newlife-podcast/feed.xml` (verified live, 40 episodes,
  RSS 2.0 + iTunes namespace).
- `PodcastFeedClient` (Infrastructure): `HttpClient` (30s timeout, one retry on transient
  network error), parse with `System.ServiceModel.Syndication.SyndicationFeed.Load`.
  - `FeedGuid` ← item `Id` (the `<guid>`); skip items without Id or enclosure.
  - `Title` ← `Title.Text` (trim, max 500).
  - `AudioUrl` ← first `SyndicationLink` with `RelationshipType == enclosure`.
  - `PublishedAt` ← `PublishDate` (fallback `LastUpdatedTime`, else now) as UTC.
  - `ArtworkUrl` ← item image extension (`itunes:image href`) else feed image.
  - `DurationSeconds` ← `itunes:duration` element extension; accept `HH:MM:SS`, `MM:SS`, or
    plain seconds; invalid → null.
  - `DescriptionHtml` ← `Summary.Text`; `Summary` ← HTML-stripped first 400 chars.
- Upsert by `FeedGuid` (insert new, update title/description/duration/artwork/publishedAt on
  existing; never touch `IsVisible`).
- `RssSyncHostedService`: initial sync 30s after startup, then every
  `Sermons:SyncIntervalMinutes` (default 60). Failures log a warning and never crash the host.
- Manual sync: `POST /api/v1/admin/sermons/sync` returns counts.

---

## 11. Media storage

`IStorageProvider`:

```csharp
Task<PresignedUpload> CreatePresignedUploadAsync(string key, string contentType, TimeSpan expiry, CancellationToken ct);
Task DeleteAsync(string key, CancellationToken ct);
string GetPublicUrl(string key);
```

- **R2** (`Storage:Provider=R2`): AWSSDK.S3 with `ServiceURL=https://{AccountId}.r2.cloudflarestorage.com`,
  SigV4 presigned `PUT`, 15-minute expiry, `DisablePayloadSigning=true`,
  `DisableDefaultChecksumValidation=true` (R2 compatibility). Bucket public via custom domain
  `Storage:R2:PublicBaseUrl` (e.g. `https://media.nlcbcgh.com`).
- **Local** (`Storage:Provider=Local`, default in Development): writes under
  `wwwroot/uploads/{key}`, serves via static files, returns absolute URL using
  `ASPNETCORE_URLS`/request base. Presigned upload = `PUT {PublicBaseUrl}/uploads/{key}`
  handled by a dev-only controller endpoint `PUT /api/v1/dev/uploads/{**key}` (Development
  environment only; 404 in Production).
- Keys: `media/{yyyy}/{MM}/{guid:N}{extension}` (extension from allowlisted content type).
  Presign requests must match `^media/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$`; reject any
  other key with `400 media.invalid_key` (blocks `..`, absolute paths, arbitrary extensions).
- Register endpoint re-validates the key regex and that the object exists (HEAD) before
  inserting the row.
- Delete: best-effort R2 delete, then row delete. R2 failures log and still delete the row
  (orphan object acceptable).

---

## 12. Seeding and migrations

`DbSeeder` runs at startup after migrations:

1. **Roles** (always): `Admin`, `ContentEditor`, `EventManager`.
2. **First admin** (only if `AspNetUsers` empty and `Seed:AdminEmail` + `Seed:AdminPassword`
   set): create `AdminUser` with `Admin` role, `FullName = Seed:AdminFullName`.
3. **Site settings** (always, insert-if-missing): `sermons.latestYoutubeUrl` (value from
   `SEED_YOUTUBE_URL` env or `""`), `home.heroAnnouncement` (`""`).
4. **Demo data** (only when `Seed:DemoData=true` and DB has no events): 3 prayers
   (1 answered, 1 anonymous, 1 flagged), 2 events (1 published upcoming with ticketing,
   ticket types "Regular" GHS 50 / "VIP" GHS 100, 1 draft), 2 announcements
   (1 pinned published), 1 sample media asset row is NOT seeded (needs real upload).

Migration commands (document in README):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Newlife.Infrastructure --startup-project src/Newlife.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Newlife.Infrastructure --startup-project src/Newlife.Api
```

`Microsoft.EntityFrameworkCore.Design` must be referenced by **Newlife.Api** (the startup
project); a Design reference only in Infrastructure is not enough and `dotnet ef` will fail
with "startup project doesn't reference Microsoft.EntityFrameworkCore.Design".

Local Postgres via `docker compose up -d db` (port 5433 to avoid clashes):
`Host=localhost;Port=5433;Database=newlife;Username=newlife;Password=newlife_dev`.
Neon: pooled connection string, `SSL Mode=Require`.

---

## 13. Configuration catalogue

`appsettings.json` (safe defaults; secrets only via env on Render):

```json
{
  "ConnectionStrings": { "Default": "Host=localhost;Port=5433;Database=newlife;Username=newlife;Password=newlife_dev" },
  "Database": { "RunMigrationsOnStartup": false },
  "JWT": { "Issuer": "newlife-community-apis", "Audience": "newlife-website",
           "SigningKey": "", "AccessTokenMinutes": 60, "RefreshTokenDays": 30 },
  "Cors": { "AllowedOrigins": [ "http://localhost:4080", "http://localhost:3000" ],
            "AllowVercelPreviews": false },
  "ReverseProxy": { "Enabled": false },
  "Hubtel": { "ApiId": "", "ApiKey": "", "MerchantAccountNumber": "",
              "CreateCheckoutUrl": "https://payproxyapi.hubtel.com/items/initiate",
              "StatusBaseUrl": "https://api-txnstatus.hubtel.com",
              "RefundBaseUrl": "https://refund-api.hubtel.com",
              "CallbackBaseUrl": "http://localhost:8080", "CallbackToken": "",
              "EnforceCallbackIp": true, "Sandbox": false },
  "Storage": { "Provider": "Local",
               "R2": { "AccountId": "", "AccessKeyId": "", "SecretAccessKey": "",
                       "BucketName": "", "PublicBaseUrl": "" } },
  "Sermons": { "FeedUrl": "https://media.rss.com/the-newlife-podcast/feed.xml",
               "SyncIntervalMinutes": 60 },
  "Email": { "Provider": "None", "SmtpHost": "", "SmtpPort": 587, "SmtpUser": "",
             "SmtpPassword": "", "FromAddress": "no-reply@nlcbcgh.com" },
  "Seed": { "AdminEmail": "", "AdminPassword": "", "AdminFullName": "Administrator",
            "DemoData": false },
  "Frontend": { "BaseUrl": "http://localhost:4080" }
}
```

Environment variables (Render dashboard, double-underscore nesting):
`ConnectionStrings__Default`, `JWT__SigningKey`, `JWT__Issuer`, `JWT__Audience`,
`Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, `Cors__AllowVercelPreviews`,
`Hubtel__ApiId`, `Hubtel__ApiKey`, `Hubtel__MerchantAccountNumber`, `Hubtel__CallbackBaseUrl`,
`Hubtel__CallbackToken`, `Hubtel__EnforceCallbackIp`, `Hubtel__Sandbox`,
`Storage__Provider`, `Storage__R2__*`, `Sermons__FeedUrl`, `Seed__AdminEmail`,
`Seed__AdminPassword`, `Frontend__BaseUrl`, `ReverseProxy__Enabled=true`,
`Database__RunMigrationsOnStartup=true`,
`ASPNETCORE_ENVIRONMENT=Production`, `SEED_YOUTUBE_URL`.

Startup validation: options classes use `ValidateOnStart()`; fail fast with clear messages for
missing `JWT:SigningKey` (< 64 chars, prod), `Hubtel:*` (prod), `Hubtel:CallbackToken` (prod),
`Hubtel:EnforceCallbackIp=false` (prod — must be explicitly acknowledged), `Storage:R2:*` when
provider is R2. `ReverseProxy:Enabled=true` in Production (enables ForwardedHeaders with
`ForwardLimit=1`). Hubtel/Email integrations are optional in Development (features degrade
gracefully, logged).

---

## 14. Testing strategy

### 14.1 Unit tests (`tests/Newlife.UnitTests`)

xUnit v2 + Shouldly + NSubstitute. No DB. Must cover:

- `TicketCodeGenerator`: Crockford alphabet, length, uniqueness smoke (10k codes).
- Order pricing: totals, quantity limits, zero-total (free) path.
- Hubtel DTO mapping: PascalCase callback parse; status parse; response code mapping.
- RSS parsing: fixture XML (embed a trimmed real-feed sample) → episodes, durations
  (`HH:MM:SS`, `MM:SS`, seconds, invalid), guid dedupe.
- Validators: prayer submit, order create, event create, ticket type.

### 14.2 Integration tests (`tests/Newlife.IntegrationTests`)

`WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (Docker required; available on
dev machine and GitHub runners) + `Microsoft.AspNetCore.Mvc.Testing` (pinned). `ApiFactory` overrides connection string, sets
`Seed:DemoData=true` (the seeder grows per milestone and must tolerate not-yet-implemented
modules), `Database:RunMigrationsOnStartup=true`, `Hubtel:ApiId=""` (payments
disabled) and replaces `IPaymentGateway` with a fake. Helpers: `TestAuth.LoginAsAdminAsync()`
(seeds admin, returns bearer), `TestData` builders.

Must cover (acceptance):

- Auth: login success/failure, refresh rotation, reuse detection revokes chain, `me`.
- Prayers: submit validation, anonymous hides name, wall excludes flagged/deleted, pagination,
  stats numbers, answer/flag/unflag/delete; **401** for anonymous admin calls, **403** for a
  valid token with the wrong role.
- Media: presign (fake storage provider), register, list, delete in-use → 409, invalid key
  (`../`, wrong extension) → 400.
- Events: CRUD, slug uniqueness, publish filter, ticket types on sale calculation.
- Announcements: visibility window (publishAt/expiresAt).
- Sermons: manual sync with fake feed client inserts episodes, second sync updates without
  duplicates; latest endpoint returns youtubeUrl from settings.
- Orders: capacity check (serializable), free order issues tickets immediately, paid flow via
  fake gateway → webhook → tickets issued once (duplicate callback is a no-op), order token
  guards reads, QR endpoint content-type; retry-payment creates attempt 2 and rejects a 4th
  attempt; retry requires the order token; callback for a cancelled order flags
  `RequiresReview` without issuing tickets; callback-only fallback (gateway status throws) sets
  `RequiresReview` and reconciliation re-verifies with the fake returning Paid.
- Refunds: refund on `Paid` → `RefundPending`; success callback → `Refunded` + tickets
  `Cancelled`; failure callback → `RefundFailed` with tickets still `Valid`; refund without a
  transaction id → `409 order.not_refundable`.
- Check-in: valid → CheckedIn; second scan → AlreadyCheckedIn; unknown → Invalid; cancelled
  ticket → Cancelled.

Run: `dotnet test` (all) · `dotnet test tests/Newlife.UnitTests` ·
`dotnet test tests/Newlife.IntegrationTests --filter FullyQualifiedName~OrdersTests`.

---

## 15. Operational details

### 15.1 Dockerfile

Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`,
non-root (`USER $APP_UID`), `EXPOSE 8080`, entrypoint shell-form:
`exec dotnet Newlife.Api.dll --urls "http://+:${PORT:-8080}"` (Render injects `PORT`).
`docker-compose.yml`: `db` (postgres:17, port 5433, volume) + `api` (build ., port 8080,
`ConnectionStrings__Default` pointing at `db`).

### 15.2 CI (`.github/workflows/ci.yml`)

On push/PR to `main`: `actions/checkout@v4`, `actions/setup-dotnet@v4` (`dotnet-version: 10.0.x`),
`dotnet restore`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`
(Docker available on ubuntu-latest for Testcontainers), `docker build -t newlife-apis .`.

### 15.3 Deployment (Render + Neon + R2)

1. Neon: create project → database `newlife` → copy **pooled** connection string
   (`postgresql://...?sslmode=require`). Free tier: 0.5 GB, compute suspends when idle.
2. Cloudflare R2: bucket `newlife-media`, public custom domain `media.nlcbcgh.com`,
   API token with Object Read & Write.
3. Render: New → Web Service → Docker → repo `Newlife-Community-Devs/newlife-community-apis`,
   branch `main`, plan Free, health check path `/health`, env vars from Section 13
   (`Database__RunMigrationsOnStartup=true`, `Hubtel__EnforceCallbackIp=true`,
   `Hubtel__CallbackBaseUrl=https://<service>.onrender.com`).
4. Uptime pinger (cron-job.org, free): GET `https://<service>.onrender.com/health` every
   10 minutes to prevent free-tier spin-down (Render sleeps after 15 min idle; cold start ~30–60s).
5. After first deploy: login as seeded admin, create real admin users, set
   `Seed__AdminPassword` to empty.

### 15.4 Email (optional, non-blocking)

`IEmailSender` with two implementations: `NullEmailSender` (logs, default) and
`SmtpEmailSender` (`Email:Provider=Smtp`, e.g. Resend SMTP). Used only for order confirmation
(ticket codes + order page link). Failures never block fulfilment.

### 15.5 Logging and monitoring

- Serilog console JSON; Render captures stdout (searchable in dashboard).
- Health endpoints (6.10) for Render + pinger.
- Reconciliation warnings are the primary payment-health signal; also log callback
  rejections (`ip_rejected`, `unknown_reference`, `duplicate`).

---

## 16. Frontend integration contract (website repo)

> Executed module-by-module **after** this backend ships. Frontend work is tracked separately;
> this section is the authoritative mapping.

### 16.1 Environment

- `NEXT_PUBLIC_API_URL` = API base (e.g. `https://<service>.onrender.com`).
- Remove `API_BASE_URL.PRAYER` and `BACKEND_URL` usage; all calls go through
  `NEXT_PUBLIC_API_URL + /api/v1/...`.

### 16.2 Legacy → new mapping

| Frontend consumer | Legacy call | New call |
|---|---|---|
| `services/auth.ts` (NextAuth `authorize`) | `POST {BACKEND_URL}/auth/token` form | `POST /api/v1/auth/login` `{ email, password }` → store `accessToken` + `refreshToken` + `user.roles` in NextAuth JWT |
| `components/PrayerWallGrid.tsx` | `GET .../prayers/wall?skip&limit&sort_by&sort_order` | `GET /api/v1/prayers/wall?page&pageSize&sort&order` (map `total` → `totalCount`, `has_more` → `hasMore`; fields now camelCase) |
| `components/ui/RequestForm.tsx` | `POST .../prayers/submit` `{name, phoneNumber, prayerRequest, keepAnonymous}` | `POST /api/v1/prayers` `{ submitterName, phoneNumber, content, isAnonymous }` |
| Admin dashboard stats (mock) | — | `GET /api/v1/admin/prayers/stats` |
| Admin prayer table (mock) | — | `GET /api/v1/admin/prayers` + actions endpoints |
| Sermons section (RSS embed) | player.rss.com embed | `GET /api/v1/sermons/latest` + list (keep embed as fallback) |
| Events/announcements (future) | — | public endpoints above |

NextAuth JWT callback: store `accessToken`, `refreshToken`, `accessTokenExpiresAt`; when
expired, call `POST /api/v1/auth/refresh` and rotate both tokens; on refresh failure force
re-login. Role-gate admin UI from `user.roles`. Concurrent refresh calls are tolerated by the
backend's 60-second rotation grace window (Section 7.2) — still serialize refreshes in the
frontend where practical.

### 16.3 CORS

Frontend origins must be in `Cors:AllowedOrigins` (Section 6.7). This fixes the current
localhost CORS failure.

---

## 17. Build milestones (implement in order)

> Each milestone ends with: `dotnet build` green, `dotnet test` green (scoped tests), a commit
> with a conventional message. Do not start the next milestone with a red build.

### M0 — Repository bootstrap (`chore:` commits)

1. `global.json` (SDK 10.0.400, latestPatch), `.editorconfig` (C# defaults, 4-space, LF),
   `.gitignore` (`bin/ obj/ *.user .vs/ .idea/ .env appsettings.Local.json`).
2. `Directory.Build.props`: `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`,
   `TreatWarningsAsErrors=true`, `InvariantGlobalization=true`, `LangVersion=latest`.
3. Create the solution **first** — `dotnet new sln -n Newlife --format slnx` — then the 6
   projects:
   `dotnet new webapi --use-controllers -o src/Newlife.Api`, three `dotnet new classlib`
   (Domain/Application/Infrastructure), `dotnet new xunit` for **both** test projects.
   Add all to the solution; delete template `WeatherForecast`/`Class1`/`UnitTest1` files; wire
   project references per Section 4.
4. Add `Directory.Packages.props` (Section 3.2) **after** project creation, then remove every
   `Version="..."` attribute from template-generated `PackageReference`s (CPM forbids them →
   NU1008) and add the missing references (`Microsoft.EntityFrameworkCore.Design` to
   **Newlife.Api**; `Microsoft.AspNetCore.Mvc.Testing` to IntegrationTests). Tool manifest +
   `dotnet-ef` 10.0.15.
5. `Program.cs` skeleton: Serilog, OpenAPI, exception middleware placeholder, liveness `/health`
   only (readiness/DB check lands in M1), `app.Run()`; add `public partial class Program { }`
   for WebApplicationFactory.
6. Dockerfile + docker-compose; CI workflow; `.env.example`; README stub.
   **Acceptance:** `dotnet build` green; `dotnet test` green (template smoke test);
   `docker compose up -d db` healthy; CI file present.

### M1 — Cross-cutting foundation (`feat:` commits)

1. ProblemDetails exception middleware + domain exceptions (6.4).
2. FluentValidation + `ValidationFilter` (6.3).
3. `PagedResult`/`PaginationQuery` (6.5); JSON options (6.1); CORS (6.7);
   rate limiter (6.6); forwarded headers; OpenAPI + Swagger UI (6.9).
4. `BaseEntity`, `AppDbContext` with `SaveChangesAsync` UTC stamping; `IAppDbContext`.
5. **Acceptance:** a temporary `GET /api/v1/ping` returns 200; 404 ProblemDetails shape
   verified by integration test; readiness `/health/ready` reports DB status; a temporary
   `ping` rate-limit policy returns 429 after its configured limit (the login limiter is
   verified in M2).

### M2 — Identity and access (`feat:` commits)

1. `AdminUser`, `RefreshToken`, Identity setup, JWT auth + `JwtTokenService` (7.2).
2. `AuthController` (login/refresh/logout/me) + validators + `auth` rate limit.
3. `AdminUsersController` (8.10) + role seeding + first-admin seeder (12).
4. Migration `InitialIdentity`; integration tests (14.2 Auth).
   **Acceptance:** login returns tokens; expired/reused refresh behaves per 7.2; admin-only
   endpoints 403 without role; migration applies to empty Postgres.

### M3 — Prayer Wall (`feat:` commits)

1. `PrayerRequest` entity/config/migration.
2. `PrayerService` + public/admin controllers (8.2) + validators + rate limit.
3. Demo seed prayers (Section 12, item 4).
4. Integration tests (14.2 Prayers).
   **Acceptance:** all 8.2 endpoints behave per contract; wall hides flagged/anonymous
   submitter names; stats match seeded data; 429 after 5 rapid submits.

### M4 — CMS core: media, events, announcements, settings (`feat:` commits)

1. Entities/configs/migration; `MediaService` + `IStorageProvider` (Local + R2) (11) +
   dev upload endpoint.
2. Media endpoints (8.3); Events + ticket types (8.4); Announcements (8.5); Settings (8.7).
3. Integration tests (14.2 Media/Events/Announcements).
   **Acceptance:** presign→register→list→delete cycle works with fake storage; event slug
   uniqueness; announcement windows; public settings whitelist only; ticket-type `remaining`
   returns full `quantity` (order guards activate in M6).

### M5 — Sermons (`feat:` commits)

1. `SermonEpisode` entity/config/migration; `PodcastFeedClient` + parsing (10).
2. `SermonsController` + admin sync/visibility (8.6); `RssSyncHostedService`.
3. Unit tests (RSS fixtures) + integration test with fake feed client.
   **Acceptance:** sync ingests fixture feed; second sync adds 0; latest returns settings
   youtubeUrl; hidden episodes excluded publicly.

### M6 — Ticketing and Hubtel payments (`feat:` commits)

1. Order/OrderItem/OrderPaymentAttempt/Ticket/HubtelCallbackLog entities/configs/migration;
   code generator.
2. `OrderService` (pricing, capacity transaction, free path, retry-payment), `HubtelClient`
   (9.3), `PaymentFulfillmentService` (9.5) + `PaymentFulfillmentHostedService`, callback
   controllers (9.4 + refund callback 9.7).
3. Admin orders/check-in/refund (8.9); QR endpoint (8.8); hosted services (9.6);
   expiry job (5.2).
4. Unit tests (pricing/codes/Hubtel DTOs) + integration tests with fake gateway
   (Orders/HubtelCallback/CheckIn).
5. `IEmailSender` + `NullEmailSender`/`SmtpEmailSender` (15.4) wired to post-fulfilment
   confirmation.
   **Acceptance:** free order → tickets immediately; fake-paid flow issues tickets exactly
   once on duplicate callbacks; oversell prevented under concurrency test; check-in
   idempotent; refund path maps Hubtel codes; `clientReference` ≤ 32.

### M7 — Hardening, docs, deploy (`chore:`/`docs:` commits)

1. Security pass: startup config validation, `EnforceCallbackIp=true` in prod docs, no PII in
   logs, HTTPS/HSTS, forwarded headers correctness, rate limits re-checked.
2. README: setup, env catalogue, migrations, test commands, deploy runbook (Section 15.3),
   Hubtel operational notes (9.8), Render cold-start/pinger note.
3. `DECISIONS.md` seeded with the deviations recorded during implementation.
4. Render + Neon + R2 provisioning; smoke test in prod: health, login, prayer submit/wall,
   event listing, sermon sync.
   **Acceptance:** production smoke test passes; reconciliation job logs healthy; `/health`
   green from external pinger.

---

## 18. Global Definition of Done

- [ ] All milestones M0–M7 complete; `dotnet build -c Release` and `dotnet test` green.
- [ ] Every endpoint in Section 8 implemented with validation + auth + pagination + ProblemDetails.
- [ ] Integration tests cover every acceptance bullet in 14.2.
- [ ] No secrets in repo; `.env.example` documents every variable.
- [ ] Migrations committed; fresh DB migrates + seeds cleanly.
- [ ] Docker image builds and runs as non-root on port `${PORT:-8080}`.
- [ ] CI green on `main`; Render auto-deploy green; `/health` reachable externally.
- [ ] Hubtel sandbox integration checks pass (or documented blockers in `DECISIONS.md`).
- [ ] README deploy runbook reproducible by a second engineer.

---

## 19. Non-goals (v1)

- No admin web UI in this repo (the Next.js site owns UI).
- No SMS/WhatsApp notifications; email is optional (15.4).
- No multi-currency; GHS only.
- No seat maps/reserved seating; general admission quantities only.
- No Spotify/Apple Podcasts APIs; RSS ingestion only.
- No user self-registration, social login, or public API keys.
- No persistent local file storage in production (R2 only).
- No data migration of legacy prayers in v1; if the church provides legacy DB/API access, a
  one-off import script can be added later (Appendix A).

---

## 20. Risks and open questions

### 20.1 Platform risks

1. **Render free tier sleeps** after 15 min idle (~30–60s cold start). Mitigation: uptime pinger
   every 10 min. Acceptable for a church site.
2. **Neon free tier** suspends compute when idle (~0.5s resume) and has a 0.5 GB cap. Fine for
   this workload; monitor size.
3. **No static outbound IP on Render free** vs Hubtel status-endpoint allowlisting (9.8). This
   is the top launch risk. Mitigations documented; decide before go-live.

### 20.2 Hubtel contract risks (from verification)

1. No production callback signature — rely on source-IP check (`108.129.40.25`), secret callback
   token, and status re-query when reachable.
2. Callback retry/ack semantics are "open contract questions" in Hubtel's docs — handler must be
   idempotent and never 4xx/5xx on accepted-source callbacks.
3. `clientReference` retention window in production is unconfirmed (sandbox: 7 days). Never
   reuse references.
4. Card refunds are undocumented; v1 supports mobile-money refunds only.
5. Sandbox availability contradiction (one 2026 blog says no sandbox; docs site documents one).
   Verify with Hubtel before go-live sign-off.

### 20.3 Accepted fallback

If the status endpoint cannot be allowlisted, callback-first fulfilment with token+IP checks is
the accepted fallback; it is recorded as a deliberate, auditable trade-off — not a silent one.

### 20.4 Verified sources (provenance)

- Package versions: `api.nuget.org` flat-container index + nuspec inspection, 2026-11-14;
  compatibility empirically built on SDK 10.0.400.
- Hubtel: `code.hubtel.com/docs` + `llms.txt` content, 2026-11-14 (see Section 9).
- RSS feed: `https://media.rss.com/the-newlife-podcast/feed.xml` fetched 2026-11-14
  (40 items, RSS 2.0 + iTunes namespace).
- .NET 10 LTS lifecycle: 2026-11-14 → EOL 2028-11-14.

---

## Appendix A — Optional legacy prayer import

If legacy data access is provided, add `tools/import-prayers/` (console app or SQL script) that
maps legacy fields to `prayer_requests` (`title`, `content`, `submitter_name`, `phone_number`,
`is_anonymous`, `is_answered`, `is_flagged`, `created_at`), dedupes on
`(content, created_at)`, and runs inside a transaction. Not part of v1 acceptance.

## Appendix B — Adding a future module (recipe)

1. Domain entity + configuration + migration.
2. Application service + DTOs + validators + interfaces as needed.
3. Controller under `/api/v1/...` (public) and `/api/v1/admin/...` (role-gated).
4. Integration tests (happy path + auth + validation + pagination).
5. OpenAPI appears automatically; update README route table.
6. Commit as `feat: <module>`.
