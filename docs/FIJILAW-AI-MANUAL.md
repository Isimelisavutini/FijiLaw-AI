# FijiLaw AI Manual

**System:** FijiLaw AI  
**Document type:** Product, User, Technical and Operational Manual  
**Version:** 1.0 Draft  
**Document date:** 24 August 2026  
**Repository:** `Isimelisavutini/FijiLaw-AI`  
**Primary jurisdiction:** Republic of Fiji  
**Document status:** Living system manual

---

## Document Purpose

This manual is the consolidated reference for FijiLaw AI. It explains what the system is, why it exists, who it serves, how the legal-AI workflow operates, how membership and FijiLaw Credits work, how the web and iOS clients connect to the platform, how access is controlled, what is already implemented, what is still planned, and what operational safeguards are required before a full public commercial launch.

The manual is intended for:

- FijiLaw product owners and administrators;
- developers and technical maintainers;
- lawyers and law firms considering use of the platform;
- justice-sector and institutional partners;
- investors, funders and commercial partners;
- reviewers assessing the legal-AI safety model;
- future staff joining the project;
- future AI agents or developers who need to understand the system without reconstructing the project history.

This manual is deliberately written as a source-of-truth document rather than a marketing brochure. Where the repository contains an implemented feature, this manual describes it as implemented. Where only a shell, integration adapter, workflow design or future capability exists, the manual labels it accordingly.

---

# 1. Executive Summary

FijiLaw AI is a Fiji-focused legal information, legal triage, legal research and legal-service connection platform. Its purpose is to improve access to justice by helping people understand a legal situation earlier, organize the relevant facts and documents, identify possible areas of Fijian law, retrieve verified legal authorities, determine what information is still missing, and identify safe next steps.

The current system is not represented as a licensed lawyer and is not designed to autonomously appear in court or provide unsupervised legal representation. Its current role is supervised legal information and legal triage. High-risk, uncertain, time-sensitive, representation-related or procedurally complex matters are intended to be escalated to a qualified Fiji legal practitioner.

The system combines several product layers:

1. a public FijiLaw web experience;
2. authenticated membership and subscription access;
3. an Advanced Legal Triage engine;
4. verified Fiji-law retrieval and source grounding;
5. legal-document analysis;
6. a legal-services directory;
7. user-specific dashboards;
8. FijiLaw Credits for metered AI usage;
9. persistent PostgreSQL data storage;
10. server-side payment infrastructure;
11. a native iOS application;
12. an administrative and future institutional operating layer.

The strategic vision is broader than a chatbot. FijiLaw AI is being designed as a legal-technology platform that can connect citizens, lawyers, law firms, legal-aid services and approved institutions while preserving separation between legal reasoning, commercial advertising, identity, billing and professional legal responsibility.

---

# 2. What FijiLaw AI Is About

## 2.1 The problem

Accessing legal assistance can be difficult when a person does not know:

- what area of law applies;
- whether the situation is urgent;
- which facts are legally important;
- what documents should be preserved;
- what legal office or practitioner may be appropriate;
- which legislation or procedural rule is relevant;
- whether a deadline may exist;
- how to explain the matter clearly to a lawyer.

Legal services may also be constrained by cost, geography, availability, administrative burden and limited legal literacy.

FijiLaw AI addresses the first stage of that problem: understanding, organizing, verifying and routing a legal matter.

## 2.2 The system objective

The objective is to provide a central Fiji-focused digital legal platform that can:

- help users describe a legal problem in ordinary language;
- classify the issue into one or more legal areas;
- retrieve verified Fiji legal authorities where available;
- refuse to invent statutes, cases, rules or deadlines when verification is unavailable;
- generate a structured Advanced Legal Triage Report;
- analyse supported legal documents;
- identify information and evidence gaps;
- connect users with legal services;
- support persistent member workspaces;
- support lawyer and law-firm workflow tools;
- support institutional access where authorised;
- create sustainable recurring revenue through subscriptions and FijiLaw Credits.

## 2.3 Long-term vision

The long-term vision is to progressively increase the capability of the legal agent while preserving human, professional and regulatory oversight. Any future movement toward autonomous legal representation, advocacy, filing or appearance would require separate legal, ethical, professional, regulatory and technical approval. Nothing in the current implementation should be interpreted as claiming that FijiLaw AI is licensed to practise law.

---

# 3. Current Scope and Status

## 3.1 Status legend

This manual uses the following status labels:

| Status | Meaning |
|---|---|
| **Implemented** | Code and/or persistent infrastructure exists in the repository and has been integrated into the application. |
| **Deployed** | The implemented capability has been deployed to a production or controlled-production environment. |
| **Scaffolded** | User interface or structural code exists, but one or more required backing APIs/data flows are incomplete. |
| **Prepared** | Integration code exists, but activation requires an external account, credential or approval. |
| **Planned** | Defined in architecture, workplan or product design but not yet fully implemented. |
| **Out of current scope** | Deliberately excluded from the present release. |

## 3.2 Current system summary

| Capability | Status | Notes |
|---|---|---|
| Public web application | Implemented / Deployed | Next.js frontend hosted through the Vercel workflow. |
| ASP.NET Core API | Implemented / Deployed | Production API runs separately from the web client. |
| PostgreSQL membership storage | Implemented / Deployed | Production database is Neon PostgreSQL. |
| Member registration and login | Implemented | PostgreSQL-backed credentials, sessions and security tables exist. |
| Email verification | Implemented / external delivery dependent | Email sender requires configured delivery provider/domain. |
| Password reset | Implemented / external delivery dependent | Secure token flow exists; email delivery depends on provider configuration. |
| Advanced Legal Triage | Implemented | Authenticated, metered workflow. |
| Verified legal-source retrieval | Implemented foundation | PostgreSQL legal-source store and retrieval layer exist. Corpus depth must continue to grow. |
| Document analysis | Implemented | Current endpoint supports PDF, DOCX and TXT extraction/analysis. |
| Legal-services directory | Implemented | Includes verified Legal Aid listings and listed private firms with verification states. |
| Paid dashboards | Implemented shell / partially backed | Shared role-aware dashboard exists; several modules still require persistent case/referral/analytics APIs. |
| FijiLaw Credits | Implemented / persistent | Wallets, allowances, reservations, debits, refunds and transaction history exist. |
| Windcave payment adapter | Prepared | Checkout and server verification exist; merchant credentials are still required for live purchases. |
| OpenAI inference | Prepared | Provider adapter exists; production key must remain server-side. |
| iOS application | Implemented MVP scaffold | Native SwiftUI client exists; App Store work remains. |
| Autonomous legal representation | Out of current scope | Requires future legal and regulatory approval. |

---

# 4. Core Product Principles

## 4.1 Verification before citation

FijiLaw AI must not invent legal authorities. If the system cannot retrieve and verify an applicable statute, provision, case, court rule or limitation requirement, it must state that the authority requires verification.

This principle applies especially to:

- legislation and section numbers;
- constitutional provisions;
- High Court or procedural rules;
- limitation periods;
- case names and holdings;
- statutory powers and decision-making authority.

## 4.2 No invented limitation deadlines

The Advanced Legal Triage engine is designed to avoid calculating a legal deadline unless an applicable procedural authority has been retrieved and verified. When no verified rule is available, the system should report that the limitation calculation has not been completed rather than infer a deadline from model memory.

## 4.3 Facts are different from legal interpretation

The system should preserve a distinction between:

