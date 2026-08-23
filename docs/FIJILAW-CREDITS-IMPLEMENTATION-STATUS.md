# FijiLaw Credits — Implementation & Deployment Status

## Status
Core FijiLaw Credits metering, persistent wallet storage, and Fiji-ready hosted payment checkout infrastructure are implemented. Live credit purchasing still requires merchant credentials from the selected Fiji-supported payment gateway.

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
- [x] Persistent credit payment-order table
- [x] Idempotent server-side purchased-credit grant path
- [x] Windcave Hosted Payment Page adapter
- [x] Server-side Windcave session verification before granting credits
- [x] Exact-order payment verification: session ID, purchase type, amount, currency and merchant reference
- [x] Authorised transaction must match the same order before wallet crediting
- [x] Verification mismatches fail closed and cannot grant credits
- [x] Dedicated checkout/status rate limiting
- [x] Separate provider-notification rate limiting
- [x] Hosted-payment return/status flow on `/credits`
- [x] Vercel frontend build pipeline configured
- [x] Railway backend health deployment pipeline configured

## Fiji payment-provider decision
Stripe is not currently listed as a directly supported merchant country for Fiji. FijiLaw therefore treats Stripe as an optional future provider for a supported overseas entity rather than the default Fiji merchant route.

For a Fiji-based merchant, the primary implementation path is **Windcave Hosted Payment Page**, consistent with Westpac Fiji's Internet Payment Gateway offering. Mastercard Payment Gateway Services remains a possible second Fiji adapter.

The Hosted Payment Page model keeps card capture on the payment provider's secure environment. FijiLaw stores only order/payment references and never receives raw card numbers or CVV values.

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

The current allowance key for paid plans is calendar-month based. Future subscription billing should align allowance renewal to the authoritative billing period.

## Metered services currently live

| Service | Credit cost |
|---|---:|
| Advanced Legal Triage Report | 10 |
| Document analysis | 15 |

Planned services remain in the catalogue but are not charged until their workflows are implemented.

## Payment API endpoints
- `GET /api/credits/catalog`
- `GET /api/credits/wallet`
- `GET /api/credits/history`
- `POST /api/credits/checkout`
- `GET|POST /api/credits/payment/notify?orderId=...`
- `GET /api/credits/payment/status/{orderId}`
- `POST /api/admin/credits/grant`
- `POST /api/legal/triage` — authenticated + metered
- `POST /api/legal/documents/analyse` — authenticated + metered

## Windcave runtime configuration
The implementation expects these backend-only environment variables:

- `WINDCAVE_API_USERNAME`
- `WINDCAVE_API_KEY`
- `WINDCAVE_API_BASE` — optional; defaults to the Windcave production REST API base
- `PUBLIC_WEB_URL`
- `PUBLIC_API_URL`

These values must never be exposed through browser environment variables or committed to source.

## Payment completion rules
1. User selects a FijiLaw Credit package.
2. FijiLaw creates a persistent pending payment order.
3. Backend creates a Windcave Hosted Payment Page session.
4. Customer is redirected to Windcave for card entry/payment.
5. FijiLaw receives a notification or the customer returns to the credits page.
6. FijiLaw queries the Windcave session directly from the backend.
7. The returned session ID, type, amount, FJD currency and merchant reference must exactly match the stored FijiLaw payment order.
8. The first provider transaction must be authorised and its type, amount, currency and merchant reference must also match the order.
9. Any verification mismatch fails closed and the order is marked as verification failed.
10. Credits are granted only after all server-side checks succeed.
11. The payment order and purchased-credit transaction are committed idempotently so repeat callbacks cannot double-credit the wallet.

## Production database
The production Railway API uses Neon PostgreSQL for membership, authentication, subscriptions, credit wallets, credit transactions and payment orders. Database credentials are runtime-only.

## Remaining external integrations

### Windcave merchant credentials — REQUIRED FOR LIVE PURCHASES
The code is ready, but live checkout remains disabled until a Westpac Fiji/Windcave merchant account provides REST API credentials.

Remaining operational steps:
- [ ] Obtain/approve Fiji e-commerce merchant facility
- [ ] Obtain Windcave REST API username and API key
- [ ] Configure credentials in Railway
- [ ] Run payment-provider test transactions
- [ ] Validate approved/declined/cancelled paths
- [ ] Define refund/chargeback credit policy
- [ ] Commercial/legal review of credit expiry and refund terms

### Production OpenAI API provider — setup required
The application supports OpenAI when `OPENAI_API_KEY` is configured on the Railway backend. The key must remain server-side only and must never be exposed to Vercel browser code or users.

FijiLaw Credits remain FijiLaw usage units regardless of the underlying model/provider and must not be marketed as OpenAI API tokens.

## Security invariants
1. Browser code cannot grant or deduct credits.
2. API resolves authenticated member identity before wallet access.
3. Credit balance changes are recorded in a transaction ledger.
4. Metered workflows reserve credits before execution and refund on failure.
5. Paid credit purchases require authoritative server-side provider verification.
6. Provider session and transaction values must exactly match the stored internal payment order.
7. Duplicate provider callbacks cannot grant the same purchase twice.
8. Checkout and reconciliation endpoints are rate limited.
9. Payment card data is handled by the hosted payment provider rather than FijiLaw servers.
10. OpenAI and payment-provider credentials remain server-side.
11. Sponsored commercial placement remains separate from legal reasoning and neutral legal recommendations.

## Release readiness
The wallet and payment workflow are technically ready for controlled testing. Real customer card collection becomes production-ready only after merchant credentials are configured, provider test transactions are completed, and the commercial/refund terms are reviewed.
