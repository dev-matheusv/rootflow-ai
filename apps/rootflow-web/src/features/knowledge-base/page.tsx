import { useRef, useState, type ChangeEvent } from "react";
import { CheckCircle2, Clock3, FileText, FileUp, Filter, FolderKanban, Sparkles, UploadCloud } from "lucide-react";

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
  const [lastUploadedDocumentName, setLastUploadedDocumentName] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const uploadMutation = useUploadDocumentMutation();

  const documents = documentsQuery.data ?? [];
  const processingCount = documents.filter((document) => document.status === 2).length;
  const visibleDocuments = filterProcessedOnly ? documents.filter((document) => document.status === 3) : documents;
  const processedCount = documents.filter((document) => document.status === 3).length;
  const failedCount = documents.filter((document) => document.status === 4).length;

  const handleFileSelection = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    uploadMutation.reset();
    setLastUploadedDocumentName(null);

    try {
      const uploadedDocument = await uploadMutation.mutateAsync({ file });
      setLastUploadedDocumentName(uploadedDocument.originalFileName);
    } finally {
      event.target.value = "";
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Knowledge Base"
        title="Curate a business-ready source of truth."
        description="Keep uploads, processing states, and document readiness easy to scan so the knowledge base feels operational, not busy."
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
                  <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">A quieter upload flow with clearer readiness signals.</h2>
                  <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">
                    Large targets, simple status language, and calmer spacing keep the ingestion surface usable in real team workflows.
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
            <div className="rounded-[24px] border border-border/70 bg-secondary/35 p-4">
              <div className="text-sm font-semibold text-foreground">Still processing</div>
              <p className="mt-2 text-3xl font-display tracking-[-0.04em] text-foreground">{processingCount}</p>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {processingCount > 0
                  ? "The page is auto-refreshing while these documents finish processing."
                  : "Auto-refresh stays off until a new upload enters processing."}
              </p>
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
              <div className="flex flex-wrap items-center gap-2">
                {processingCount > 0 ? (
                  <Badge variant="secondary">
                    <Clock3 className="size-3.5" />
                    Auto-refreshing
                  </Badge>
                ) : null}
                <Badge variant="secondary">
                  <FolderKanban className="size-3.5" />
                  {visibleDocuments.length} shown
                </Badge>
              </div>
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
                      {document.status === 4 && document.failureReason ? (
                        <p className="mt-2 text-xs leading-5 text-destructive">{document.failureReason}</p>
                      ) : document.status === 2 ? (
                        <p className="mt-2 text-xs leading-5 text-muted-foreground">
                          RootFlow is still preparing this file for retrieval.
                        </p>
                      ) : null}
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

        <div className="space-y-4">
          {uploadMutation.isError ? (
            <ErrorState
              title="Upload failed"
              description={uploadMutation.error.message}
              onRetry={() => fileInputRef.current?.click()}
            />
          ) : lastUploadedDocumentName ? (
            <Card className="border-emerald-500/20 bg-emerald-500/8">
              <CardHeader>
                <div className="flex size-12 items-center justify-center rounded-2xl bg-emerald-500/12 text-emerald-600 dark:text-emerald-300">
                  <CheckCircle2 className="size-5" />
                </div>
                <CardTitle>Upload accepted</CardTitle>
                <CardDescription>{lastUploadedDocumentName} was sent to RootFlow and is now tracked in the live library.</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm leading-7 text-muted-foreground">
                  {processingCount > 0
                    ? "This page will keep polling automatically until every processing document reaches a final state."
                    : "The document list is up to date and polling stays idle until a new upload enters processing."}
                </p>
              </CardContent>
            </Card>
          ) : (
            <EmptyState
              icon={Sparkles}
              title="Upload flow is live"
              description="Document uploads now call the real backend endpoint, show clear processing states, and only auto-refresh while work is still running."
            />
          )}

          {processingCount > 0 ? (
            <Card className="bg-card/72">
              <CardHeader>
                <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <Clock3 className="size-5" />
                </div>
                <CardTitle>Processing in progress</CardTitle>
                <CardDescription>
                  {processingCount} document{processingCount === 1 ? "" : "s"} still need chunking, embeddings, or final storage.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm leading-7 text-muted-foreground">
                  The page is polling every few seconds while these jobs remain in progress, then it stops automatically when all documents finish.
                </p>
              </CardContent>
            </Card>
          ) : null}
        </div>
      </section>
    </div>
  );
}
