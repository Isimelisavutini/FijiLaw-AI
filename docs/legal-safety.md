# Legal AI Safety Model

FijiLaw AI is initially designed as a supervised legal technology system, not an autonomous licensed lawyer.

## Mandatory safeguards

### Source grounding
Legal conclusions should be based on retrieved, approved legal sources wherever possible. The system must retain source identifiers and citations used to produce an answer.

### No fabricated authorities
The model must not invent Acts, sections, regulations, judgments, quotations, case numbers, courts, or legal authorities. If an authority cannot be verified, the response must communicate that limitation.

### Human escalation
The platform should support mandatory escalation where a matter involves high consequences, insufficient evidence, uncertain law, conflicts of interest, court representation, or another activity requiring a qualified legal practitioner.

### Auditability
Record relevant AI operations including case/request identifier, model, tool calls, retrieved authorities, output, safety decision, and human approval where applicable.

### Privacy
Legal matters can contain highly sensitive information. Apply least-privilege access, encryption in transit and at rest, secure secret storage, retention controls, and appropriate separation between users and matters.

## Proposed risk levels

- **Low:** general legal education and navigation.
- **Medium:** fact-specific triage, document explanation, and research assistance.
- **High:** legal strategy, formal legal documents, deadlines, litigation, criminal matters, family safety matters, or substantial financial/property consequences.
- **Restricted:** autonomous representation or regulated practice unless expressly permitted by applicable law and platform authorization.

The risk engine should determine when human review is mandatory before an output can be treated as actionable legal work.
