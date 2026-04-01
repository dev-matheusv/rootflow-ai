# RootFlow Web

Premium React frontend for RootFlow.

This app is the separate product-grade web client for the RootFlow platform. It is designed to feel polished, modern, and ready for client demos from the start, while staying clean enough to grow into a commercial SaaS product.

## Stack

- React
- TypeScript
- Vite
- Tailwind CSS
- shadcn/ui-style component foundation
- React Router
- TanStack Query
- React Hook Form
- Zod
- Lucide icons

## Current Scope

This phase includes:

- separate frontend scaffold under `apps/rootflow-web`
- premium design system foundation
- light and dark themes
- sidebar + topbar app shell
- polished main product pages
- route structure prepared for future auth pages

The frontend runtime expects an explicit API base URL through `VITE_API_BASE_URL`.

## Main Routes

- `/dashboard`
- `/knowledge-base`
- `/assistant`
- `/conversations`
- `/auth/login`
- `/auth/forgot-password`
- `/auth/reset-password`
- `/auth/invite`

## Local Development

The frontend reads its API target from `VITE_API_BASE_URL`.

### Point The Frontend At The Local Backend

1. Start the API from the repository root with the default local HTTP profile:

```powershell
dotnet run --project src/RootFlow.Api --launch-profile http
```

This serves the backend on `http://localhost:5011`.

2. In `apps/rootflow-web`, copy `.env.example` to `.env.local`.

```powershell
Copy-Item .env.example .env.local
```

3. Keep the default value in `.env.local` for the normal local backend flow:

```dotenv
VITE_API_BASE_URL=http://localhost:5011
```

4. Start the frontend:

```bash
npm install
npm run dev
```

Default local URL:

```bash
http://localhost:5173
```

In development, the top bar shows the resolved API base URL so you can immediately confirm which backend the frontend is using.

The frontend no longer falls back to `localhost` or same-origin defaults at runtime. If `VITE_API_BASE_URL` is missing, requests fail fast with a clear configuration error.

### Optional Local HTTPS Backend

If you want to run the backend with the HTTPS launch profile instead:

```powershell
dotnet run --project src/RootFlow.Api --launch-profile https
```

Set the frontend env var to:

```dotenv
VITE_API_BASE_URL=https://localhost:7088
```

If the browser rejects the local certificate, trust the ASP.NET Core development certificate first:

```powershell
dotnet dev-certs https --trust
```

`UseHttpsRedirection()` in the API does not mean the frontend should always call HTTPS locally. The correct local URL depends on the backend launch profile:

- `http://localhost:5011` for `--launch-profile http`
- `https://localhost:7088` for `--launch-profile https`

Restart the Vite dev server after changing `.env.local` so the updated value is picked up.

## Deployment

### Vercel

Set the project root to `apps/rootflow-web`.

- Install command: `npm ci`
- Build command: `npm run build`
- Output directory: `dist`
- Environment variable: `VITE_API_BASE_URL=https://your-api-domain`

[`vercel.json`](C:/RootFlow/apps/rootflow-web/vercel.json) includes an SPA rewrite so client-side routes resolve correctly.

## Validation

```bash
npm run lint
npm run build
```

## Notes

- `src/app` contains the shell, providers, and router
- `src/features` contains product areas
- `src/components/ui` contains reusable design-system primitives
- `src/components/diagnostics` contains small development-only runtime indicators
- `src/lib/api` contains the RootFlow API client layer
- `src/lib/config` contains environment resolution for the frontend runtime
