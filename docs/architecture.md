# FijiLaw AI Architecture

## Design goal

Build a legal AI platform that can begin as a supervised legal-information and lawyer-copilot system while preserving a technical path toward greater autonomy if future Fiji law and professional regulation permit it.

## High-level flow

```text
Client / Lawyer
      |
      v
Next.js Web Application (Vercel)
      |
      v
ASP.NET Core API
      |
      +--> Identity & Authorization
      +--> Case Management
      +--> AI Agent Orchestrator
      +--> Document Service
      +--> Audit Service
                |
                v
          Legal Retrieval Layer
        /         |          \
Legislation    Case Law    Approved Resources
        \         |          /
                v
             LLM Layer
                |
                v
      Safety / Citation Validation
                |
                v
       Response / Lawyer Review
```

## AI agent responsibilities

The agent should use controlled tools rather than relying on model memory for Fiji law. Planned tools include:

- `search_fiji_legislation`
- `search_case_law`
- `retrieve_legal_source`
- `analyse_case_document`
- `build_case_timeline`
- `identify_missing_evidence`
- `draft_legal_document`
- `request_human_review`

## Legal reasoning response model

Important legal analyses should be representable as structured data containing:

- Issue
- User-supplied facts
- Relevant law
- Retrieved authorities
- Analysis
- Missing information/evidence
- Options or next steps
- Confidence/uncertainty indicators
- Human-review requirement
- Citations

## Trust boundaries

The web client is untrusted. Authorization and access control are enforced by the API. Legal documents and case data should not be exposed directly through public storage URLs. Model providers receive only information required for the particular operation and subject to the platform's privacy configuration.

## Human supervision

High-risk matters, low-confidence results, court representation, final legal documents, and other regulated activities must be capable of being routed to an authorized human practitioner.
