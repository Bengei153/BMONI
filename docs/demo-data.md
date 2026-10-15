# Sandbox demo data

All amounts in the peer-lending demo use the simulated ledger. `/api/bootstrap` seeds Ada with NGN 20,000 and Kunle with NGN 15,000. Tolu starts with no simulated balance and can use the sandbox top-up endpoint. The bootstrap password comes from `DEMO_USER_PASSWORD`.

## BMONI sandbox identities

Use only the [official BMONI sandbox personas](https://embedded-docs.bmoni.com/api-reference/sandbox-test-data/) for BMONI onboarding; never enter real BVNs or identity documents. The sandbox checks names and phone numbers against each identity, so keep each persona together:

| Demo role | BMONI name | BVN | Phone for the BMONI API |
| --- | --- | --- | --- |
| Borrower (Tolu) | Bunch Dillon | `95888168924` | `+2348000000000` |
| Lender (Ada) | Samson Jabo | `22222222222` | `+2348000000001` |
| Lender (Kunle) | Simulated only | — | — |

`POST /api/bmoni/onboarding/nigeria` creates the BMONI user and managed CNGN wallet, submits the sandbox KYC profile, and starts Nigeria onboarding. It requires identification and proof-of-address files; do not upload real documents. Sandbox wallets start empty. Request test tokens from the [BMONI sandbox funding page](https://embedded-docs.bmoni.com/request-test-tokens/) after onboarding; this is a manual BMONI process and may take up to one business day. The platform’s peer-loan deposits, contributions, disbursements, and repayments remain simulated regardless of the BMONI wallet balance.

## Demo scam-check examples

These are sample inputs for `POST /api/scam-check`. The response is advisory. Nigerian account names are checked through BMONI only if the authenticated user has completed onboarding and the REST API key is configured. It is not proof that a particular sender or account is fraudulent.

1. **High-risk pattern:** “Your school bursary is approved. Pay the ₦5,000 processing fee in 10 minutes or your slot goes to someone else. Send your BVN and OTP to confirm.”
2. **High-risk pattern:** “Your bank account will be frozen today. Reply with your PIN and OTP so we can protect your balance.”
3. **High-risk pattern:** “Guaranteed remote job, no interview. Pay ₦3,000 for onboarding now and send your account login to receive your salary.”
4. **Ordinary message:** “The student association meeting is at 4 PM in the library. Please bring your ID card.”

Scam examples should be submitted as untrusted text. An analysis can warn a user; it must never initiate or approve a payment.
