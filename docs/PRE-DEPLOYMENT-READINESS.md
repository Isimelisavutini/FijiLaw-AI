# FijiLaw AI — Pre-Deployment Readiness Gate

Deployment is intentionally deferred until the application passes the checks below. A Git push or hosting-provider build is not, by itself, evidence that the system is production-ready.

## 1. Build and automated validation
- [ ] `dotnet restore FijiLaw.sln` succeeds
- [ ] `dotnet build FijiLaw.sln --configuration Release --no-restore` succeeds
- [ ] `dotnet test FijiLaw.sln --configuration Release --no-build` succeeds
- [ ] `npm run typecheck` succeeds in `src/FijiLaw.Web`
- [ ] `npm run build` succeeds in `src/FijiLaw.Web`
- [ ] GitHub CI is green on the exact release commit

## 2. Local database validation
- [x] PostgreSQL + pgvector local compose definition exists
- [x] Base schema bootstrap exists
- [x] Membership authentication/security bootstrap exists
- [x] Password reset schema exists
- [ ] Fresh database starts successfully from `docker compose up`
- [ ] API initializes against a clean database without migration errors

## 3. Membership flow validation
- [x] Pricing page implemented
- [x] Pricing-before-registration funnel implemented
- [x] Selected plan is persisted as user intent
- [x] Registration creates Free subscription only; paid access is not activated without billing
- [x] Login/session implementation exists
- [x] Dashboard authorization is server-side
- [x] Unverified users are denied paid dashboard access
- [x] Free users are denied paid dashboard access
- [x] Expired/cancelled paid subscriptions are excluded from active entitlement resolution
- [x] Password reset flow implemented and revokes existing sessions
- [ ] Registration tested against real PostgreSQL
- [ ] Login/logout tested against real PostgreSQL
- [ ] Free-user dashboard denial verified end-to-end
- [ ] Paid entitlement test verified end-to-end using a controlled test subscription

## 4. Email validation
- [x] Verification tokens are hashed at rest
- [x] Verification requests require an authenticated member session
- [x] Verification link page auto-processes valid tokens
- [x] Password-reset tokens are hashed at rest and expire after 30 minutes
- [x] Resend API client implemented server-side
- [ ] A Resend sending domain is verified
- [ ] `RESEND_API_KEY` configured server-side
- [ ] `EMAIL_FROM` uses the verified domain
- [ ] `PUBLIC_WEB_URL` configured correctly
- [ ] Verification email delivered and link tested
- [ ] Password-reset email delivered and link tested

## 5. API security validation
- [x] CORS is restricted to configured/approved FijiLaw origins
- [x] Login and registration are rate-limited
- [x] Verification and password recovery are rate-limited
- [x] Session tokens are random and stored only as hashes server-side
- [x] PBKDF2 password hashing uses per-user random salt
- [x] Password reset revokes active sessions
- [x] Basic no-sniff, referrer and no-store response headers are set
- [ ] Security tests cover all paid endpoints
- [ ] Abuse/fraud review completed
- [ ] Session-storage token design reviewed before handling highly sensitive member cases

## 6. External-service configuration
- [ ] Production PostgreSQL available and backed up
- [ ] Production `DATABASE_URL` configured
- [ ] OpenAI key stored server-side only
- [ ] OpenAI privacy/data-retention posture reviewed for legal data
- [ ] Resend production configuration complete
- [ ] Approved production web origin configured

## 7. Functional smoke test before production
- [ ] Landing page loads
- [ ] Pricing page loads with live plan catalogue
- [ ] Register button routes through pricing
- [ ] Registration succeeds
- [ ] Verification email succeeds
- [ ] Verification link succeeds
- [ ] Login succeeds
- [ ] Free dashboard is blocked with upgrade path
- [ ] Password reset succeeds
- [ ] Legal triage succeeds
- [ ] Document analysis succeeds
- [ ] Legal-services directory succeeds
- [ ] API outage produces safe user-facing errors

## Release rule
Do not deliberately deploy/promote a production release until all required build checks and the critical membership/security smoke tests above are complete. Any unresolved item must be documented as an accepted limitation before release.
