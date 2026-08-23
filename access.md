# FijiLaw AI — User Access Levels

This file is the product-level source of truth for FijiLaw AI roles, subscription levels, dashboard access and permission expectations.

## Core rule

Access is calculated from four separate concepts:

`Identity + Role + Active Subscription + Permissions = Dashboard Experience`

The frontend may hide or preview features, but authorization MUST be enforced by the API. A client-side plan name is never sufficient to unlock a paid or privileged function.

## Roles

| Role code | User type | Purpose |
|---|---|---|
| `guest` | Anonymous visitor | Public website plus a limited three-report legal-triage trial |
| `citizen` | Registered public user | Citizen/member identity |
| `lawyer` | Individual legal practitioner | Lawyer professional tools |
| `firm_staff` | Law firm staff | Firm workflow access assigned by organisation |
| `firm_admin` | Law firm administrator | Firm management and staff administration |
| `institutional` | Partner organisation user | Approved institutional/justice-sector access |
| `platform_admin` | FijiLaw administrator | Full platform administration |

`guest` is a product state and is not currently stored as a database role.

## Subscription plans

| Plan code | Name | Intended user | Monthly FJD | Dashboard |
|---|---|---|---:|---|
| `free` | Free | Registered citizen | 0 | No |
| `personal_plus` | Personal Plus | Citizen/individual | 20 | Yes |
| `lawyer_professional` | Lawyer Professional | Individual lawyer | 100 | Yes |
| `firm_starter` | Law Firm Starter | Small law firm | 200 | Yes |
| `firm_professional` | Law Firm Professional | Growing law firm | 350 | Yes |
| `firm_premium` | Law Firm Premium | Established law firm | 600 | Yes |
| `institutional` | Institutional | Legal/justice partner | Contract | Yes |

Annual prices currently use approximately ten months of monthly pricing for twelve months of access where a standard annual price exists.

## Permission catalogue currently implemented

- `Dashboard.Access`
- `Cases.Create`
- `Cases.ViewOwn`
- `Cases.Manage`
- `Documents.Analyse`
- `Documents.Store`
- `Referrals.Request`
- `Referrals.Manage`
- `Leads.View`
- `Leads.Manage`
- `LawyerProfile.Manage`
- `Firm.Manage`
- `FirmUsers.Manage`
- `Analytics.View`
- `Billing.View`
- `Billing.Manage`
- `Directory.PriorityPlacement`

## Guest

Dashboard access: **No**

Allowed experience:
- Public landing page
- Pricing page
- Public legal information/resources
- Up to **3 successful Advanced Legal Triage Reports without registration**
- Find a Lawyer / Find Legal Aid
- Registration and sign-in

Guest-trial rules:
- A guest receives three successful legal-triage reports free of FijiLaw Credits.
- Failed triage workflows do not consume a guest attempt.
- The guest allowance is enforced by the API and persisted in `guest_triage_trials`; frontend state is informational only.
- The web client supplies a random guest trial identifier. The API stores only its one-way hash, not the raw identifier.
- After the third successful report, further guest triage requests are denied and the user is directed to create a free account or sign in.
- Guest document analysis remains unavailable; document analysis requires an authenticated member and FijiLaw Credits.
- Guest legal-triage access is additionally subject to API rate limiting and abuse controls.

Conversion path:

`Guest (3 free triage reports) -> Free Member -> Paid Member`

## Registered Free Member

Role: normally `citizen`
Plan: `free`
Dashboard access: **No full dashboard**

Allowed experience:
- Account/profile
- Email verification
- Security and password recovery
- Limited legal triage/public tools
- Pricing and upgrade flow

The account experience should preview paid dashboard value but must not call paid APIs as if access were granted.

## Personal Plus

Role: `citizen`
Plan: `personal_plus`
Dashboard access: **Yes**

Current entitlements:
- `Dashboard.Access`
- `Cases.Create`
- `Cases.ViewOwn`
- `Documents.Analyse`
- `Documents.Store`
- `Referrals.Request`
- `Billing.View`

Target dashboard modules:
- Overview
- My Legal Matters
- AI Legal Assistant
- Advanced Legal Triage Reports
- Documents
- Evidence
- Deadlines
- Lawyers
- Referrals
- Notifications
- Billing
- Account

