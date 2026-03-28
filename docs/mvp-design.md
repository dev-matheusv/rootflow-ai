# RootFlow MVP Design

## Purpose

RootFlow is a standalone AI assistant platform for businesses.
The MVP should let a business upload knowledge, process it, ask questions, and receive grounded answers using retrieval-augmented generation (RAG).

## Product Goals

- Keep the first version simple and practical.
- Build a foundation that is reusable and commercial-ready.
- Avoid early complexity that is not needed for the MVP.
- Keep the structure ready for future multi-tenant growth.

## MVP Scope

The MVP includes:

- Document upload
- Basic text extraction
- Text chunking
- Embedding generation through an external AI API
- Embedding storage in PostgreSQL
- Semantic search
- Chat endpoint with contextual answers
- Conversation history

The MVP does not include:

- Admin panel
- Workflow automation
- WhatsApp integration
- Full multi-tenant runtime support
- Advanced guardrails
- Billing or analytics

## High-Level Architecture

RootFlow should use a modular monolith with Clean Architecture:

- `RootFlow.Api`
- `RootFlow.Application`
- `RootFlow.Domain`
- `RootFlow.Infrastructure`

Flow:

`Client -> Api -> Application -> Domain -> Infrastructure -> PostgreSQL / File Storage / AI Provider`

## Project Responsibilities

### `RootFlow.Domain`

- Core entities
- Enums
- Business rules

### `RootFlow.Application`

- Use cases
- DTOs
- Application contracts
- Orchestration logic

### `RootFlow.Infrastructure`

- Database access
- AI provider integrations
- Document processing
- File storage
- Semantic search implementation

### `RootFlow.Api`

- HTTP endpoints
- Request and response contracts
- Dependency injection
- Minimal transport-level validation

## Core MVP Entities

- `Workspace`
- `KnowledgeDocument`
- `DocumentChunk`
- `Conversation`
- `ConversationMessage`

Supporting enums:

- `DocumentStatus`
- `MessageRole`

## Core Application Contracts

- `IWorkspaceRepository`
- `IKnowledgeDocumentRepository`
- `IDocumentChunkRepository`
- `IConversationRepository`
- `IUnitOfWork`
- `IFileStorage`
- `IDocumentTextExtractor`
- `ITextChunker`
- `IEmbeddingService`
- `IKnowledgeSearchService`
- `IChatCompletionService`
- `IClock`

## MVP Use Cases

- Upload document
- Get document by id
- List documents
- Ask question
- Get conversation history

## Build Order

1. Create the domain and application foundation.
2. Add persistence and infrastructure integrations.
3. Implement document upload and processing.
4. Implement semantic retrieval and chat.
5. Add supporting improvements after the MVP works.

## Later Phases

- Multi-tenant support
- Admin features
- Messaging integrations
- Workflow automation
- Logging and guardrails
- Better retrieval quality and fallback strategies
