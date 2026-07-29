# Knowledge Assistant API Documentation

**Version:** 1.0.0
**Base URL (local development):** `http://localhost:5299/`

## Overview

The Knowledge Assistant API is a .NET Web API that powers a knowledge-assistant application. It exposes endpoints for:

- **Chat** — sending messages to an AI model and generating conversation titles
- **Configuration** — managing the selected model and document chunking settings
- **Conversations** — creating, retrieving, updating, and deleting chat conversations
- **Documents** — ingesting, uploading, updating, and deleting knowledge-base documents
- **Models** — listing available AI models and managing their context window sizes
- **Topics** — creating, updating, deleting, and listing topics used to organize documents

The API integrates with a local **Ollama** instance for model inference and a **PostgreSQL** database (with `pgvector`) for storage and vector search over documents.

## Authentication

No authentication scheme is currently defined in the API specification. If authentication is added later (e.g. JWT bearer tokens), this section should be updated accordingly.

## CORS

The API allows cross-origin requests from configured origins (matched via wildcard patterns, e.g. `http://*:4200`), primarily to support a local Angular development frontend.

## Error Handling

The API uses a global exception handler and returns errors using the [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807) format. Specific error schemas are not detailed in the current OpenAPI spec — responses beyond `200 OK` are not yet documented and should be added as the error handler design solidifies.

---

## Endpoints

### Chat

#### `POST /api/chat`
Sends a message to the assistant and receives a response.