- facts supplied by the user;
- information inferred from documents;
- retrieved legal authorities;
- AI-generated legal analysis;
- unresolved questions or missing information.

## 4.4 Human escalation

Human legal review is especially important for:

- urgent court deadlines;
- criminal charges or detention;
- domestic violence or immediate safety issues;
- constitutional litigation;
- judicial review;
- complex land disputes;
- contested public appointments;
- high-value commercial disputes;
- matters requiring representation, filing or appearance;
- any matter where the system has low retrieval confidence.

## 4.5 Commercial neutrality

Paid placement, law-firm subscription status or advertising must not influence neutral legal reasoning. Sponsored or enhanced directory placement must be clearly labelled and kept separate from AI legal conclusions and neutral referral logic.

---

# 5. User Types, Roles and Access

The system uses the following access model:

`Identity + Role + Active Subscription + Permissions = Dashboard Experience`

Authorization is enforced server-side. The browser or mobile app may hide unavailable features, but a client-side plan label can never be trusted as permission to access a paid or privileged API.

The canonical access matrix is maintained in [`../access.md`](../access.md).

## 5.1 Guest

**Role state:** anonymous  
**Dashboard:** no

Typical access:

- landing page;
- pricing information;
- public legal information;
- legal-services directory;
- registration and sign-in;
- other limited public tools where enabled.

The intended conversion path is:

`Guest -> Free Member -> Paid Member`

## 5.2 Registered Free Member

**Typical role:** `citizen`  
**Plan:** `free`  
**Full dashboard:** no

Free members can maintain an account and use introductory or limited platform functions. The free experience can preview paid dashboard value without bypassing paid API authorization.

## 5.3 Personal Plus

**Role:** `citizen`  
**Plan:** `personal_plus`  
**Monthly price:** FJD 20  
**Dashboard:** yes

Current entitlements include:

- `Dashboard.Access`
- `Cases.Create`
- `Cases.ViewOwn`
- `Documents.Analyse`
- `Documents.Store`
- `Referrals.Request`
- `Billing.View`

Target workspace modules include legal matters, AI assistance, saved reports, documents, evidence, deadlines, lawyers, referrals, notifications, billing and account settings.

## 5.4 Lawyer Professional

**Role:** `lawyer`  
**Plan:** `lawyer_professional`  
**Monthly price:** FJD 100  
**Dashboard:** yes

Current entitlements include case management, document analysis, referral management, lead management, professional-profile management, analytics and billing visibility.

The professional workspace is intended to support lawyers receiving and assessing FijiLaw-generated enquiries before accepting a matter.

## 5.5 Law Firm Starter

**Typical roles:** `firm_admin`, `firm_staff`  
**Plan:** `firm_starter`  
**Monthly price:** FJD 200

Focus:

- firm visibility;
- enquiries;
- referrals;
- basic analytics;
- firm profile management.

## 5.6 Law Firm Professional

**Typical roles:** `firm_admin`, `firm_staff`  
**Plan:** `firm_professional`  
**Monthly price:** FJD 350

Adds firm-team management and is intended to support operational workflows such as lead pipeline, cases, clients, staff, appointments, documents and referrals.

## 5.7 Law Firm Premium

**Typical roles:** `firm_admin`, `firm_staff`  
**Plan:** `firm_premium`  
**Monthly price:** FJD 600

Adds enhanced growth features such as priority directory placement. Any sponsored placement must remain clearly labelled and must not affect legal reasoning.

## 5.8 Institutional Partner

**Role:** `institutional`  
**Plan:** `institutional`  
**Pricing:** contract-based

Potential users include approved justice-sector bodies, Legal Aid, legal professional bodies, government entities and approved NGOs.

Institutional access must be explicitly permissioned. Individual-case information should not be exposed merely because a user belongs to an institution. Aggregated or de-identified analytics are preferred when detailed case access is not necessary.

## 5.9 Platform Administrator

**Role:** `platform_admin`

The administrator role is role-based rather than subscription-based. It receives privileged administration capabilities and all administrative actions should remain auditable.

---

# 6. Membership and Authentication

## 6.1 Registration

A member account contains identity data in the `app_users` table. Registration supports a requested plan code so a user can express commercial intent without the system falsely activating a paid subscription before payment succeeds.

The registration flow is conceptually:

```text
Select plan
   ↓
Register account
   ↓
Create Free membership
   ↓
Record requested plan
   ↓
Verify email
   ↓
Complete payment/activation later
```

## 6.2 Login

The API validates credentials server-side and issues an access session. Session tokens are stored as hashes in the database. The web and iOS clients send the access token as a bearer token to protected APIs.

## 6.3 Password storage

The membership schema stores:

- password salt;
- password hash;
- iteration count;
- credential timestamps.

Raw passwords must never be stored.

## 6.4 Sessions

`auth_sessions` stores:

- session identifier;
- user identifier;
- token hash;
- expiry time;
- optional revocation time;
- creation time.

Logout revokes the session.

## 6.5 Email verification

The system supports hashed email-verification tokens with expiry and consumption timestamps. Delivery requires a configured transactional email provider and verified sending domain.

## 6.6 Password recovery

The system supports:

1. a generic forgot-password response to reduce account enumeration;
2. a hashed password-reset token;
3. token expiry;
4. password replacement;
5. session revocation after successful password reset.

## 6.7 Membership audit trail

Membership events are recorded in `membership_audit_events`, including the affected user, acting user where relevant, event type, reason, metadata and timestamp.

---

# 7. Advanced Legal Triage

## 7.1 Purpose

Advanced Legal Triage converts a raw legal scenario into a structured legal-assessment report. It is not simply a conversational answer.

The engine attempts to:

- classify the legal issue;
- identify multiple relevant legal domains;
- retrieve verified authorities;
- explain the relevance of those authorities;
- identify legal vulnerabilities or potential causes of action;
- identify missing facts and evidence;
- identify procedural issues;
- recommend immediate next steps;
- identify when human legal escalation is appropriate.

## 7.2 Current legal taxonomy

The classifier can identify areas including:

- Public & Administrative Law;
- Constitutional Law;
- Public Governance;
- Criminal Procedure;
- Land & Customary Land;
- Employment;
- Tenancy;
- Family Law;
- Domestic Violence;
- Consumer Rights;
- General Legal Issue.

The design is multi-label. A matter can be classified across more than one legal area.

## 7.3 Advanced Legal Triage Report structure

The current report contract contains eight main sections.

### 1. Matter Metadata & Classification Taxonomy

Includes:

- case reference;
- priority;
- jurisdiction;
- primary legal area;
- secondary legal areas;
- doctrinal tags.

### 2. Automated Retrieval & Statutory Authorities

Contains verified authorities and explains why each authority may be relevant.

Typical authority categories include:

- Supreme Law;
- Procedural Rules;
- Primary Statute;
- Verified Authority.

### 3. Deep Legal Analysis & Vulnerability Matrix

The report can present:

- possible cause of action or legal issue;
- legal threshold or standard;
- fact application;
- risk rating.

### 4. Procedural Roadmap & Limitation Period Calculator

The system extracts candidate dates from the user's text, identifies whether a verified procedural authority was retrieved, and produces a procedural roadmap.

A deadline is not treated as verified merely because the AI model knows a commonly cited rule.

### 5. Evidence & Information Gap Analysis

Separates:

- facts established from the user input;
- available documents;
- critical missing documents;
- missing dates;
- facts requiring verification.

