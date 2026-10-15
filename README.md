# B Moni peer lending demo

A sandbox-first API for a student microfinance demo. The lending ledger is simulated and keeps every deposit, loan contribution, purchase, disbursement, repayment, and lender return behind an explicit approval. BMONI's REST integration is available for sandbox onboarding, account-name checks, wallet reads, and separately approved sandbox transfers.

## Run locally

Requirements: .NET 8 SDK. The API uses SQLite by default and applies EF Core migrations plus idempotent demo bootstrap on startup.

1. Copy `.env.example` as a reference. The API does **not** load `.env` files; set variables in your shell, .NET User Secrets, or your deployment service.
2. Set `JWT_SECRET` to a random value with at least 32 bytes and `DEMO_USER_PASSWORD` to a strong demo password.
3. Start the API:

   ```powershell
   dotnet run --project src/BMoni.Api
   ```

4. Open `/swagger` on the address printed by `dotnet run`. `GET /api/health` checks liveness and `GET /api/bootstrap` returns the sandbox demo login details.

The seeded users are Tolu (`tolu@bmoni.demo`), Ada (`ada@bmoni.demo`), and Kunle (`kunle@bmoni.demo`). The bootstrap endpoint returns the configured demo password. Use only for a private hackathon demo; do not expose seeded demo credentials as a public production login.

`BMoni.http` contains a sample request sequence. Run through loan creation, assessment, lender contributions, borrower acceptance, approved spend, repayment, and lender returns. The API creates a pending approval before money moves; confirm or reject it explicitly.

## Configuration

Set secrets as environment variables or in Render's service **Environment** settings. Do not commit real values to `appsettings.json`, `.env`, or source control. Render injects configured environment variables into the running service.

| Variable | Purpose |
| --- | --- |
| `JWT_SECRET` | Required; random secret of at least 32 bytes for access tokens. |
| `DEMO_USER_PASSWORD` | Required for seeded demo accounts. |
| `Database__Provider` | `Sqlite` (default) or `PostgreSql`. |
| `DATABASE_URL` | SQLite connection string locally, or PostgreSQL URL/connection string. |
| `ALLOWED_ORIGINS` | Comma-separated browser origins; leave unset if no browser client is used. |
| `BMONI_BASE_URL` | Defaults to `https://embedded-dev.bmoni.com`. |
| `BMONI_API_KEY` | BMONI REST API key for sandbox requests. |
| `BMONI_MASTER_KEY` | Signer-only 32-byte hex encryption key for generated owner keys at rest. Generate a random key; this is not issued by BMONI and is not needed by the API service. |
| `BMONI_WEBHOOK_SECRET` | Webhook signing secret from the BMONI webhook configuration. |
| `BMONI_MCP_KEY` | Reserved for MCP access; this API currently uses the REST API, not MCP. |
| `ANTHROPIC_API_KEY` | Optional; enables advisory scam-check and assistant calls. |
| `ANTHROPIC_MODEL` | Optional; model name, default `claude-sonnet-5`. |
| `SIGNER_URL` | Optional signer service URL; default `http://localhost:8090`. |
| `SIGNER_SECRET` | Shared secret for the signer service. |
| `SIGNER_KEYS_PATH` | Signer-only encrypted key file path (signer service setting). |

The API accepts Render's `postgresql://`/`postgres://` `DATABASE_URL`. For Render PostgreSQL, set `Database__Provider=PostgreSql` and use the database's **Internal Database URL** when the API is in the same Render region. Render's attached disk is needed only if SQLite is used; a managed PostgreSQL database is preferred for a deployed demo.

The signer is a separate Python service in `signer/`. Its `BMONI_MASTER_KEY` and `SIGNER_SECRET` must be configured in that service, and its encrypted key file should live on restricted persistent storage. The .NET API needs the signer URL and matching `SIGNER_SECRET` only if signer routes are used. Never log or commit private keys or secrets.

## BMONI sandbox setup

The BMONI API key is sufficient to exercise REST operations allowed for the sandbox account. Configure it as `BMONI_API_KEY` in the API service environment; no credential needs to be placed in `appsettings.json`. In BMONI, create a partner-scoped webhook subscription with `POST /v1/webhooks/config`, your public callback URL (`https://<your-render-service>/webhooks/bmoni`), and `partnerId`. Subscribe to `onboarding.completed`, `onboarding.failed`, `kyc.action_required`, and the partner-scoped money events `employee.deposit.completed` and `employee.withdrawal.completed`. Save the returned `secretKey` immediately as `BMONI_WEBHOOK_SECRET` in Render. The receiver verifies the raw request body with HMAC-SHA256, compares the event ID header, deduplicates IDs, and queues a small subset of event fields for processing. Onboarding events update local onboarding status; money events are only recorded and deduplicated, and do not change the simulated lending ledger.

Nigeria sandbox onboarding requires BMONI's test personas and sandbox documents. See [docs/demo-data.md](docs/demo-data.md) for persona mapping and manual sandbox funding details. Real KYC documents and real-money transfers are outside this demo. Peer lending balances remain simulated even when BMONI wallets are linked.

## Deploy to Render

1. Create a PostgreSQL database and a Web Service from this repository using the `Dockerfile` at the repository root.
2. Set `Database__Provider=PostgreSql`, `DATABASE_URL` to the database's internal URL, `JWT_SECRET`, and `DEMO_USER_PASSWORD` in the web service environment.
3. Add `BMONI_API_KEY` and `BMONI_WEBHOOK_SECRET` only when those sandbox integrations are configured. Add Anthropic and signer settings only if using those optional services.
4. Deploy and check `/health`, then `/swagger`. Configure the BMONI webhook after the public service URL is available.

Startup applies migrations and seeds demo users, so the database user must be allowed to create/update schema. Use PostgreSQL for Render; the default SQLite file is ephemeral unless a persistent disk is mounted.

## Checks

```powershell
dotnet test BMoni.sln
python -m pip install -r signer/requirements.txt
python -m pytest signer/tests -q
```

These checks use local databases and fakes. They do not verify live BMONI credentials, Render deployment, or external money movement. Live sandbox behavior still requires the API key, webhook secret, sandbox onboarding, and BMONI-issued test tokens.

## AI disclosure

See [AI_DISCLOSURE.md](AI_DISCLOSURE.md) for generated-code and external-service disclosure. AI advisory output is informational only; it does not score or approve a loan and cannot authorize a payment.
