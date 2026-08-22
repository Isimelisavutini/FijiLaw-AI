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

### M1 — Data model
- [ ] Create User/Identity model integration
- [ ] Create Role model
- [ ] Create SubscriptionPlan model
- [ ] Create Subscription model
- [ ] Create Permission model
- [ ] Create RolePermission/PlanEntitlement mapping
- [ ] Create Organisation model
- [ ] Create OrganisationMembership model
- [ ] Create UsageLedger for AI/document limits
- [ ] Create BillingEvent/Audit model

### M2 — Authentication & authorization
- [ ] Implement sign-up/sign-in
- [ ] Email verification
- [ ] Password/security controls or managed identity provider
- [ ] Implement role-based authorization
- [ ] Implement subscription entitlement authorization
- [ ] Add Dashboard.Access policy
- [ ] Ensure Free users cannot access paid dashboard APIs
- [ ] Ensure expired/cancelled subscriptions lose paid entitlements safely
- [ ] Add admin override/audit rules

### M3 — Dashboard gating
- [ ] Create `/dashboard` route
- [ ] Require authentication
- [ ] Require Dashboard.Access entitlement
- [ ] Free member sees upgrade page instead of dashboard
- [ ] Personal Plus dashboard shell
- [ ] Lawyer dashboard shell
- [ ] Firm dashboard shell
- [ ] Institutional dashboard shell
- [ ] Admin dashboard shell

### M4 — Plans & pricing UI
- [ ] Create pricing page
- [ ] Free plan card
- [ ] Personal Plus plan card
- [ ] Lawyer Professional plan card
- [ ] Starter FJD 200 plan card
- [ ] Professional FJD 350 plan card
- [ ] Premium FJD 600 plan card
- [ ] Institutional contact-sales option
- [ ] Monthly/annual selector
- [ ] Feature comparison
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
- [ ] Prevent client-side-only subscription gating
- [ ] Payment/billing threat model
- [ ] Privacy review for paid case storage
- [ ] Data retention/deletion rules
- [ ] Audit subscription and permission changes
- [ ] Rate limiting by account/plan
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

## Definition of Done
The membership programme is not complete until authorization is enforced server-side, billing state controls entitlements, paid dashboard routes cannot be accessed by free/expired users, subscription events are auditable, and revenue/AI-cost metrics can be measured.