### 6. Recommended Immediate Actions

Provides ordered actions such as preserving evidence, obtaining missing documents or identifying the source of a public decision-making power.

### 7. Human Lawyer Escalation

Identifies whether human legal review is recommended and the urgency of that review.

### 8. Verification & Confidence Statement

Records:

- retrieval confidence;
- number of verified authorities;
- authorities requiring further verification;
- conclusions dependent on missing facts;
- generation timestamp.

## 7.4 Case references

Current generated report references use a structure similar to:

`FJ-[YYYY]-[MMDD]-[TAG]`

Examples of tags include constitutional, administrative, employment, criminal, land, family, domestic violence, consumer and tenancy categories.

## 7.5 Risk levels

The domain model contains:

- Low;
- Medium;
- High;
- Restricted.

Higher-risk or restricted matters trigger stronger human-review recommendations.

---

# 8. Legal Retrieval and RAG Architecture

## 8.1 Purpose

Retrieval-Augmented Generation is used to reduce unsupported legal claims and keep legal reasoning tied to a curated Fiji-law corpus.

The architecture separates:

1. legal source storage;
2. source verification status;
3. chunking and embeddings;
4. retrieval;
5. model generation;
6. user-visible citation and verification status.

## 8.2 Legal source storage

The database schema includes `legal_sources` with fields for:

- jurisdiction;
- source type;
- title;
- provision;
- canonical URL;
- effective date;
- verified status;
- full content;
- content hash;
- creation timestamp.

## 8.3 Legal source chunks

`legal_source_chunks` stores:

- source reference;
- chunk index;
- chunk content;
- vector embedding.

The current schema defines `vector(1536)` for embeddings.

## 8.4 Verification state

A legal source has an explicit `verified` property. The system should prefer verified sources and should not treat unverified material as authoritative merely because it is semantically similar to a query.

## 8.5 Retrieval quality objectives

The retrieval layer should continue to improve through:

- legal-domain metadata filtering;
- hybrid semantic and keyword retrieval;
- strict relevance thresholds;
- source-type filtering;
- effective-date awareness;
- jurisdiction filtering;
- exact provision matching;
- duplicate suppression;
- case-law citation normalization.

## 8.6 Corpus administration

The API includes an administrative legal-source ingestion endpoint. Ingestion requires server-side administrative authorization. Legal-source administration is a privileged operation because incorrect or unverified source material can directly affect legal analysis quality.

---

# 9. AI Provider Layer

## 9.1 Provider abstraction

The AI layer is designed behind an `ILanguageModelProvider` abstraction. This allows FijiLaw AI to change or extend model providers without rewriting the legal-agent domain logic.

## 9.2 OpenAI integration

The backend supports OpenAI when `OPENAI_API_KEY` is configured server-side.

The API key must never be:

- committed to GitHub;
- stored in public frontend variables;
- embedded in the iOS application;
- returned through API responses;
- included in screenshots or documentation.

`OPENAI_MODEL` is separately configurable so model changes do not require rewriting the application.

## 9.3 Provider-disabled behavior

When an AI provider is not configured, the application can fall back to safe non-model guidance and verified-source behavior rather than inventing legal content.

## 9.4 Legal-agent boundary

The language model is not the legal source of truth. The legal source store, verification flags, user-supplied facts and explicit procedural safeguards remain separate from model generation.

---

# 10. Legal Document Analysis

## 10.1 Current capability

The document-analysis endpoint currently supports:

- PDF;
- DOCX;
- TXT.

The MVP extracts text, constructs a legal-analysis request and sends the resulting context into the legal triage engine.

## 10.2 Current credit cost

Document analysis costs **15 FijiLaw Credits** when the workflow completes successfully.

## 10.3 Current storage behavior

The current endpoint processes uploaded files in memory and states that the uploaded file is not persisted by that endpoint.

Persistent secure document storage is a separate planned capability and must use explicit authorization, retention rules and secure storage controls.

## 10.4 OCR roadmap

Image-only scanned PDFs and large scanned court bundles require a dedicated OCR/vision pipeline. This remains an important future enhancement for Fiji, where legal and government documents may frequently be distributed as scanned copies.

## 10.5 Safety requirements

Document analysis should:

- avoid executing document content;
- enforce supported file types and size limits;
- prevent path traversal and unsafe filenames;
- avoid exposing file content to unauthorised users;
- apply retention and deletion policies when persistent storage is introduced;
- log analysis correlation IDs without unnecessarily logging private legal content.

---

# 11. Legal Services Directory

## 11.1 Purpose

The directory connects users with legal assistance rather than leaving the AI report as a dead end.

The current directory contains:

- Legal Aid Commission offices across Fiji;
- listed private law firms;
- city;
- address;
- phone where available;
- website where available;
- practice areas;
- verification status;
- verification note.

## 11.2 Verification model

Legal Aid listings are currently marked verified where sourced from the official Legal Aid directory.

Private firms may appear with a verification-pending state until practitioner or Fiji Law Society verification is completed.

## 11.3 Search

Users can search by:

- city;
- service type;
- legal area;
- name/address/text query.

## 11.4 Neutrality requirement

The directory must distinguish:

- verified status;
- ordinary listings;
- sponsored or enhanced placement.

Commercial placement must not be presented as a legal-quality ranking unless objective ranking criteria are separately defined and disclosed.

---

# 12. Dashboard System

The dashboard architecture is defined in [`DASHBOARD-BY-USER-WORKPLAN.md`](DASHBOARD-BY-USER-WORKPLAN.md).

## 12.1 Shared shell

The web dashboard uses a common shell with:

- top navigation;
- permission-aware sidebar;
- user/plan badge;
- responsive mobile navigation;
- KPI presentation;
- recent activity presentation;
- role-specific module entries.

## 12.2 Free-member experience

Free members do not receive the full paid dashboard. They receive an upgrade-oriented experience that can preview the value of paid features without loading protected paid data.

## 12.3 Personal Plus workspace

Implemented shell areas include:

- overview;
- legal matters;
- documents/evidence;
- lawyers/referrals;
- billing/account.

Persistent saved reports, deadlines and several case-management functions remain to be fully backed by data APIs.

## 12.4 Lawyer workspace

Implemented shell areas include:

- lawyer overview;
- enquiries/leads;
- matters;
- AI legal research entry;
- professional profile;
- analytics.

Referral review cards and deeper professional workflow require persistent referral/case data.

## 12.5 Law-firm workspace

Starter, Professional and Premium dashboard shells support progressively broader firm operations.

Current plan distinctions are enforced through permissions such as:

- `Firm.Manage`;
- `FirmUsers.Manage`;
- `Directory.PriorityPlacement`.

Premium growth tools and multi-office architecture remain planned.

## 12.6 Institutional workspace

The institutional shell includes referrals, offices/users and analytics surfaces. Institutional access is intentionally conservative; deeper permissions must be introduced only when backing APIs and contractual access rules are ready.

## 12.7 Administrator workspace

The administrator shell includes entry points for:

- users;
- memberships;
- subscriptions;
- firms and organisations;
- practitioner verification;
- legal corpus;
- AI operations;
- commercial analytics;
- security and audit.

Many administrative screens remain dependent on future live administration APIs.

---

# 13. FijiLaw Credits

## 13.1 What FijiLaw Credits are

FijiLaw Credits are FijiLaw's own prepaid or plan-included usage units for metered AI services.

They are not:

