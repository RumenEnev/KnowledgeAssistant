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
