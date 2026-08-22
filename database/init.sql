CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS legal_sources (
  id UUID PRIMARY KEY,
  jurisdiction TEXT NOT NULL DEFAULT 'FJ',
  source_type TEXT NOT NULL,
  title TEXT NOT NULL,
  provision TEXT,
  canonical_url TEXT,
  effective_date DATE,
  verified BOOLEAN NOT NULL DEFAULT FALSE,
  content TEXT NOT NULL,
  content_hash TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS legal_source_chunks (
  id UUID PRIMARY KEY,
  legal_source_id UUID NOT NULL REFERENCES legal_sources(id) ON DELETE CASCADE,
  chunk_index INTEGER NOT NULL,
  content TEXT NOT NULL,
  embedding vector(1536),
  UNIQUE (legal_source_id, chunk_index)
);

CREATE TABLE IF NOT EXISTS ai_audit_events (
  id UUID PRIMARY KEY,
  correlation_id TEXT NOT NULL,
  event_type TEXT NOT NULL,
  payload JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_legal_sources_verified ON legal_sources(verified);
CREATE INDEX IF NOT EXISTS idx_audit_correlation ON ai_audit_events(correlation_id);
