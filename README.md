# Newlife Community APIs

Backend REST API for the Newlife Community Baptist Church website
([nlcbcgh.com](https://www.nlcbcgh.com)) — prayer wall, CMS (media, events, announcements,
sermons), ticketing with Hubtel payments, and JWT auth for the site's admin console.

**Stack:** ASP.NET Core Web API on .NET 10 LTS · PostgreSQL (EF Core + Npgsql) ·
Cloudflare R2 · Hubtel Online Checkout · Render.

> **This repository is built from `PLAN.md`.** The plan is the authoritative build
> specification (architecture, data model, API contracts, verified third-party integration
> details, milestones, and acceptance criteria). Read it first. The full README (setup,
> configuration, deployment runbook) is produced in milestone M7 of the plan.

## Quick start (once M0 lands)

```bash
dotnet tool restore
docker compose up -d db
dotnet build
dotnet test
dotnet run --project src/Newlife.Api
```

API surface: `/api/v1/...` · health: `/health` · OpenAPI (dev): `/openapi/v1.json` ·
Swagger UI (dev): `/swagger`.

## Repository status

- [x] Build plan (`PLAN.md`)
- [ ] M0 repository bootstrap
- [ ] M1 cross-cutting foundation
- [ ] M2 identity and access
- [ ] M3 prayer wall
- [ ] M4 CMS core
- [ ] M5 sermons
- [ ] M6 ticketing and payments
- [ ] M7 hardening, docs, deploy

## License

Private — Newlife Community Baptist Church.