- OpenAI API tokens;
- cryptocurrency;
- stored cash;
- transferable OpenAI access;
- an API key.

Users buy or receive FijiLaw Credits and spend them on FijiLaw services. FijiLaw separately pays the infrastructure/model provider.

## 13.2 Plan allowances

| Plan | Included credits |
|---|---:|
| Free | 10 introductory |
| Personal Plus | 100 monthly |
| Lawyer Professional | 700 monthly |
| Law Firm Starter | 1,500 monthly |
| Law Firm Professional | 3,500 monthly |
| Law Firm Premium | 7,500 monthly |
| Institutional | 5,000 monthly default |

The current allowance key for paid plans is calendar-month based. Future subscription billing should align allowance renewal to the authoritative billing period.

## 13.3 Metered services currently implemented

| Service | Credit cost |
|---|---:|
| Advanced Legal Triage Report | 10 |
| Document analysis | 15 |

Additional services can be added later to the catalogue.

## 13.4 Credit packages

Current catalogue values are:

| Package | Credits | Price (FJD) |
|---|---:|---:|
| Starter | 50 | 10 |
| Standard | 120 | 20 |
| Plus | 300 | 45 |
| Professional | 750 | 100 |
| Firm | 2,000 | 250 |

These prices are product configuration and can be revised as cost, demand and margin data become available.

## 13.5 Credit transaction flow

A metered AI request follows this pattern:

```text
User starts AI service
        ↓
API authenticates member
        ↓
API calculates FijiLaw Credit cost
        ↓
Reserve credits atomically
        ↓
Run legal/AI workflow
        ↓
Success? -------------------- No
  ↓                           ↓
Complete debit           Refund reservation
  ↓                           ↓
Write ledger             Write refund ledger
```

This design prevents a failed AI workflow from permanently consuming reserved credits.

## 13.6 HTTP behavior

If a user lacks sufficient credits, the API returns HTTP `402 Payment Required` with information about the required credits, current balance and the credit-store path.

## 13.7 Credit data

The credit subsystem maintains:

- wallet balance;
- lifetime purchased credits;
- lifetime granted credits;
- lifetime used credits;
- allowance key;
- reservation/debit/refund history;
- provider/payment reference where applicable.

---

# 14. Payments

## 14.1 Web payment strategy

For Fiji-based web payments, the implemented adapter uses Windcave Hosted Payment Page infrastructure. Live processing remains dependent on approved merchant credentials.

## 14.2 Why hosted payment pages

The hosted model keeps card capture within the payment provider's secure environment. FijiLaw should store order and payment references but should not receive raw card numbers or CVV values.

## 14.3 Payment-order lifecycle

```text
User selects credit package
        ↓
FijiLaw creates pending payment order
        ↓
Backend creates Windcave session
        ↓
User enters payment details on hosted page
        ↓
Provider returns/notifies FijiLaw
        ↓
FijiLaw retrieves provider session server-to-server
        ↓
Exact order verification
        ↓
Authorised and matched?
     Yes        No
      ↓          ↓
Grant credits   No credits
      ↓
Mark order completed
```

## 14.4 Exact verification rules

Before purchased credits can be granted, the implementation verifies that provider data matches the internal order, including:

- session identifier;
- purchase type;
- amount;
- FJD currency;
- merchant reference;
- authorised transaction values.

Verification mismatches fail closed.

## 14.5 Idempotency

Repeated provider callbacks must not credit a wallet more than once. The payment order and credit grant path are designed to be idempotent.

## 14.6 Rate limiting

Checkout/status endpoints and provider-notification endpoints have separate rate-limit policies to reduce abuse while allowing legitimate payment-provider notifications.

## 14.7 External activation requirements

Live credit purchasing still requires:

- approved merchant facility;
- Windcave REST API username;
- Windcave API key;
- provider testing;
- approved refund/chargeback policy;
- commercial/legal review of credit terms.

## 14.8 iOS payment requirement

Credits consumed inside the native iOS application should use StoreKit 2 / Apple In-App Purchase for App Store distribution, with server-side transaction verification. The web Windcave flow should not simply be embedded as the final in-app digital-goods payment method.

---

# 15. Web Application

## 15.1 Technology

The web client uses Next.js and React.

## 15.2 Main public areas

The web experience includes or is designed to include:

- landing page;
- pricing;
- registration;
- sign-in;
- legal triage;
- document analysis;
- legal-services directory;
- credit store;
- member dashboard;
- account/security flows.

## 15.3 API communication

The web client communicates with the ASP.NET Core API through the configured `NEXT_PUBLIC_API_URL`.

Authentication-required requests send a bearer access token.

## 15.4 Production web URL

Current documented production URL:

`https://fijilaw-ai-pasifika-solutions.vercel.app`

This address may be replaced by a custom FijiLaw domain later.

---

# 16. Native iOS Application

The native iOS code is stored under [`../ios/FijiLaw-iOS`](../ios/FijiLaw-iOS).

## 16.1 Technology

- Swift;
- SwiftUI;
- iOS 17+ target;
- Xcode/XcodeGen project configuration;
- production Railway API integration;
- iOS Keychain for bearer-session storage.

## 16.2 Current iOS MVP features

The iOS client currently includes:

- sign in;
- account creation;
- secure session storage;
- profile view;
- logout;
- FijiLaw Credits balance;
- credit catalogue;
- Advanced Legal Triage;
- English, iTaukei and Fiji Hindi selection in the triage interface;
- document analysis;
- Legal Aid and law-firm search;
- phone and website actions;
- legal disclaimers.

## 16.3 Main tab structure

The current SwiftUI application uses tabs for:

- Home;
- AI Triage;
- Legal Help;
- Credits;
- Profile.

## 16.4 Build workflow

The Xcode project is generated through XcodeGen:

```bash
cd ios/FijiLaw-iOS
brew install xcodegen
xcodegen generate
open FijiLaw.xcodeproj
```

## 16.5 Before TestFlight/App Store

The iOS release still requires:

- Apple Developer Team configuration;
- approved App ID/bundle identifier;
- production icons and launch branding;
- StoreKit 2 products;
- server-side App Store transaction verification;
- privacy disclosures;
- legal review of Terms/Privacy content;
- accessibility testing;
- archive and TestFlight validation.

---

# 17. Backend API

## 17.1 Technology

The backend is ASP.NET Core running on .NET 8.

## 17.2 Main responsibilities

The API is responsible for:

- authentication;
- authorization;
- membership plans;
- legal retrieval;
- legal triage;
- document analysis;
- FijiLaw Credits;
- payment verification;
- legal-services directory;
- administrative legal-source ingestion;
- security controls and rate limiting.

## 17.3 Endpoint reference

### Health and configuration

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Service readiness and major integration status. |
| GET | `/api/membership/plans` | Active membership plans and entitlements. |

### Authentication and membership

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Create member account. |
| POST | `/api/auth/login` | Authenticate and create session. |
| POST | `/api/auth/logout` | Revoke current session. |
| POST | `/api/auth/forgot-password` | Request password recovery. |
| POST | `/api/auth/reset-password` | Complete password reset. |
| POST | `/api/auth/request-email-verification` | Request verification email. |
| POST | `/api/auth/verify-email` | Confirm verification token. |
| GET | `/api/membership/me` | Get authenticated member profile/access state. |
| GET | `/api/dashboard` | Get dashboard summary if authorised. |
| GET | `/api/authz/{permission}` | Test member permission. |

