# Knowledge Assistant — Application Documentation

Version 1.0.1.378

This is the top-level documentation for the Knowledge Assistant application. It provides a high-level overview of the system and links out to detailed documentation for each component. Component-specific docs live alongside this file in the same folder.

## What is Knowledge Assistant?

Knowledge Assistant is a Retrieval-Augmented Generation (RAG) application that lets users chat with an AI assistant grounded in their own ingested documents. Users can upload or paste documents, organize them by topic, chat with context-aware answers, and configure which local AI model (via Ollama) is used.

## Architecture Overview

The application is composed of the following major parts:

- **Frontend** — An Angular single-page application that provides the chat interface, document management UI, and configuration screens.
- **Backend API** — A .NET (ASP.NET Core) Web API that exposes REST endpoints for chat, conversations, documents, topics, models, and configuration.
- **Database** — PostgreSQL with the `pgvector` extension, used for relational data (conversations, topics, documents) and vector similarity search over document embeddings.
- **Model Inference** — A local **Ollama** instance that serves the AI models used for chat responses and generating conversation titles.
- **RAG Pipeline** — Document ingestion, chunking, and embedding logic that prepares uploaded content for retrieval during chat.

```
┌─────────────┐      HTTP/JSON      ┌──────────────────┐      SQL / pgvector      ┌──────────────┐
│   Angular    │ ──────────────────▶│   .NET Web API   │ ────────────────────────▶│  PostgreSQL   │
│   Frontend   │◀────────────────── │ (KnowledgeAssistant.Api) │◀──────────────────│  + pgvector   │
└─────────────┘                     └──────────────────┘                          └──────────────┘
                                              │
                                              │ HTTP
                                              ▼
                                      ┌───────────────┐
                                      │    Ollama     │
                                      │ (model server)│
                                      └───────────────┘
```

## Components & Detailed Documentation

| Component | Description | Documentation |
|---|---|---|
| **Backend API** | Full REST API reference: endpoints, request/response schemas, error handling, CORS | [KnowledgeAssistant-API-Documentation.md](./KnowledgeAssistant-API-Documentation.md) |
| **RAG / Schema Setup** | Database schema, pgvector setup, chunking and embedding pipeline | [rag-schema-setup.md](./rag-schema-setup.md) *(to be added)* |
| **Frontend (Angular)** | UI structure, routing, state management, build/dev setup | *(not yet written)* |
| **Deployment / Infrastructure** | How the app is deployed, environment configuration, Ollama setup | *(not yet written)* |

> As new component docs are added to this folder, add a row here linking to them so this file stays the single entry point for anyone new to the project.

## Getting Started (Local Development)

1. **Backend** — Run the .NET API (`dotnet run` or F5 in Visual Studio). Requires PostgreSQL with `pgvector` and a running Ollama instance.
2. **Database** — See [rag-schema-setup.md](./rag-schema-setup.md) for schema creation and `pgvector` configuration.
3. **Model server** — Ensure Ollama is running locally and the models referenced in configuration are pulled/available.
4. **Frontend** — Run the Angular dev server; CORS is pre-configured to allow local origins matching `http://*:4200`.
5. **API reference** — Once running, interactive docs are available (if Scalar is configured) or via the raw spec at `/openapi/v1.json`. See [KnowledgeAssistant-API-Documentation.md](./KnowledgeAssistant-API-Documentation.md) for the full written reference.

## Configuration

Key configuration values (set via `appsettings.json` or environment variables):

| Setting | Purpose |
|---|---|
| `Ollama:BaseUrl` | Base URL of the running Ollama instance |
| `ConnectionStrings:KnowledgeAssistant` | PostgreSQL connection string |
| `Cors:AllowedOrigins` | Allowed frontend origin patterns (supports `*` wildcards) |

## Document & Chat Flow (High Level)

1. A document is uploaded or ingested as text (`/api/documents` or `/api/documents/upload`).
2. The document is chunked according to configured chunking settings and stored with vector embeddings (see [rag-schema-setup.md](./rag-schema-setup.md)).
3. When a user sends a chat message (`/api/chat`), relevant document chunks are retrieved via vector similarity search and used as context for the model response.
4. Conversations, titles, and topic associations are persisted and manageable via the Conversations and Topics endpoints.

## Roadmap for This Documentation

- [x] API reference (`KnowledgeAssistant-API-Documentation.md`)
- [ ] RAG / schema setup (`rag-schema-setup.md`)
- [ ] Frontend architecture doc
- [ ] Deployment / infrastructure doc

---

*This document is the entry point for the Knowledge Assistant project. Keep it updated as new component docs are added.*
