# FijiLaw AI — Clerk Verified Identity Integration

## Purpose

FijiLaw AI uses Clerk as the external verified-identity layer for member registration and sign-in. Clerk verifies the user's identity method, then FijiLaw links that verified identity to its own PostgreSQL-backed membership, role, subscription, permission and FijiLaw Credits system.

Clerk is not the source of truth for FijiLaw plans, roles, permissions, credits, legal matters or billing. Those remain inside FijiLaw.

## Supported registration methods

The intended production identity methods are:

- Google account
- Apple account
- Fiji mobile number (+679) with SMS verification code
- Email account with Clerk email verification

Every protected FijiLaw member identity must be verified before it is linked to the internal membership system.

## Architecture

```text
Visitor
  ↓
Clerk Sign In / Sign Up
  ↓
Google / Apple / Email / Fiji +679 verification
  ↓
Clerk authenticated session
  ↓
Next.js /api/auth/bridge
  ↓
Server reads verified Clerk identity
  ↓
AUTH_BRIDGE_SECRET protected server-to-server call
  ↓
Railway /api/auth/external-session
  ↓
Create or link FijiLaw app_user
  ↓
Create FijiLaw access session
  ↓
Role + Plan + Subscription + Permissions + Credits
  ↓
Dashboard
```

## Required environment variables

### Vercel / Next.js

```text
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=<Clerk publishable key>
CLERK_SECRET_KEY=<Clerk secret key>
AUTH_BRIDGE_SECRET=<strong random shared secret>
NEXT_PUBLIC_API_URL=https://fijilaw-api-production-production.up.railway.app
```

`NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` is designed for browser use. `CLERK_SECRET_KEY` and `AUTH_BRIDGE_SECRET` are server-side secrets and must never be committed to GitHub or exposed through `NEXT_PUBLIC_*` variables.

### Railway / FijiLaw API

```text
AUTH_BRIDGE_SECRET=<same strong random shared secret used on Vercel>
```

The Railway API already exposes `/api/auth/external-session` and compares the bridge secret using a constant-time hash comparison before accepting identity-link requests.

## Clerk Dashboard configuration

Configure the Clerk application so the production/development instance enables the required sign-up/sign-in methods.

### Google

Enable Google social connection in Clerk. For a public production application, configure the required OAuth credentials and redirect domains according to Clerk/Google production requirements.

### Apple

Enable Sign in with Apple in Clerk and complete the Apple Developer configuration required for production use.

### Email

Enable email address sign-up/sign-in and require email verification before the account is treated as verified.

### Fiji mobile

Enable phone-number sign-up/sign-in and SMS verification. FijiLaw's bridge enforces the Fiji mobile format:

```text
+679 followed by 7 digits
```

Example validation pattern used by the bridge:

```regex
^\+679\d{7}$
```

If Clerk allows country restrictions, Fiji (+679) should be included in the permitted phone-number countries for this flow.

## FijiLaw account linking

After Clerk verification, the Next.js route `/api/auth/bridge` uses Clerk's server-side `currentUser()` to read the verified identity. It only forwards verified email or verified phone information to the Railway API.

The bridge records:

- Clerk user ID as the external identity subject
- detected identity provider
- verified email when available
- verified Fiji phone number when available
- identity verification state
- display name
- selected/requested FijiLaw plan

The Railway endpoint creates or links the FijiLaw member and returns the normal FijiLaw access token used by the rest of the system.

## Security rules

1. Never put `CLERK_SECRET_KEY` in source code.
2. Never put `CLERK_SECRET_KEY` in a browser/public variable.
3. Never commit `.env.local` or production environment files.
4. Never reuse the Clerk secret as `AUTH_BRIDGE_SECRET`.
5. Generate `AUTH_BRIDGE_SECRET` independently with high entropy.
6. Rotate any secret that has been pasted into chat, tickets, email or other uncontrolled channels.
7. Use Clerk development (`pk_test_` / `sk_test_`) keys only for development/testing. Use a production Clerk instance and `pk_live_` / `sk_live_` keys for the public production launch.
8. FijiLaw's own API remains responsible for roles, subscriptions, permissions, dashboard access and FijiLaw Credits.

## Environment setup workflow

1. Create/regenerate the Clerk Secret Key in the Clerk Dashboard.
2. Add the Clerk Publishable Key to Vercel as `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`.
3. Add the regenerated Clerk Secret Key to Vercel as `CLERK_SECRET_KEY`.
4. Generate a new random `AUTH_BRIDGE_SECRET`.
5. Add that same bridge secret to both Vercel and Railway.
6. Redeploy Vercel and Railway.
7. Confirm `/health` reports `verifiedIdentityBridge: configured` on the Railway API.
8. Open `/account?mode=register` and verify the Clerk registration UI appears.
9. Test email verification.
10. Test Google login.
11. Test Apple login when configured.
12. Test a Fiji +679 SMS verification flow.
13. Confirm successful Clerk verification redirects through `/auth/complete` and then into the FijiLaw dashboard.
14. Confirm the created FijiLaw member record is marked identity-verified and receives the expected Free or requested-plan state.

## Production checklist

- [ ] Exposed/test Clerk secret revoked
- [ ] Fresh Clerk secret stored only in Vercel secret environment
- [ ] Production Clerk instance created before public commercial release
- [ ] Google configured
- [ ] Apple configured
- [ ] Email verification configured
- [ ] Fiji +679 SMS verification configured
- [ ] `AUTH_BRIDGE_SECRET` generated independently
- [ ] Same bridge secret configured in Vercel and Railway
- [ ] Clerk middleware active
- [ ] `/api/auth/bridge` works server-side
- [ ] `/api/auth/external-session` accepts only the matching bridge secret
- [ ] Verified identity links to existing FijiLaw member where appropriate
- [ ] New identity creates a single FijiLaw member rather than duplicates
- [ ] Dashboard permissions continue to come from FijiLaw server-side authorization
- [ ] Logout/session behaviour tested
- [ ] Registration/sign-in tested on mobile and desktop

## Current code locations

```text
src/FijiLaw.Web/app/layout.tsx
src/FijiLaw.Web/middleware.ts
src/FijiLaw.Web/app/account/page.tsx
src/FijiLaw.Web/app/account/VerifiedIdentityAccess.tsx
src/FijiLaw.Web/app/auth/complete/page.tsx
src/FijiLaw.Web/app/api/auth/bridge/route.ts
src/FijiLaw.Api/MembershipEndpoints.cs
src/FijiLaw.Api/Program.cs
```

## Important note

Clerk verifies identity. FijiLaw remains responsible for deciding what that verified person is allowed to do inside the legal platform.
