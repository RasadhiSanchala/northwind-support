# Task 3 — Agent JWT Authentication

This is an additive **full-file replacement package** for branch `feature/task-2-ticket-triage` *after* Task 2-D was pushed. It does not include generated EF migrations; those MUST be produced locally and reviewed for destructive operations.

## Design decisions

- `AgentCredentials` linked one-to-one with the existing `Agents` table, nullable legacy data unnecessary. Old tickets and assignments untouched. EF Core migration must create only that table.
- PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte random salt, 32-byte derived key; per-user salt and constant-time comparison. Do not use in production without reviewing password hashing policy, lockout/rate limiting and password rotation.
- Development seeder runs *after* the existing ticket seeder even when tickets already exist, creating missing password hashes only. Only used in Development.
- Local development demo credentials: `alex.turner@northwind-support.example` / `LocalOnly_Dev_Pass_2026!`. Other agents use the same fixture password, but inactive Yuki cannot log in.
- JWT lifetime 60 minutes; signing key only via `Jwt__Key` environment variable (no key committed), HMAC-SHA256, issuer/audience/time validation, 0 clock skew; revoked by deactivating the corresponding Agent row.
- ASP.NET Core fallback authorization policy centrally protects every controller endpoint except `[AllowAnonymous]` on `/api/auth/login`. Swagger middleware remains public in Development. **Task 4 will add explicit public `/health`.**
- Escalation request ignores any supplied `escalatedBy`; controller gets authenticated agent email from JWT claims. History retains the actor identity.
- Browser stores token and limited user metadata in `localStorage`: survives refresh and is simple for this assessment, but is accessible after XSS. Recommend secure HttpOnly same-site cookies or a BFF in production.
- Logout is client-side token deletion; there is no revocation list or refresh tokens. Avoid adding these out-of-scope features.

## 1. Applying the ZIP

Stop `SupportDesk.Presentation` before copying files. Copy all relative paths from ZIP into repo root, preserving structure. Back up/check `git status --short` first. New C# and TS files are complete; modified files are complete replacements.

## 2. JWT signing key (must be set before EF commands or running API)

In the same PowerShell terminal where you plan to run `npm run api` or `npm run ef`:

```powershell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$env:Jwt__Key = [Convert]::ToBase64String($bytes)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

Keep the key private. If you regenerate it after restart, previously-issued JWTs will become invalid (log in again). `.env` is not loaded automatically by `npm run api`. Do not commit `Jwt__Key` or `.env`.

## 3. Build and test BEFORE migration

```powershell
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --verbosity normal
if ($LASTEXITCODE -ne 0) { throw 'Backend unit tests failed' }
dotnet build apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'API build failed' }
npm run test --workspace apps/web
if ($LASTEXITCODE -ne 0) { throw 'Web tests failed' }
npm run typecheck --workspace apps/web
if ($LASTEXITCODE -ne 0) { throw 'Typecheck failed' }
npm run lint --workspace apps/web
if ($LASTEXITCODE -ne 0) { throw 'Lint failed' }
```

**Important:** There is no .NET SDK in the package-building environment. Unit tests/build MUST be verified on your Windows machine. Fix any compile/test errors before proceeding.

## 4. Generate safe EF Core migration

```powershell
npm run ef -- migrations add AddAgentCredentials --output-dir Data/Migrations
```

Use `--output-dir Data/Migrations` so the new migration and model snapshot remain beside the existing migrations. Inspect `Up()` and `Down()` in the new `AddAgentCredentials.cs`. Only new table `AgentCredentials`, its PK, FK and related index are expected. **STOP if migrations contain `DropTable` in `Up()`, `AlterColumn`, `DropColumn`, changes to existing tables, or data deletion. Do NOT reset the database.**

If safe:

```powershell
npm run db:migrate
```

## 5. Run and live verify

Start API in the same terminal with `$env:Jwt__Key`: `npm run api`. This applies migration and hashed demo credentials on startup in Development.

Another terminal:

```powershell
.\scripts\verify-task3-auth.ps1
```

Script verifies missing token 401, incorrect password 401, inactive agent 401, successful login+`/me`, protected API 200 and escalation actor from token. It also creates a real ticket and one escalation in your SQL Server dev database. Start React in a different terminal: `npm run web`; visit `http://localhost:5173/tickets` and confirm redirect to `/login`, sign in, logout and return-to-page behavior.

## 6. Git commit suggestions (only after verification)

**Backend:**

```powershell
git add -- Directory.Packages.props .env.example apps/api/src/SupportDesk.Infrastructure/Data/AgentCredential.cs apps/api/src/SupportDesk.Infrastructure/Data/Configurations/AgentCredentialConfiguration.cs apps/api/src/SupportDesk.Infrastructure/Data/DevelopmentCredentialSeeder.cs apps/api/src/SupportDesk.Infrastructure/Services/Pbkdf2PasswordService.cs apps/api/src/SupportDesk.Infrastructure/Services/AgentAuthenticationService.cs apps/api/src/SupportDesk.Infrastructure/DependencyInjection.cs apps/api/src/SupportDesk.Infrastructure/Data/Migrations apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/Program.cs apps/api/src/SupportDesk.Presentation/Controllers/AuthController.cs apps/api/src/SupportDesk.Presentation/Controllers/TicketsController.cs apps/api/src/SupportDesk.Presentation/Auth/JwtTokenService.cs apps/api/src/SupportDesk.Presentation/Extensions/WebApplicationExtensions.cs apps/api/src/SupportDesk.Presentation/appsettings.json apps/api/src/SupportDesk.Presentation/appsettings.Development.json

git diff --cached --check
git diff --cached --stat
git commit -m "feat(api): add agent password authentication and protected JWT endpoints"
```

**Frontend:**

```powershell
git add -- apps/web/src/api/client.ts apps/web/src/api/auth.ts apps/web/src/features/auth apps/web/src/App.tsx apps/web/src/routes.tsx apps/web/src/main.tsx apps/web/src/types/api.ts apps/web/src/features/tickets/components/EscalationPanel.tsx apps/web/src/features/tickets/components/EscalationPanel.test.tsx
git diff --cached --check
git commit -m "feat(web): add agent login, guarded routes and authenticated escalation"
```

**Tests and guide:**

```powershell
git add -- apps/api/tests/SupportDesk.UnitTests/Infrastructure/Services scripts/verify-task3-auth.ps1 TASK3_AUTH_GUIDE.md
git diff --cached --check
git commit -m "test(auth): cover password verification and secure agent login"

git status --short
git push origin feature/task-2-ticket-triage
```

## 7. Submit README / APPROACH.md later

Record chosen approach, credentials, token lifetime, localStorage XSS trade-off, and missing rate limiting/lockout/revocation. The assignment asks for honest AI usage and actual test evidence.
