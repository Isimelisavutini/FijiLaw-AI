# FijiLaw AI — Pre-Deployment Stabilization Status

This document is the gate before further production deployment work. A deployment is not considered ready merely because a build succeeds.

## Stabilized

- [x] Next.js application can be built from the repository root through the root workspace configuration.
- [x] Root Vercel configuration points production output to `src/FijiLaw.Web/.next`.
- [x] Frontend has an explicit TypeScript typecheck script.
- [x] CI runs .NET restore/build/test plus frontend install/typecheck/build.
- [x] Pricing page remains usable when the membership-plan API is temporarily unavailable by showing the configured pricing catalogue.
- [x] Pricing is shown before registration and selected plan codes carry into the registration flow.
- [x] Account page checks `/health` before accepting credentials.
- [x] Account registration/sign-in are disabled when secure PostgreSQL membership storage is unavailable instead of allowing users to hit a 503 dead end.
- [x] Account, dashboard and email-verification pages use shared API timeout and normalized error handling.
- [x] Paid dashboard access remains enforced by the backend entitlement policy rather than only in the browser.
- [x] Email verification remains required for paid dashboard access.
- [x] Public legal tools and pricing remain available even while the member service is unavailable.

## External production dependencies still required

- [ ] PostgreSQL/pgvector service provisioned and `DATABASE_URL` connected to `fijilaw-api-production`.
- [ ] Membership schema initialization verified against the production database.
- [ ] Live registration → login → logout flow tested against PostgreSQL.
- [ ] Free member verified to receive no `Dashboard.Access` entitlement.
- [ ] Active paid member verified to receive the correct entitlements.
- [ ] Expired/cancelled subscription verified to lose paid entitlements.
- [ ] Resend sending domain added and DNS verified.
- [ ] Verification email delivery wired to the backend and tested.
- [ ] Payment provider selected for Fiji and server-side billing/webhook workflow implemented.
- [ ] Paid subscription activation tested from verified payment event through dashboard access.

## Security work before broad public launch

- [ ] Replace browser `sessionStorage` bearer-session handling with a hardened same-site HttpOnly cookie/BFF session design, or formally accept/document the residual XSS risk for the pilot.
- [ ] Add login rate limiting and credential-stuffing protection.
- [ ] Add account lockout/backoff and password-reset flow.
- [ ] Add CSRF protections if cookie-based sessions are adopted.
- [ ] Add Content Security Policy and review frontend XSS exposure.
- [ ] Add automated dependency/security scanning.
- [ ] Complete privacy/data-retention review before storing legal case documents.

## Deployment gate

Future production deployment work should resume only after the relevant build checks pass and the production dependencies for the feature being enabled are present. Membership UI may be deployed while account storage is unavailable because it now fails closed and clearly reports the member-service state, but registration must not be marketed as live until the PostgreSQL and email-verification dependencies are verified.
