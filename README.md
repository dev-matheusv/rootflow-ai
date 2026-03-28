# RootFlow

RootFlow is a standalone AI assistant platform for businesses.

It lets a business upload knowledge, store it, search it with embeddings, and answer questions through a grounded chat flow using RAG.

## MVP Scope

The current MVP includes:

- document upload
- text extraction for text files and PDF
- chunking
- embeddings generation
- PostgreSQL + pgvector storage
- semantic search
- chat endpoint with grounded answers
- conversation history

This repository is focused on a practical MVP foundation, not a finished product.

## Tech Stack

- .NET 9
- ASP.NET Core minimal API
- PostgreSQL
- pgvector
- xUnit
- Docker Compose for local database setup

## High-Level Architecture

RootFlow uses a simple Clean Architecture modular monolith:

`Client -> RootFlow.Api -> RootFlow.Application -> RootFlow.Domain -> RootFlow.Infrastructure -> PostgreSQL / File Storage / AI Provider`

Layer responsibilities:

- `RootFlow.Domain`: core entities and business rules
- `RootFlow.Application`: use cases, DTOs, and contracts
- `RootFlow.Infrastructure`: PostgreSQL, file storage, AI integrations, and search
- `RootFlow.Api`: HTTP endpoints and application startup

## Project Structure

```text
rootflow-ai/
|-- src/
|   |-- RootFlow.Api/
|   |-- RootFlow.Application/
|   |-- RootFlow.Domain/
|   `-- RootFlow.Infrastructure/
|-- tests/
|   |-- RootFlow.UnitTests/
|   `-- RootFlow.Api.IntegrationTests/
|-- docs/
|-- docker-compose.yml
|-- RootFlow.sln
`-- README.md
```

## Local Development

### Prerequisites

- .NET SDK 9.x
- Docker Desktop or Docker Engine with Docker Compose

### 1. Start PostgreSQL with pgvector

```powershell
docker compose up -d
```

This starts:

- PostgreSQL on `localhost:5432`
- persistent local volume for database data

### 2. Run the API locally

Development mode is configured to use:

- the local Docker PostgreSQL database
- deterministic fake AI mode

This means you can run and validate the main flow without a real OpenAI key.

```powershell
dotnet run --project src/RootFlow.Api --launch-profile http
```

The API will be available at:

- `http://localhost:5011`

### 3. Manual API smoke test

Use:

- [RootFlow.Api.http](C:/RootFlow/src/RootFlow.Api/RootFlow.Api.http)

That file includes requests for:

- health check
- document upload
- list documents
- ask question
- conversation history

## AI Modes

RootFlow supports two AI modes:

### Fake mode

Used by default in `Development`.

Characteristics:

- deterministic embeddings
- deterministic chat output
- no external API dependency
- stable for local testing and integration tests

### OpenAI mode

Used by default in the base config.

To run with OpenAI, set:

```powershell
$env:OPENAI_API_KEY="your-key"
$env:AI__Mode="OpenAI"
dotnet run --project src/RootFlow.Api --launch-profile http
```

## Testing

### Unit tests

```powershell
dotnet test tests/RootFlow.UnitTests/RootFlow.UnitTests.csproj
```

### Integration tests

The integration tests use:

- the real API pipeline
- PostgreSQL
- fake AI mode
- a dedicated test database: `rootflow_test`
- table cleanup before each test run

Before running them, make sure Docker Compose is running.

```powershell
dotnet test tests/RootFlow.Api.IntegrationTests/RootFlow.Api.IntegrationTests.csproj
```

### Full solution test run

```powershell
dotnet test RootFlow.sln
```

## Configuration Summary

Base settings are in:

- [appsettings.json](C:/RootFlow/src/RootFlow.Api/appsettings.json)

Local development overrides are in:

- [appsettings.Development.json](C:/RootFlow/src/RootFlow.Api/appsettings.Development.json)

Important settings:

- `ConnectionStrings:Postgres`
- `AI:Mode`
- `OpenAI:BaseUrl`
- `OpenAI:ChatModel`
- `OpenAI:EmbeddingModel`
- `Storage:RootPath`

## Current Local Validation Flow

1. Start Docker Compose.
2. Run the API in Development.
3. Use the `.http` file to upload a document.
4. Ask a question against the uploaded knowledge.
5. Check the saved conversation history.

## Roadmap

The next product phases may add:

- multi-tenant support
- admin tools
- external integrations
- guardrails
- better retrieval quality

Those are intentionally out of scope for the current developer-experience phase.
