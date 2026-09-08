-- Migration: add model-provider tracking to the RAG evaluation tables.
-- Apply after RAG_Eval_database.sql (and after any prior migration that added
-- rag.eval_runs.judge_model, if that was applied out-of-band).
--
-- Adds:
--   rag.eval_queries.generator_provider / generator_model - which provider/model
--     produced each synthetic test question (set by "Generate Test Set").
--   rag.eval_runs.chat_provider / judge_provider - which provider was used for
--     chat generation and for judging during that run (set by "Run Evaluation";
--     chat and judge can each use a different provider).

BEGIN;

ALTER TABLE rag.eval_queries
    ADD COLUMN IF NOT EXISTS generator_provider text,
    ADD COLUMN IF NOT EXISTS generator_model text;

ALTER TABLE rag.eval_runs
    ADD COLUMN IF NOT EXISTS chat_provider text,
    ADD COLUMN IF NOT EXISTS judge_provider text;

COMMIT;
