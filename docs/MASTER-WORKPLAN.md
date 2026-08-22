# FijiLaw AI — Master Development Workplan

This file is the authoritative implementation checklist for FijiLaw AI. Update it whenever an item is completed. Do not mark an item complete until it has been implemented and, where applicable, verified in production.

Status: [x] complete · [~] in progress · [ ] not started

## Phase 0 — Stabilise Current MVP
- [x] GitHub repository established
- [x] ASP.NET Core backend
- [x] Next.js frontend
- [x] Railway backend deployment
- [x] Vercel frontend deployment
- [x] /health endpoint
- [x] Production CORS controls
- [x] Frontend API-health monitoring
- [x] User-friendly connectivity errors
- [x] Deployment/resilience documentation
- [x] Legal triage MVP
- [x] PDF/DOCX/TXT document extraction
- [x] Legal services directory MVP
- [x] Curated verified Fiji-law fallback
- [x] Section-level authority retrieval
- [ ] Verify current Vercel production deployment end-to-end
- [ ] Test triage production browser → Railway → response
- [ ] Test document upload end-to-end
- [ ] Test legal-services directory end-to-end
- [ ] Remove obsolete/failed Railway services
- [ ] Establish development, staging and production environments
- [ ] Add automated deployment smoke tests

Exit: user can reliably perform triage, document analysis and directory searches in production.

## Phase 1 — Legal Knowledge Infrastructure
### Database
- [ ] Provision PostgreSQL
- [ ] Enable pgvector
- [ ] Configure DATABASE_URL
- [ ] Create database migrations
- [ ] Create legal-source tables
- [ ] Create legal-document tables
- [ ] Create sections/provisions table
- [ ] Create embeddings table
- [ ] Create source-version history
- [ ] Create ingestion audit table

### Fiji legal corpus
Initial corpus: Constitution; Employment Relations Act; FCCC legislation; Family Law Act; Domestic Violence Act; Criminal Procedure Act; iTaukei Land Trust Act; Land Transfer Act; Legal Practitioners Act; Agricultural Landlord and Tenant Act.

For each source:
- [ ] Retrieve authoritative text
- [ ] Store title / Act / year / section
- [ ] Store provision text
- [ ] Store official source URL
- [ ] Store effective/commencement information where available
- [ ] Store verification status
- [ ] Store source hash
- [ ] Record ingestion date
- [ ] Track amendments/repeals

### Retrieval
- [ ] Chunk legislation intelligently
- [ ] Generate embeddings
- [ ] Semantic/vector search
- [ ] Keyword search
- [ ] Hybrid search
- [ ] Filter by legal domain
- [ ] Filter by jurisdiction
- [ ] Rank authorities
- [ ] Remove irrelevant authorities
- [ ] Citation validator
- [ ] Source freshness checks

Exit: correct provisions retrieved for benchmark questions with very low irrelevant-source retrieval.

## Phase 2 — AI Reasoning Layer
- [x] ILanguageModelProvider architecture
- [x] OpenAI provider code
- [x] Deterministic fallback
- [ ] Configure production model API credentials
- [ ] Model router
- [ ] Low-cost classification model
- [ ] Legal reasoning model
- [ ] Extraction model
- [ ] Provider fallback
- [ ] Ollama provider
- [ ] OpenAI-compatible provider
- [ ] Qwen/Llama/Mistral testing
- [ ] Token/cost monitoring
- [ ] Prompt versioning
- [ ] Enforce facts → retrieval → reasoning → citation verification → safety → answer pipeline

## Phase 3 — Conversational Legal Interview
- [ ] Conversation/session model
- [ ] Dynamic follow-up questions
- [ ] Legal-area-specific interviews
- [ ] Extract parties/dates/events/outcome/evidence
- [ ] Identify missing evidence
- [ ] Detect deadlines
- [ ] Build timeline
- [ ] User correction/confirmation of extracted facts
- [ ] Generate final case summary

## Phase 4 — Authentication & My Cases
- [ ] Authentication
- [ ] Registration and email verification
- [ ] Security controls
- [ ] RBAC
- [ ] Citizen / Lawyer / Law Firm / Legal Aid / Partner / Administrator roles
- [ ] Create/save/continue case
- [ ] Case number and status
- [ ] Conversation and assessment history
- [ ] Evidence/document storage
- [ ] Timeline, notes and deadlines
- [ ] Audit history

## Phase 5 — Advanced Document Intelligence
- [x] PDF/DOCX/TXT extraction
- [x] Legal triage of extracted text
- [ ] OCR/scanned PDF
- [ ] Multiple-document upload
- [ ] Document classification
- [ ] Parties/dates/deadlines/clauses/amounts/obligations extraction
- [ ] Risk identification
- [ ] Contract / termination / court / demand-letter / lease analysis
- [ ] Compare documents
- [ ] Evidence timeline
- [ ] Malware scanning
- [ ] Encrypted object storage

