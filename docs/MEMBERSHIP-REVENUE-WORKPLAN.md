# FijiLaw AI — Membership, Access & Recurring Revenue Workplan

## Objective
Build a role-, subscription-, and permission-based membership system in which public legal access remains useful, while persistent dashboards and professional workflow features generate recurring subscription revenue.

## Architecture rule
Identity, subscription and permissions MUST remain separate concepts.

### Identity roles
- Guest
- Registered Citizen
- Lawyer
- Law Firm Staff
- Law Firm Administrator
- Institutional User
- FijiLaw Administrator

### Subscription plans
- Free
- Personal Plus — proposed FJD 20/month
- Lawyer Professional — proposed FJD 100/month
- Law Firm Starter — FJD 200/month
- Law Firm Professional — FJD 350/month
- Law Firm Premium — FJD 600/month
- Institutional — contract pricing

Pricing is configuration, not hard-coded authorization logic.

## Access model
### Guest
- Public landing page
- Legal information/resources
- Find legal services
- Limited anonymous AI triage
- No dashboard

### Registered Free Member
- Account/profile
- Limited AI assessments
- Lawyer/legal-service search
- No paid dashboard
- Upgrade preview

### Personal Plus
- Paid dashboard
- My Legal Matters
- Saved assessments
- Case history
- Document analysis allowance
- Saved lawyers/referrals
- Timeline/deadlines/notifications as implemented

### Lawyer Professional
- Paid lawyer dashboard
- Verified professional profile workflow
- Enquiries/leads
- Matter/referral management
- Availability and practice areas
- Analytics

### Law Firm Starter
- Paid firm dashboard
- Firm listing
- Basic enquiries/referrals
- Basic analytics

### Law Firm Professional
- Starter features
- Multiple practitioners/staff
- Firm landing page
- Lead/case management
- Enhanced analytics

### Law Firm Premium
- Professional features
- Enhanced directory visibility
- Clearly labelled sponsored/promotional placement
- Advanced analytics
- Advertising/promotional tooling
- Sponsored status MUST NOT influence AI legal analysis or legal recommendations

### Institutional
- Organisation dashboard
- Offices/users
- Referral and service management
- Privacy-preserving reporting/analytics
- Contract-specific permissions

### Administrator
- Subscription/plan management
- User/organisation management
- Practitioner verification
- Content/legal corpus administration
- AI/RAG monitoring
- Audit/security administration

## Permission model
Initial permission catalogue:
- Dashboard.Access
- Cases.Create
- Cases.ViewOwn
- Cases.Manage
- Documents.Analyse
- Documents.Store
- Referrals.Request
- Referrals.Manage
- Leads.View
- Leads.Manage
- LawyerProfile.Manage
- Firm.Manage
- FirmUsers.Manage
- Analytics.View
- Billing.View
- Billing.Manage
- Directory.PriorityPlacement
- Admin.Users
- Admin.Subscriptions
- Admin.Verification
- Admin.LegalCorpus
- Admin.AI

## Revenue principles
1. Free access should create adoption and access-to-justice value.
2. Charge primarily for persistent value: dashboards, saved matters, document workflows, referrals/leads, firm management, analytics and visibility.
3. Meter expensive AI/document operations by plan so subscription revenue is not overwhelmed by model costs.
4. Offer monthly and annual billing; proposed annual pricing can provide approximately two months free, subject to final commercial review.
5. Sponsored placements must be clearly labelled and separated from legal reasoning/recommendations.
6. Keep prices configurable so plans can be changed without redeploying authorization code.

## Implementation checklist

### M1 — Data model — COMPLETED 2026-08-23
- [x] Create User/Identity model integration
- [x] Create Role model
- [x] Create SubscriptionPlan model
- [x] Create Subscription model
- [x] Create Permission model
- [x] Create RolePermission/PlanEntitlement mapping
- [x] Create Organisation model
- [x] Create OrganisationMembership model
- [x] Create UsageLedger for AI/document limits
- [x] Create BillingEvent/Audit model

Implementation evidence:
- `database/init.sql`
- `src/FijiLaw.Domain/Membership.cs`
- `src/FijiLaw.Infrastructure/PostgresMembershipInitializer.cs`
- `src/FijiLaw.Infrastructure/PostgresMembershipRepository.cs`
- Public plan catalogue endpoint: `GET /api/membership/plans`

