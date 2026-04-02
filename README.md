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

### 2. Configure environment variables

The API reads its sensitive runtime values from environment variables.

For local `dotnet run --launch-profile http` and `--launch-profile https`, the committed launch profiles already provide:

- `ConnectionStrings__Postgres`
- `ROOTFLOW_JWT_KEY`

Set `OPENAI_API_KEY` in your shell before starting the API, or switch to fake mode for local-only testing:

```powershell
$env:OPENAI_API_KEY="your-key"
```

Optional local fake mode:

```powershell
$env:AI__Mode="Fake"
```

Reference values are documented in [.env.example](C:/RootFlow/.env.example).

### 3. Run the API locally

```powershell
dotnet run --project src/RootFlow.Api --launch-profile http
```

The API will be available at:

- `http://localhost:5011`

### 4. Manual API smoke test

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

Use fake mode only when you explicitly want deterministic local testing or integration-test behavior.

Characteristics:

- deterministic embeddings
- deterministic chat output
- no external API dependency
- stable for local testing and integration tests

### OpenAI mode

Used by default in the base config and in `Development`.

To run with OpenAI, set:

```powershell
$env:OPENAI_API_KEY="your-key"
dotnet run --project src/RootFlow.Api --launch-profile http
```

To explicitly force fake mode for local experiments, set:

```powershell
$env:AI__Mode="Fake"
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

Base defaults remain in:

- [appsettings.json](C:/RootFlow/src/RootFlow.Api/appsettings.json)

Development-only non-sensitive overrides remain in:

- [appsettings.Development.json](C:/RootFlow/src/RootFlow.Api/appsettings.Development.json)

Important runtime settings:

- `ROOTFLOW_JWT_KEY`
- `ROOTFLOW_DATABASE_URL`
- `DATABASE_URL`
- `ConnectionStrings__Postgres`
- `ROOTFLOW_ALLOWED_ORIGINS`
- `ROOTFLOW_FRONTEND_BASE_URL`
- `ROOTFLOW_EMAIL_FROM_ADDRESS`
- `ROOTFLOW_EMAIL_FROM_NAME`
- `ROOTFLOW_EMAIL_SMTP_HOST`
- `ROOTFLOW_EMAIL_SMTP_PORT`
- `ROOTFLOW_EMAIL_SMTP_USERNAME`
- `ROOTFLOW_EMAIL_SMTP_PASSWORD`
- `ROOTFLOW_EMAIL_SMTP_ENABLE_SSL`
- `OPENAI_API_KEY`
- `AI__Mode`
- `OpenAI__BaseUrl`
- `OpenAI__ChatModel`
- `OpenAI__EmbeddingModel`
- `Storage:RootPath`

Precedence notes:

- `ROOTFLOW_DATABASE_URL` and `DATABASE_URL` override `ConnectionStrings:Postgres`
- `ROOTFLOW_JWT_KEY` overrides `Jwt:Key`
- `ROOTFLOW_FRONTEND_BASE_URL` overrides both `PasswordReset:FrontendBaseUrl` and `WorkspaceInvitations:FrontendBaseUrl`
- `OPENAI_API_KEY` overrides `OpenAI:ApiKey`

See [deployment.md](C:/RootFlow/docs/deployment.md) for deploy-specific steps.

## Deployment Targets

- Railway or Render for the backend
- Vercel for the frontend under `apps/rootflow-web`

Build and deploy details are documented in [deployment.md](C:/RootFlow/docs/deployment.md).

## Current Local Validation Flow

1. Start Docker Compose.
2. Set `OPENAI_API_KEY` or `AI__Mode=Fake`.
3. Run the API in Development.
4. Use the `.http` file to upload a document.
5. Ask a question against the uploaded knowledge.
6. Check the saved conversation history.

## Roadmap

The next product phases may add:

- multi-tenant support
- admin tools
- external integrations
- guardrails
- better retrieval quality

Those are intentionally out of scope for the current developer-experience phase.
