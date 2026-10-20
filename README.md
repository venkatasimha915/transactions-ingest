# Transactions Ingest

Hourly-style import of card transactions from three source systems (**Store Uptown**, **Store Downtown**, **Online**)
into SQLite, with change detection, revocation, finalization, audit history and an Angular UI to browse the result.

- **Backend:** ASP.NET Core 10 (minimal APIs), EF Core 10 code-first, SQLite
- **Frontend:** Angular 22 (standalone components, signals)
- **Tests:** 27 xUnit tests (pure rules, SQLite integration, HTTP end-to-end) + an Angular component test

---

## 1. Build

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), Node.js 22.22.2+ or 24.15+ (required by Angular CLI 22).

```bash
# backend
dotnet restore
dotnet build
dotnet test                # 27 tests

# frontend
cd client
npm ci
npm run build
npm test -- --watch=false
```

## 2. Run

Two terminals:

```bash
# Terminal 1 – API on http://localhost:5080 (creates/migrates transactions.db on startup)
dotnet run --project src/Transactions.Api

# Terminal 2 – UI on http://localhost:4200 (proxies /api to :5080)
cd client
npm start
```

Open <http://localhost:4200> and click **Run import**.
The OpenAPI document is at <http://localhost:5080/openapi/v1.json> (Development only), and
`src/Transactions.Api/Transactions.Api.http` has ready-made requests.

### Try the reconciliation rules

The mock sources re-read `src/Transactions.Api/SampleData/*.json` on every import, so with the API running:

| Do this | Then Run import | You should see |
|---|---|---|
| Nothing | twice | 2nd run: everything `unchanged`, no new audit rows (idempotent) |
| Change an `amount` in `online.json` | once | `1 updated`; the row's audit history shows `Amount: old → new` |
| Delete an item from `online.json` | once | `1 revoked`, status badge **Revoked** |
| Put it back | once | `1 reactivated` |
| Set `"SimulateFailure": true` for a source in `appsettings.json` (restart) | once | that source is listed as failed; **none** of its transactions are revoked |

`SampleData/README.md` explains the data format (`ageMinutes` vs. absolute `transactionTime`).

### API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/import` | Runs the import, returns the run summary. **409** if one is already running. |
| `GET` | `/api/transactions?source=&status=&search=&sort=&direction=&page=&pageSize=` | Paged list. `sort` ∈ sourceSystem, transactionId, locationCode, productName, amount, transactionTime (default), status. |
| `GET` | `/api/transactions/{id}` | One transaction with its full audit history. |
| `GET` | `/api/imports?take=20` | Recent import runs. |
| `GET` | `/api/sources` | Configured source names (for UI filters). |
| `GET` | `/health` | Liveness. |

Errors are returned as RFC 7807 `application/problem+json` (validation → 400, unknown id → 404, concurrent import → 409, unhandled → 500 without stack traces).

---

## 3. Design decisions

### Solution layout

```
src/
  Transactions.Core            domain entities, ITransactionSource, reconciliation rules (pure, no EF / ASP.NET)
  Transactions.Infrastructure  EF Core DbContext + migrations, JSON mock sources, TransactionImportService, DI
  Transactions.Api             minimal-API endpoints, Program.cs, appsettings.json, SampleData
tests/Transactions.Tests       unit (pure rules) + integration (real SQLite) + API (WebApplicationFactory)
client/                        Angular app
```

- **The import is host-agnostic.** Everything is registered by `services.AddTransactionsIngest(...)` and invoked through
  `ITransactionImportService.RunAsync(trigger)`. The API endpoint is one caller; an hourly scheduler (Hangfire, Quartz,
  a `BackgroundService`, a cron'd console app) would resolve the same interface and call the same method, unchanged.
- **Rules are pure functions.** `TransactionReconciler.Reconcile(...)` takes the stored rows + the snapshot + "now" and
  mutates/creates entities and audit rows with no I/O. `TransactionImportService` only loads, calls it and saves.
  That split is what makes the rules cheap to test exhaustively.
- **Time is injected** (`TimeProvider`), so tests move the clock with `FakeTimeProvider` to test the 24-hour boundary.

### Data model

| Table | Notes |
|---|---|
| `Transactions` | Spec fields + `Status` (Active / Revoked / Finalized), `Revision`, `CreatedAtUtc`, `UpdatedAtUtc`. Surrogate PK `Id`; **unique index on (`SourceSystem`, `TransactionId`)** enforces the logical key. |
| `TransactionAudits` | One row per event: `Created`, `Updated` (**one row per changed field** with old/new value), `Revoked`, `Reactivated`, `Finalized`. Linked to the transaction and to the import run. |
| `ImportRuns` | One row per execution: trigger, start/end, counters, failed sources. Groups the audit rows a run produced. |

- **Amount**: `decimal` in C#, rounded to 2 dp. SQLite has no decimal type (EF stores TEXT, which can't be sorted or
  compared in SQL), so it is persisted as **integer cents** via a value converter: exact and sortable.
