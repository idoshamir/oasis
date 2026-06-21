# IdentityHub - NHI Management Platform (PoC)

IdentityHub is a Non-Human Identity (NHI) management platform. Organizations use our product to track and manage their service accounts, API keys, service principals, and other machine identities across cloud environments. This proof-of-concept enables organizations to rapidly create Jira tickets when identity-related issues (e.g., stale service accounts, overprivileged keys) are discovered.

## 🏗️ Architecture & Tech Stack

To ensure a clear separation between UI and backend layers, the application is divided into three functionally named boundaries:

1. **`JiraIntegration.Client` (Frontend UI):** Built with Angular. Acts as a lightweight client responsible only for presentation and user experience.
2. **`JiraIntegration.Server` (Backend API):** Built with C# .NET Core. Handles all core business logic, secure session management, multi-tenancy enforcement, and third-party API orchestration.
3. **`BlogScanner.Worker` (Bonus Automation):** Built with Python. An external scheduled task that polls for the most recent blog post, generates an AI-powered summary, and pushes the payload to the API via REST.

## 🧠 Design Decisions & Assumptions

### 1. Database Selection: SQLite over PostgreSQL
**Decision:** The backend utilizes a local **SQLite** database via Entity Framework (EF) Core.
**Reasoning:** The primary technical requirement mandated that the solution be runnable in the easiest, most frictionless way possible. SQLite requires zero external dependencies, no Docker daemon, and no container configuration to run locally. Because the data access layer is abstracted behind EF Core, migrating to PostgreSQL in a production environment simply requires swapping the database provider.

### 2. Jira Integration & Atlassian OAuth 2.0 (3LO)
**Decision:** The application implements the complete Atlassian OAuth 2.0 App-to-App authorization flow rather than relying on basic API tokens.
**Reasoning:** To properly handle multi-tenancy, users must be able to securely connect their own isolated Jira workspaces.
* The backend generates a cryptographically secure `state` parameter to prevent CSRF attacks during the redirect.
* Upon callback, the API exchanges the authorization code for an `access_token` and `refresh_token`.
* The API dynamically queries Atlassian's `accessible-resources` endpoint to discover the user's specific `CloudId` required for API routing.

### 3. Security Practices: Credential Management
**Decision:** Sensitive configuration (JWT signing keys, Atlassian OAuth app credentials) is never committed to source control. In local development, [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) store these values outside the project directory.
**Reasoning:** As an NHI security tool, the platform must practice defense-in-depth and follow secure coding standards. All sensitive third-party credentials (Access Tokens, Refresh Tokens) are encrypted at the application layer before being persisted to the database. *(Note: In a production environment, application secrets would be supplied via environment variables or a managed service like AWS Secrets Manager or Azure Key Vault.)*

### 4. Scope & Feature Assumptions
* **Issue Types:** The Jira integration defaults to creating standard `Task` issue types to ensure maximum compatibility across different users' custom Jira configurations.
* **Authentication:** A lightweight, local salted/hashed authentication system is implemented to demonstrate secure session management without requiring the configuration of a third-party IdP just to run the application.

### 5. User Registration: Login Only
**Decision:** The UI provides login but no self-service registration flow.
**Reasoning:** A Register button and sign-up flow were considered, but they fall outside the scope of the assignment and were not required for the PoC. Building registration would add time without advancing the core Jira integration goals.

### 6. Jira Workspace Limit: One per User
**Decision:** Each user may connect at most one Jira workspace.
**Reasoning:** Allowing multiple Jira workspaces per user was considered for flexibility, but for this PoC one workspace is sufficient to demonstrate the OAuth flow and ticket-creation path. Multi-workspace support can be added later if needed.

### 7. Ticket History: Local DB over Jira-Only Query
**Decision:** Tickets created through the app are persisted in the local database and listed from there, rather than querying Jira on every request as the sole source of truth.
**Reasoning:** The assignment requires displaying only tickets *"that were created from this app."* Two approaches were considered:

