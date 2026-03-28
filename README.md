# RootFlow

RootFlow is an AI-powered assistant platform designed to help businesses turn their knowledge into intelligent, automated interactions.

It allows companies to upload their internal data, search it using AI, and generate grounded responses through chat using Retrieval-Augmented Generation (RAG).

This repository contains the foundation of the RootFlow MVP — a clean, scalable, and SaaS-ready architecture.

---

## 🚀 Overview

RootFlow is being built as a reusable and commercial-ready platform, not tied to any specific business.

The goal is to provide a flexible foundation that can evolve into:

- multi-tenant SaaS platform
- AI assistants for different industries
- business automation workflows
- integrations with external systems (e.g. WhatsApp, CRMs, ERPs)

---

## 🎯 MVP Scope

The initial version focuses on a simple but powerful core:

- document upload
- text extraction
- text chunking
- embeddings generation via API
- PostgreSQL storage with vector search
- chat endpoint with grounded responses (RAG)
- conversation history

---

## 🧱 Tech Stack

- .NET 9
- ASP.NET Core Web API
- C#
- PostgreSQL
- pgvector for vector storage and semantic search
- xUnit for testing

Planned integrations:

- OpenAI-compatible providers (embeddings + chat)
- file storage abstraction (local/cloud)
- EF Core for persistence

---

## 🧠 Architecture

RootFlow follows a Clean Architecture approach with a modular monolith design:

Client
  → RootFlow.Api
  → RootFlow.Application
  → RootFlow.Domain
  → RootFlow.Infrastructure
  → PostgreSQL / File Storage / AI Provider

### Layer Responsibilities

- Domain: core business entities and rules
- Application: use cases, contracts, orchestration
- Infrastructure: database, AI providers, external services
- API: HTTP endpoints, configuration, dependency injection

---

## 📁 Project Structure

rootflow-ai/
├─ src/
│  ├─ RootFlow.Api/
│  ├─ RootFlow.Application/
│  ├─ RootFlow.Domain/
│  └─ RootFlow.Infrastructure/
├─ tests/
│  └─ RootFlow.UnitTests/
├─ .github/workflows/
├─ RootFlow.sln
├─ README.md
├─ .gitignore
├─ .editorconfig
├─ .gitattributes
└─ global.json

---

## ⚙️ Getting Started

### Prerequisites

- .NET SDK 9.x

### Run locally

git clone <your-repository-url>
cd rootflow-ai
dotnet restore
dotnet build
dotnet test
dotnet run --project src/RootFlow.Api

### Current Endpoints

- GET / → basic API check
- GET /health → health check

Swagger/OpenAPI is available in development mode.

---

## 🗺️ Roadmap

### MVP

1. Define domain entities and application contracts
2. Add persistence layer and PostgreSQL setup
3. Implement document ingestion pipeline
4. Implement chunking and embeddings
5. Implement semantic search + RAG
6. Persist conversation history

### Future

- multi-tenant support
- admin dashboard
- automation workflows (agents)
- external integrations
- guardrails and observability

---

## 📦 Status

This repository represents the baseline of the RootFlow MVP.

It is designed to evolve into a production-ready SaaS platform, with a strong focus on simplicity, scalability, and real-world applicability.

---

## 🤝 Vision

RootFlow aims to become a platform where businesses can:

- plug in their knowledge
- automate interactions
- build intelligent assistants
- reduce operational effort with AI