### Legal services

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/legal-services` | Search Legal Aid and listed law firms. |
| POST | `/api/legal/triage` | Run metered Advanced Legal Triage. |
| POST | `/api/legal/documents/analyse` | Run metered document analysis. |

### FijiLaw Credits

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/credits/catalog` | Credit packages, service prices and payment readiness. |
| GET | `/api/credits/wallet` | Current authenticated wallet. |
| GET | `/api/credits/history` | Credit transaction history. |
| POST | `/api/credits/checkout` | Create top-up checkout. |
| GET/POST | `/api/credits/payment/notify` | Payment-provider notification/reconciliation entry point. |
| GET | `/api/credits/payment/status/{orderId}` | Authenticated payment status and wallet reconciliation. |
| POST | `/api/admin/credits/grant` | Admin-only credit grant. |

### Administration

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/admin/legal-sources` | Administrative legal-source ingestion. |
| POST | `/api/admin/membership/users/{targetUserId}/roles/{roleCode}` | Platform-admin role assignment. |

## 17.4 API error behavior

Important HTTP responses include:

- `400 Bad Request` — invalid input;
- `401 Unauthorized` — missing or invalid session;
- `402 Payment Required` — insufficient FijiLaw Credits;
- `403 Forbidden` — authenticated but not authorised;
- `429 Too Many Requests` — rate limit exceeded;
- `503 Service Unavailable` — required external configuration or subsystem unavailable.

---

# 18. Data Architecture

## 18.1 Database platform

Production membership and credit data use PostgreSQL. The production deployment is connected to Neon PostgreSQL.

The schema also enables:

- `vector` extension for embeddings;
- `pgcrypto` for cryptographic/database support.

## 18.2 Legal data tables

### `legal_sources`
Stores verified or unverified legal source metadata and content.

### `legal_source_chunks`
Stores chunked source content and embeddings.

### `ai_audit_events`
Stores AI-related correlation/event payloads.

## 18.3 Membership tables

### `app_users`
Stores member identity information.

### `roles`
Stores role definitions.

### `user_roles`
Links users to roles.

### `subscription_plans`
Stores commercial plans and pricing.

### `subscriptions`
Stores active/inactive subscription state and future provider identifiers.

### `permissions`
Stores the permission catalogue.

### `role_permissions`
Links roles to permissions.

### `plan_entitlements`
Links subscription plans to permissions.

### `organisations`
Stores law firms and institutional organisations.

### `organisation_memberships`
Links users to organisations.

## 18.4 Security tables

### `user_credentials`
Password-hash storage.

### `auth_sessions`
Hashed session tokens and expiry/revocation state.

### `email_verification_tokens`
Hashed verification tokens.

### `password_reset_tokens`
Hashed password-reset tokens.

### `membership_audit_events`
Membership/security action audit trail.

## 18.5 Usage and billing foundation

### `usage_ledger`
Tracks usage type, quantity, cost estimate and correlation information.

### `billing_events`
Stores external billing event references and payload metadata.

## 18.6 FijiLaw Credit storage

The runtime credit subsystem maintains persistent wallet, transaction and payment-order data. It records balances and all major credit lifecycle events so that usage can be reconciled.

---

# 19. Authorization Model

## 19.1 Principle

Permissions, not frontend visibility, determine access.

## 19.2 Current permission catalogue

Current permissions include:

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
- `Admin.Users`
- `Admin.Subscriptions`
- `Admin.Verification`
- `Admin.LegalCorpus`
- `Admin.AI`

The access matrix in [`../access.md`](../access.md) remains the product-level source of truth.

## 19.3 Subscription rules

A paid plan is not the same thing as a role.

Examples:

- a `lawyer` identity does not automatically receive every paid lawyer feature if the required subscription/permission is inactive;
- a `platform_admin` receives privileged role permissions without buying a subscription;
- institutional privileges require deliberate configuration;
- cancelled or expired subscriptions should stop granting paid plan entitlements.

---

# 20. Security Architecture

## 20.1 Secret handling

The following must remain server-side:

- OpenAI API key;
- database connection string;
- Resend API key;
- Windcave credentials;
- administrative API keys.

No production secret should use a `NEXT_PUBLIC_*` frontend variable.

## 20.2 Response security headers

The backend applies security-oriented headers including:

- `X-Content-Type-Options: nosniff`;
- restrictive referrer policy;
- no-store cache behavior for sensitive API responses.

## 20.3 Rate limiting

The backend defines rate-limit policies for:

- authentication;
- verification/recovery;
- payments;
- payment-provider notifications.

## 20.4 CORS

Allowed browser origins are controlled through configured web-origin settings plus approved FijiLaw deployment host rules.

## 20.5 Auditability

Security-relevant and membership operations should produce auditable events. Legal-AI workflows should maintain correlation identifiers so retrieval, generation, credit usage and support incidents can be traced without relying on user-visible text alone.

## 20.6 Demo accounts

Controlled demo accounts are useful during development but must not remain casually exposed during public paid launch. The production checklist requires disabling/removing demo seeding after controlled testing.

---

# 21. Privacy and Legal-Data Handling

Legal questions and uploaded documents can contain highly sensitive personal information. FijiLaw AI should apply privacy-by-design principles.

## 21.1 Data minimisation

Users should be reminded not to submit unnecessary information such as:

- passwords;
- banking PINs;
- unrelated personal identifiers;
- irrelevant third-party sensitive information.

## 21.2 Document retention

The current document-analysis endpoint does not persist the uploaded file. When persistent document storage is introduced, the system must define:

- storage location;
- encryption requirements;
- retention period;
- deletion process;
- member access rules;
- lawyer/institution sharing rules;
- audit requirements;
- breach-response procedures.

## 21.3 Institutional analytics

Institutional analytics should be aggregated or de-identified whenever individual-case access is not needed.

## 21.4 Model-provider privacy

Legal-content processing through external AI providers must be governed by the selected provider configuration, organisational data controls, contractual requirements and FijiLaw privacy disclosures.

---

# 22. Deployment Architecture

## 22.1 Logical production architecture

```text
                    ┌──────────────────────┐
                    │      End Users       │
                    │ Web / iOS / Future   │
                    └──────────┬───────────┘
                               │
                               ▼
                  ┌──────────────────────────┐
                  │   Next.js Web / SwiftUI  │
                  └────────────┬─────────────┘
                               │ HTTPS
                               ▼
                  ┌──────────────────────────┐
                  │  ASP.NET Core FijiLaw API│
                  │        Railway           │
                  └───────┬────────┬─────────┘
                          │        │
               ┌──────────┘        └────────────┐
               ▼                                ▼
      ┌───────────────────┐          ┌────────────────────┐
      │ Neon PostgreSQL   │          │ External Providers │
      │ users / RAG /     │          │ OpenAI / Resend /  │
      │ credits / audit   │          │ Windcave           │
      └───────────────────┘          └────────────────────┘
```

## 22.2 Current runtime responsibilities

### Vercel
Hosts the Next.js frontend build/deployment workflow.

### Railway
Hosts the ASP.NET Core production API.

Current documented API URL:

`https://fijilaw-api-production-production.up.railway.app`

### Neon
Provides persistent PostgreSQL storage.

### GitHub
Stores source control and CI/workflow definitions.

## 22.3 Health endpoint

`/health` reports service readiness and major integration state, including storage mode, credit-metering state, payment readiness, email-delivery readiness and AI-provider status.

