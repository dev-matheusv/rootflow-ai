import { FileText, FileUp, Filter, FolderKanban, Sparkles, UploadCloud } from "lucide-react";

import { EmptyState } from "@/components/feedback/empty-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";

const documents = [
  { name: "employee-handbook-2026.pdf", type: "PDF", status: "Processed", updated: "2 hours ago", chunks: 48 },
  { name: "billing-runbook.md", type: "Markdown", status: "Processed", updated: "Today", chunks: 12 },
  { name: "support-escalation-policy.docx", type: "DOCX", status: "Processing", updated: "Just now", chunks: 0 },
] as const;

export function KnowledgeBasePage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Knowledge Base"
        title="Curate a business-ready source of truth."
        description="Design the knowledge experience around trust: clear ingestion states, elegant upload patterns, and confidence that every answer can be traced back to a document."
        actions={
          <>
            <Button>
              <UploadCloud />
              Upload documents
            </Button>
            <Button variant="outline">
              <Filter />
              Filter library
            </Button>
          </>
        }
      />

      <section className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(35,95,255,0.11),transparent_44%,rgba(133,203,255,0.16))]" />
            <div className="relative flex h-full flex-col justify-between gap-8 p-6 md:p-8">
              <div className="space-y-4">
                <Badge>Ingestion surface</Badge>
                <div className="space-y-3">
                  <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">A premium upload flow that feels simple for business teams.</h2>
                  <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">
                    This area is designed for real client demos: large drop targets, clean empty states, and clear processing signals instead of technical clutter.
                  </p>
                </div>
              </div>

              <div className="rounded-[28px] border border-dashed border-primary/28 bg-background/68 p-6 backdrop-blur-sm">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div className="flex items-start gap-4">
                    <div className="flex size-14 items-center justify-center rounded-[24px] bg-primary/10 text-primary">
                      <FileUp className="size-6" />
                    </div>
                    <div className="space-y-2">
                      <div className="text-base font-semibold text-foreground">Drop files here or choose documents from your device</div>
                      <p className="text-sm leading-6 text-muted-foreground">
                        Supports policies, runbooks, FAQs, and internal documentation. Structured to evolve into richer pipelines later.
                      </p>
                    </div>
                  </div>
                  <Button size="lg">Choose files</Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Knowledge operating standards</CardTitle>
            <CardDescription>What the product should communicate to business users while they ingest content.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              ["Clear processing states", "Make ingestion progress visible without exposing technical noise."],
              ["Client-friendly explanations", "Use supportive language that explains what happens after upload."],
              ["Traceable source quality", "Each document should reinforce the feeling of grounded, auditable answers."],
            ].map(([title, description]) => (
              <div key={title} className="rounded-[24px] border border-border/70 bg-secondary/35 p-4">
                <div className="text-sm font-semibold text-foreground">{title}</div>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p>
              </div>
            ))}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Current library</CardTitle>
                <CardDescription>First-pass document states for the product shell.</CardDescription>
              </div>
              <Badge variant="secondary">
                <FolderKanban className="size-3.5" />
                3 assets
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {documents.map((document) => (
              <div
                key={document.name}
                className="grid gap-3 rounded-[24px] border border-border/70 bg-background/65 p-4 md:grid-cols-[minmax(0,1fr)_120px_120px_90px]"
              >
                <div className="flex items-start gap-3">
                  <div className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <FileText className="size-5" />
                  </div>
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-foreground">{document.name}</div>
                    <div className="text-sm text-muted-foreground">{document.type}</div>
                  </div>
                </div>
                <div className="text-sm text-muted-foreground">
                  <div className="font-medium text-foreground">Status</div>
                  <div>{document.status}</div>
                </div>
                <div className="text-sm text-muted-foreground">
                  <div className="font-medium text-foreground">Updated</div>
                  <div>{document.updated}</div>
                </div>
                <div className="text-sm text-muted-foreground">
                  <div className="font-medium text-foreground">Chunks</div>
                  <div>{document.chunks}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <EmptyState
          icon={Sparkles}
          title="Semantic structure comes next"
          description="This page is ready to connect to live upload and listing endpoints, but the visual model is already polished enough for demos and internal reviews."
        />
      </section>
    </div>
  );
}