### M2 — Authentication & authorization — CODE COMPLETE; LIVE VALIDATION PENDING
- [x] Implement sign-up/sign-in
- [x] Email verification token/confirmation flow
- [x] Transactional verification sender integration (configuration still required)
- [x] Password/security controls
- [x] Password recovery/reset flow
- [x] Implement role-based authorization
- [x] Implement subscription entitlement authorization
- [x] Add Dashboard.Access policy
- [x] Ensure Free users cannot access paid dashboard APIs
- [x] Ensure expired/cancelled subscriptions lose paid entitlements safely
- [x] Add admin override/audit rules
- [x] Add auth and verification/password-recovery rate limiting
- [ ] Validate full flow against real PostgreSQL
- [ ] Validate verification/reset email delivery through a verified sender domain

### M3 — Dashboard gating — PARTIAL
- [x] Create `/dashboard` route
- [x] Require authentication
- [x] Require Dashboard.Access entitlement
- [x] Free member sees upgrade page instead of dashboard
- [x] Generic paid member dashboard shell
- [ ] Personal Plus dashboard tailored shell
- [ ] Lawyer dashboard tailored shell
- [ ] Firm dashboard tailored shell
- [ ] Institutional dashboard tailored shell
- [ ] Admin dashboard tailored shell

### M4 — Plans & pricing UI — MOSTLY COMPLETE
- [x] Create pricing page
- [x] Free plan card
- [x] Personal Plus plan card
- [x] Lawyer Professional plan card
- [x] Starter FJD 200 plan card
- [x] Professional FJD 350 plan card
- [x] Premium FJD 600 plan card
- [x] Institutional contact/register option
- [x] Monthly/annual selector
- [x] Feature comparison/highlights
- [x] Pricing-before-registration funnel
- [x] Persist selected plan as registration intent
- [ ] Upgrade/downgrade/cancel UX

### M5 — Billing
- [ ] Select Fiji-compatible payment provider(s)
- [ ] Implement checkout
- [ ] Verify payment server-side
- [ ] Subscription activation
- [ ] Renewal handling
- [ ] Failed-payment handling
- [ ] Cancellation
- [ ] Refund/admin workflow
- [ ] Invoice/receipt history
- [ ] Webhook signature verification
- [ ] Idempotent billing events
- [ ] Billing audit trail

### M6 — Usage & margin controls
- [ ] Define AI triage allowance per plan
- [ ] Define document-analysis allowance per plan
- [ ] Define storage allowance per plan
- [ ] Define lawyer lead/referral entitlements
- [ ] Track model/token cost per member/organisation
- [ ] Track infrastructure/storage cost
- [ ] Soft-limit warnings
- [ ] Hard-limit/overage policy
- [ ] Admin revenue/cost dashboard
- [ ] Gross-margin reporting by plan

### M7 — Paid member features
- [ ] Saved cases
- [ ] Continue case
- [ ] Assessment history
- [ ] Persistent document storage
- [ ] Evidence timeline
- [ ] Deadlines/reminders
- [ ] Saved lawyers
- [ ] Referral tracking
- [ ] Notifications

### M8 — Professional monetisation
- [ ] Lawyer verification workflow
- [ ] Lawyer leads
- [ ] Consultation requests
- [ ] Law firm team accounts
- [ ] Firm landing page
- [ ] Lead pipeline
- [ ] Practice-area analytics
- [ ] Office management
- [ ] Enhanced directory profile
- [ ] Clearly labelled sponsored placement
- [ ] Advertising campaign tools

### M9 — Security & compliance gates
- [ ] Authorization tests for every paid API
- [x] Prevent client-side-only subscription gating
- [ ] Payment/billing threat model
- [ ] Privacy review for paid case storage
- [ ] Data retention/deletion rules
- [x] Audit role, registration, verification and password-reset security events
- [x] Basic authentication/verification rate limiting
- [ ] Rate limiting by account/plan for metered paid features
- [ ] Abuse/fraud controls
- [ ] Terms/subscription disclosures review

### M10 — Commercial analytics
- [ ] Monthly recurring revenue (MRR)
- [ ] Annual recurring revenue (ARR)
- [ ] Active paid members
- [ ] Trial/free-to-paid conversion
- [ ] Churn
- [ ] ARPU
- [ ] Customer acquisition cost field/reporting
- [ ] AI cost per subscriber
- [ ] Gross margin per plan
- [ ] Law-firm lead/referral conversion

## Pre-deployment rule
See `docs/PRE-DEPLOYMENT-READINESS.md`. Deployment/promotion should be deferred until build/test gates and critical membership/security smoke tests pass.

## Definition of Done
The membership programme is not complete until authorization is enforced server-side, billing state controls entitlements, paid dashboard routes cannot be accessed by free/expired users, subscription events are auditable, and revenue/AI-cost metrics can be measured.
