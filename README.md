# RootFlow

RootFlow is a standalone AI assistant platform for businesses.

The goal of the product is simple: let a business upload its knowledge, search that knowledge with AI, and get grounded answers through chat using RAG.

This repository is the baseline for the MVP. It currently contains the clean solution scaffold and repository setup. Product features will be added step by step.

## Project Description

RootFlow is being built as a reusable and sellable SaaS-ready foundation.

It is not tied to any single company or system. The platform is designed so it can later support:

- multiple companies
- different AI providers
- business integrations
- admin and automation features

## MVP Scope

The MVP is focused on a small and practical feature set:

- document upload
- text extraction
- text chunking
- embeddings generation through API
- PostgreSQL storage with vector search
- chat endpoint with grounded answers
- conversation history

## Tech Stack

- .NET 9
- ASP.NET Core Web API
- C#
- PostgreSQL
- `pgvector` for vector storage and semantic search
- xUnit for tests

Planned infrastructure for the MVP:

- OpenAI-compatible embeddings and chat provider integration
- local or cloud file storage abstraction
- EF Core for persistence

## High-Level Architecture

RootFlow follows a simple Clean Architecture approach:

```text
Client
  -> RootFlow.Api
  -> RootFlow.Application
  -> RootFlow.Domain
  -> RootFlow.Infrastructure
  -> PostgreSQL / File Storage / AI Provider
```

Layer roles:

- `RootFlow.Domain`: business entities and core rules
- `RootFlow.Application`: use cases, contracts, DTOs, orchestration
- `RootFlow.Infrastructure`: database, AI, file storage, external services
- `RootFlow.Api`: HTTP endpoints, dependency injection, configuration

## Project Structure

```text
rootflow-ai/
├─ src/
│  ├─ RootFlow.Api/
│  ├─ RootFlow.Application/
│  ├─ RootFlow.Domain/
│  └─ RootFlow.Infrastructure/
├─ .github/
│  └─ workflows/
│     └─ ci.yml
├─ tests/
│  └─ RootFlow.UnitTests/
├─ RootFlow.sln
├─ README.md
├─ .gitignore
├─ .editorconfig
├─ .gitattributes
└─ global.json
```

## Getting Started

### Prerequisites

- .NET SDK 9.0.308 or compatible 9.0 SDK

### Run locally

```powershell
git clone <your-repository-url>
cd rootflow-ai
dotnet restore
dotnet build
dotnet test
dotnet run --project src/RootFlow.Api
```

### Current baseline behavior

The API currently exposes simple baseline endpoints:

- `GET /`
- `GET /health`

OpenAPI is available in development mode.

## Roadmap / Next Steps

Planned next steps for the MVP:

1. Define domain entities and application contracts
2. Add persistence abstractions and PostgreSQL setup
3. Implement document ingestion flow
4. Implement chunking and embeddings
5. Implement semantic search and chat with RAG
6. Persist conversation history

Later phases:

- multi-tenant support
- admin features
- workflow automations
- external integrations
- guardrails and operational hardening

## Repository Status

This is the professional baseline for the RootFlow MVP.

The repository is prepared to grow into a commercial product with a clean structure, clear boundaries, and minimal unnecessary complexity.
