# FijiLaw AI — Credits & AI Usage Monetization

## Product rule
FijiLaw sells **FijiLaw Credits**, not OpenAI API tokens. Credits are prepaid usage units redeemable for eligible FijiLaw AI services. They are not cryptocurrency, stored cash, transferable API access, or ownership of OpenAI capacity.

## Architecture

`User -> FijiLaw Credit Wallet -> FijiLaw AI service -> FijiLaw backend -> AI provider`

The browser never decides balances or deducts credits. Credit authorization and charging happen server-side.

## Credit service prices

| Service | Credits |
|---|---:|
| Advanced Legal Triage Report | 10 |
| Document analysis | 15 |
| Follow-up analysis | 3 |
| Detailed legal research | 20 |
| Compare verified authorities | 15 |
| Lawyer case-preparation report | 25 |
| Large bundle analysis | 40+ |

Only services currently implemented in the API are charged in code. Planned services remain catalogue entries until their backing workflows exist.

## Credit packages

| Package | Credits | Price FJD |
|---|---:|---:|
| Starter | 50 | 10 |
| Standard | 120 | 20 |
| Plus | 300 | 45 |
| Professional | 750 | 100 |
| Firm | 2,000 | 250 |

## Included plan credits

| Plan | Included credits |
|---|---:|
| Free | 10 introductory credits |
| Personal Plus | 100 / billing month |
| Lawyer Professional | 700 / billing month |
| Law Firm Starter | 1,500 / billing month |
| Law Firm Professional | 3,500 / billing month |
| Law Firm Premium | 7,500 / billing month |
| Institutional | 5,000 / billing month by default; contract override planned |

Included credits are FijiLaw usage entitlements, not a promise of a specific quantity of provider tokens.

## Charging workflow

1. Resolve authenticated member server-side.
2. Resolve service credit price.
3. Ensure the member wallet exists and apply any due plan allowance.
4. Atomically reserve the required credits.
5. Run the AI/legal workflow.
6. If successful, complete the debit and write an audit transaction.
7. If the workflow fails, refund the reservation.
8. Return the result to the user.
9. If the balance is insufficient, return HTTP `402 Payment Required` with the current balance and required credits.

## Database model

### `credit_wallets`
- `user_id`
- `balance`
- `lifetime_purchased`
- `lifetime_granted`
- `lifetime_used`
- `last_allowance_key`
- timestamps

### `credit_transactions`
- transaction id
- user id
- transaction type: allowance, purchase, usage, refund, adjustment
- status: completed, reserved, refunded
- signed credit amount
- balance before/after
- service type
- correlation id
- provider reference
- metadata JSON
- timestamp

## Payment policy

A completed top-up must only credit the wallet after the payment provider confirms payment server-side. Client-side success pages must never grant credits. Payment webhooks must be signature-verified and idempotent.

Until a payment provider is connected, package checkout remains unavailable in persistent production mode. Controlled demo mode may simulate top-ups for dashboard testing and clearly labels them as non-financial.

## Margin controls

Track at minimum:
- credits sold
- credits consumed
- FJD revenue per package
- AI/provider cost per request when available
- storage/infrastructure cost
- gross margin per plan
- gross margin per credit package
- unused credit liability/expiry policy once commercially reviewed

## User-facing terminology

Use: **FijiLaw Credits**, **AI Credits**, **Credit Wallet**, **Buy Credits**, **Included Credits**.

Avoid marketing FijiLaw Credits as “OpenAI tokens”. OpenAI tokens are an internal/provider usage measurement and are separate from FijiLaw's commercial usage units.

## Terms copy — draft

> FijiLaw Credits are prepaid usage credits redeemable for eligible FijiLaw AI services. Credits are not OpenAI API tokens, cryptocurrency, stored cash, or transferable API access. Credit requirements may vary by service and may change with notice. Legal information generated through AI services remains subject to FijiLaw AI's legal-information, verification and human-review safeguards.

This language is a product draft and should receive Fiji legal/commercial review before public launch.
