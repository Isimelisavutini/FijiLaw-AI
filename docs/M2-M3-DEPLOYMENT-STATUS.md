# FijiLaw AI — M2/M3 Deployment Status

## Implemented
- Member registration and sign-in UI
- Paid dashboard access UI
- Email-verification-aware account flow
- Dedicated `/verify-email` page
- Free-member upgrade state
- Paid-but-unverified member lock state
- Server-side `Dashboard.Access` enforcement
- Server-side email verification requirement for paid dashboard access
- Session-based member API access
- Platform-admin role override with membership audit logging

## Current production API
Railway service: `fijilaw-api-production`

Latest verified backend deployment containing M2 security work:
- Commit: `d33e36ec25073edf4c6659648ae4f7f8f64ef968`
- Deployment: `8ffdbadc-289a-42de-a6c7-e0bd09c50062`
- Status: SUCCESS
- Healthcheck: PASSED

## PostgreSQL blocker
The production Railway project is on the free plan and cannot provision another resource while the obsolete services remain. Creating `fijilaw-postgres` currently fails with `Free plan resource provision limit exceeded`.

Until `DATABASE_URL` exists, registration, sign-in, verification and paid membership activation intentionally remain unavailable rather than falling back to insecure temporary storage.

## Resend blocker
The Resend account is connected, but currently has no verified sending domain. Email-verification token generation exists in the API, but production verification messages cannot be sent until a domain is added and verified.

## Vercel blocker
The frontend source changes are committed to GitHub, but the connected Vercel team (`Pasifika Solutions`) currently returns zero projects. The deployment action also exposes an incompatible runtime schema and rejects deployment requests before creation. The former FijiLaw Vercel alias could not be resolved through the connected Vercel account.

Therefore the frontend source is ready, but a new Vercel production deployment cannot be truthfully marked complete from the current connection.

## Next infrastructure actions
1. Approve/remove obsolete Railway services or upgrade Railway plan.
2. Provision PostgreSQL + pgvector.
3. Set `DATABASE_URL` on `fijilaw-api-production`.
4. Add and verify a FijiLaw sending domain in Resend.
5. Reconnect/import the FijiLaw GitHub repository into the Pasifika Solutions Vercel team.
6. Deploy `src/FijiLaw.Web` and verify `/`, `/account`, `/pricing`, `/verify-email`, and `/dashboard`.
7. Run end-to-end member test: register → verify → login → free dashboard denied → paid entitlement allowed.
