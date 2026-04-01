# Deployment Readiness

RootFlow is prepared to deploy as:

- backend: Railway or Render
- frontend: Vercel

## Backend Environment Variables

Required:

- `ROOTFLOW_JWT_KEY`
- one of `ROOTFLOW_DATABASE_URL`, `DATABASE_URL`, or `ConnectionStrings__Postgres`

Required when `AI__Mode=OpenAI`:

- `OPENAI_API_KEY`

Recommended:

- `ROOTFLOW_ALLOWED_ORIGINS`
- `AI__Mode`
- `OpenAI__BaseUrl`
- `OpenAI__ChatModel`
- `OpenAI__EmbeddingModel`

Notes:

- `ROOTFLOW_DATABASE_URL` and `DATABASE_URL` take precedence over `ConnectionStrings__Postgres`
- `postgres://` and `postgresql://` URLs are normalized into Npgsql connection strings at startup
- `ROOTFLOW_JWT_KEY` takes precedence over `Jwt:Key`
- `OPENAI_API_KEY` takes precedence over `OpenAI:ApiKey`
- database migrations still run automatically on startup and remain idempotent through `schema_migrations`
- `PORT` is honored automatically when `ASPNETCORE_URLS` is not already set, which keeps Railway and Render startup straightforward

## Frontend Environment Variables

Required:

- `VITE_API_BASE_URL`

Notes:

- the frontend no longer guesses a local API URL
- `VITE_API_BASE_URL` must point at the backend origin, for example `https://api.rootflow.example`

## Local Development

Backend:

1. Start PostgreSQL with `docker compose up -d`
2. Set `OPENAI_API_KEY` in your shell, or set `AI__Mode=Fake`
3. Run `dotnet run --project src/RootFlow.Api --launch-profile http`

The committed launch profiles already provide local development values for:

- `ConnectionStrings__Postgres`
- `ROOTFLOW_JWT_KEY`

Frontend:

1. Copy [apps/rootflow-web/.env.example](C:/RootFlow/apps/rootflow-web/.env.example) to `apps/rootflow-web/.env.local`
2. Run `npm ci`
3. Run `npm run dev`

## Railway Or Render Backend

Use a Web Service / service with:

- build command: `dotnet publish src/RootFlow.Api/RootFlow.Api.csproj -c Release -o out`
- start command: `dotnet out/RootFlow.Api.dll`

Set:

- `ROOTFLOW_JWT_KEY`
- `ROOTFLOW_ALLOWED_ORIGINS=https://your-frontend-domain`
- `OPENAI_API_KEY`
- either `ROOTFLOW_DATABASE_URL`, `DATABASE_URL`, or `ConnectionStrings__Postgres`

If the platform injects `DATABASE_URL`, RootFlow will use it automatically.

### Railway Docker deployment

RootFlow can also deploy on Railway with the root-level [Dockerfile](C:/RootFlow/Dockerfile), which avoids monorepo detection issues in Railpack.

- Docker build context: repository root
- Container port: `8080`
- Runtime binding: `ASPNETCORE_URLS=http://0.0.0.0:8080`

Set these Railway variables:

- `ROOTFLOW_DATABASE_URL` with the Supabase PostgreSQL connection string, or `DATABASE_URL`
- `ROOTFLOW_JWT_KEY`
- `OPENAI_API_KEY`
- `ROOTFLOW_ALLOWED_ORIGINS=https://your-frontend-domain`

The API already runs its built-in PostgreSQL schema migrations on startup through `PostgresDatabaseInitializer`, so no extra migration step is required in Railway.

## Vercel Frontend

Configure the Vercel project with:

- root directory: `apps/rootflow-web`
- install command: `npm ci`
- build command: `npm run build`
- output directory: `dist`
- environment variable: `VITE_API_BASE_URL=https://your-backend-domain`

[apps/rootflow-web/vercel.json](C:/RootFlow/apps/rootflow-web/vercel.json) handles SPA route rewrites.
