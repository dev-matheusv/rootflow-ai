# Training Mode + Certificates — Plan

## Why

T&D (Training & Development) is a SKU expansion for RootFlow that reuses ~70% of
the existing primitives: document storage, embeddings, RAG, DocumentEngine. It
opens a market (Brazilian companies pay R$300–500/month for T&D tooling) without
competing with the chat assistant.

Concrete deliverable per customer:
- Upload training material → AI generates quiz draft → creator reviews/edits →
  publishes module → employees take the quiz → on pass, system issues a PDF
  certificate with a public verification URL.

## Out of scope (for the MVP)

- Multi-attempt cooldown windows / proctoring / anti-cheat heuristics
- SCORM/xAPI export
- Multi-language quiz generation (PT-BR only at launch)
- Email notifications about certificate issuance (logs only; email is Phase 2)
- Manager dashboards across multiple employees (employee + admin views only)

## Domain model

New aggregates (under `RootFlow.Domain.Training`):

| Aggregate | Key fields | Notes |
|---|---|---|
| `TrainingProgram` | id, workspaceId, name, slug, description, isPublished, createdByUserId, createdAt, updatedAt | A curriculum / course. One program per business topic. |
| `TrainingModule` | id, programId, orderIndex, title, description, sourceDocumentIds (text[]), passingScore (default 70) | A chapter inside a program. Tied to one or more source documents that feed quiz generation. |
| `TrainingQuestion` | id, moduleId, orderIndex, prompt, type (SingleChoice / MultiChoice / TrueFalse), options (jsonb), correctAnswerIndices (int[]), explanation, sourceDocumentId, sourceChunkId, status (Draft / Published) | Created from AI generation; creator marks as Published before module goes live. |
| `TrainingAttempt` | id, moduleId, userId, startedAt, completedAt, score, status (InProgress / Passed / Failed) | One per user per attempt. |
| `TrainingAnswer` | id, attemptId, questionId, selectedIndices (int[]), isCorrect | One per question per attempt. |
| `TrainingCertificate` | id, programId, userId, issuedAt, code (12-char alphanumeric, unique), pdfStorageKey | Issued when user passes the last module of a program. |

## API surface

Authoring (workspace admin / owner):

| Verb | Route | Purpose |
|---|---|---|
| `POST` | `/api/training/programs` | Create program (draft). |
| `PATCH` | `/api/training/programs/{id}` | Edit name/description/passingScore. |
| `POST` | `/api/training/programs/{id}/publish` | Flip `isPublished` to true. Validates all modules have at least 3 published questions. |
| `POST` | `/api/training/programs/{id}/modules` | Add module. |
| `POST` | `/api/training/modules/{id}/generate-quiz` | AI-generates questions from `sourceDocumentIds`. Returned as Draft. |
| `PATCH` | `/api/training/questions/{id}` | Edit question text/options/correctness. Sets status back to Draft if was Published. |
| `POST` | `/api/training/questions/{id}/publish` | Mark question as Published. |
| `DELETE` | `/api/training/questions/{id}` | Remove. |

Consumer (any workspace member):

| Verb | Route | Purpose |
|---|---|---|
| `GET` | `/api/training/programs` | Lists published programs. |
| `GET` | `/api/training/programs/{id}` | Program detail + module list + my progress. |
| `POST` | `/api/training/modules/{id}/attempts` | Start an attempt. Returns the question list. |
| `POST` | `/api/training/attempts/{id}/answer` | Submit one answer. |
| `POST` | `/api/training/attempts/{id}/submit` | Finalize attempt → computes score, marks pass/fail. |
| `GET` | `/api/training/certificates` | List my certificates. |
| `GET` | `/api/training/certificates/{id}/pdf` | Download PDF. |

Public (no auth):

| Verb | Route | Purpose |
|---|---|---|
| `GET` | `/api/public/training/verify/{code}` | Returns `{ valid, employeeName, programName, workspaceName, issuedAt }`. |

## Frontend routes

| Route | Purpose |
|---|---|
| `/training` | Employee dashboard — published programs + my progress. |
| `/training/programs/{id}` | Program detail with module list. |
| `/training/programs/{id}/modules/{moduleId}/attempt` | Quiz taking UI. |
| `/training/certificates` | My certificates with download buttons. |
| `/admin/training` | Creator dashboard. |
| `/admin/training/programs/{id}/edit` | Program + module editor. |
| `/admin/training/programs/{id}/modules/{moduleId}/edit` | Question editor (AI-generated drafts, manual edits). |
| `/verify/{code}` | Public verification page. |

## Quiz generation (the AI part)

Input:
- Module title + description
- Top N (~20) chunks from the source documents, fetched via existing embedding search using the module title/description as query

Prompt (PT-BR, instruct to also accept EN if doc language is EN):
- Request a JSON array of 5–8 questions
- Each question: prompt, type, options (3–4), correctAnswerIndices, explanation referencing the source chunk
- Use OpenAI structured outputs (json_schema) to enforce the schema
- Reject if model returns questions that aren't grounded (we'll do a similarity sanity check against the source chunk in code)

Generated questions land as `status = Draft`. Creator reviews in the editor and clicks "Publish all" or edits individually.

## Certificate PDF

Built-in DocumentEngine template (one per workspace, auto-seeded):
- Fields: `employee_name`, `program_name`, `workspace_name`, `issued_date`, `certificate_code`, `verification_url`
- Default design: landscape A4, decorative border, signature line. Customers can edit the template if they want their logo / brand.

## Billing gating

Two-tier approach:
1. **MVP**: gated by a single workspace feature flag `training_enabled`. Default false. Set to true manually by us for early customers, or by a Stripe webhook when they buy the add-on.
2. **Phase 2 (post-MVP)**: Stripe product "RootFlow T&D Add-on" with monthly price. Checkout flow + webhook to flip the flag.

## Implementation phases

**Phase A — Schema + domain (~3 days)**
- DB migrations for all 6 tables + indexes
- Domain models + repositories
- Unit tests for invariants (e.g. can't publish program without published questions)

**Phase B — Authoring flow (~4 days)**
- POST/PATCH endpoints for programs/modules/questions
- AI generation endpoint with structured outputs
- Admin UI: program editor, module editor, question reviewer

**Phase C — Consumer flow (~3 days)**
- GET endpoints
- Attempt + answer + submit flow with score calculation
- Employee UI: program browser, quiz UI, results screen

**Phase D — Certificates (~3 days)**
- Certificate aggregate + PDF generation via DocumentEngine
- Built-in certificate template seeded per workspace
- Public verification endpoint + page
- Employee UI to view/download

**Phase E — Billing gating + polish (~2 days)**
- Workspace feature flag schema field
- Stripe product/price config
- Checkout flow integration
- E2E happy path test

Total realistic: **~2.5 to 3 weeks of focused work**, broken into ~15 commits across as many small PRs as makes sense (probably 3–4 PRs aligned with phases).

## Open questions for the user

1. **Branding the certificate**: should the default template carry the customer's workspace logo automatically, or stay neutral until the customer customizes it?
2. **Multiple attempts**: does failing a module let the user retry immediately, or should there be a cooldown / max attempts? *Default proposal: unlimited retries with score tracking — keep simple.*
3. **Mandatory training**: should admins be able to mark a program as "required" and see who hasn't completed it? Or save this for Phase 2? *Default proposal: Phase 2.*