---

# 23. Environment Configuration

The repository contains `.env.example` as a configuration reference. Production values must be stored in the relevant hosting platform's secret/environment-variable system, not committed to source.

Key configuration categories include:

| Category | Example variables |
|---|---|
| Web/API URLs | `NEXT_PUBLIC_API_URL`, `PUBLIC_WEB_URL`, `PUBLIC_API_URL` |
| Browser origins | `WebOrigin`, `AllowedWebOrigins` |
| AI provider | `OPENAI_API_KEY`, `OPENAI_MODEL` |
| Database | `DATABASE_URL` |
| Email | `RESEND_API_KEY`, `EMAIL_FROM` |
| Payments | `WINDCAVE_API_USERNAME`, `WINDCAVE_API_KEY`, optional `WINDCAVE_API_BASE` |
| Administration | `ADMIN_API_KEY` |
| Optional storage/search | Azure storage/search variables where used |

Environment-variable documentation may evolve as architecture changes. Runtime code and deployment configuration take precedence over stale example variables.

---

# 24. User Guide — Citizen / Personal Member

## 24.1 Create an account

1. Open the FijiLaw website or iOS application.
2. Select registration.
3. Enter the required account details.
4. Create the account.
5. Complete email verification when delivery is configured.
6. Sign in.

## 24.2 Check plan and credits

After sign-in, the member can view the current plan and FijiLaw Credit balance.

The Free plan receives introductory credits. Paid plans receive larger periodic allowances according to the current catalogue.

## 24.3 Run Advanced Legal Triage

1. Open **Tell Me My Rights** or **AI Legal Triage**.
2. Describe what happened.
3. Include relevant dates, documents, decision-makers and the outcome sought.
4. Do not include passwords or unnecessary sensitive information.
5. Submit the request.
6. The API reserves the required credits.
7. If the workflow succeeds, the debit is completed.
8. Review the report, especially missing information, verified authorities and human-review recommendations.

## 24.4 Upload a document

1. Open document analysis.
2. Select a supported PDF, DOCX or TXT file.
3. Submit the document.
4. The system extracts text and runs legal analysis.
5. Review the assessment and the disclaimer.

## 24.5 Find legal help

1. Open **Find Legal Help**.
2. Search by city, firm or legal area.
3. Review verification status.
4. Use phone or website contact actions where provided.

## 24.6 Buy more credits

On the web, live payment becomes available after the Windcave merchant integration is activated.

On iOS, App Store distribution should use StoreKit 2 rather than relying on the web credit checkout inside the app.

---

# 25. User Guide — Lawyer Professional

A lawyer account is intended to receive professional workflow tools in addition to general FijiLaw AI services.

Current and target functions include:

- professional profile;
- client enquiries;
- referrals;
- matter management;
- AI legal research;
- document analysis;
- analytics;
- billing.

A key design principle is that a lawyer should be able to review the AI triage summary before accepting a referral. This can reduce intake friction while preserving the lawyer's independent professional judgment.

No AI-generated report should be treated as a substitute for the lawyer's own review of the client, facts, documents and law.

---

# 26. User Guide — Law Firm

Law-firm accounts use organisation-aware access.

## 26.1 Starter

Focuses on profile, enquiries, referrals and basic analytics.

## 26.2 Professional

Adds team-management entitlement and broader workflow tooling.

## 26.3 Premium

Adds enhanced directory placement and future growth tools.

## 26.4 Firm-admin responsibilities

Firm administrators should eventually be able to:

- manage firm users;
- manage firm profile information;
- assign internal responsibility for leads/referrals;
- review analytics;
- manage billing;
- ensure practitioner information remains accurate.

The deeper case/client/team APIs required for this workflow are still part of the dashboard backing-data roadmap.

---

# 27. User Guide — Institutional Partner

Institutional access is contract- and permission-sensitive.

Potential functions include:

- referral queues;
- office/service availability;
- authorised case work queues;
- user/practitioner management;
- legal-demand trends;
- regional analytics;
- reporting.

Institutional users must not assume that dashboard access automatically grants access to personal legal matters. Each sensitive workflow requires explicit authorisation.

---

# 28. Administrator Guide

## 28.1 Administrator role

The `platform_admin` role is the highest platform role and should be tightly controlled.

## 28.2 Administrative responsibilities

Administrator responsibilities include:

- member administration;
- subscription oversight;
- role assignment;
- organisation verification;
- practitioner verification;
- legal corpus management;
- credit adjustments;
- AI operations;
- audit/security review;
- commercial analytics;
- system-health monitoring.

## 28.3 Credit grants

Administrative credit grants are supported through a protected API. Grants should include a reason and provider/audit reference so unexplained manual balance changes are avoided.

## 28.4 Legal source ingestion

Only approved administrators should ingest verified legal material. Verification should include source provenance, canonical URL, document identity, effective date where relevant and content integrity.

## 28.5 Role assignment

Role assignment is a privileged action and should be auditable. A user should never be promoted to a privileged role through client-side state alone.

---

# 29. Commercial Model

FijiLaw AI is designed around recurring and usage-based revenue.

## 29.1 Revenue streams

Primary planned revenue streams are:

- monthly/annual subscriptions;
- FijiLaw Credit top-ups;
- law-firm professional plans;
- institutional contracts;
- clearly labelled directory enhancement/advertising;
- future professional workflow services.

## 29.2 Why subscriptions and credits are separate

Subscriptions provide predictable recurring revenue and access to product features. FijiLaw Credits meter variable AI consumption and help protect platform margins.

The combined model is:

```text
Subscription revenue
        +
AI credit top-ups
        +
Institutional / professional services
        ↓
Sustainable FijiLaw platform revenue
```

## 29.3 AI cost control

The credit model makes it possible to measure:

- cost per AI workflow;
- credits consumed per member;
- AI cost per plan;
- gross margin;
- high-cost workflows;
- top-up demand.

## 29.4 Business metrics

The administrator dashboard is intended to track metrics such as:

- registered users;
- paid members;
- monthly recurring revenue (MRR);
- annual recurring revenue (ARR);
- average revenue per user (ARPU);
- churn;
- free-to-paid conversion;
- AI cost per subscriber;
- gross margin;
- active lawyers;
- active law firms;
- AI assessments.

Many of these metrics still require completed analytics aggregation APIs before they can be treated as live production metrics.

---

# 30. Safety, Ethics and Professional Responsibility

## 30.1 Current legal position of the system

FijiLaw AI provides legal information and triage. It is not currently a licensed legal practitioner and should not independently provide legal representation.

## 30.2 Required footer principle

User-visible legal reports should retain a disclaimer equivalent to:

> FijiLaw AI provides legal information, legal triage and AI-assisted legal research. It does not independently provide legal representation. Authorities, limitation periods and procedural requirements should be verified against current Fiji law before legal action is taken.

## 30.3 Professional oversight

Lawyers remain responsible for their own professional advice, legal strategy, filings, representations and client duties.

## 30.4 Escalation rather than overconfidence

The system should prefer:

- `requires verification` over an invented citation;
- `human review recommended` over unsupported certainty;
- `deadline not calculated` over an unverified limitation date;
- `insufficient information` over a confident but speculative conclusion.

---

# 31. Testing and Verification

## 31.1 Backend build validation

The .NET backend should be restored, built and published in Release configuration before deployment.

## 31.2 Health checks

Railway deployments use `/health` as the application health-check path.

