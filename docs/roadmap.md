# FijiLaw AI Roadmap

## Phase 1 — Safe MVP
- Legal issue intake and triage
- Risk classification and human escalation
- No fabricated citations
- Next.js client and ASP.NET Core API
- CI and local development environment

## Phase 2 — Verified Fiji-law RAG
- Build ingestion pipeline for authorized, authoritative legal sources
- Version and hash source documents
- PostgreSQL/pgvector or Azure AI Search retrieval
- Return paragraph/section-level citations
- Evaluation dataset reviewed by Fiji legal professionals

## Phase 3 — Lawyer Copilot
- Authentication and role-based access
- Matter/case workspace
- Secure document upload and evidence timeline
- Legal research workspace
- Draft documents with mandatory approval gates
- Immutable AI/human review audit trail

## Phase 4 — Institutional Pilot
- Partner onboarding and governance
- Privacy/security assessment
- Red-team and legal accuracy evaluation
- Controlled pilot with lawyer supervision
- Metrics for access, accuracy, escalation, and user outcomes

## Phase 5 — Regulated Autonomy (future, only if lawful)
- Define permitted practice categories with regulators
- Independent competency and safety testing
- Professional accountability and insurance model
- Certification/licensing framework if created under Fiji law
- Enable only the autonomous functions explicitly authorized

## MVP definition of done
The MVP is technically complete when the web app builds, API tests pass, the triage endpoint works, no legal authority is fabricated when retrieval is unavailable, CI is green, and deployment configuration is documented. It is not production/legal-service ready until verified legal sources, authentication, privacy controls, monitoring, and professional review are implemented.
