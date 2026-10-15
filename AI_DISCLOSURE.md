# AI and software disclosure

This repository was scaffolded and implemented with assistance from OpenAI Codex. A developer must review generated code and configure credentials locally before connecting to external services.

## Libraries and services

- .NET 8, ASP.NET Core, Entity Framework Core, MediatR, FluentValidation, Npgsql, SQLite, JWT, and BCrypt for the API and persistence layers.
- Python 3.11, FastAPI, Uvicorn, `eth-account==0.13.7`, and `cryptography==50.0.2` for the local EVM signer. `eth-account` implements raw 32-byte digest signing and EIP-191 personal message signing; `cryptography` encrypts signer keys at rest with AES-256-GCM. Pytest and HTTPX exercise the signer endpoints and signing vectors.
- Anthropic Messages API through `HttpClient` for optional credit-statement sub-scores, scam classification, and assistant wording. AI output is JSON-validated, retried once if invalid, and falls back to neutral/deterministic responses. C# weights and computes the final credit score; balances, pricing, allocations, repayment, and approvals remain deterministic application code.
- BMONI Embedded API sandbox for wallet, onboarding, balance, transaction, and transfer integration.
- BMONI webhook events are verified with HMAC-SHA256 over the raw request body and deduplicated by webhook event ID. The event processor stores only the event ID/type and onboarding status fields needed by the demo.

No production credentials or private wallet keys belong in this repository. Runtime secrets are supplied through environment variables; locally generated signer keys are encrypted in the configured key store.
