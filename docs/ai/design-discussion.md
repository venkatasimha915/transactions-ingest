# Design discussion

Summary of the decisions taken while building, the alternatives considered, and why.

| Topic | Decision | Alternatives considered | Why |
|---|---|---|---|
| Where the rules live | Pure `TransactionReconciler` in Core; EF service only loads/saves | All logic inside the EF service | Rules testable without a DB; service stays small; any host can reuse it |
| Source outage | Skip the source entirely, record it in `ImportRun.FailedSources` | Treat as empty snapshot; fail the whole run | Empty snapshot would revoke everything; failing the whole run blocks healthy sources |
| Atomicity | One `SaveChanges` (= one DB transaction) per source | One transaction for the whole run | Sources are independent; a bad source shouldn't roll back good ones |
| Older than 24h | Finalize (immutable) — implemented the optional criterion | Keep updating forever | Bounded reconciliation; matches "can be finalized and become immutable" |
| Revoked then reappears | Reactivate, audited | Insert duplicate / ignore | Logical key must stay unique; treats it as a source correction |
| Late arrival > 24h old | Insert directly as Finalized | Reject | Don't lose real payments; still immutable |
| Amount in SQLite | Integer cents via value converter | EF default TEXT; `double` | TEXT can't be sorted/compared in SQL; `double` is inexact |
| DateTime kind | Converter stamps `Utc` on read | Store as ticks / DateTimeOffset | Keeps model simple; SQLite drops Kind |
| Over-long text | Reject identifying fields; truncate ProductName with warning | Reject everything / truncate everything | The spec's own sample product name is 21 chars; dropping a payment over a description is worse |
| Overlapping runs | Singleton `ImportRunLock`, 409 on conflict | Queue / static semaphore | Fast feedback for a manual click; injected so tests are isolated |
| Mock data timestamps | `ageMinutes` relative to the start of the current hour | Fixed Feb-2026 timestamps | Fixed dates are always >24h old, so revocation could never be demoed |
| Loading existing rows | Only non-finalized rows + rows the snapshot mentions | Load all rows of the source | Keeps the query bounded as history grows |
| Tests DB | SQLite in-memory with open connection | EF InMemory provider | InMemory ignores unique indexes and SQL translation |

## Problems hit during the session
- **NuGet blocked in the build sandbox** at first: the pure rule tests were compiled and run against a tiny xUnit
  stand-in until access was enabled; then the real suite was run.
- **API test returned 409**: the first version used a `static SemaphoreSlim`, shared across parallel test classes.
  Replaced with an injected singleton `ImportRunLock` (better design anyway).
- **Vulnerability warnings** (SQLitePCLRaw, Microsoft.OpenApi) with the 10.0.0 packages → bumped to 10.0.13.
- **Status column pushed off-screen** when the details panel was open → shorter headers, narrower panel.
- **Angular CLI 22 needs Node ≥ 22.22.2**.
