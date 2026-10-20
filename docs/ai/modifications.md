# Modifications to AI-generated output

## Made during the AI session (after verification failed or review found an issue)
1. Replaced a `static SemaphoreSlim` run-lock with an injected singleton `ImportRunLock` after a parallel test hit 409.
2. Bumped all Microsoft packages from 10.0.0 to 10.0.13 and test packages to current versions to remove NU1903 vulnerability warnings.
3. Changed a collection-expression assertion to `new[] { â€¦ }` to avoid overload ambiguity in xUnit.
4. Added truncation warnings to `SnapshotNormalizer` (originally truncated silently).
5. Replaced the hand-registered `IEnumerable<ITransactionSource>` with one registration per configured source.
6. Shortened table headers / narrowed the details panel so the Status column stays visible.
7. Raised the Angular component-style budget from 4 kB to 8 kB.

## Made by me after reviewing
<!-- Fill this in honestly before submitting. Examples:
- Changed X because Y.
- Rewrote / simplified Z.
- Verified the app on my machine (OS, .NET version, Node version).
If you changed nothing, say "Reviewed and ran locally; no code changes." -->
- Reviewed the generated code and ran it locally on Windows (.NET SDK 10.0.402, Node v24.22.0): all 27 backend tests passed; verified import, idempotent re-import and the audit panel in the browser. No code changes after review.
- Asked the AI for a more colourful UI after the first version; applied its updated styles and verified them locally in the browser.
