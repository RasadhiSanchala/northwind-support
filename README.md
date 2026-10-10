# Northwind Support Desk

A .NET 10 / SQL Server API and React 19 / TypeScript support-ticket application. This fork implements **Tasks 1–3** of [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md): a filter-reactivity defect fix, SQL-side ticket filtering, automatic triage and escalation, SLA monitoring, and authenticated agent access. **Task 4 (API Dockerisation) was not implemented**; the supplied Compose file starts *SQL Server only*.

## What changed

| Task | Delivered |
| --- | --- |
| **1 — DEFECT-117** | Fixed the ticket-list filter dependency logic so changing a single dropdown/checkbox immediately requests new data, while search text uses a 300 ms debounce. Added a regression test for the outgoing request. |
| **1 — SQL filtering** | Filters for status, priority, category, customer, assignee, unassigned tickets, and trimmed title/reference/customer search. Filters combine with `AND`; SQL filters run before `Count`, sorting, `Skip` and `Take`. |
| **2 — Triage** | Requested priority or Medium by default; categories flagged `ForcesCriticalPriority` become Critical. Configurable SLA hours (Low 72 / Medium 24 / High 8 / Critical 4); Premium multiplier 0.5, with a one-hour floor. Eligible active agents below capacity are ranked by fewest open tickets, then agent ID. Specialist categories require a matching specialization. Creation succeeds unassigned if no agent qualifies, with a reason. |
| **2 — Escalation and SLA** | One priority level per escalation; due date restarts from escalation time; eligible existing agent retained (including at exactly their capacity) or an alternative is selected. Audit history captures old/new priority, assignee, deadline, actor, reason and timestamp. Derived SLA states: NotApplicable, WithinSla, AtRisk, Breached and Met. Includes SQL-side `slaStatus` filtering, list/detail indicators, form and history UI. |
| **3 — Authentication** | Agent email/password login, salted PBKDF2 hashes, JWT bearer authentication with a central fallback authorization policy, `/api/auth/me`, login form, protected React routes, token attachment, logout and expired/invalid-token handling. Escalation audit actor now comes from the authenticated token, not the request body. |

**Defect root cause:** the list hook treated changes to most non-search filters independently, but **omitted Status from the fields that trigger an immediate request**. Changing status alone therefore left the displayed list stale; typing in Search caused a later request carrying that status. The fix tracks **all immediate filter fields** (including the new SLA filter) in one request effect, while debouncing only search edits. See `apps/web/src/features/tickets/hooks/useTicketList.ts` and its regression test.

## Run locally (Windows PowerShell)

Prerequisites: **.NET SDK 10** (`global.json`), **Node.js 22** (`.nvmrc`), npm, and Docker Desktop with Linux containers. From the **repository root**:

```powershell
npm install
npm run db:up
```

Wait for the SQL Server container to be healthy (`docker compose ps`). Generate a local JWT signing key **in the terminal where you will run the API**:

```powershell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$env:Jwt__Key = [Convert]::ToBase64String($bytes)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
npm run api
```

The Development API automatically applies EF Core migrations and seeds sample tickets, agents and **salted password hashes** without wiping existing records. The signing key is **not** stored in source control or automatically loaded from `.env` by `npm run api`; generate/set it again in each new backend shell. Previously issued tokens become invalid if the key changes.

In **another terminal**:

```powershell
npm run web
```

Open **http://localhost:5173**. API: **http://localhost:5043**; Development Swagger: **http://localhost:5043/swagger**. The Vite development server proxies `/api` calls. The Docker Compose setup here contains only the `sqlserver` service, **not** a Dockerised API.

**Development login (local fixture only):**

```text
Email:    alex.turner@northwind-support.example
Password: LocalOnly_Dev_Pass_2026!
```

Other pre-seeded agents have development credentials, but inactive Yuki cannot authenticate. Do not reuse demo credentials or SQL Server's local placeholder password in production. The demo password is present in Development configuration and is **hashed with a fresh salt at seed time**; only the hash is persisted.

### Test and verification commands

```powershell
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj
npm run test --workspace apps/web
npm run typecheck --workspace apps/web
npm run lint --workspace apps/web
npm run build
```

Run against the locally started API (both scripts use the Development fixture; **`verify-task3-auth.ps1` creates a test ticket and an escalation**):

```powershell
.\scripts\verify-task3-auth.ps1
.\scripts\verify-t2d.ps1
```

