# IdentityHub — NHI Management Platform (PoC)

IdentityHub is a Non-Human Identity (NHI) management platform. Organizations use it to track and manage service accounts, API keys, service principals, and other machine identities across cloud environments. This proof-of-concept lets organizations rapidly create Jira tickets when identity-related issues are discovered — e.g., stale service accounts or overprivileged keys.

---

## Contents

- [Quick Start](#-quick-start)
- [Architecture & Tech Stack](#-architecture--tech-stack)
- [Design Decisions & Assumptions](#-design-decisions--assumptions)
- [Detailed Setup](#-detailed-setup)
- [REST API Usage](#-rest-api-usage)
- [Bonus: NHI Blog Digest Worker](#-bonus-nhi-blog-digest-worker)
- [Known Limitations](#-known-limitations)

---

## 🚀 Quick Start

**Prerequisites:**
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/)
- An [Atlassian OAuth 2.0 (3LO) app](https://developer.atlassian.com/cloud/jira/platform/oauth-2-3lo-apps/)
- PowerShell, to run the seed script. Windows has this built in; on macOS/Linux install [PowerShell (pwsh)](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell) first.

**Steps:**

1. Clone the repo.
2. Configure backend secrets ([details](#1-configure-backend-secrets)).
3. Seed the database: from `IdentityHub/src/JiraIntegration.Server`, run `./reset-demo.ps1` ([details](#2-seed-the-database)). Requires the EF Core CLI: `dotnet tool install --global dotnet-ef`.
4. Start the API: `dotnet run` (from `IdentityHub/src/JiraIntegration.Server`) → `localhost:5282`.
5. Start the Angular portal: `npm install && npm start` (from `IdentityHub/src/JiraIntegration.Client`) → `localhost:4200`.
6. Log in with a seeded demo account — see [credentials](#2-seed-the-database).
7. Connect your Jira workspace, select a project, and create a ticket from the dashboard.
8. Generate an API key for the project in the UI, then call `POST /api/v1/nhi-findings` to test programmatic ticket creation ([details](#-rest-api-usage)).

---

## 🏗️ Architecture & Tech Stack

The application is divided into three functional boundaries, with a strict separation between UI and backend layers:

1. **`JiraIntegration.Client` (Frontend UI)** — Angular. A lightweight client responsible only for presentation and state management.
2. **`JiraIntegration.Server` (Backend API)** — C# .NET Core 9. Handles core business logic, secure session management, multi-tenancy enforcement, and Atlassian API orchestration.
3. **`BlogScanner.Worker` (Bonus Automation)** — Python. An external scheduled task that polls the Oasis blog, generates an AI-powered summary, and pushes the payload to the API via REST.

---

## 🧠 Design Decisions & Assumptions

### 1. Database: SQLite over PostgreSQL
The backend uses a local **SQLite** database via EF Core. SQLite requires zero external dependencies or Docker configuration for the reviewer. Because the data access layer is abstracted behind EF Core, swapping to PostgreSQL for production is a provider change, not a rewrite.

### 2. Jira Integration: Atlassian OAuth 2.0 (3LO)
The app implements the full Atlassian OAuth 2.0 App-to-App authorization flow so each user connects their own isolated Jira workspace:
- A cryptographically secure `state` parameter prevents CSRF during the redirect.
- The API queries Atlassian's `accessible-resources` endpoint to discover the user's `CloudId` for API routing.

### 3. Credential Management
Sensitive configuration (JWT signing keys, Atlassian OAuth credentials) is kept out of source control via `.NET User Secrets` locally; in production this would move to a managed secret store (e.g., Azure Key Vault). All Atlassian access/refresh tokens are encrypted at the application layer (`TokenEncryptionService`, via ASP.NET Data Protection) before being persisted to SQLite.

### 4. Scope Assumptions
- **Issue types:** ticket creation defaults to standard `Task` issues, for maximum compatibility across customized Jira tenants.
- **Authentication:** user sign-in uses **OpenIddict** and **ASP.NET Core Identity** (JWT access tokens, HttpOnly refresh cookies, token revocation) so the PoC does not depend on a third-party IdP. The UI relies on seeded demo users rather than self-service registration, to keep scope at MVP.
- **Jira workspaces:** each user may connect at most one Jira workspace — enough to demonstrate the OAuth flow and ticket-creation path without multi-tenant UI routing overhead.

### 5. Ticket History: Local Ledger over Jira JQL
Tickets created through the app are persisted in a local ledger and listed from there, instead of querying Jira via JQL on every page load.
- **Performance & resilience:** no Atlassian network round-trip on page load; the dashboard stays usable even if the Jira API is degraded.
- **Scope accuracy:** the requirement was tickets *"created from this app,"* and a local ledger guarantees exactly that, with no risk of picking up manually-labeled Jira issues.
- **Isolation:** ledger records are scoped per `UserId`, so concurrent users don't interfere with each other.

### 6. REST API: Per-Project API Keys
External callers authenticate via an `X-Api-Key` header, scoped to a specific user and Jira project. Plaintext keys are never stored — only a SHA-256 hash. Before creating a ticket, the backend resolves the key to its owning user and confirms that user still has an active Atlassian OAuth token, so API access never exceeds the underlying human user's permissions.

### 7. BlogScanner: Stateless Execution
The Python worker always processes the latest blog post on each run, with no duplicate-post tracking. This keeps the worker focused on summarization and API integration for a clean one-shot demo; see [Known Limitations](#-known-limitations) for the production implication.

---

## 🛠️ Detailed Setup

### 1. Configure Backend Secrets

From `IdentityHub/src/JiraIntegration.Server`, store secrets outside the project folder:

**JWT signing key** (minimum 32 characters):
```bash
dotnet user-secrets set "Jwt:Secret" "DevOnlySecretKey_ChangeInProduction_Min32Chars!"
```

**Atlassian OAuth credentials** — create an OAuth 2.0 (3LO) app with callback URL `http://localhost:5282/api/jira/callback` and scopes `read:jira-work`, `read:jira-user`, `write:jira-work`:
```bash
dotnet user-secrets set "Atlassian:ClientId" "<your-atlassian-client-id>"
dotnet user-secrets set "Atlassian:ClientSecret" "<your-atlassian-client-secret>"
```

### 2. Seed the Database

Make sure the API isn't running, then from `IdentityHub/src/JiraIntegration.Server`:

```powershell
./reset-demo.ps1
```

Requires the EF Core CLI tools: `dotnet tool install --global dotnet-ef`.

This creates `oasis.db` and seeds two isolated demo accounts:

| Username | Password |
|---|---|
| `demo` | `Demo123!` |
| `testuser` | `Test123!` |

> These are local, PoC-only credentials with no real data behind them — don't reuse this pattern past the demo.

### 3. Run the Application

**Backend API:**
```bash
cd IdentityHub/src/JiraIntegration.Server
dotnet run
```
Runs on `localhost:5282`.

**Frontend Angular client:**
```bash
cd IdentityHub/src/JiraIntegration.Client
npm install
npm start
```
Runs on `localhost:4200`.

---

## 📡 REST API Usage

After connecting Jira and generating an API key in the UI, external systems (e.g., CI/CD pipelines) can create tickets programmatically:

```bash
curl -X POST http://localhost:5282/api/v1/nhi-findings \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ih-your-api-key-here" \
  -d '{"projectKey":"KAN","title":"Stale service account","description":"Service account inactive for 90+ days."}'
```

---

## 🤖 Bonus: NHI Blog Digest Worker

A stateless Python script that polls the Oasis blog, summarizes new posts with Google Gemini, and pushes the finding to the REST API above.

1. Copy `.env.example` to `.env` in `IdentityHub/src/BlogScanner.Worker`.
2. Populate `API_KEY` (generated from the UI), your target `PROJECT_KEY`, and `GEMINI_API_KEY`.
3. Run it:
   ```bash
   cd IdentityHub/src/BlogScanner.Worker
   pip install -r requirements.txt
   python main.py
   ```

This can be wired to Windows Task Scheduler, a cron job, or GitHub Actions for scheduled execution — see [Known Limitations](#-known-limitations) before scheduling it to run repeatedly.

---

## ⚠️ Known Limitations

These are deliberate scope cuts for the PoC, not oversights:

- **Single Jira workspace per user** — sufficient to demonstrate the OAuth flow; multi-workspace support would need additional UI routing.
- **No reconciliation with Jira deletions** — tickets deleted natively in Jira aren't removed from the local ledger. In production this would be handled via Atlassian webhooks.
- **BlogScanner has no duplicate-post tracking** — every run re-processes the latest post. Running it on a recurring schedule (cron, Task Scheduler, GitHub Actions) as-is will create duplicate tickets; a production version would persist the last-seen post ID/timestamp.
- **No self-service registration** — only the two seeded demo accounts exist; there's no sign-up flow.