-- Migration: add RAG evaluation tables to the rag schema.
-- Apply manually against the existing database (rag.documents, rag.chunks,
-- rag.topics, rag.document_topics already exist and are untouched).

BEGIN;

-- Test query set (built from synthetic generation, or curated by hand).
CREATE TABLE rag.eval_queries (
    id                  SERIAL PRIMARY KEY,
    query_text          text NOT NULL,
    query_type          text NOT NULL DEFAULT 'SingleChunk'
                            CHECK (query_type IN ('SingleChunk', 'MultiHop', 'Distractor')),
    topic_id            int NOT NULL REFERENCES rag.topics(id),
    source_document_id  int REFERENCES rag.documents(id) ON DELETE SET NULL,
    expected_answer     text,
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- Ground-truth chunk(s) for each query. One row per expected chunk
-- (kept as a table, not an array column, so multi-hop queries with
-- several expected chunks are supported later without a schema change).
CREATE TABLE rag.eval_query_expected_chunks (
    query_id  int NOT NULL REFERENCES rag.eval_queries(id) ON DELETE CASCADE,
    chunk_id  int NOT NULL REFERENCES rag.chunks(id) ON DELETE CASCADE,
    PRIMARY KEY (query_id, chunk_id)
);

-- One evaluation run = one model/config combination, executed against the whole test query set.
CREATE TABLE rag.eval_runs (
    id                  SERIAL PRIMARY KEY,
    run_name            text NOT NULL,
    chunking_config     jsonb NOT NULL DEFAULT '{}'::jsonb,
    chat_model          text NOT NULL,
    embedding_model     text NOT NULL,
    notes               text,
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- Raw retrieval results: every chunk the topic-scoped search returned,
-- flagged with whether the token-budget selection actually included it.
CREATE TABLE rag.eval_retrieval_results (
    id                    SERIAL PRIMARY KEY,
    run_id                int NOT NULL REFERENCES rag.eval_runs(id) ON DELETE CASCADE,
    query_id              int NOT NULL REFERENCES rag.eval_queries(id) ON DELETE CASCADE,
    chunk_id              int NOT NULL REFERENCES rag.chunks(id),
    rank                  int NOT NULL,
    included_in_budget    boolean NOT NULL,
    approx_tokens         int NOT NULL
);

CREATE TABLE rag.eval_retrieval_metrics (
    run_id            int NOT NULL REFERENCES rag.eval_runs(id) ON DELETE CASCADE,
    query_id          int NOT NULL REFERENCES rag.eval_queries(id) ON DELETE CASCADE,
    precision_at_k    double precision NOT NULL,
    recall_at_k       double precision NOT NULL,
    reciprocal_rank   double precision NOT NULL,
    ndcg_at_k         double precision NOT NULL,
    PRIMARY KEY (run_id, query_id)
);

-- Generation results per query per run.
CREATE TABLE rag.eval_generation_results (
    id                    SERIAL PRIMARY KEY,
    run_id                int NOT NULL REFERENCES rag.eval_runs(id) ON DELETE CASCADE,
    query_id              int NOT NULL REFERENCES rag.eval_queries(id) ON DELETE CASCADE,
    generated_answer      text NOT NULL,
    context_chunk_ids     jsonb NOT NULL,  -- array of chunk ids actually passed to the LLM
    created_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE rag.eval_generation_metrics (
    generation_result_id  int PRIMARY KEY REFERENCES rag.eval_generation_results(id) ON DELETE CASCADE,
    faithfulness_score    double precision NOT NULL,
    relevance_score       double precision NOT NULL,
    completeness_score    double precision NOT NULL,
    judge_model           text NOT NULL,
    judge_prompt_version  text NOT NULL,
    judge_rationale       text
);

CREATE INDEX idx_eval_retrieval_results_run ON rag.eval_retrieval_results(run_id);
CREATE INDEX idx_eval_generation_results_run ON rag.eval_generation_results(run_id);
CREATE INDEX idx_eval_query_expected_chunks_query ON rag.eval_query_expected_chunks(query_id);
CREATE INDEX idx_eval_queries_topic ON rag.eval_queries(topic_id);

COMMIT;