## Phase 6 — Lawyer & Human Review
- [ ] Lawyer registration and professional verification
- [ ] Practice areas/languages/location/availability/pro bono
- [ ] Lawyer dashboard and assigned cases
- [ ] Review/correct AI assessment
- [ ] Add legal opinion
- [ ] Approve/reject output
- [ ] Request evidence
- [ ] Client messaging
- [ ] Accept referral
- [ ] Clearly label AI Generated / Lawyer Reviewed / Lawyer Approved

## Phase 7 — Find Legal Help & Referral Engine
- [x] Legal Aid directory MVP
- [x] Private-firm directory MVP
- [x] Search/filter functionality
- [ ] Move directory to PostgreSQL
- [ ] Verified lawyer/law-firm profiles
- [ ] Practice areas and coordinates
- [ ] Distance search / availability / languages
- [ ] Consultation request and referral workflow
- [ ] Pro bono matching
- [ ] Legal Aid eligibility pathway
- [ ] Profile claiming and admin verification

## Phase 8 — Multilingual FijiLaw
- [ ] English production language workflow
- [ ] iTaukei
- [ ] Fiji Hindi
- [ ] Rotuman / additional languages later
- [ ] Interface/interview/explanation/search/AI response translation
- [ ] Lawyer language matching
- [ ] Human legal review of translations

## Phase 9 — Partner Platform
- [ ] Partner authentication
- [ ] Organisation/office/practitioner management
- [ ] Referrals and case allocation
- [ ] Pro bono management
- [ ] Regional demand/legal issue trends/response times/service availability
- [ ] Privacy-preserving analytics
- [ ] Reports/export

## Phase 10 — Security, Privacy & Governance
- [ ] Privacy impact assessment
- [ ] Threat model
- [ ] Encryption at rest/in transit
- [ ] RBAC and rate limiting
- [ ] WAF/security headers/secrets
- [ ] Audit/login monitoring
- [ ] File malware scanning
- [ ] Data retention/deletion policy
- [ ] Backup/disaster recovery
- [ ] Incident response
- [ ] Penetration testing
- [ ] Prompt-injection/AI abuse/sensitive-data controls

## Phase 11 — AI Legal Safety Framework
- [ ] Formal output levels: legal information → AI triage → AI assessment → lawyer reviewed → legal advice → representation
- [ ] Risk classification
- [ ] Urgent-case detection
- [ ] Human escalation rules
- [ ] Citation completeness
- [ ] Hallucination detection
- [ ] Unsupported-conclusion rejection
- [ ] Confidence indicators
- [ ] Versioned prompts and model audit logs
- [ ] Legal-source audit logs
- [ ] Lawyer override
- [ ] AI decision traceability

## Phase 12 — Testing & Legal Evaluation
- [ ] Build 500 lawyer-reviewed Fiji scenarios
- [ ] Measure domain accuracy, retrieval precision/recall, citation accuracy, hallucination rate, missing-fact detection, risk classification, unsafe recommendations, lawyer agreement, translation accuracy, latency and cost
- [ ] Target effectively zero fabricated authorities in validated production output

## Phase 13 — Production & Public Pilot
- [ ] Development / staging / production
- [ ] CI and automated tests
- [ ] Deployment smoke tests
- [ ] Monitoring/error/performance/database/AI/cost monitoring
- [ ] Backup verification
- [ ] Pilot user group
- [ ] Lawyer pilot group
- [ ] Feedback and pilot report
- [ ] Security and legal/governance review
- [ ] Controlled public release

## Phase 14 — Long-Term AI Lawyer Programme
- [ ] Legal advisory group and practising-lawyer engagement
- [ ] Institutional/regulatory engagement as appropriate
- [ ] Publish evaluation methodology
- [ ] Maintain AI performance evidence
- [ ] Independent testing
- [ ] Professional responsibility/liability/explainability framework
- [ ] Define AI-permitted vs lawyer-required tasks
- [ ] Explore regulated AI functions only when evidence supports them

## Immediate Sprint Order
1. Verify production frontend/backend end-to-end.
2. Clean Railway/Vercel deployment configuration.
3. Provision PostgreSQL + pgvector.
4. Design production database schema.
5. Build legal-source ingestion pipeline.
6. Ingest first authoritative Fiji-law corpus.
7. Implement hybrid semantic retrieval.
8. Fix cross-domain/irrelevant retrieval.
9. Activate AI reasoning provider.
10. Add citation validation.
11. Build conversational legal interview.
12. Add authentication + My Cases.
13. Build lawyer-review workflow.
14. Establish automated legal evaluation suite.

## Milestones
- M1 Stable MVP — production reliably works.
- M2 FijiLaw Knowledge Engine — verified legislation searchable at provision level.
- M3 Legal Agent — interviews users, researches verified law and produces cited assessments.
- M4 Case Platform — saved cases, documents, evidence and conversations.
- M5 Human Supervision — lawyers review/correct/approve AI work.
- M6 Controlled Pilot — real users and lawyers test against measurable safety criteria.