## 31.3 Frontend validation

The Next.js application should pass type checking and production build validation before being treated as release-ready.

## 31.4 iOS validation

The iOS workflow should:

- generate the Xcode project;
- compile on a macOS runner;
- validate iOS deployment target;
- test archive locally before TestFlight.

## 31.5 Authorization testing

Every protected feature should be tested for:

- unauthenticated denial;
- free-plan denial where applicable;
- paid-plan grant;
- incorrect-role denial;
- expired/cancelled subscription behavior;
- platform-admin behavior;
- direct API access without relying on UI hiding.

## 31.6 Credit testing

Credit tests should confirm:

- allowance grant;
- successful reservation;
- successful debit;
- failed-workflow refund;
- insufficient-credit `402`;
- no double debit;
- no double purchase grant;
- transaction history consistency.

## 31.7 Payment testing

Before live launch, test:

- approved payment;
- declined payment;
- cancelled payment;
- provider timeout;
- repeated callback;
- mismatched amount;
- mismatched currency;
- mismatched merchant reference;
- mismatched session;
- authorised transaction with mismatched metadata.

No mismatch should result in credit issuance.

---

# 32. Operations and Monitoring

## 32.1 Operational surfaces

Key operational systems include:

- GitHub source/CI;
- Vercel frontend deployment;
- Railway API deployment and logs;
- Neon PostgreSQL;
- OpenAI usage/budget controls when enabled;
- email-delivery provider;
- Windcave merchant portal when enabled;
- future App Store Connect for iOS.

## 32.2 Incident categories

Common incident categories may include:

- API unavailable;
- database connection failure;
- AI provider failure;
- insufficient credits;
- payment-provider failure;
- email-delivery failure;
- legal-source retrieval quality issue;
- incorrect user entitlement;
- suspected credential exposure;
- privacy/security incident.

## 32.3 Incident priority

Security, privacy, payment-integrity and incorrect legal-authority issues should receive higher priority than ordinary interface defects.

---

# 33. Troubleshooting Guide

## 33.1 “Member accounts are temporarily unavailable”

Likely cause: membership database is not connected or the backend is in a fallback configuration.

Check:

- `DATABASE_URL`;
- database connectivity;
- API `/health`;
- database initialization logs.

## 33.2 Login returns unauthorised

Check:

- email/password accuracy;
- active user status;
- credential record;
- session creation logs;
- rate limiting.

## 33.3 Dashboard is blocked

Check:

- authenticated session;
- plan status;
- `Dashboard.Access` permission;
- subscription expiry/cancellation;
- role/plan entitlement resolution.

## 33.4 AI triage returns 401

The current metered legal-AI endpoint requires authentication. Confirm the bearer token is attached.

## 33.5 AI triage returns 402

The member does not have enough FijiLaw Credits. Check `/api/credits/wallet` and the credit catalogue.

## 33.6 Payment checkout is unavailable

If Windcave credentials are not configured, the system intentionally reports that online credit purchase is not yet available.

Check:

- `WINDCAVE_API_USERNAME`;
- `WINDCAVE_API_KEY`;
- `PUBLIC_WEB_URL`;
- `PUBLIC_API_URL`;
- provider readiness.

## 33.7 AI provider is disabled

Check `/health` and the server-side `OPENAI_API_KEY` configuration. Never place the key in the browser or iOS app.

## 33.8 Verification email is not received

Check:

- transactional email API key;
- verified sending domain;
- configured `EMAIL_FROM`;
- provider logs;
- audit event for send success/failure.

---

# 34. Known Gaps and Current Limitations

The following limitations remain important:

1. The legal corpus must continue expanding and be maintained for currency and accuracy.
2. Case-management persistence is not yet complete across all dashboard modules.
3. Referral persistence and lawyer acceptance workflow require further implementation.
4. Lead-pipeline backing data is not complete.
5. Appointments, notifications and deadline management require persistent models/APIs.
6. Institutional permission depth remains deliberately limited.
7. Premium marketing/growth tooling remains incomplete.
8. Live Windcave credit purchases require merchant credentials and provider testing.
9. Production OpenAI inference requires a server-side API key and budget controls.
10. Persistent legal-document storage requires a completed secure-storage/retention design.
11. OCR for scanned legal documents remains a roadmap item.
12. iOS StoreKit 2 payments are not yet complete.
13. App Store privacy, legal and accessibility review remains outstanding.
14. Autonomous legal practice or court representation is not implemented or licensed.

---

# 35. Development Roadmap

## Phase A — Legal foundation

- expand verified Fiji legislation corpus;
- ingest and normalize procedural rules;
- introduce verified Fiji case-law corpus;
- add source effective-date/version management;
- improve multi-domain legal classification;
- improve hybrid retrieval and domain filtering.

## Phase B — Member case workspace

- persistent legal matters/cases;
- saved Advanced Legal Triage Reports;
- evidence records;
- deadlines;
- case timeline;
- secure document storage;
- notifications.

## Phase C — Lawyer and referral network

- practitioner verification workflow;
- referral persistence;
- lawyer acceptance/decline;
- consultation scheduling;
- professional case-preparation views;
- client-lawyer communication controls.

## Phase D — Law-firm operations

- lead pipeline;
- team management;
- client/matter operations;
- multiple offices;
- firm analytics;
- premium marketing features.

## Phase E — Institutional integration

- contract-specific institutional permissions;
- Legal Aid/referral work queues;
- office/service-availability management;
- de-identified justice-demand analytics;
- reporting/export controls.

## Phase F — Commercial production

- activate live payment merchant facility;
- implement subscription billing provider workflow;
- align credit allowance renewal to billing periods;
- refunds/chargebacks;
- invoices/receipts;
- revenue reconciliation;
- business analytics.

## Phase G — Mobile release

- branded iOS UI;
- App Store icon/launch assets;
- StoreKit 2;
- server-side Apple transaction verification;
- push notifications;
- universal links;
- accessibility review;
- TestFlight;
- App Store submission.

## Phase H — Advanced regulated legal agent

Any future progression toward lawyer-equivalent or advocacy functions must be treated as a separate regulatory programme. It would require legal authority, professional responsibility rules, identity/representation controls, evidence handling, filing controls, auditability, insurance/liability analysis and formal human/regulatory oversight.

---

# 36. Repository Structure

Current high-level repository structure:

```text
FijiLaw-AI/
├── src/
│   ├── FijiLaw.AI/
│   ├── FijiLaw.Api/
│   ├── FijiLaw.Domain/
│   ├── FijiLaw.Infrastructure/
│   └── FijiLaw.Web/
├── ios/
│   └── FijiLaw-iOS/
├── database/
│   ├── init.sql
│   └── 02-membership-security.sql
├── docs/
│   ├── DASHBOARD-BY-USER-WORKPLAN.md
│   ├── FIJILAW-CREDITS-IMPLEMENTATION-STATUS.md
│   ├── PRODUCTION-INTEGRATION-CHECKLIST.md
│   └── FIJILAW-AI-MANUAL.md
├── tests/
├── access.md
├── Dockerfile
├── docker-compose.yml
├── FijiLaw.sln
├── README.md
└── vercel.json
```

## 36.1 `FijiLaw.Domain`

Contains core domain records, membership models, credit models and legal-triage contracts.

## 36.2 `FijiLaw.AI`

Contains legal-agent logic, model-provider abstraction and source-retrieval integration.

## 36.3 `FijiLaw.Infrastructure`

