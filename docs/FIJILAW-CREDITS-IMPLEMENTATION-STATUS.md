# FijiLaw Credits — Implementation & Deployment Status

## Status
Core FijiLaw Credits metering and persistent wallet storage are implemented and deployed.

## Completed
- [x] FijiLaw Credits product terminology and commercial catalogue
- [x] Credit packages and FJD catalogue
- [x] Plan-included credit allowances
- [x] Persistent PostgreSQL credit wallets
- [x] Persistent credit transaction ledger
- [x] Atomic credit reservation before metered AI workflows
- [x] Complete debit after successful workflow
- [x] Automatic refund after failed workflow
- [x] HTTP 402 response for insufficient credits
- [x] Authenticated wallet endpoint
- [x] Credit history endpoint
- [x] Credit package catalogue endpoint
- [x] Admin credit adjustment endpoint
- [x] Advanced Legal Triage metering — 10 credits
- [x] Document analysis metering — 15 credits
- [x] Credit wallet/store page
- [x] Membership pricing shows included credits
- [x] Landing-page AI workflows send authenticated bearer sessions
- [x] Dashboard displays FijiLaw Credit balance and usage summary
- [x] Neon PostgreSQL connected to Railway production
- [x] Persistent demo accounts seeded for each user/plan level
- [x] Demo plan allowances verified in Neon
- [x] Vercel frontend build succeeded
- [x] Railway backend health deployment succeeded

## Verified plan allowances

| Plan | Included credits |
|---|---:|
| Free | 10 introductory |
| Personal Plus | 100 monthly |
| Lawyer Professional | 700 monthly |
| Law Firm Starter | 1,500 monthly |
| Law Firm Professional | 3,500 monthly |
| Law Firm Premium | 7,500 monthly |
| Institutional | 5,000 monthly default |

The current allowance key for paid plans is calendar-month based. A future billing integration should align renewal grants to the authoritative subscription billing period rather than calendar month where necessary.

## Metered services currently live

| Service | Credit cost |
|---|---:|
| Advanced Legal Triage Report | 10 |
| Document analysis | 15 |

Planned services remain in the catalogue but are not charged until their workflows are implemented.

## API endpoints
- `GET /api/credits/catalog`
- `GET /api/credits/wallet`
- `GET /api/credits/history`
- `POST /api/credits/checkout`
- `POST /api/admin/credits/grant`
- `POST /api/legal/triage` — authenticated + metered
- `POST /api/legal/documents/analyse` — authenticated + metered

## Production database
The production Railway API uses Neon PostgreSQL for membership, authentication, subscriptions, credit wallets and transactions. Database credentials are stored only as runtime configuration and must never be committed to source or exposed in browser code.

## Remaining external integrations

### Real-money payment checkout — NOT complete
Persistent production checkout intentionally refuses to grant purchased credits until a payment provider is connected. Required work:
- [ ] Connect approved payment provider
- [ ] Create server-side checkout sessions
- [ ] Verify webhook signatures
- [ ] Make payment events idempotent
- [ ] Grant credits only after confirmed payment
- [ ] Store provider transaction references
- [ ] Handle refunds/chargebacks and credit adjustments
- [ ] Commercial review of credit expiry/refund terms

Demo simulated top-ups are non-financial and must remain clearly labelled.

### Production OpenAI API provider — setup required
The application supports an OpenAI-backed language model provider when `OPENAI_API_KEY` is configured on the backend. The current Railway configuration must be checked before representing OpenAI inference as enabled. The OpenAI key must remain server-side only and must never be sent to Vercel client code or exposed to users.

FijiLaw Credits remain FijiLaw usage units regardless of the underlying AI provider and must not be marketed as OpenAI API tokens.

## Security invariants
1. Browser code cannot grant or deduct credits.
2. API resolves authenticated member identity before wallet access.
3. Credit balance changes are recorded in a transaction ledger.
4. Metered workflows reserve before execution and refund on failure.
5. Paid credit purchases require authoritative server-side payment confirmation.
6. OpenAI/provider API credentials remain server-side.
7. Sponsored commercial placement remains separate from legal reasoning and neutral legal recommendations.

## Release readiness
Core credit metering is suitable for controlled testing with persistent accounts. Real customer payment collection is not considered production-complete until payment-provider checkout and webhook verification are implemented and commercially reviewed.