* **Jira as source of truth:** Apply a dedicated label (or a hidden JSON property on the issue) and fetch matching issues via JQL. This is not significantly more complex—it is a tradeoff, not a capability gap. A hidden JSON property would also address the label-spoofing concern below.
* **Local DB (chosen):** Record each created ticket in SQLite after a successful Jira API call.

Local persistence was chosen for simplicity and because it better fits the PoC constraints:

* **Performance:** Listing tickets from the DB is faster than round-tripping to the Jira API on every page load.
* **Resilience:** The dashboard can still show created tickets when Jira is temporarily unavailable.
* **Accuracy:** A visible label can be applied manually by users in Jira, which would pollute a label-based filter. Jira also offers eventual consistency—records arrive, but not always immediately—so a JQL-based list may lag briefly after creation.
* **Downside — stale records:** Tickets deleted in Jira are not removed from the local DB, so they continue to appear in the UI until reconciliation is implemented (e.g. periodic Jira sync or webhooks).

**User separation:** When two users connect to the same Jira workspace and share a project, tickets created by one user are not shown to the other because records are scoped per app user in the DB. That aligns with the assignment's multi-tenant intent, though the exact interpretation of *"users separation"* is somewhat open to debate.

### 8. REST API Authentication: Per-Project API Keys
**Decision:** External callers authenticate with an API key scoped to a specific user and Jira project. Keys are generated per project, hashed at rest in the database, and passed on each request via the `X-Api-Key` header.
**Reasoning:** The assignment requires programmatic ticket creation via REST, with an API key tied to a user and a project key supplied as part of the request. Before creating a ticket, the backend resolves the key to its owning user and verifies that user still has write permission to the target project in Jira (returning `403` if not).

* **One key per project:** Each user can hold at most one active key per Jira project. Regenerating revokes the previous key for that project and issues a new one.
* **Secure storage:** Plaintext keys are never persisted—only a one-way hash is stored. The full key is returned once at generation/regeneration time.
* **UI management:** The dashboard exposes **Generate**, **Regenerate**, and **Copy** controls so users can create and retrieve keys without leaving the portal.
* **Request scoping:** The `POST /api/v1/nhi-findings` body includes `ProjectKey`, `Title`, and `Description`. If the API key is bound to a project, the request's `ProjectKey` must match; otherwise the key's scoped project is used.

## Security notes