## Lawyer Professional

Role: `lawyer`
Plan: `lawyer_professional`
Dashboard access: **Yes**

Current entitlements:
- `Dashboard.Access`
- `Cases.Manage`
- `Documents.Analyse`
- `Referrals.Manage`
- `Leads.View`
- `Leads.Manage`
- `LawyerProfile.Manage`
- `Analytics.View`
- `Billing.View`

Target dashboard modules:
- Overview
- Client Enquiries
- Referrals
- Matters
- AI Legal Research
- Document Analysis
- Clients
- Appointments
- My Professional Profile
- Analytics
- Billing
- Settings

## Law Firm Starter

Typical roles: `firm_admin`, `firm_staff`
Plan: `firm_starter`
Dashboard access: **Yes**

Current plan entitlements:
- `Dashboard.Access`
- `Cases.Manage`
- `Documents.Analyse`
- `Referrals.Manage`
- `Leads.View`
- `Leads.Manage`
- `Firm.Manage`
- `Analytics.View`
- `Billing.View`

Target focus:
- Firm overview
- Firm profile
- Lawyers
- Enquiries
- Referrals
- Basic analytics
- Billing

## Law Firm Professional

Typical roles: `firm_admin`, `firm_staff`
Plan: `firm_professional`
Dashboard access: **Yes**

Includes Starter entitlements plus:
- `FirmUsers.Manage`

Target modules:
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

## Law Firm Premium

Typical roles: `firm_admin`, `firm_staff`
Plan: `firm_premium`
Dashboard access: **Yes**

Includes Professional entitlements plus:
- `Directory.PriorityPlacement`

Target modules:
- All Professional modules
- Advanced analytics
- Enhanced directory profile
- Clearly labelled sponsored placement
- Advertising/promotional tools
- Multiple-office support
- Priority support

Sponsored placement MUST be clearly labelled and MUST NOT influence FijiLaw AI legal reasoning or neutral legal recommendations.

## Institutional Partner

Role: `institutional`
Plan: `institutional`
Dashboard access: **Yes**

The current database seed grants `Dashboard.Access` only. Additional institutional permissions must be introduced deliberately before institutional workflow APIs are exposed.

Target modules:
- Overview
- Referrals
- Cases/work queues where contractually authorised
- Offices
- Users
- Practitioners
- Service availability
- Legal demand trends
- Regional analytics
- Reports
- Administration

Institutional analytics should be aggregated/de-identified unless individual-case access is necessary and explicitly authorised.

## Platform Administrator

Role: `platform_admin`
Dashboard access: **Yes through role permissions**

The platform administrator role receives every currently registered permission.

Target modules:
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
- Cases / Referrals
- Commercial Analytics
- Security
- Audit Logs
- System Health
- Settings

Admin actions must remain auditable.

## Dashboard routing principles

1. `/dashboard` requires a valid authenticated session.
2. `Dashboard.Access` must be checked server-side.
3. A Free user receives the upgrade experience rather than dashboard data.
4. A paid user with an unverified email remains blocked from sensitive paid workflows where verification is required.
5. The dashboard shell may display only modules supported by the member's permissions.
6. Organisation roles and plan entitlements are additive, but privileged API endpoints must check the specific permission they require.
7. `platform_admin` access is role-based and audited, not dependent on buying a subscription.
8. Expired, cancelled or inactive subscriptions must stop granting paid plan entitlements.

## Planned permission extensions

These are planned and are NOT yet implemented in the backend permission catalogue:

- `Appointments.View`
- `Appointments.Manage`
- `Evidence.Manage`
- `Deadlines.Manage`
- `Notifications.Manage`
- `Clients.View`
- `Clients.Manage`
- `Research.Advanced`
- `Reports.Export`
- `Organisation.Offices.Manage`
- `Institution.Analytics.View`
- `Admin.Users`
- `Admin.Subscriptions`
- `Admin.Verification`
- `Admin.LegalCorpus`
- `Admin.AI`
- `Admin.Security`

These should only be added when their backing APIs and authorization tests are implemented.
