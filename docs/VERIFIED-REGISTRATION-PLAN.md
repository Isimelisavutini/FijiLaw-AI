# FijiLaw AI — Verified Registration Plan

## Goal
Every FijiLaw member must complete identity verification before protected member features can be used. Registration supports:

1. Google account
2. Apple account
3. Fiji mobile number (+679)

The existing FijiLaw PostgreSQL membership, roles, subscriptions, permissions, audit events, and internal session tokens remain the system of record for application access. The external identity provider verifies the identifier; FijiLaw decides what the verified user is allowed to do.

## Verification policy

| Registration method | Verification | FijiLaw result |
| --- | --- | --- |
| Google | OAuth identity + verified email. Production policy should enable force-email-verification-after-SSO if an additional email OTP is required. | Verified member session |
| Apple | Sign in with Apple + verified email. Production policy should enable force-email-verification-after-SSO if an additional email OTP is required. | Verified member session |
| Fiji mobile | SMS one-time code to a +679 number | Verified member session |
| Legacy email/password | Existing verification flow retained as a fallback until migration is complete | Protected access remains locked until verified |

A verified identity is required independently of plan or role. Verification does not grant paid access. Roles, subscription entitlements, and `Dashboard.Access` are still enforced by the FijiLaw API.

## User journey

### Google / Apple
1. User opens `/account?mode=register`.
2. User chooses Google or Apple.
3. Identity provider completes authentication.
4. Any required email verification code is completed inside the identity flow.
5. Browser is redirected to `/auth/complete`.
6. The Next.js server validates the active identity session and verifies that an email or phone identifier is marked verified by the provider.
7. The Next.js server calls the FijiLaw API through the server-to-server identity bridge.
8. FijiLaw links the external identity to an existing member when a verified identifier matches, otherwise creates a new citizen/free member record.
9. FijiLaw issues its own 30-day application session token.
10. User continues to the dashboard or upgrade state based on FijiLaw permissions and subscription.

### Fiji mobile
1. User chooses phone registration and enters a number in E.164 form (`+679XXXXXXX`).
2. An SMS OTP is sent by the configured identity provider.
3. User enters the code.
4. The identity provider marks the phone number verified.
5. The same `/auth/complete` bridge creates or links the FijiLaw member.
6. The API records `phone_verified=true` and `identity_verified_at`.

Fiji's country code is +679 and the national number is seven digits. The API rejects non-Fiji phone numbers for the Fiji mobile registration method.

## Account linking rules

The API resolves a verified user in this order:

1. Existing `(identity_provider, identity_subject)` link.
2. Existing account with the same verified email.
3. Existing account with the same verified Fiji phone number.
4. New FijiLaw account.

This prevents a returning Google/Apple/mobile user from receiving a new FijiLaw profile every time. Unique indexes protect external identities and phone numbers from being attached to more than one member.

Phone-only accounts use an internal, non-routable hashed email alias because the current membership schema requires the `email` column to remain non-null. The actual verified phone number is stored separately and should be displayed as the user's primary identifier. This can be migrated to a nullable email column later without changing the authentication contract.

## Security controls

- External identity claims are never trusted directly from browser JSON.
- The Next.js server reads the authenticated identity from the provider's server SDK.
- The API `/api/auth/external-session` endpoint is protected by `AUTH_BRIDGE_SECRET` and rate limiting.
- Bridge-secret comparison uses constant-time hash comparison.
- FijiLaw continues to issue and hash its own application session tokens.
- Passwords for legacy accounts remain PBKDF2-SHA256 hashed with 210,000 iterations.
- Verification is checked server-side before dashboard/permission access.
- Auth and verification endpoints remain rate limited.
- Authentication events are written to `membership_audit_events`.
- No OTP value is stored by FijiLaw when the external identity provider manages OTP delivery.

## Database changes

`app_users` adds / guarantees:

- `identity_provider`
- `identity_subject`
- `phone_number`
- `email_verified`
- `phone_verified`
- `identity_verified_at`

Indexes:

- unique verified external identity `(identity_provider, identity_subject)`
- unique non-null `phone_number`
- identity-verification timestamp index

## Web implementation

- `ClerkProvider` is enabled only when Clerk environment variables are configured.
- `/account` switches to the verified identity registration UI when enabled.
- Google, Apple, and phone strategies are rendered through the provider-managed authentication component.
- `/auth/complete` exchanges the verified provider identity for a FijiLaw application session.
- `/api/auth/bridge` performs the trusted server-to-server exchange.
- Existing email/password membership remains available when verified identity configuration is absent, preventing an incomplete provider setup from taking the site offline.

## Provider configuration

### Clerk application
Configure a production Clerk application with:

- Google social connection enabled.
- Apple social connection enabled.
- Phone sign-up/sign-in enabled with verification at sign-up.
- SMS country allowlist enabled for Fiji (+679).
- Email verification code enabled when email-based verification is required.
- Force email verification after SSO enabled if the product policy requires every Google/Apple registration to enter a separate emailed code.
- Production domain configured for FijiLaw.

Phone authentication requires a production plan that supports SMS verification. Confirm Fiji is selectable in the Clerk SMS allowlist before launch. If Fiji cannot be enabled for the selected Clerk plan, use Twilio Verify as the +679 SMS adapter; Twilio supports Fiji SMS delivery.

### Apple
Production Sign in with Apple requires the Apple Developer configuration for the FijiLaw web domain, including Services ID / client configuration and the required signing key details.

## Environment variables

### Next.js / Vercel
- `NEXT_PUBLIC_API_URL`
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`
- `CLERK_SECRET_KEY`
- `AUTH_BRIDGE_SECRET`

### FijiLaw API / Railway
- `DATABASE_URL`
- `AUTH_BRIDGE_SECRET` — must exactly match the web value
- existing application variables such as `PUBLIC_WEB_URL`, `WebOrigin`, and `AllowedWebOrigins`

Never expose `CLERK_SECRET_KEY` or `AUTH_BRIDGE_SECRET` through `NEXT_PUBLIC_*` variables.

## Deployment sequence

1. Merge code after CI succeeds.
2. Configure Clerk production application and provider credentials.
3. Generate one high-entropy `AUTH_BRIDGE_SECRET` and set the same value on Railway and Vercel.
4. Set Clerk keys on Vercel.
5. Redeploy the FijiLaw API so the database migration runs.
6. Deploy the Next.js frontend.
7. Verify `/health` reports `verifiedIdentityBridge=configured`.
8. Run end-to-end registration tests:
   - Google
   - Apple
   - Vodafone Fiji +679 test number
   - Digicel Fiji +679 test number
   - invalid/non-Fiji phone rejection
   - duplicate identifier account linking
   - unverified user denial
   - verified free-member upgrade state
   - verified paid-member dashboard access
9. Confirm logout, re-login, password fallback, and audit logging.

## Release acceptance criteria

- No protected member feature can be opened by an unverified identity.
- Google registration creates/links exactly one FijiLaw account.
- Apple registration creates/links exactly one FijiLaw account.
- A +679 mobile registration cannot complete until SMS verification succeeds.
- Non-+679 phone registration is rejected for the Fiji mobile path.
- Re-registering an already linked verified identifier does not create a duplicate user.
- Existing roles/subscriptions are preserved during account linking.
- Authentication secrets are server-side only.
- CI build/tests pass before merge.
- Railway backend deployment is successful and healthy.
- Frontend production deployment is reachable and the three registration methods are visible and operational.
