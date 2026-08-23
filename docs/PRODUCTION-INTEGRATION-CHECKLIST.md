# FijiLaw AI — Production Integration Checklist

This checklist contains the remaining account-level steps that cannot be committed to source code because they involve private third-party credentials.

## 1. OpenAI production inference

Status: **Application code ready; production API key not yet present in Railway.**

Required account action:

- Create a project-scoped OpenAI API key named `FijiLaw AI Production` using the secure OpenAI Platform key setup flow.
- Store the key directly in Railway production as `OPENAI_API_KEY`.
- Do not commit the key to GitHub, add it to Vercel browser variables, place it in screenshots, or send it through normal application logs.
- Keep `OPENAI_MODEL` as a separately configurable backend variable so model changes do not require a code deployment.

Verification after configuration:

- Railway `/health` must report `aiEnabled: true`.
- Run an authenticated Advanced Legal Triage request.
- Confirm the FijiLaw Credit wallet is debited only after the workflow succeeds.
- Confirm a failed provider request refunds the reserved FijiLaw Credits.

## 2. Fiji online payment merchant facility

Status: **Windcave Hosted Payment Page integration is deployed; merchant API credentials are not yet present in Railway.**

FijiLaw's primary Fiji merchant path is Westpac Fiji Internet Payment Gateway with Windcave. Westpac Fiji also identifies Mastercard Payment Gateway Services as an available alternative.

Required merchant action:

- Apply for / activate a Westpac Fiji Internet Payment Gateway merchant facility.
- Select Windcave as the payment service provider for the current FijiLaw integration.
- Obtain the Windcave REST API username and API key for the merchant account.
- Complete any provider merchant onboarding, settlement-account, PCI and test-environment requirements.

Required Railway production variables:

- `WINDCAVE_API_USERNAME`
- `WINDCAVE_API_KEY`
- `WINDCAVE_API_BASE` (optional; use only if Windcave supplies a non-default/test endpoint)
- `PUBLIC_WEB_URL=https://fijilaw-ai-pasifika-solutions.vercel.app`
- `PUBLIC_API_URL=https://fijilaw-api-production-production.up.railway.app`

Do not store Windcave secrets in GitHub or client-side Vercel variables.

Verification after configuration:

1. `/health` reports `creditPayments: windcave-ready`.
2. `/api/credits/catalog` reports payment checkout ready.
3. Sign in with a controlled test user.
4. Choose the smallest FijiLaw Credit package.
5. Verify redirection to the Windcave Hosted Payment Page.
6. Test approved, declined and cancelled payments.
7. Confirm only an authorised server-verified payment creates a `purchase` credit transaction.
8. Repeat the provider callback and confirm the wallet is not credited twice.
9. Reconcile the FijiLaw payment order, credit transaction and provider transaction reference.

## 3. Before public paid launch

- Disable or remove seeded demo accounts from production.
- Change `SEED_DEMO_ACCOUNTS` to `false` after controlled testing.
- Confirm email verification and password recovery delivery.
- Publish reviewed FijiLaw Credit purchase/refund/expiry terms.
- Define chargeback handling and purchased-credit reversal policy.
- Add monitoring for failed payment verification and AI-provider failures.
- Set budget/rate limits for the OpenAI project.
- Confirm legal/privacy review of user legal documents and AI processing.

## Security rule

The browser may initiate checkout but can never grant credits. The backend independently verifies payment with the provider and grants purchased credits inside the persistent database transaction. OpenAI and payment-provider credentials remain server-side only.
