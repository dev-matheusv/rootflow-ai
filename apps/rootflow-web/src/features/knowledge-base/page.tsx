import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { useI18n } from "@/app/providers/i18n-provider";
import { useDocumentsQuery, useUploadDocumentMutation } from "@/hooks/use-rootflow-data";
import { formatFileSize, formatRelativeDate, getDocumentTypeLabel } from "@/lib/formatting/formatters";
import { useRef, useState, type ChangeEvent } from "react";
import { AlertTriangle, CheckCircle2, Clock3, FileText, Filter, FolderKanban, UploadCloud } from "lucide-react";

export function KnowledgeBasePage() {
  const { locale, t } = useI18n();
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
  const sortedByRecentUpdate = [...documents].sort(
    (left, right) =>
      new Date(right.processedAtUtc ?? right.createdAtUtc).getTime() -
      new Date(left.processedAtUtc ?? left.createdAtUtc).getTime(),
  );
  const latestDocument = sortedByRecentUpdate[0];
  const processingDocuments = documents.filter((document) => document.status === 2).slice(0, 2);

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
        title={t("knowledgeBase.title")}
        description={t("knowledgeBase.description")}
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
              {uploadMutation.isPending ? t("assistant.sending") : t("common.actions.uploadDocuments")}
            </Button>
            <Button variant="outline" onClick={() => setFilterProcessedOnly((value) => !value)}>
              <Filter />
              {filterProcessedOnly ? t("common.actions.showAllDocuments") : t("common.actions.showProcessedOnly")}
            </Button>
          </>
        }
      />

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { label: t("common.labels.documents"), value: documents.length, hint: t("common.helper.librarySize"), icon: FolderKanban },
          { label: t("common.labels.ready"), value: processedCount, hint: t("common.helper.searchableNow"), icon: CheckCircle2 },
          { label: t("common.labels.processing"), value: processingCount, hint: t("common.helper.parsingLive"), icon: Clock3 },
          { label: t("knowledgeBase.failedMetric"), value: failedCount, hint: t("common.helper.needsReview"), icon: AlertTriangle },
        ].map((metric) => {
          const Icon = metric.icon;

          return (
            <Card key={metric.label} className="border-border/80 bg-card/86">
              <CardContent className="space-y-3 p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex size-9 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <Icon className="size-4.5" />
                  </div>
                  <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{metric.hint}</div>
                </div>
                <div className="space-y-1">
                  <div className="text-sm font-medium text-foreground/82">{metric.label}</div>
                  <div className="font-display text-[1.9rem] font-semibold tracking-[-0.05em] text-foreground">{metric.value}</div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </section>

      <section className="grid gap-3 xl:grid-cols-[1.15fr_0.85fr]">
        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0 space-y-1">
                <CardTitle>{t("knowledgeBase.documentsTitle")}</CardTitle>
                <div className="text-sm text-muted-foreground">
                  {latestDocument
                    ? t("knowledgeBase.latestUpdate", { time: formatRelativeDate(latestDocument.processedAtUtc ?? latestDocument.createdAtUtc, locale) })
                    : t("knowledgeBase.noDocumentActivity")}
                </div>
              </div>
              <div className="flex min-w-0 flex-wrap items-center gap-2">
                {processingCount > 0 ? (
                  <Badge variant="secondary">
                    <Clock3 className="size-3.5" />
                    {t("knowledgeBase.refreshing")}
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
                title={t("knowledgeBase.loadingDocumentsTitle")}
                description={t("knowledgeBase.loadingDocumentsDescription")}
              />
            ) : documentsQuery.isError ? (
              <ErrorState
                title={t("knowledgeBase.documentsErrorTitle")}
                description={t("knowledgeBase.documentsErrorDescription")}
                onRetry={() => documentsQuery.refetch()}
              />
            ) : visibleDocuments.length === 0 ? (
              <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                {filterProcessedOnly
                  ? t("knowledgeBase.noProcessedDocumentsYet")
                  : t("knowledgeBase.uploadFirstDocumentHint")}
              </div>
            ) : (
              <div className="overflow-hidden rounded-[22px] border border-border/80 bg-card/76">
                {visibleDocuments.map((document) => (
                  <div
                    key={document.id}
                    className="grid gap-3 px-4 py-3.5 transition-[transform,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:bg-background/82 hover:shadow-[inset_0_1px_0_rgba(255,255,255,0.28)] md:grid-cols-[minmax(0,1.35fr)_160px_120px_88px] md:items-center [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/72"
                  >
                    <div className="flex items-start gap-3">
                      <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl border border-primary/14 bg-primary/[0.11] text-primary">
                        <FileText className="size-4.5" />
                      </div>
                      <div className="min-w-0 space-y-1">
                        <div className="truncate text-sm font-semibold text-foreground">{document.originalFileName}</div>
                        <div className="text-xs font-medium text-muted-foreground">{formatFileSize(document.sizeBytes)}</div>
                      </div>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      <div className="font-medium text-foreground/88">{t("common.labels.status")}</div>
                      <div className="mt-1">
                        <StatusBadge status={document.status} />
                        {document.status === 4 && document.failureReason ? (
                          <p className="mt-2 text-xs leading-5 text-destructive">{document.failureReason}</p>
                        ) : document.status === 2 ? (
                          <p className="mt-2 text-xs leading-5 text-muted-foreground">{t("common.labels.processing")}</p>
                        ) : null}
                      </div>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      <div className="font-medium text-foreground/88">{t("common.labels.updatedLabel")}</div>
                      <div className="font-medium text-foreground">{formatRelativeDate(document.processedAtUtc ?? document.createdAtUtc, locale)}</div>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      <div className="font-medium text-foreground/88">{t("common.labels.type")}</div>
                      <div className="truncate font-medium text-foreground" title={document.contentType}>
                        {getDocumentTypeLabel(document.originalFileName, document.contentType)}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>{t("knowledgeBase.uploadTitle")}</CardTitle>
              <div className="text-sm text-muted-foreground">{t("knowledgeBase.uploadDescription")}</div>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-4">
              <Button className="w-full justify-between" onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
                {uploadMutation.isPending ? t("assistant.sending") : t("common.actions.chooseFile")}
                <UploadCloud />
              </Button>
              <div className="flex flex-wrap gap-2">
                {["PDF", "DOCX", "DOC", "TXT", "MD"].map((fileType) => (
                  <Badge key={fileType} variant="secondary">
                    {fileType}
                  </Badge>
                ))}
              </div>

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
                      <div className="text-sm font-semibold text-foreground">{t("knowledgeBase.uploadedTitle")}</div>
                      <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{lastUploadedDocumentName}</p>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="space-y-3 rounded-[20px] border border-border/80 bg-card/76 p-4">
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-foreground">{t("knowledgeBase.recentActivity")}</div>
                    <div className="text-sm text-muted-foreground/95">{t("knowledgeBase.recentActivityHint")}</div>
                  </div>
                  <div className="space-y-2 text-sm text-muted-foreground">
                    <div className="flex min-w-0 items-center justify-between gap-3">
                      <span>{t("common.labels.latestFile")}</span>
                      <span className="min-w-0 max-w-[12rem] truncate text-right text-foreground" title={latestDocument?.originalFileName}>
                        {latestDocument?.originalFileName ?? t("common.helper.none")}
                      </span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>{t("common.labels.readyNow")}</span>
                      <span className="text-foreground">{processedCount}</span>
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="border-t border-border/75 pt-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-foreground">{t("knowledgeBase.processingTitle")}</div>
                  <div className="text-sm text-muted-foreground">
                    {processingCount > 0
                      ? t(processingCount === 1 ? "knowledgeBase.processingCount" : "knowledgeBase.processingCountPlural", { count: processingCount })
                      : t("knowledgeBase.currentProcessing")}
                  </div>
                </div>
                {processingCount > 0 ? (
                  <Badge variant="secondary">
                    <Clock3 className="size-3.5" />
                    {t("common.labels.live")}
                  </Badge>
                ) : null}
              </div>
              {processingDocuments.length > 0 ? (
                <div className="mt-3 overflow-hidden rounded-[20px] border border-border/80 bg-card/76">
                  {processingDocuments.map((document) => (
                    <div
                      key={document.id}
                      className="flex min-w-0 items-center justify-between gap-3 px-3.5 py-3 text-sm transition-colors duration-200 hover:bg-background/80 [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/72"
                    >
                      <span className="min-w-0 flex-1 truncate text-foreground" title={document.originalFileName}>
                        {document.originalFileName}
                      </span>
                      <span className="shrink-0 text-muted-foreground">{formatRelativeDate(document.createdAtUtc, locale)}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="mt-3 rounded-[18px] border border-dashed border-border/80 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                  {t("knowledgeBase.processingPlaceholder")}
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
