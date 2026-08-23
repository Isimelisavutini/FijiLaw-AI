# FijiLaw AI — Dashboard Architecture by User Type

## Objective
Create one shared FijiLaw AI dashboard application whose navigation, modules and data are controlled by authenticated identity, role, active subscription and server-side permissions.

## Product principle

`Identity + Role + Active Subscription + Permissions = Dashboard Experience`

The dashboard is a paid or authorised workspace. Public and free users retain useful legal-access features, while persistent legal workflows, professional lead/referral tools, firm operations and analytics create recurring commercial value.

## 1. Guest
Dashboard: No.

Public experience:
- Landing page
- Limited legal triage
- Legal resources
- Find a Lawyer / Legal Aid
- Pricing
- Register / Sign in

Conversion target: Guest -> Free Member -> Paid Member.

## 2. Registered Free Member
Dashboard: No full dashboard.

Account experience:
- Personal details
- Email verification
- Security
- Current plan
- Usage summary when available
- Upgrade membership

Show a locked dashboard preview with My Legal Matters, Saved Reports, Documents, Deadlines and Lawyer Referrals.

## 3. Personal Plus — FJD 20/month
Primary navigation:
- Overview
- My Legal Matters
- AI Legal Assistant
- Documents
- Evidence
- Deadlines
- Lawyers
- Referrals
- Notifications
- Billing
- Account

Overview cards:
- Active Matters
- Documents
- Upcoming Deadlines
- Lawyer Referrals

Priority modules:
- Continue latest case
- Saved Advanced Legal Triage Reports
- Evidence/document list
- Referral status

## 4. Lawyer Professional — FJD 100/month
Navigation:
- Overview
- Client Enquiries
- Referrals
- Matters
- AI Legal Research
- Document Analysis
- Clients
- Appointments
- My Profile
- Analytics
- Billing
- Settings

Overview metrics:
- New Enquiries
- Active Matters
- Referrals
- Profile Views
- Consultations

Referral cards should expose the AI triage summary before the lawyer accepts or declines the matter.

## 5. Law Firm Starter — FJD 200/month
Focus: visibility and client acquisition.

Modules:
- Overview
- Firm Profile
- Lawyers
- Enquiries
- Referrals
- Basic Analytics
- Billing

Metrics:
- Profile Views
- Legal Enquiries
- Referral Requests
- Calls
- Website Clicks

## 6. Law Firm Professional — FJD 350/month
Focus: firm workflow and lead management.

Modules:
- Overview
- Lead Pipeline
- Cases
- Clients
- Lawyers
- Staff
- Appointments
- Documents
- Referrals
- Firm Website
- Analytics
- Billing

Lead lifecycle:
`NEW -> CONTACTED -> CONSULTATION -> CLIENT -> MATTER OPENED`

## 7. Law Firm Premium — FJD 600/month
Includes Professional plus growth/marketing features:
- Advanced analytics
- Enhanced firm profile
- Clearly labelled sponsored listings
- Advertising/promotional tools
- Lead attribution
- Multiple offices
- Priority support

Sponsored placement must never influence legal reasoning or neutral matching presented as non-sponsored.

## 8. Institutional Partner
Potential users include Legal Aid, Fiji Law Society, government/justice-sector bodies, approved NGOs and other contracted partners.

Modules:
- Overview
- Referrals
- Cases/work queues where authorised
- Offices
- Users
- Practitioners
- Service Availability
- Legal Demand
- Regional Analytics
- Reports
- Administration

Analytics should be aggregated/de-identified where individual case access is not required.

## 9. FijiLaw Platform Administrator
Modules:
- Platform Overview
- Users
- Memberships
- Subscriptions
- Payments
- Lawyers
- Law Firms
- Organisations
- Verification
- Legal Resources / Corpus
- AI Operations
- Cases
- Referrals
- Analytics
- Security
- Audit Logs
- System Health
- Settings

Business metrics:
- Registered Users
- Paid Members
- MRR
- ARR
- ARPU
- Churn
- Free-to-paid conversion
- AI cost per subscriber
- Gross margin
- Active Law Firms
- Active Lawyers
- AI Assessments

## Shared dashboard shell
Desktop layout:

```text
+-------------------------------------------------------+
| FijiLaw AI       Search      Notifications      User |
+---------------+---------------------------------------+
| Overview      |                                       |
| Cases         |  Role-aware dashboard content        |
| Documents     |                                       |
| AI            |  KPI cards                           |
| Referrals     |  Recent activity                     |
| Lawyers       |  Tasks/deadlines                     |
| Analytics     |                                       |
| Billing       |                                       |
+---------------+---------------------------------------+
```

On mobile the sidebar becomes a compact menu/navigation surface.

## Implementation workplan

### D1 — Shared dashboard shell
- [x] Existing server-side `Dashboard.Access` endpoint/gate
- [ ] Shared top bar
- [ ] Permission-aware sidebar
- [ ] User/plan badge
- [ ] Responsive mobile navigation
- [ ] Reusable KPI card
- [ ] Reusable activity/matter list

### D2 — Free upgrade experience
- [x] Existing free-user 403/upgrade handling
- [ ] Rich locked dashboard preview
- [ ] Compare-plan CTA
- [ ] Preserve intended plan into registration/checkout flow

### D3 — Personal Plus
- [ ] Overview
- [ ] Legal matters module shell
- [ ] Saved reports module shell
- [ ] Documents/evidence module shell
- [ ] Deadlines module shell
- [ ] Lawyers/referrals module shell
- [ ] Billing/account links

### D4 — Lawyer Professional
- [ ] Lawyer overview
- [ ] Enquiries/leads shell
- [ ] Referral review cards
- [ ] Matter list
- [ ] AI legal research entry
- [ ] Professional profile module
- [ ] Analytics shell

### D5 — Law Firm dashboards
- [ ] Starter overview/profile/enquiries
- [ ] Professional lead pipeline
- [ ] Professional team/staff controls
- [ ] Premium growth/placement tools
- [ ] Multiple office architecture

### D6 — Institutional
- [ ] Institutional navigation
- [ ] Referral/work queues
- [ ] Offices/users
- [ ] Regional/service-demand analytics
- [ ] Reporting/export permissions

### D7 — Platform Admin
- [ ] Admin navigation
- [ ] Membership/subscription metrics
- [ ] User/organisation administration
- [ ] Practitioner verification
- [ ] Legal corpus operations
- [ ] AI operations
- [ ] Security/audit views
- [ ] Revenue analytics

### D8 — Backing data/APIs
- [ ] Cases API/data model
- [ ] Documents/evidence persistence
- [ ] Referral persistence
- [ ] Leads pipeline
- [ ] Appointment model
- [ ] Notification model
- [ ] Analytics aggregation
- [ ] Billing provider integration

## Definition of done
A dashboard module is complete only when:
1. its backing API/data exists where required;
2. the API checks the relevant permission server-side;
3. the UI hides or locks unavailable capabilities;
4. free/expired users cannot bypass entitlement rules;
5. sensitive actions are auditable where appropriate;
6. responsive behavior is verified for desktop/mobile.
