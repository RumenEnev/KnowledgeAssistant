# Setting Up the `rag` Schema in PostgreSQL

This guide walks through creating the dedicated `rag` schema and its tables for the document retrieval feature. It assumes `pgvector` is already installed and enabled in your database (see prerequisite check below if you haven't confirmed this yet).

## Prerequisite: Confirm `pgvector` is enabled

Run this in pgAdmin's Query Tool (or `psql`), connected to your app's database:

```sql
SELECT * FROM pg_extension WHERE extname = 'vector';
```

If this returns a row, you're good — skip to the next section.

If it returns nothing, install and enable it first (Ubuntu 24.04 / Postgres 16 example):

```bash
sudo apt update
sudo apt install postgresql-16-pgvector
sudo systemctl restart postgresql
```

Then in the Query Tool:

```sql
CREATE EXTENSION vector;
```

## Step 1 — Create the schema

Connect to your app's existing database (the same one used by your chat app — not a separate database), and run:

```sql
CREATE SCHEMA IF NOT EXISTS rag;
```

This creates a namespace called `rag` inside your existing database. All tables below live inside it, separate from `public` and your app's core tables.

## Step 2 — Create the tables

```sql
CREATE TABLE rag.topics (
    id SERIAL PRIMARY KEY,
    name TEXT UNIQUE NOT NULL
);

CREATE TABLE rag.documents (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL,
    original_text TEXT,
    created_at TIMESTAMP DEFAULT now()
);

CREATE TABLE rag.document_topics (
    document_id INT REFERENCES rag.documents(id) ON DELETE CASCADE,
    topic_id INT REFERENCES rag.topics(id) ON DELETE CASCADE,
    PRIMARY KEY (document_id, topic_id)
);

CREATE TABLE rag.chunks (
    id SERIAL PRIMARY KEY,
    document_id INT REFERENCES rag.documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,
    chunk_text TEXT NOT NULL,
    embedding vector(768) NOT NULL
);
```

**What each table is for:**
- `topics` — the fixed list of labels (includes `general` and `unknown`)
- `documents` — one row per uploaded document (raw text + title)
- `document_topics` — join table linking documents to one or more topics (many-to-many)
- `chunks` — the actual paragraph-level pieces of each document, each with its own embedding vector, used for similarity search

## Step 3 — Seed the required topics

`general` and `unknown` are required — the retrieval logic uses these as a signal to skip document lookup entirely when the conversation doesn't match a real topic:

```sql
INSERT INTO rag.topics (name) VALUES ('general'), ('unknown');
```

Add any additional real topics you plan to use right away, or do this later through the admin panel once it exists:

```sql
INSERT INTO rag.topics (name) VALUES ('billing'), ('onboarding');
```

## Step 4 — (Optional, later) Add the similarity search index

Skip this step until you've ingested a real batch of documents — `ivfflat` needs existing data to build a useful index and is a no-op on an empty table:

```sql
CREATE INDEX ON rag.chunks USING ivfflat (embedding vector_cosine_ops);
```

## Step 5 — Verify

Confirm everything was created correctly:

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = 'rag';
```

You should see: `topics`, `documents`, `document_topics`, `chunks`.

```sql
SELECT * FROM rag.topics;
```

You should see `general` and `unknown` (plus any others you added).

## Note for application code

Since these tables live in `rag`, not `public`, your app's queries need to reference them with the schema prefix:

```sql
SELECT * FROM rag.documents;
INSERT INTO rag.chunks (...) VALUES (...);
```

Alternatively, you can add `rag` to your database connection's `search_path` so unqualified table names resolve correctly — but explicit schema-qualification is usually clearer and less error-prone once you have multiple schemas in play.

## Related additions to the `ai_interactions` schema

A few features build on top of the `rag` schema by adding columns to tables in `ai_interactions` (the schema that stores conversations, messages, and app-wide configuration). These aren't part of `rag` itself, but they reference it, so they're documented here for completeness. The columns below are shown as part of each table's full creation script — if the table already exists in your database, just add the missing columns to match.

### `ai_interactions.configuration`

Single global configuration row (selected model, chunking settings):

```sql
CREATE TABLE IF NOT EXISTS ai_interactions.configuration (
    id uuid PRIMARY KEY,
    selected_model_id uuid,
    chunk_target_size_chars integer NOT NULL DEFAULT 1000,
    chunk_overlap_chars integer NOT NULL DEFAULT 150
);
```

`chunk_target_size_chars` / `chunk_overlap_chars` control the document chunking size/overlap used when ingesting documents, and are editable from the "Manage Documents" window in both the Angular and WPF apps. If the row doesn't exist yet, the app falls back to the defaults above (1000 / 150).

### `ai_interactions.conversations`

```sql
CREATE TABLE IF NOT EXISTS ai_interactions.conversations (
    id uuid PRIMARY KEY,
    title text,
    created_at timestamp NOT NULL DEFAULT now(),
    updated_at timestamp NOT NULL DEFAULT now(),
    selected_model_id uuid,
    topic_id integer REFERENCES rag.topics(id)
);
```

`topic_id` is nullable and stores the result of automatic conversation topic classification: after the second user message in a conversation, the app asks the LLM to classify it into one of the existing `rag.topics`. Classification happens once per conversation and is never re-evaluated afterwards; conversations remain unclassified (`topic_id = NULL`) until enough messages have been exchanged, or if the LLM doesn't find a good match among the available topics.

### ai_interactions.models

```sql
CREATE TABLE IF NOT EXISTS ai_interactions.models
(
    "Id" uuid,
    size bigint,
    is_installed boolean,
    last_seen timestamp with time zone,
    name character varying(50) COLLATE pg_catalog."default",
    display_name character varying(20) COLLATE pg_catalog."default",
    provider character varying(30) COLLATE pg_catalog."default",
    family character varying(30) COLLATE pg_catalog."default",
    context_window_tokens integer
)
```

`context_window_tokens` is nullable and stores the configured context window size (in tokens) for the model, editable from the "Settings > Model Context Windows" screen in both UIs. When `NULL`, the model's default context window is used.

### `ai_interactions.tools`

Stores tool definitions the model may call (see `ToolDefinition` in `KnowledgeAssistant.Application.Abstraction`), so tools can be added/edited/enabled without redeploying code:

```sql
CREATE TABLE IF NOT EXISTS ai_interactions.tools (
    id uuid PRIMARY KEY,
    name character varying(100) NOT NULL UNIQUE,
    description text NOT NULL,
    parameters_json_schema text NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    endpoint_url text,
    http_method character varying(10) NOT NULL DEFAULT 'GET',
    auth_login_url text,
    auth_username text,
    auth_password text,
    created_at timestamp NOT NULL DEFAULT now(),
    updated_at timestamp NOT NULL DEFAULT now()
);
```

`name` must be unique since it's what the model uses to reference the tool in a tool call. `parameters_json_schema` holds the raw JSON Schema text describing the tool's parameters object. `is_enabled` lets a tool be disabled without deleting its row. `endpoint_url` / `http_method` tell the app how to actually execute the tool when the model calls it: the app makes an HTTP request to `endpoint_url` using `http_method` and feeds the response back to the model as context.

`auth_login_url` / `auth_username` / `auth_password` are optional: when `auth_login_url` is set, the app first `POST`s `{"email": auth_username, "password": auth_password}` to it, reads the `token` field from the JSON response, and sends it as `Authorization: Bearer {token}` on the call to `endpoint_url`. A fresh login is performed on every tool call (no token caching yet). **Note:** `auth_password` is currently stored as plain text in the database, matching this app's existing practice of keeping credentials in plain configuration (e.g. the Postgres connection string in `appsettings.json`) rather than a secrets manager — restrict access to this table/database accordingly.

### Seeding the `get_tasks` tool

Example row that lets the assistant answer prompts like "show me my tasks" by calling a task-list API:

```sql
INSERT INTO ai_interactions.tools (id, name, description, parameters_json_schema, is_enabled, endpoint_url, http_method)
VALUES (
    gen_random_uuid(),
    'get_tasks',
    'Gets the current list of the user''s tasks/todos. Use this whenever the user asks to see, show, or list their tasks.',
    '{"type":"object","properties":{}}',
    true,
    'http://192.168.0.200:4401/tasks',
    'GET'
);
```