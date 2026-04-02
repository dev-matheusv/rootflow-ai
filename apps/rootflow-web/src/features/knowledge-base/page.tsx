import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { useDocumentsQuery, useUploadDocumentMutation } from "@/hooks/use-rootflow-data";
import { formatFileSize, formatRelativeDate } from "@/lib/formatting/formatters";
import { useRef, useState, type ChangeEvent } from "react";
import { CheckCircle2, Clock3, FileText, Filter, FolderKanban, UploadCloud } from "lucide-react";

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
    <div className="space-y-5">
      <PageHeader
        title="Knowledge Base"
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

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[
          ["Documents", documents.length],
          ["Ready", processedCount],
          ["Processing", processingCount],
          ["Failed", failedCount],
        ].map(([label, value]) => (
          <Card key={label} className="border-border/70 bg-background/72 shadow-none">
            <CardContent className="space-y-1 p-4">
              <div className="text-sm text-muted-foreground">{label}</div>
              <div className="font-display text-[1.75rem] tracking-[-0.045em] text-foreground">{value}</div>
            </CardContent>
          </Card>
        ))}
      </section>

      <section className="grid gap-3 xl:grid-cols-[1.15fr_0.85fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Documents</CardTitle>
              <div className="flex flex-wrap items-center gap-2">
                {processingCount > 0 ? (
                  <Badge variant="secondary">
                    <Clock3 className="size-3.5" />
                    Refreshing
                  </Badge>
                ) : null}
                <Badge variant="secondary">
                  <FolderKanban className="size-3.5" />
                  {visibleDocuments.length}
                </Badge>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {documentsQuery.isLoading ? (
              <LoadingState
                title="Loading documents"
                description="Fetching the current library."
              />
            ) : documentsQuery.isError ? (
              <ErrorState
                title="Could not load documents"
                description="Try again."
                onRetry={() => documentsQuery.refetch()}
              />
            ) : visibleDocuments.length === 0 ? (
              <EmptyState
                icon={UploadCloud}
                title={filterProcessedOnly ? "No processed documents" : "No documents"}
                description={
                  filterProcessedOnly
                    ? "Uploads are still processing."
                    : "Upload a document to start retrieval."
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
                        <p className="mt-2 text-xs leading-5 text-muted-foreground">Processing</p>
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
          <Card className="border-border/70 bg-background/72 shadow-none">
            <CardHeader>
              <CardTitle>Upload</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <Button className="w-full justify-between" onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
                {uploadMutation.isPending ? "Uploading..." : "Choose file"}
                <UploadCloud />
              </Button>

              {uploadMutation.isError ? (
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {uploadMutation.error.message}
                </div>
              ) : lastUploadedDocumentName ? (
                <div className="rounded-[22px] border border-emerald-500/20 bg-emerald-500/8 p-4">
                  <div className="flex items-start gap-3">
                    <div className="flex size-10 items-center justify-center rounded-2xl bg-emerald-500/12 text-emerald-600 dark:text-emerald-300">
                      <CheckCircle2 className="size-5" />
                    </div>
                    <div className="space-y-1">
                      <div className="text-sm font-semibold text-foreground">Uploaded</div>
                      <p className="text-sm text-muted-foreground">{lastUploadedDocumentName}</p>
                    </div>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">Supports .txt, .md, .pdf, .doc, and .docx.</p>
              )}
            </CardContent>
          </Card>

          {processingCount > 0 ? (
            <Card className="border-border/70 bg-background/72 shadow-none">
              <CardHeader>
                <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <Clock3 className="size-5" />
                </div>
                <CardTitle>Processing</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  {processingCount} document{processingCount === 1 ? "" : "s"} in progress. This page refreshes automatically.
                </p>
              </CardContent>
            </Card>
          ) : null}
        </div>
      </section>
    </div>
  );
}
