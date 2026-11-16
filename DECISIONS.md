# DECISIONS

Deviations and accepted risks recorded during implementation of `PLAN.md`.
The plan is the source of truth; this file explains where reality forced a choice.

## D-001 — Hubtel Online Checkout on Render free tier (no static egress IP)

**Context (PLAN §9.8).** The Hubtel transaction-status endpoint allowlists the caller's
server IP. Render's free tier assigns rotating outbound IPs, so a status call from this API
is not guaranteed to reach Hubtel with an allowlistable source address. There is no
callback signature to verify either (not part of the published v1 contract).

**Decision.** Implement callback-first fulfilment:

- Hubtel callbacks are accepted only behind a secret query-string token
  (`Hubtel:CallbackToken`, constant-time compared) and, in production, an IP allowlist check
  against the documented source `108.129.40.25` (`Hubtel:EnforceCallbackIp=true`, enforced at
  startup — setting it `false` in Production fails the build).
- Fulfilment verifies the payment via the status endpoint (confirmed `Paid` + amount match).
- If the status endpoint is unreachable/forbidden but a token+IP-valid callback reports
  `Success` with a matching amount, the order is fulfilled with `RequiresReview=true`,
  `ReviewReason="callback_only_unverified"`, `HubtelTransactionId=NULL`, and the raw callback
  is stored in `hubtel_callback_logs` for audit.
- The reconciliation job re-verifies such orders continuously; when the status endpoint
  eventually confirms `Paid`, the transaction id is filled and review cleared; a non-Paid
  status flips `ReviewReason="callback_status_mismatch"` for manual admin action — issued
  tickets are never auto-cancelled.

**Residual risk / follow-up.** Ask Hubtel to allowlist Render's published egress ranges, or
move the API to a host with a static outbound IP. Until then, treat `callback_only_*`
review flags in `/api/v1/admin/orders?status=Paid` as a routine check.

## D-002 — `RateLimits:Enabled` configuration toggle

Rate limiting is always on per PLAN §6.6. One integration milestone needs >5 auth/prayer
calls per window, so registration of the limiter is gated by `RateLimits:Enabled`
(default `true`; integration tests set `false`). Production behaviour is unchanged.

## D-003 — Payment background-job intervals are configurable

PLAN §9.6/§5.2 hard-codes the reconciliation loop (60s) and expiry loop (5min). The
intervals and reconciliation age are now configurable via `Payments:ReconciliationIntervalSeconds`,
`Payments:ReconciliationMinAgeSeconds` and `Payments:ExpiryIntervalSeconds`,
with the plan's values as defaults, to make exactly-once and late-callback paths
deterministic in integration tests.

## D-004 — Search matching uses `lower()/LIKE`, not `pg_trgm`/`ILIKE`

Application services must not reference Npgsql (layer rule, PLAN §4). Case-insensitive
`search` filters therefore translate to `LIKE lower(...)` instead of `ILIKE` or a trigram
index. `plan §5.1` already marks the GIN trigram index as optional; add it (with raw SQL
migration) if search performance becomes a problem.

## D-005 — R2 presign flags via SDK v4 config

PLAN §11 asks for `DisablePayloadSigning`/`DisableDefaultChecksumValidation` for R2
compatibility. In AWSSDK.S3 4.1 those flags exist only on `PutObjectRequest`, and
`GetPreSignedURL` accepts only `GetPreSignedUrlRequest`. The equivalent behaviour is set on
the client instead: `RequestChecksumCalculation`/`ResponseChecksumValidation` = `WHEN_REQUIRED`
(query-signed URLs never sign the payload). R2-compatible presigned PUTs, no per-request flags.

## D-006 — Demo seed guards per entity

PLAN §12 gates demo data on `Seed:DemoData=true` and "DB has no events". Because milestones
land incrementally, each demo section guards on its own table being empty (prayers, then
events/announcements). Result on a fresh database is identical; re-running the seeder on a
partially demo-populated database stays idempotent.

## D-007 — Single M1+M2 commit

PLAN §17 expects separate milestone commits. M1 (foundation) and M2 (auth) both touch
`Program.cs`, `AppDbContext` and DI; splitting them would leave the first commit not
compiling on its own, breaking bisectability. One commit per remaining milestone (M3+) is
kept, each with a green build and passing tests.

## D-008 — Atomic stock counter instead of Serializable retry

PLAN §5.2 prescribes order creation in a **Serializable** transaction with up to 3 retries
on `40001`. Under 40-way concurrent load in integration tests, EF Core 10 + Npgsql 10 showed
ambiguous commit behavior: a serialization error could surface for a request whose order had
actually committed (the row persisted while the client saw `40001`), leaving orphaned
`PendingPayment` orders and double-commits on retry. `CommitAsync`-failure recovery cannot
close that window without an idempotency key.

Decision: `ticket_types.stock_reserved` is a monotonic counter maintained by atomic
conditional updates (`UPDATE ... WHERE stock_reserved + q <= quantity`); a zero-row result is
`409 order.capacity_exceeded`. No Serializable isolation, no retries, deterministic under any
concurrency. The release rule from §5.2 (Cancelled/Expired/Refunded/RefundFailed release
stock) is enforced by explicit decrements in the expiry job, refund callbacks and refund
status handling. `RefundPending` keeps its reservation until the refund callback resolves it.
Npgsql's default retrying execution strategy is replaced with `NonRetryingExecutionStrategy`
(it wrapped every exception, including business errors, in `InvalidOperationException`).
