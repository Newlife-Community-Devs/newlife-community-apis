# Newlife Community APIs

Backend REST API for the Newlife Community Baptist Church website
([nlcbcgh.com](https://www.nlcbcgh.com)) — prayer wall, CMS (media, events, announcements,
sermons), ticketing with Hubtel payments, and JWT auth for the site's admin console.

**Stack:** ASP.NET Core Web API (controllers) on .NET 10 LTS · PostgreSQL (EF Core + Npgsql) ·
ASP.NET Identity + JWT · Cloudflare R2 (S3-compatible media storage) · Hubtel Online
Checkout · Docker on Render.

> [`PLAN.md`](PLAN.md) is the authoritative build specification (data model, API contracts,
> verified Hubtel details, milestones). [`DECISIONS.md`](DECISIONS.md) records deviations and
> accepted operational risks.

## Architecture

```
src/
  Newlife.Domain           entities, enums, constants (no dependencies)
  Newlife.Application      services, DTOs, validators, interfaces (no Infrastructure)
  Newlife.Infrastructure   EF Core AppDbContext, Hubtel client, storage, email, hosted jobs
  Newlife.Api              controllers, middleware, Program.cs (composition root)
tests/
  Newlife.UnitTests            pure unit tests (no DB)
  Newlife.IntegrationTests     WebApplicationFactory + Testcontainers PostgreSQL
```

Central Package Management (`Directory.Packages.props`) pins every version. The solution uses
`.slnx`. Migrations live in `Newlife.Infrastructure/Persistence/Migrations`.

## Local setup

Prerequisites: .NET 10 SDK, Docker.

```bash
dotnet tool restore                  # installs dotnet-ef (pinned in dotnet-tools.json)
cp .env.example .env                 # fill in secrets as needed
docker compose up -d db              # PostgreSQL 17 on localhost:5433

dotnet build
dotnet test                          # runs unit + integration (Docker required)

dotnet run --project src/Newlife.Api # http://localhost:8080 (dev JWT key + demo seed in appsettings.Development.json)
```

Endpoints: API at `/api/v1/...` · health `/health` (+ `/health/ready` with DB check) ·
Swagger UI (dev only) `/swagger` · OpenAPI JSON (dev only) `/openapi/v1.json`.

Full API + local stack: `docker compose up --build` (API on 8080, db on 5433).

### First admin and demo data

The API seeds on startup (when `Database:RunMigrationsOnStartup=true`):

1. Roles `Admin`, `ContentEditor`, `EventManager` — always.
2. First admin — only when `AspNetUsers` is empty **and** `Seed:AdminEmail` +
   `Seed:AdminPassword` are set. Clear `Seed:AdminPassword` after first login.
3. Site settings (`sermons.latestYoutubeUrl`, `home.heroAnnouncement`) — insert-if-missing.
4. Demo data (`Seed:DemoData=true`) — prayers, events with ticket types, announcements.

### Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Newlife.Infrastructure --startup-project src/Newlife.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Newlife.Infrastructure --startup-project src/Newlife.Api
```

Migrations also run at startup when `Database:RunMigrationsOnStartup=true`.

## Configuration

All keys below map to env vars with `__` (e.g. `ConnectionStrings__Default`). `.env.example`
is the full catalogue. Non-obvious ones:

| Key | Notes |
|---|---|
| `DATABASE_URL` | Optional. URI form (`postgresql://…`) from Neon/managed hosts; used when `ConnectionStrings__Default` is unset. URIs are normalized to Npgsql key-value form at startup (Npgsql does not parse URIs itself). |
| `JWT:SigningKey` | HS256, **≥ 64 characters** (validated at startup). |
| `JWT:RefreshRotationGraceSeconds` | 60s grace for concurrent refresh rotations. |
| `Hubtel:ApiId` / `Hubtel:ApiKey` | Workbench → API keys. Empty `ApiId` disables payments paths that need the gateway (reconciliation skips). |
| `Hubtel:MerchantAccountNumber` | Collection account from Hubtel Workbench. |
| `Hubtel:CallbackBaseUrl` | Public HTTPS base of this API, e.g. `https://<service>.onrender.com`. |
| `Hubtel:CallbackToken` | Long random secret guarding the webhook; **required in Production**. |
| `Hubtel:EnforceCallbackIp` | `true` default; **Production refuses to start when false** (explicit-decision gate). |
| `Hubtel:Sandbox` | Rewrites calls to `code.hubtel.com/sandbox/v1/...` test endpoints. |
| `Storage:Provider` | `Local` (dev; `wwwroot/uploads`) or `R2` (Cloudflare). |
| `Storage:R2:*` | AccountId/keys/bucket + `PublicBaseUrl` (e.g. `https://media.nlcbcgh.com`). |
| `Payments:*` | Background-job tuning (reconciliation/expiry) — defaults match production behavior. |
| `RateLimits:Enabled` | `true` in prod; integration tests disable. |
| `Cors:AllowedOrigins` | Website origins; `Cors:AllowVercelPreviews` for preview deployments. |
| `Forwarded`/`ReverseProxy:Enabled` | Must be `true` on Render (TLS + client IP come via proxy headers). |

## Testing

```bash
dotnet test                                    # everything (needs Docker running)
dotnet test tests/Newlife.UnitTests            # fast, no Docker
dotnet test tests/Newlife.IntegrationTests --filter FullyQualifiedName~OrdersTests
```

Integration tests spin a PostgreSQL 17 container per test class (Testcontainers), run real
migrations, seed an admin (`admin@newlife.test` / `AdminPass123`) + demo data, and replace
`IStorageProvider`, `IPodcastFeedClient` and `IPaymentGateway` with scripted fakes — no
external network calls ever.

## Payments: operational notes (read before launch)

- **`clientReference` format** `NL{8-char order suffix}{2-digit attempt}` — unique forever.
  Never reuse a reference: retrying the same one returns "Duplicated client reference",
  and retrying a *paid* checkout risks a double charge. Payment retries create attempt 2, 3
  (max 3), then `409 order.retry_limit_reached`.
- **Callback** `POST /api/v1/payments/hubtel/callback?token=…` — PascalCase payload,
  documented source IP `108.129.40.25`, **no signature** in the v1 public contract. The
  handler always returns 200 (even for rejects) to avoid Hubtel retry storms; fulfilment is
  queued on a bounded channel and processed by a single background consumer, exactly-once.
- **Status endpoint** (`api-txnstatus.hubtel.com`) requires your server IP allowlisted by
  Hubtel. Render's free tier has **no static outbound IP**: fulfilment therefore falls back
  to *callback-only* evidence (token+IP-validated, amount matched) and flags the order
  `RequiresReview=true, ReviewReason="callback_only_unverified"`; the reconciliation job
  clears the flag once the status endpoint confirms. Details and follow-up in
  [DECISIONS.md D-001](DECISIONS.md).
- **Refunds**: mobile-money only (`422 refund.unsupported_channel` for cards); require the
  `HubtelTransactionId` (callback-only orders are `409 order.not_refundable` until verified).
  State machine `Paid → RefundPending → Refunded | RefundFailed`.
- **Expired holds**: `OrderExpiryHostedService` runs one status check before cancelling an
  `AwaitingPayment` hold. A callback that arrives after cancellation sets
  `RequiresReview="late_payment"` — resolve manually (fulfil or refund).
- Sandbox testing needs a public URL for callbacks (cloudflared/ngrok) and the test numbers
  in `PLAN.md` §9.8.

## Deployment (Render + Neon + R2)

1. **Neon**: create project → database `newlife` → copy the **pooled** connection string
   (`sslmode=require`). Free tier suspends idle compute — the app runs migrations at startup.
2. **Cloudflare R2**: bucket `newlife-media`, public custom domain `media.nlcbcgh.com`,
   API token with Object Read & Write → `Storage__Provider=R2` + `Storage__R2__*`.
3. **Render**: New → Web Service → Docker → `Newlife-Community-Devs/newlife-community-apis`,
   branch `main`, Free plan, health check path `/health`. Env vars from `.env.example` with
   production values: `ASPNETCORE_ENVIRONMENT=Production`,
   `Database__RunMigrationsOnStartup=true`, `ReverseProxy__Enabled=true`,
   `Hubtel__EnforceCallbackIp=true`, `Hubtel__CallbackBaseUrl=https://<service>.onrender.com`,
   real `JWT__SigningKey` + `Hubtel__CallbackToken`, seed admin credentials (first deploy).
4. **Pinger**: Render free tier sleeps after ~15 min idle (cold start 30–60 s). Schedule a
   cron-job.org (or similar) GET to `https://<service>.onrender.com/health` every 10 minutes.
5. **After first deploy**: login as the seeded admin, create real users via
   `POST /api/v1/admin/users`, then clear `Seed__AdminPassword`.

## CI

`.github/workflows/ci.yml`: restore → build (Release, warnings as errors) → test (Docker
available on runners for Testcontainers).

## Frontend integration

The website (`nlcbcgh.com`, Next.js) repoints module-by-module per `PLAN.md` §16:
`NEXT_PUBLIC_API_URL` + `/api/v1/...`, JWT login with rotating refresh tokens (60 s grace),
role-gated admin, and CORS allowlisting of the site origins.

## License

Private — Newlife Community Baptist Church.
