# Implementation approach — Northwind Support Desk

This document explains the decisions and checks for the **completed Tasks 1–3** in `ASSIGNMENT_CANDIDATE.md`. **Task 4 was intentionally not implemented** to prioritize functionality, tests, security and a usable submission within the two-day time budget. The project was extended in the existing .NET / EF Core / React structure rather than rebuilt or visually redesigned.

## Task 1 — Diagnose DEFECT-117 and fix SQL filtering

**Reproduction / root cause.** Changing the Status dropdown to *In progress* alone did not issue a fresh ticket API request; typing into Search then sent a request containing the status, and clearing Search could return stale results. The hook used logic distinguishing debounced search from filters that should refetch immediately, but the Status field was omitted from the immediate-filter comparison. That is why Search appeared to fix a status change incidentally.

**Chosen change.** Keep filter state in `useTicketList`; treat each non-search filter as an immediate request dependency, including Status and later `slaStatus`. Debounce only search typing by 300 ms. Invalidate stale responses so an older request cannot overwrite the result of a newer filter. **Rejected:** in-memory filtering, component remount keys, or full-page reloads would hide the symptom and still leave the server/client contract inconsistent.

**Verification.** Frontend hook tests assert the number and arguments of `ticketsApi.getTickets` calls for a Status change without typing. Other tests exercise each immediate filter, rapid search debounce and out-of-order responses. The defect fix and server filtering were committed separately to preserve an understandable history.

**Database filtering design.** In `TicketQueries`, compose typed, optional `Where` expressions over `IQueryable` and execute them before `CountAsync()`, sorting, `Skip()` and `Take()`. Exact filters combine with `AND`; a trimmed term searches title, ticket reference or customer name using an escaped `LIKE` pattern. `AsNoTracking()` and DTO projections avoid loading/tracking entire aggregates for list views. **Rejected:** `ToList` then `Where` in memory, because it would give an incorrect `totalCount`/`totalPages` and scale poorly. The method was left extensible so Task 2 could add just an SLA filter branch, not rewrite the query pipeline.

**How checked.** xUnit SQLite-backed query tests verify priority, combined `AND` criteria, customer-name search, assignee/unassigned cases and filtered count before paging; local SQL Server API requests were also used for a real-database check.

## Task 2 — Automatic triage, escalation and SLA

**Rule placement.** The ticket aggregate owns valid state changes, and application command handlers orchestrate dependencies and transactions. A dedicated `SlaDeadlineCalculator` calculates deadlines from configurable hours, customer tier, multiplier and minimum window; an `AutomaticAgentSelector` chooses a candidate from eligibility/workload data. This keeps rules shared between creation and escalation. **Rejected:** putting category-specific decisions in React or controllers would duplicate business rules and make API behavior inconsistent. Category flags rather than string comparisons handle forced Critical and specialist requirements.

**Create decision.** Use requested priority, or Medium when absent, except where `ForcesCriticalPriority` applies. Calculate due time starting at creation; Premium windows are halved with a one-hour minimum. Query active agents' open-ticket counts (excluding Resolved/Closed), require specialism when indicated, exclude agents at or above capacity, and pick the least loaded, then lowest ID. A missing eligible agent is **not** an exception: return `201`, leave owner null and explain why. The response also includes human-readable priority, SLA and assignment reasons.

**Escalation decision.** Move exactly one priority level; reject Critical, Resolved and Closed tickets with `409`, invalid reason with `400`, and nonexistent tickets with `404`. Recalculate deadline from **the escalation time**. Re-evaluate assignment; the current active/qualified agent may retain their existing ticket at *exactly* capacity, because retaining is not adding new work. If not eligible, select another qualifying agent or leave the ticket unassigned. Domain changes and the new escalation snapshot are saved in one unit of work. After Task 3, the actor comes from the authenticated JWT rather than a user-editable form field.

**Audit design.** `TicketEscalations` is an append-only record of before/after priority, assigned agent and due time, plus reason, actor and time. The ticket FK is restricted, and `IX_TicketEscalations_Ticket_Time_Id` supports newest-first history and the query that obtains the most recent SLA restart time. **Rejected:** keeping only the current ticket values would lose traceability and make audit history impossible.

**Derived SLA status.** Do not persist a status that goes stale as time passes. The evaluator uses an injected clock and current SLA window (ticket creation or latest escalation) to return NotApplicable, Met, Breached, AtRisk (remaining time <= configured percentage, default 25%), or WithinSla. Exact due instant is AtRisk until the deadline is exceeded. SQL uses comparable filter predicates and `DateDiffMillisecond` over the current window *before* paging/counting. The UI reuses a textual SLA indicator in the list and detail view, and provides a typed escalation form/history without a full-page reload.

**How checked.** FixedClock and Moq tests cover deadline combinations, boundaries, priority overrides, specialist/capacity/tie rules, escalation retention/reassignment/history and failures. Vitest/RTL checks short reasons do not call the API, an HTTP 409 is displayed without an optimistic state change, SLA labels remain distinguishable, and changing the SLA filter makes an immediate request. `scripts/verify-t2d.ps1` checks server-side SLA state partitioning/counts; after Task 3 it authenticates first.

## Task 3 — Authentication without rebuilding the agent model

