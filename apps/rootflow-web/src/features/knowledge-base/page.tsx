import { useRef, useState, type ChangeEvent } from "react";
import { FileText, FileUp, Filter, FolderKanban, Sparkles, UploadCloud } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { useDocumentsQuery, useUploadDocumentMutation } from "@/hooks/use-rootflow-data";
import { formatFileSize, formatRelativeDate } from "@/lib/formatting/formatters";

export function KnowledgeBasePage() {
  const [filterProcessedOnly, setFilterProcessedOnly] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const documentsQuery = useDocumentsQuery();
  const uploadMutation = useUploadDocumentMutation();

  const documents = documentsQuery.data ?? [];
  const visibleDocuments = filterProcessedOnly ? documents.filter((document) => document.status === 3) : documents;
  const processedCount = documents.filter((document) => document.status === 3).length;
  const failedCount = documents.filter((document) => document.status === 4).length;

  const handleFileSelection = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    await uploadMutation.mutateAsync({ file });
    event.target.value = "";
  };

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Knowledge Base"
        title="Curate a business-ready source of truth."
        description="Design the knowledge experience around trust: clear ingestion states, elegant upload patterns, and confidence that every answer can be traced back to a document."
        actions={
          <>
            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              onChange={handleFileSelection}
              accept=".txt,.md,.pdf,.doc,.docx"
            />
            <Button onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
              <UploadCloud />
              {uploadMutation.isPending ? "Uploading..." : "Upload documents"}
            </Button>
            <Button variant="outline" onClick={() => setFilterProcessedOnly((value) => !value)}>
              <Filter />
              {filterProcessedOnly ? "Show all documents" : "Show processed only"}
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
                  <Button size="lg" onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
                    {uploadMutation.isPending ? "Uploading..." : "Choose files"}
                  </Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Knowledge operating standards</CardTitle>
            <CardDescription>
              Live product signals from the current backend, paired with a calm ingestion experience.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[24px] border border-border/70 bg-secondary/35 p-4">
              <div className="text-sm font-semibold text-foreground">Documents available</div>
              <p className="mt-2 text-3xl font-display tracking-[-0.04em] text-foreground">{documents.length}</p>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">Live count from the current RootFlow document endpoint.</p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-secondary/35 p-4">
              <div className="text-sm font-semibold text-foreground">Processed and ready</div>
              <p className="mt-2 text-3xl font-display tracking-[-0.04em] text-foreground">{processedCount}</p>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">Documents already prepared for retrieval and grounded answers.</p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-secondary/35 p-4">
              <div className="text-sm font-semibold text-foreground">Attention needed</div>
              <p className="mt-2 text-3xl font-display tracking-[-0.04em] text-foreground">{failedCount}</p>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">Failed ingestions surface clearly so operators can intervene quickly.</p>
            </div>
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Current library</CardTitle>
                <CardDescription>Live data from the RootFlow backend with strong loading and empty states.</CardDescription>
              </div>
              <Badge variant="secondary">
                <FolderKanban className="size-3.5" />
                {visibleDocuments.length} shown
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {documentsQuery.isLoading ? (
              <LoadingState
                title="Loading document library"
                description="Fetching the current knowledge base so the interface reflects live ingestion data."
              />
            ) : documentsQuery.isError ? (
              <ErrorState
                title="Could not load the knowledge base"
                description="The frontend could not reach the RootFlow document endpoint. Check the API and try again."
                onRetry={() => documentsQuery.refetch()}
              />
            ) : visibleDocuments.length === 0 ? (
              <EmptyState
                icon={Sparkles}
                title={filterProcessedOnly ? "No processed documents yet" : "Your knowledge base is empty"}
                description={
                  filterProcessedOnly
                    ? "Documents are uploaded, but none are processed yet. Keep this page open and the status will reflect the backend state."
                    : "Upload your first document to turn this polished shell into a live knowledge workspace."
                }
              />
            ) : (
              visibleDocuments.map((document) => (
                <div
                  key={document.id}
                  className="grid gap-3 rounded-[24px] border border-border/70 bg-background/65 p-4 md:grid-cols-[minmax(0,1fr)_160px_140px_110px]"
                >
                  <div className="flex items-start gap-3">
                    <div className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                      <FileText className="size-5" />
                    </div>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-foreground">{document.originalFileName}</div>
                      <div className="text-sm text-muted-foreground">{formatFileSize(document.sizeBytes)}</div>
                    </div>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    <div className="font-medium text-foreground">Status</div>
                    <div className="mt-1">
                      <StatusBadge status={document.status} />
                    </div>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    <div className="font-medium text-foreground">Updated</div>
                    <div>{formatRelativeDate(document.processedAtUtc ?? document.createdAtUtc)}</div>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    <div className="font-medium text-foreground">Type</div>
                    <div>{document.contentType}</div>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        {uploadMutation.isError ? (
          <ErrorState
            title="Upload failed"
            description={uploadMutation.error.message}
            onRetry={() => fileInputRef.current?.click()}
          />
        ) : (
          <EmptyState
            icon={Sparkles}
            title="Upload flow is live"
            description="Document uploads now call the real backend endpoint, refresh the list automatically, and preserve the polished SaaS feel."
          />
        )}
      </section>
    </div>
  );
}
