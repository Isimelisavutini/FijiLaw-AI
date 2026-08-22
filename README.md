# FijiLaw AI

FijiLaw AI is an AI-powered legal assistance platform for Fiji, designed to improve access to justice through legal triage, research, case analysis, document assistance, and lawyer-supervised guidance grounded in Fijian law.

## Current scope

The first release is a supervised legal AI assistant. It is not presented as a licensed legal practitioner and should not provide autonomous legal representation.

## Planned architecture

- **Frontend:** Next.js + React, deployable to Vercel
- **Backend:** ASP.NET Core Web API
- **Database:** PostgreSQL
- **Vector search:** pgvector or Azure AI Search
- **Document storage:** Azure Blob Storage
- **AI layer:** model-provider abstraction with retrieval-augmented generation (RAG)
- **Observability:** structured audit logs and application telemetry

## Core capabilities

- Legal issue triage
- Fiji-law grounded answers with citations
- Evidence and document analysis
- Case timeline generation
- Legal research support
- Draft document generation for human review
- Lawyer escalation and supervision
- Full audit trail for AI-assisted actions

## Repository structure

```text
src/
  FijiLaw.Api/
  FijiLaw.Domain/
  FijiLaw.Infrastructure/
  FijiLaw.AI/
  FijiLaw.Web/
docs/
knowledge/
database/
tests/
```

## Safety principles

1. Prefer authoritative Fiji legal sources.
2. Cite the source relied on for legal conclusions.
3. Distinguish facts supplied by the user from legal interpretation.
4. Escalate uncertain, high-risk, or representation-related matters to a qualified lawyer.
5. Keep an auditable record of retrievals, model outputs, approvals, and user-visible advice.

## Status

Early-stage architecture and MVP setup.