- Jira OAuth tokens are encrypted at rest via ASP.NET Data Protection (`TokenEncryptionService`).
- Data Protection keys are persisted locally for development; on Windows they are encrypted at rest with DPAPI. In production on Linux or containers, keys would be protected with a certificate or a managed store such as Azure Key Vault.
- Application secrets (JWT signing key, Atlassian OAuth credentials) are supplied via user-secrets or environment variables and are not committed to source control.
- Local runtime artifacts (`oasis.db`, `data-protection-keys/`) are gitignored and should be omitted from any submission archive.

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the Angular frontend)
- An [Atlassian OAuth 2.0 (3LO) app](https://developer.atlassian.com/cloud/jira/platform/oauth-2-3lo-apps/) (only required for Jira connect)

### Quick demo path

1. Clone the repo and configure backend secrets (JWT + Atlassian OAuth).
2. Run `.\reset-demo.ps1` in `IdentityHub/src/JiraIntegration.Server` to create a clean database with test users (see below).
3. Start the API (`dotnet run` in `IdentityHub/src/JiraIntegration.Server`).
4. Start the Angular portal and log in with **demo** / **Demo123!** or **testuser** / **Test123!**.
5. Connect your Jira workspace, select a project, and create a ticket from the dashboard.
6. Generate an API key for the project and call `POST /api/v1/nhi-findings` (see example below).
7. *(Optional)* Run the BlogScanner worker to create a ticket from a blog post via the REST API.

### 1. Clone the repository

```bash
git clone https://github.com/idoshamir/oasis.git
cd oasis
```

### 2. Configure backend secrets (required)

The backend stores secrets in [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during local development. These are loaded automatically when `ASPNETCORE_ENVIRONMENT=Development` and never live in the repo or project folder.

From the backend project directory:

```bash
cd IdentityHub/src/JiraIntegration.Server
```

Set the required secrets (run each command once per machine):

**JWT signing key** — signs and validates login JWTs (minimum 32 characters):

```bash
dotnet user-secrets set "Jwt:Secret" "DevOnlySecretKey_ChangeInProduction_Min32Chars!"
```

**Atlassian OAuth credentials** — create an [OAuth 2.0 (3LO) app](https://developer.atlassian.com/console/myapps) in the Atlassian developer console, then:

1. Under **Authorization -> Add**, set the callback URL to `http://localhost:5282/api/jira/callback` (use your API server host and port).
2. Under **Jira API**, add scopes: `read:jira-work`, `read:jira-user`, `write:jira-work`.
3. Copy the **Client ID** and **Client Secret** from the app.

```bash
dotnet user-secrets set "Atlassian:ClientId" "<your-atlassian-client-id>"
dotnet user-secrets set "Atlassian:ClientSecret" "<your-atlassian-client-secret>"
```

| Secret | Purpose | Required for |
|--------|---------|--------------|
| `Jwt:Secret` | Signs session tokens after login | All authenticated API usage |
| `Atlassian:ClientId` | OAuth app identifier | Connecting Jira workspaces |
| `Atlassian:ClientSecret` | OAuth app secret | Connecting Jira workspaces |

Non-secret settings (redirect URLs, CORS origins, JWT issuer/audience) remain in `appsettings.json`.

**Production:** use environment variables instead (`Jwt__Secret`, `Atlassian__ClientId`, `Atlassian__ClientSecret`).

**Verify secrets are set:**

```bash
dotnet user-secrets list
```

### 3. Seed the first login user (required)

The UI is login-only (no self-service registration). Use the included script to create a fresh SQLite database with a demo user.

**Stop the API first** if it is already running — SQLite cannot be reset while the database file is locked.

```powershell
cd IdentityHub/src/JiraIntegration.Server
.\reset-demo.ps1
```

| Username | Password |
|---|---|
| `demo` | `Demo123!` |
| `testuser` | `Test123!` |

Requires the [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

The script deletes the local database, reapplies EF Core migrations, and applies `demo.sql` to insert these users with no Jira connection, API keys, or ticket history. You can run it again at any time to wipe local data back to this state.

### 4. Start the Backend API

```bash
dotnet run
```

The SQLite database is created and migrated automatically on first startup.

Default URLs:
- API: `http://localhost:5282`
- Jira OAuth callback: `http://localhost:5282/api/jira/callback` (must match your Atlassian app settings)

### 5. Start the Frontend Portal

```bash
cd ../JiraIntegration.Client
npm install
npm start
```

The UI runs at `http://localhost:4200`. Log in with one of the seeded accounts above (`demo` / `Demo123!` or `testuser` / `Test123!`).

### 6. REST API: create a ticket programmatically

After connecting Jira and generating an API key for a project in the dashboard, external callers can create tickets via:

```bash
curl -X POST http://localhost:5282/api/v1/nhi-findings \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ih-your-api-key-here" \
  -d '{"projectKey":"KAN","title":"Stale service account","description":"Service account inactive for 90+ days."}'
```

Returns `201 Created` with the Jira issue key on success. The user who owns the API key must have an active Jira connection with write access to the target project.

### 7. (Optional) BlogScanner Worker

The worker polls a blog feed, summarizes the latest post with Google Gemini, and posts an NHI finding to the REST API.

1. Complete steps 1–5 above and connect Jira for the seeded user.
2. Copy `.env.example` to `.env` in `IdentityHub/src/BlogScanner.Worker`.
3. Set `API_KEY` to a key from the dashboard.
4. Set `PROJECT_KEY` to your Jira project key and `GEMINI_API_KEY` from [Google AI Studio](https://aistudio.google.com/apikey).
5. Install dependencies and run:

```bash
cd IdentityHub/src/BlogScanner.Worker
pip install -r requirements.txt
python main.py
```

Ensure `JiraIntegration.Server` is running before starting the worker.
