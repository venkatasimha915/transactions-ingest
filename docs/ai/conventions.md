# Conventions and rules applied

Taken from the exercise PDF, plus the conventions the assistant was asked/chose to follow.

## From the exercise
- ASP.NET 10, EF Core code-first, SQLite, `appsettings.json` configuration, logging, error handling, automated tests.
- Logical key `(SourceSystem, TransactionId)`; field types/lengths from the data-model table.
- `POST /api/import` and `GET /api/transactions` must exist.
- No scheduler; the import must be reusable by one without modification.
- Angular (latest) frontend showing Source System, Transaction ID, Location Code, Product Name, Amount, Transaction Time, Status.

## Code conventions
- Layering: `Core` (no framework deps) → `Infrastructure` (EF, I/O) → `Api` (HTTP). Business rules live in pure functions.
- All times UTC; time obtained only through `TimeProvider`.
- Money as `decimal` with 2 dp; never `double`.
- Return DTOs from endpoints, never EF entities. Typed results (`Results<Ok<T>, NotFound>`), ProblemDetails for errors.
- Validate query input; whitelist sort columns; cap page size.
- Every state change writes an audit row; field updates write one row per field with old/new values.
- An unreachable source must never cause revocations.
- Tests use real SQLite in-memory (not the EF InMemory provider) so constraints and converters are exercised.
- Nullable reference types enabled; file-scoped namespaces; primary constructors for DI.
- Angular: standalone components, signals, new control flow (`@if`/`@for`), no UI library.