**Credentials and seed approach.** Create one-to-one `AgentCredentials` linked to existing `Agents` by `AgentId` rather than inserting password data into the Agent domain object. Keep historical ticket relationships untouched. Use standard .NET PBKDF2-HMAC-SHA256 with a randomly generated 16-byte salt, 210,000 iterations and a 32-byte derived key; encode the algorithm/parameters alongside the hash. Verify hashes in constant time. A Development-only seeder creates missing credential rows for existing agents without resetting the SQL Server database. Login uses one generic `401` response for missing email, wrong password and inactive agent to avoid account enumeration.

**API security.** Issue signed HS256 JWTs containing agent identity for **60 minutes**. Validate signing key, issuer, audience and lifetime; require the key via `Jwt__Key` process environment, not a checked-in literal. Use an ASP.NET Core authorization **fallback policy** so an endpoint is protected by default; only `[AllowAnonymous]` login (and Development Swagger middleware) are public. An agent's active state is checked during token validation. `GET /api/auth/me` returns the current active identity. The escalation controller takes `escalatedBy` from that identity, disregarding a spoofed request-body value. **Rejected:** decorating only selected controllers with `[Authorize]` risks leaving new endpoints public.

**React client.** A labelled `/login` form validates fields, blocks repeat submissions and shows a generic failure. `RequireAuth` preserves the desired location; a shared API client attaches the bearer token to protected requests and clears the session on `401`. The app shell displays the agent name and logout action. A simple `localStorage` session survives refresh; it is intentionally a **development assessment trade-off**, not a production security recommendation. In production, prefer secure HttpOnly same-site cookies / BFF and stronger session controls.

**Testing / security checks.** Hash/verify success and failure; generic wrong-password 401; protected endpoint 401 without token / succeeds with token; inactive account blocked; frontend route guard, token attachment and invalid-session handling. The local verification script (`scripts/verify-task3-auth.ps1`) checks token-based access and that an attacker-provided `escalatedBy` cannot override the JWT actor. It creates a test ticket and escalation, so it should be run against a disposable Development database.

## Task 4 — Explicitly not completed

The supplied `docker-compose.yml` launches SQL Server for local development. There is **no completed .NET multi-stage Dockerfile**, non-root runtime image, container API/health endpoint, or image-size comparison. These acceptance items must not be claimed as delivered. Implementing them remains a clearly identified next step rather than compromising testing/documentation of Tasks 1–3.

## Patterns and trade-offs worth discussing

1. **Aggregate / domain behavior:** `Ticket` owns status and escalation invariants; external callers cannot quietly bypass them.
2. **Repository + unit of work:** domain-facing data access and one atomic commit for ticket plus history.
3. **CQRS-like separation:** ticket queries project no-tracking DTOs while command handlers change aggregates. This is lightweight and uses the existing codebase rather than adding a message bus or framework.

**Scaling conversation:** with ~10 million tickets, review generated SQL/query plans, supporting indexes, offset paging, wildcard searching and correlated latest-escalation lookups; consider full-text search/materialized reporting and keyset pagination. Avoid claiming the current sample-sized implementation has been load-tested at that scale.

**Security conversation:** the weakest area is token storage in `localStorage` and absent rate limits/session revocation. The fixed local dev password is also intentionally unsuitable for production. Suggested production changes: secure cookie/BFF, rate limiting and monitored lockout, credential lifecycle, key rotation and managed secrets, explicit deployment migrations, secure transport, and audit access controls.

## AI tooling and correction process

ChatGPT assisted with understanding the repository, generating code/test drafts, reasoning about business rules, consolidating edits into file batches and reviewing errors. The code was not assumed correct merely because it was generated: .NET tests/build and React tests/typecheck/lint were run locally; EF migrations were inspected before applying; live API behavior was checked with SQL Server. An early generated escalation test hit **CS8752** (invalid array creation); the code was amended and re-tested. A separate terminal issue (PSReadLine crashing on a long paste) led to shorter verification commands. These are concrete examples of generated output being **corrected**, not blindly accepted. The candidate must still be able to explain the final code and should adapt this section to reflect their personal review actions accurately.

## Delivery evidence to preserve

- Separate Task 1 defect and filtering commits, then Task 2 and Task 3 feature/test commits.
- Successful local results of: `dotnet test`, web `npm run test`, web `typecheck`, web `lint`, and `npm run build` (capture the actual output; do not substitute estimated counts).
- Successful local Task 2/3 authenticated smoke scripts and a manual browser pass for sign-in, filters, create/triage, escalation/history and logout.
- Reviewed EF migration files for `TicketEscalations` and `AgentCredentials`, without resetting the existing database.
- Documentation committed, and the **default `main` branch updated by a normal, non-squash merge** or a clearly provided feature-branch submission URL.

## Time spent on the assignment

| Activity | Approximate time |
|---|---|
| Task 1 – Defect investigation, regression testing, and SQL filtering | 3 hours |
| Task 2 – Automatic triage, SLA, agent assignment, escalation, and frontend integration | 8 hours |
| Task 3 – JWT authentication, secure credentials, login UI, and tests | 4 hours |
| Task 4 – Dockerisation implementation attempt and environment troubleshooting | 1 hours |
| Final testing, debugging, and documentation | 2 hours |
| **Total** | **18 hours** |

All times are approximate estimates of the effort spent on implementation, testing, debugging, and documentation.

Tasks 1–3 were implemented and tested locally. Task 4 was attempted but could not be completed or verified because of a local Docker storage issue. The incomplete Dockerisation changes were not included in the final submission.