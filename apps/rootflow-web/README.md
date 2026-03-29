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

API integration is intentionally not the focus yet.

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

```bash
npm install
npm run dev
```

Default local URL:

```bash
http://localhost:5173
```

## Validation

```bash
npm run lint
npm run build
```

## Notes

- `src/app` contains the shell, providers, and router
- `src/features` contains product areas
- `src/components/ui` contains reusable design-system primitives
- `src/lib/api` is reserved for the future RootFlow API client layer