**Request body** (`application/json`): [`ChatRequestDto`](#chatrequestdto)

**Response:** `200 OK`

---

#### `POST /api/chat/title`
Generates a title for a conversation based on a chat message.

**Request body** (`application/json`): [`ChatRequestDto`](#chatrequestdto)

**Response:** `200 OK`

---

### Configuration

#### `GET /api/configuration/selected-model`
Retrieves the currently selected AI model.

**Response:** `200 OK` — [`SelectedModelDto`](#selectedmodeldto)

---

#### `PUT /api/configuration/selected-model`
Updates the currently selected AI model.

**Request body** (`application/json`): [`UpdateSelectedModelDto`](#updateselectedmodeldto)

**Response:** `200 OK`

---

#### `GET /api/configuration/chunking-settings`
Retrieves the current document chunking configuration (used when ingesting documents for vector search).

**Response:** `200 OK` — [`ChunkingSettingsDto`](#chunkingsettingsdto)

---

#### `PUT /api/configuration/chunking-settings`
Updates the document chunking configuration.

**Request body** (`application/json`): [`ChunkingSettingsDto`](#chunkingsettingsdto)

**Response:** `200 OK`

---

### Conversations

#### `GET /api/conversations`
Retrieves the list of conversations.

**Response:** `200 OK`

---

#### `POST /api/conversations`
Creates a new conversation.

**Response:** `200 OK`

---

#### `GET /api/conversations/{conversationId}`
Retrieves a single conversation by ID.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `conversationId` | path | `uuid` | Yes |

**Response:** `200 OK`

---

#### `DELETE /api/conversations/{conversationId}`
Deletes a conversation.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `conversationId` | path | `uuid` | Yes |

**Response:** `200 OK`

---

#### `PATCH /api/conversations/{conversationId}/title`
Updates a conversation's title.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `conversationId` | path | `uuid` | Yes |
| `newTitle` | query | `string` | No |

**Response:** `200 OK`

---

#### `PATCH /api/conversations/{conversationId}/topic`
Assigns or updates a conversation's associated topic.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `conversationId` | path | `uuid` | Yes |
| `topicId` | query | `integer` | No |

**Response:** `200 OK`

---

### Documents

#### `POST /api/documents`
Ingests a new document from raw text content.

**Request body** (`application/json`): [`IngestTextRequestDto`](#ingesttextrequestdto)

**Response:** `200 OK`

---

#### `GET /api/documents`
Retrieves the list of ingested documents.

**Response:** `200 OK`

---

#### `POST /api/documents/upload`
Uploads a document file (e.g. PDF/DOCX) for ingestion.

**Request body** (`multipart/form-data`):

| Field | Type | Description |
|---|---|---|
| `file` | binary | The document file to upload |
| `title` | string | Title of the document |
| `topics` | string | Associated topic(s), likely comma- or delimiter-separated |

**Response:** `200 OK`

---

#### `PUT /api/documents/{id}`
Updates an existing document's text content.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `id` | path | `integer` | Yes |

**Request body** (`application/json`): [`IngestTextRequestDto`](#ingesttextrequestdto)

**Response:** `200 OK`

---

#### `DELETE /api/documents/{id}`
Deletes a document.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `id` | path | `integer` | Yes |

**Response:** `200 OK`

---

#### `GET /api/documents/topics`
Retrieves the list of topics associated with existing documents.

**Response:** `200 OK`

---

### Models

#### `GET /api/models`
Lists all available AI models (from the connected Ollama instance).

**Response:** `200 OK` — array of [`ModelInfoDto`](#modelinfodto)

---

#### `GET /api/models/context-windows`
Lists context window sizes configured for each model.

**Response:** `200 OK` — array of [`ModelContextWindowDto`](#modelcontextwindowdto)

---

#### `PUT /api/models/{id}/context-window`
Updates the context window size for a specific model.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `id` | path | `uuid` | Yes |

**Request body** (`application/json`): [`UpdateModelContextWindowDto`](#updatemodelcontextwindowdto)

**Response:** `200 OK`

---

### Topics

#### `GET /api/topics`
Lists all topics.

**Response:** `200 OK`

---

#### `POST /api/topics`
Creates a new topic.

**Request body** (`application/json`): [`TopicRequestDto`](#topicrequestdto)

**Response:** `200 OK`

---

#### `PUT /api/topics/{id}`
Updates an existing topic.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `id` | path | `integer` | Yes |

**Request body** (`application/json`): [`TopicRequestDto`](#topicrequestdto)

**Response:** `200 OK`

---

#### `DELETE /api/topics/{id}`
Deletes a topic.

| Parameter | Location | Type | Required |
|---|---|---|---|
| `id` | path | `integer` | Yes |

**Response:** `200 OK`

---

## Data Models (Schemas)

### ChatRequestDto
| Field | Type | Nullable | Description |
|---|---|---|---|
| `conversationId` | `uuid` | Yes | ID of the conversation this message belongs to |
| `message` | `string` | No | The user's message text |
| `model` | `string` | Yes | Optional override of the model to use for this request |
| `temperature` | `double` | Yes | Optional sampling temperature |

### ChunkingSettingsDto
| Field | Type | Description |
|---|---|---|
| `chunkTargetSizeChars` | `integer` | Target size (in characters) for each document chunk |
| `chunkOverlapChars` | `integer` | Number of overlapping characters between consecutive chunks |

### IngestTextRequestDto
| Field | Type | Description |
|---|---|---|
| `title` | `string` | Document title |
| `text` | `string` | Raw text content of the document |
| `documentType` | `DocumentType` (integer enum) | Type/category of the document |
| `topics` | `string[]` | Topics to associate with the document |

### ModelInfoDto
| Field | Type | Required |
|---|---|---|
| `name` | `string` | Yes |

### ModelContextWindowDto
| Field | Type | Nullable | Required |
|---|---|---|---|
| `id` | `uuid` | No | — |
| `name` | `string` | No | Yes |
| `contextWindowTokens` | `integer` | Yes | — |

### SelectedModelDto
| Field | Type | Nullable |
|---|---|---|
| `selectedModel` | `string` | Yes |

### UpdateSelectedModelDto
| Field | Type | Required |
|---|---|---|
| `selectedModel` | `string` | Yes |

### TopicRequestDto
| Field | Type |
|---|---|
| `name` | `string` |

### UpdateModelContextWindowDto
| Field | Type | Nullable |
|---|---|---|
| `contextWindowTokens` | `integer` | Yes |