- **DateTimes** are UTC everywhere; a converter re-stamps `DateTimeKind.Utc` on read because SQLite loses it.
- Enums are stored as strings for readable data.
- Schema is created by **EF Core migrations** applied at startup (`Persistence/Migrations`).

### Reconciliation rules (per source, per run)

Window = `Import:ReconciliationWindowHours` (24), measured from `TransactionTime`.

1. **Fetch** the snapshot. **If the fetch fails, the source is skipped entirely**: an outage must never look like
   "every transaction was removed" and cause mass revocations. Other sources still import.
2. **Validate/normalize** (`SnapshotNormalizer`): source name must match, ids positive, duplicates in a snapshot → first
   kept, null card → `""`, amount rounded, time → UTC. Invalid items are rejected and logged, not fatal.
3. **Finalize** stored Active rows older than the window → immutable from then on.
4. **Insert** unknown items (a late arrival already older than the window is inserted directly as Finalized).
5. **Update** changed items field-by-field, bump `Revision`, write one audit row per field.
6. **Reactivate** a Revoked item that reappears inside the window.
7. **Revoke** Active rows missing from the snapshot and still inside the window.
8. **Save** — one `SaveChanges` per source = one database transaction, so a source is applied all-or-nothing.

**Idempotency:** a run with identical data finds no differences, so it writes nothing except its own `ImportRuns` row
(verified by a test that compares rows, revisions, timestamps and audit counts across two runs).

**Concurrency:** an injected singleton `ImportRunLock` rejects an overlapping run with 409 instead of queueing it.

### Configuration, logging, errors

- `appsettings.json`: connection string, reconciliation window, source list (name, file, `SimulateFailure`), CORS origins.
  Options are validated at startup (`ValidateOnStart`).
- Structured logging via `ILogger`: per-source and per-run counters, rejected items, ignored changes to finalized rows,
  with the `ImportRunId` in the log scope.
- `AddProblemDetails` + `UseExceptionHandler` for consistent error responses.

### Frontend

A single standalone page (`client/src/app/transactions`): table with all required columns and a status badge,
source/status filters, search, sortable columns, paging, a **Run import** button with the run summary, and a
details panel with the audit timeline. Responsive down to phone width; follows light/dark system theme.

---

## 4. Assumptions

- A snapshot contains **everything the source currently publishes for at least the last 24 hours**. That is what makes
  "missing from the snapshot" mean "revoked".
- A revoked transaction that reappears within 24 hours is a valid correction → **reactivated** (audited).
- After 24 hours a transaction is **finalized**: later source changes (or disappearance) are ignored and logged as warnings.
- Text limits: the spec says "length < 20"; I store up to **20** characters. Identifying fields that are too long reject
  the item; `ProductName` is descriptive, so it is **truncated** with a warning instead of dropping a payment.
  (The exercise's own sample "USB-C Docking Station" is 21 characters.)
- `CardNumber` is already masked by the source; it is stored as given.
- `sourceSystem` inside an item must equal the source it came from; otherwise the item is rejected.
- Mock data: items may specify `ageMinutes` instead of `transactionTime`; the time is computed from the **start of the
  current hour**, so the demo always has data inside the 24h window while repeated runs in the same hour see identical data.
  The three `100001` samples from the exercise keep their February 2026 timestamps and therefore import as Finalized.

## 5. Production notes / what I'd do next

- Run the import from a scheduler (Hangfire/Quartz or a `BackgroundService` with `PeriodicTimer`) — no change to the import code.
- Replace `ImportRunLock` with a distributed lock when running more than one instance.
- Real `ITransactionSource` implementations with `HttpClient` + Polly-style retry/timeouts (`Microsoft.Extensions.Http.Resilience`).
- Retention/archiving for audit rows; authentication on `POST /api/import`.
- If sources can publish partial snapshots, add a per-source "snapshot is complete" contract before revoking.

## 6. AI usage

AI assistance was used. See [`docs/ai/`](docs/ai/README.md) for the disclosure, the prompts, conventions/rules
and design discussion, and the list of modifications made to the AI output.

## 7. Time

See [`TIMELOG.md`](TIMELOG.md) for the estimate and actual time spent.