Contains PostgreSQL persistence and other infrastructure concerns.

## 36.4 `FijiLaw.Api`

Contains ASP.NET Core endpoints, service registration, authentication flow, credits/payment endpoints, document extraction and legal-services directory.

## 36.5 `FijiLaw.Web`

Contains the Next.js web application.

## 36.6 `ios/FijiLaw-iOS`

Contains the native SwiftUI client.

---

# 37. Source-of-Truth Documents

| File | Purpose |
|---|---|
| [`../README.md`](../README.md) | High-level project description and architecture. |
| [`../access.md`](../access.md) | Role, plan and permission source of truth. |
| [`DASHBOARD-BY-USER-WORKPLAN.md`](DASHBOARD-BY-USER-WORKPLAN.md) | Dashboard architecture and completion checklist. |
| [`FIJILAW-CREDITS-IMPLEMENTATION-STATUS.md`](FIJILAW-CREDITS-IMPLEMENTATION-STATUS.md) | FijiLaw Credits and payment implementation status. |
| [`PRODUCTION-INTEGRATION-CHECKLIST.md`](PRODUCTION-INTEGRATION-CHECKLIST.md) | External production integration requirements. |
| [`../database/init.sql`](../database/init.sql) | Core legal, membership, role, subscription and permission schema. |
| [`../database/02-membership-security.sql`](../database/02-membership-security.sql) | Credential, session, verification, password reset and audit schema. |
| [`../ios/FijiLaw-iOS/README.md`](../ios/FijiLaw-iOS/README.md) | Native iOS architecture and release requirements. |

If this manual conflicts with executable code, database migration/initializer behavior or an explicitly updated source-of-truth file, the implementation/source-of-truth file should be reviewed and the manual updated.

---

# 38. Production Readiness Checklist

Before a full public paid launch, confirm all of the following:

- production database backups/recovery approach is defined;
- demo account seeding is disabled or secured;
- email verification delivery is operational;
- password recovery delivery is operational;
- production AI provider key is configured server-side;
- model project budget/rate limits are set;
- Windcave merchant credentials are configured;
- payment approved/declined/cancelled flows are tested;
- payment mismatch and duplicate-callback tests pass;
- credit refund/expiry/chargeback terms are published;
- Privacy Policy is reviewed;
- Terms of Service are reviewed;
- AI/legal disclaimer is reviewed;
- document retention and deletion rules are defined;
- practitioner verification process is defined;
- incident response and credential-rotation processes are defined;
- logging does not expose secrets or unnecessary legal content;
- role/permission tests pass;
- free/expired users cannot bypass dashboard restrictions;
- sensitive administrator actions are auditable;
- sponsored placement is clearly labelled;
- iOS distribution uses Apple-compliant digital-credit purchasing;
- legal/regulatory review is completed before representing any AI function as legal practice.

---

# 39. Glossary

**Advanced Legal Triage Report** — structured legal-assessment output containing classification, retrieved authorities, vulnerability analysis, procedural roadmap, evidence gaps, recommended actions, human escalation and verification statement.

**AI provider** — external or internal model service used to generate language-model output.

**Authority** — legal source such as legislation, constitutional provision, procedural rule or case law.

**Correlation ID** — identifier used to connect events, retrievals, AI workflow and audit records.

**FijiLaw Credits** — FijiLaw-owned usage units used to pay for metered FijiLaw AI services.

**Human review** — review by a qualified person, including a legal practitioner where legal advice or representation is required.

**Legal corpus** — curated collection of legal sources used by retrieval.

**Permission** — server-side capability code controlling access to an API or feature.

**RAG** — Retrieval-Augmented Generation; a workflow that retrieves relevant source material before model generation.

**Role** — identity/business function such as citizen, lawyer, firm administrator, institutional user or platform administrator.

**Subscription plan** — commercial product tier that can grant entitlements.

**Verified authority** — legal source that has passed the platform's verification requirements and is marked verified in the source store.

**Windcave** — hosted payment-provider integration currently prepared for web FijiLaw Credit purchases.

---

# 40. Documentation Maintenance Rules

This manual should be updated whenever any of the following changes:

- user roles;
- plan pricing;
- permission codes;
- credit allowances;
- AI service credit costs;
- payment provider;
- legal-triage report schema;
- supported document formats;
- deployment architecture;
- production URLs;
- iOS/App Store model;
- legal disclaimer;
- privacy/retention rules;
- database schema;
- major roadmap status.

For every material release, update the document date and add an entry to the change history.

---

# 41. Change History

| Version | Date | Summary |
|---|---|---|
| 1.0 Draft | 24 Aug 2026 | First consolidated FijiLaw AI product, user, technical and operational manual. |

---

# Appendix A — Example End-to-End Citizen Flow

```text
Citizen opens FijiLaw
        ↓
Creates account / signs in
        ↓
Checks FijiLaw Credits
        ↓
Describes legal situation
        ↓
Credits reserved
        ↓
Legal classifier detects domains
        ↓
Verified Fiji-law retrieval
        ↓
Advanced Legal Triage generated
        ↓
Credits completed or refunded on failure
        ↓
User reviews missing evidence and next steps
        ↓
Find Legal Help
        ↓
Legal Aid / verified practitioner / firm
        ↓
Human legal review where required
```

---

# Appendix B — Example Public/Administrative Law Flow

```text
User describes public decision or appointment dispute
        ↓
Classification
  Public & Administrative Law
  Constitutional Law (if applicable)
  Public Governance (if applicable)
        ↓
Request decision/appointment date
Request decision-maker/public body
Request instrument/notice/Gazette material
        ↓
Retrieve verified Fiji authorities
        ↓
Check source of legal power
Check procedural requirements
Check standing/remedy questions
        ↓
Procedural deadline verified?
   Yes → show fact-specific status
   No  → do not calculate from memory
        ↓
Recommend qualified Fiji lawyer review
```

---

# Appendix C — Example Credit Purchase Flow

```text
Member selects 120-credit Standard package
        ↓
API creates internal FJD payment order
        ↓
Windcave session created
        ↓
Customer pays on hosted provider page
        ↓
Provider notifies/returns customer
        ↓
FijiLaw retrieves session directly
        ↓
Verify:
  session ID
  purchase type
  FJD currency
  exact amount
  merchant reference
  authorised transaction
        ↓
All values match?
  Yes → grant 120 credits once
  No  → no credit grant
```

---

# Appendix D — Word Edition Preparation

This Markdown manual is intentionally structured for later conversion to a professional Word manual.

The Word edition should use:

- Word `Title`, `Heading 1`, `Heading 2` and body styles rather than manual formatting;
- an automatically generated table of contents;
- controlled page margins and section breaks;
- repeated header rows for long tables;
- page numbers and document version in the footer;
- a document-control page;
- a cover page;
- consistent warning/note styles;
- diagrams rendered as clean figures where appropriate;
- PDF export for final pagination/layout verification.

The Markdown content should remain the editable source for system facts, while the Word edition becomes the polished stakeholder/user deliverable.

---

# Appendix E — Legal Notice

This manual describes the FijiLaw AI software system and its intended safeguards. It is not legal advice, does not itself determine whether FijiLaw AI or any future AI agent is permitted to practise law, and does not replace formal legal, regulatory, privacy, cybersecurity, payments or App Store review.

The current product position is that FijiLaw AI provides legal information, legal triage and AI-assisted legal research and does not independently provide legal representation.
