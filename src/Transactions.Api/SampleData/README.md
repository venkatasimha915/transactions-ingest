# Mock source-system snapshots

Each file is the full snapshot one source system currently publishes. It is re-read on every import,
so edit it (while the API is running) and click **Run import** again to simulate the source:

| To simulate…               | Do this                                             | Expected result                          |
|----------------------------|-----------------------------------------------------|------------------------------------------|
| A correction               | change `amount` / `productName` of an item          | `Updated` + one audit row per field      |
| A late transaction         | add a new item                                      | `Inserted` (+ `Created` audit)           |
| A removal                  | delete an item that is < 24h old                    | `Revoked` (+ audit)                      |
| A reappearance             | put a revoked item back                             | `Reactivated` (+ audit)                  |
| An outage                  | set `SimulateFailure: true` in appsettings.json     | source skipped, **nothing revoked**      |

`ageMinutes` = minutes before the **start of the current hour**; the transaction time is computed from it
so the demo data always falls inside the 24h window, yet stays identical for all imports within the hour.
An absolute `transactionTime` (as in the exercise sample) can be used instead; the three `100001`
items use the exercise's February 2026 timestamps, so they are imported directly as **Finalized**.

`Online/100001` and `100002` have the 21-character product name from the exercise sample
("USB-C Docking Station"); it is truncated to the 20-character limit with a warning.