The updated `verify-t2d.ps1` first authenticates to accommodate Task 3's protected endpoints; it checks every SLA-status filter, paginated result count and overall partition count. Backend tests cover SLA/priority boundaries, agents and escalation rejection/assignment; frontend tests cover filter request behavior, validation and display states. The exact results of a clean checkout should be recorded in the submitted test logs rather than assumed from this README.

For manual verification: sign out, navigate directly to `/tickets/1` and confirm redirect to `/login`; sign in and confirm return to the requested page; change Status without typing in Search; try an SLA filter; create a ticket in Security or Outage; escalate an eligible ticket, inspect the history, and log out. Wrong credentials, missing bearer token, inactive account and invalid escalation should be rejected. **All protected API requests now require the `Authorization: Bearer <token>` header**.

## Design choices, alternatives and why

| Area | Chosen | Alternative rejected / reason |
| --- | --- | --- |
| Domain rules | Ticket aggregate and focused `SlaDeadlineCalculator`, `SlaEvaluator`, `AutomaticAgentSelector` | Duplication in controllers would create inconsistent create/escalate behavior and make boundary tests harder. |
| Reads | EF Core `IQueryable`, `AsNoTracking`, typed DTO projections and correlated latest-escalation subquery | Fetching all tickets then filtering in React/C# breaks correct paging/counts and does not scale. |
| Assignment | Fewest currently open tickets, deterministic lowest-ID tie break | Random/round-robin would ignore real workload and make tests nondeterministic. |
| History | Append-only `TicketEscalations` table with FK to Tickets and `(TicketId, EscalatedAtUtc, Id)` index | Overwriting ticket fields alone loses who escalated and the before/after values. Index supports recent-first per-ticket history and locating the latest escalation for SLA calculations. |
| Credentials | Separate one-to-one `AgentCredentials` table; PBKDF2-HMAC-SHA256 (210,000 iterations; per-user 16-byte salt, 32-byte derived key) | A plaintext/reversible password or a custom digest is unsafe; separate table keeps credentials outside the ticket-assignment agent aggregate. |
| API security | JWT HS256, **60-minute** expiry, issuer/audience/lifetime/signature validation, global fallback policy | Controller-by-controller authorization can accidentally leave an endpoint public. Login is explicitly anonymous; Swagger is exposed in Development. |
| Browser session | `localStorage` token with central fetch wrapper and 401-triggered clear | A cookie/BFF approach is preferable in production, but out of scope for the two-day exercise. `localStorage` is accessible to injected JavaScript (XSS risk). |

**Database additions:** EF Core migrations add `TicketEscalations` (restricted ticket FK, one-level priority constraint, per-ticket chronology index) and `AgentCredentials` (agent PK/FK, restricted deletion). These preserve existing tickets. Do **not** run `npm run db:reset` on data you need. In a production environment, migrations should run as an explicitly managed deployment job rather than automatically during API startup.

## Known limitations / trade-offs

- **Task 4 is not finished:** no multi-stage API Dockerfile, containerized API, non-root image, `localhost:5080` endpoint or public `/health` endpoint. Docker Compose runs local SQL Server only.
- Demo password and SQL Server local credentials are for **development only**. This exercise does not implement password reset/rotation, rate limiting, account lockout, MFA, roles or token revocation/refresh.
- Logout removes the browser token, but an already issued JWT cannot be explicitly revoked before expiry except through deactivating the agent or changing the signing key. Tokens in localStorage are exposed to XSS; use HttpOnly secure same-site cookies or a backend-for-frontend for production.
- A single-instance ticket reference generator based on the latest ID is not safe under concurrent writers; replace it with a database sequence/unique generation strategy in production.
- Substring `LIKE '%term%'` queries and offset pagination are adequate for the sample dataset but would need indexing/search changes, keyset pagination and query-plan review for millions of tickets.
- No registration, OAuth/SSO, notifications, CI, caching, report stretch goal, or visual redesign; those are outside the specified Tasks 1–3.

## AI-assisted workflow and delivery

ChatGPT was used to explore the existing code, propose rule boundaries, draft multi-file implementation/tests, review compiler and migration errors, and prepare documentation. A useful AI-proposed approach **kept after review** was the shared SLA calculator with rules read from the existing `Sla` configuration; it supports create and escalate without copying constants. The work was verified in the local environment with builds, tests and live API exercises rather than treating generated code as correct by default. **An initial generated escalation test used an invalid array construction (`CS8752`); it was corrected after the .NET compiler detected it.** The candidate should be prepared to explain and defend the final code, including the choices and limitations above. See [`APPROACH.md`](APPROACH.md) for the task-by-task reasoning and verification steps.
