# FijiLaw AI — Public Law Retrieval Precision Upgrade

Status key: `[x]` verified in code/tests, `[ ]` not yet complete.

## 1. RAG & Retrieval Precision
- [x] Add domain-aware filtering to curated fallback retrieval.
- [x] Prevent strong public-law queries from retrieving unrelated family, domestic-violence or consumer sources.
- [x] Add ranked term scoring within the curated fallback.
- [x] Add FICAC Act 2007 public-governance authorities.
- [x] Add High Court Rules 1988 Order 53 judicial-review authorities.
- [x] Add Constitution section 44 redress authority.
- [ ] Provision PostgreSQL + pgvector.
- [ ] Add corpus metadata columns for legal domain/sub-domain.
- [ ] Implement hybrid BM25/keyword + vector retrieval.
- [ ] Add configurable vector relevance thresholds.
- [ ] Add reranking and citation relevance scoring.
- [ ] Add section-level amendment/freshness checks.

## 2. Legal Classification Taxonomy
- [x] Add multi-label `LegalDomains` to triage results.
- [x] Add Constitutional Law classification.
- [x] Add Public & Administrative Law classification.
- [x] Add Public Governance classification.
- [x] Classify FICAC/JSC appointment disputes across multiple domains.
- [ ] Add Public & Administrative Law to the public landing-page legal-area menu.
- [ ] Persist taxonomy tags with cases and legal-source chunks.

## 3. Dynamic Triage & Tailored Guidance
- [x] Add public-law-specific missing-information prompts.
- [x] Request appointment instruments/JSC material where relevant.
- [x] Request FICAC charge/prosecution documents where relevant.
- [x] Add context-aware judicial-review next steps referencing High Court Rules 1988 Order 53.
- [x] Add conditional constitutional-redress pathway referencing Constitution section 44.
- [ ] Add deadline calculator once procedural dates are reliably represented.
- [ ] Add remedy classification (certiorari, prohibition, mandamus, declaration, constitutional redress) after lawyer-reviewed validation.

## 4. Directory & Partner Matching
- [ ] Add Public/Constitutional Law practitioner specialty.
- [ ] Add Administrative/Judicial Review specialty.
- [ ] Add Employment specialist filtering.
- [ ] Build verified practitioner import/sync workflow using an authorised Fiji Law Society data source or partnership.
- [ ] Replace private-law-firm `Verification pending` only after authoritative verification.

## 5. Technical & Document Ingestion
- [ ] Add OCR for scanned PDF/image documents.
- [ ] Add malware/content safety checks before OCR/storage.
- [ ] Increase upload size only after streaming/object-storage architecture is in place.
- [ ] Support multi-document court bundles.
- [ ] Add document page-level provenance for extracted facts and citations.

## Verification tests added
- [x] FICAC appointment dispute returns Constitutional Law + Public & Administrative Law + Public Governance labels.
- [x] FICAC governance query includes FICAC Act / High Court Rules sources.
- [x] FICAC governance query excludes Domestic Violence Act, Family Law Act and FCCC Act.
- [x] Public-law missing-information prompts include appointment-instrument data.
- [x] Public-law next steps include Order 53 where judicial review is relevant.

## Authoritative references verified before implementation
- Constitution of the Republic of Fiji, s 44 — High Court redress for alleged contraventions of Chapter 2 rights.
- High Court Rules 1988, Order 53 — Applications for judicial review; r 3 requires leave before an application for judicial review is made.
- Fiji Independent Commission Against Corruption Act 2007, s 5 — Commissioner appointment framework involving the President and Judicial Services Commission.

These references are used only as verified source pointers. Case-specific conclusions still require application of the law to the facts and, for important matters, qualified legal review.
