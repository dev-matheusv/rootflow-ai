import { BookOpenText, Bot, DatabaseZap, MessagesSquare } from "lucide-react";
import { Link } from "react-router-dom";

import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/features/auth/auth-provider";
import { useConversationsQuery, useDocumentsQuery, useHealthQuery } from "@/hooks/use-rootflow-data";
import { formatRelativeDate } from "@/lib/formatting/formatters";

export function DashboardPage() {
  const { session } = useAuth();
  const healthQuery = useHealthQuery();
  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const conversationsQuery = useConversationsQuery();

  const documents = documentsQuery.data ?? [];
  const conversations = conversationsQuery.data ?? [];
  const processedCount = documents.filter((document) => document.status === 3).length;
  const processingCount = documents.filter((document) => document.status === 2).length;
  const failedCount = documents.filter((document) => document.status === 4).length;
  const latestConversation = conversations[0];
  const latestDocument = [...documents].sort(
    (left, right) =>
      new Date(right.processedAtUtc ?? right.createdAtUtc).getTime() -
      new Date(left.processedAtUtc ?? left.createdAtUtc).getTime(),
  )[0];

  const metrics = [
    { label: "Documents", value: String(documents.length), note: "Total", hint: "Library size", icon: BookOpenText },
    { label: "Ready", value: String(processedCount), note: "Processed", hint: "Grounded answers", icon: Bot },
    { label: "Conversations", value: String(conversations.length), note: "Saved", hint: "Reusable sessions", icon: MessagesSquare },
    {
      label: "API",
      value: healthQuery.data?.status === "healthy" ? "Healthy" : "Checking",
      note: "Status",
      hint: "Search pipeline",
      icon: DatabaseZap,
    },
  ] as const;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Overview"
        description="A live view of workspace readiness, assistant activity, and system health."
        actions={
          <>
            <Button asChild>
              <Link to="/assistant">Open assistant</Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to="/knowledge-base">Review documents</Link>
            </Button>
          </>
        }
      />

      <section className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {documentsQuery.isLoading || healthQuery.isLoading || conversationsQuery.isLoading ? (
          <div className="md:col-span-2 xl:col-span-4">
            <LoadingState
              title="Loading overview"
              description="Fetching workspace activity."
            />
          </div>
        ) : documentsQuery.isError || healthQuery.isError || conversationsQuery.isError ? (
          <div className="md:col-span-2 xl:col-span-4">
            <ErrorState
              title="Could not load overview"
              description="Try again."
              onRetry={() => {
                void documentsQuery.refetch();
                void healthQuery.refetch();
                void conversationsQuery.refetch();
              }}
            />
          </div>
        ) : (
          metrics.map((metric) => {
            const Icon = metric.icon;
            const isPrimaryMetric = metric.label === "Ready";

            return (
              <Card
                key={metric.label}
                className={isPrimaryMetric ? "border-primary/18 bg-primary/[0.06]" : "border-border/80 bg-card/86"}
              >
                <CardContent className="space-y-3 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <div className={`flex size-9 items-center justify-center rounded-2xl ${isPrimaryMetric ? "bg-primary/14 text-primary" : "bg-primary/10 text-primary"}`}>
                      <Icon className="size-5" />
                    </div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{metric.note}</div>
                  </div>
                  <div className="space-y-1.5">
                    <div className="text-sm font-medium text-foreground/82">{metric.label}</div>
                    <div className="font-display text-[1.9rem] font-semibold tracking-[-0.05em] text-foreground">{metric.value}</div>
                    <div className="text-xs text-muted-foreground">
                      {metric.label === "Documents"
                        ? latestDocument
                          ? `Latest ${formatRelativeDate(latestDocument.processedAtUtc ?? latestDocument.createdAtUtc)}`
                          : "No uploads yet"
                        : metric.label === "Ready"
                          ? processingCount > 0
                            ? `${processingCount} processing`
                            : "Up to date"
                          : metric.label === "Conversations"
                            ? latestConversation
                              ? `Updated ${formatRelativeDate(latestConversation.updatedAtUtc)}`
                              : "No sessions yet"
                            : healthQuery.data?.status === "healthy"
                              ? "Connected"
                              : "Waiting on check"}
                    </div>
                    <div className="text-[12px] font-medium text-muted-foreground/90">{metric.hint}</div>
                  </div>
                </CardContent>
              </Card>
            );
          })
        )}
      </section>

      <div className="flex flex-wrap items-center gap-2 rounded-[20px] border border-border/78 bg-card/70 px-4 py-3 text-sm text-muted-foreground shadow-[0_18px_34px_-30px_rgba(16,36,71,0.12)]">
        <span className="font-medium text-foreground">Pipeline</span>
        <Badge variant="secondary">Upload</Badge>
        <Badge variant="secondary">Ask</Badge>
        <Badge variant="secondary">Review sources</Badge>
      </div>

      <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>Workspace</CardTitle>
              <p className="text-sm text-muted-foreground/95">Keep the latest documents and sessions close to the team.</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{session?.workspace.name ?? "Workspace"}</Badge>
              <Badge variant="secondary">@{session?.workspace.slug ?? "workspace"}</Badge>
              <Badge variant="secondary">{session?.role ?? "Member"}</Badge>
            </div>

            <div className="rounded-[22px] border border-border/82 bg-card/76">
              <div className="flex items-center justify-between gap-3 px-4 py-3">
                <div className="text-sm font-semibold text-foreground">Status</div>
                <Badge variant={failedCount > 0 ? "warning" : healthQuery.data?.status === "healthy" ? "success" : "secondary"}>
                  {failedCount > 0 ? `${failedCount} failed` : healthQuery.data?.status === "healthy" ? "Healthy" : "Checking"}
                </Badge>
              </div>
              <div className="divide-y divide-border/70">
                <div className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">Latest document</span>
                  <span className="truncate text-right text-foreground" title={latestDocument?.originalFileName}>
                    {latestDocument?.originalFileName ?? "None"}
                  </span>
                </div>
                <div className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">Latest conversation</span>
                  <span className="truncate text-right text-foreground" title={latestConversation?.title}>
                    {latestConversation?.title ?? "None"}
                  </span>
                </div>
                <div className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">Updated</span>
                  <span className="text-foreground">
                    {latestConversation ? formatRelativeDate(latestConversation.updatedAtUtc) : "No activity"}
                  </span>
                </div>
                <div className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                  <span className="text-muted-foreground">Processing</span>
                  <span className="text-foreground">
                    {processingCount === 0 ? "Clear" : `${processingCount} active`}
                  </span>
                </div>
              </div>
            </div>

            {documents.length === 0 ? (
              <div className="rounded-[20px] border border-dashed border-border/82 bg-card/60 px-4 py-3 text-sm text-muted-foreground">
                Upload your first document to turn this workspace into a searchable source of truth.
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-card/86">
          <CardHeader>
            <div className="space-y-1">
              <CardTitle>Next</CardTitle>
              <p className="text-sm text-muted-foreground/95">Move from raw files to grounded answers without losing momentum.</p>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[22px] border border-border/82 bg-card/76 p-3.5">
              <div className="flex flex-wrap gap-2">
                <Badge variant="secondary">{documents.length} docs</Badge>
                <Badge variant="secondary">{conversations.length} sessions</Badge>
                <Badge variant="secondary">{processedCount} ready</Badge>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">Upload fresh docs</Badge>
              <Badge variant="secondary">Ask specific questions</Badge>
              <Badge variant="secondary">Share cited answers</Badge>
            </div>
            <Button variant="outline" className="w-full justify-between" asChild>
              <Link to="/knowledge-base">Open documents</Link>
            </Button>
            <Button variant="outline" className="w-full justify-between" asChild>
              <Link to="/assistant">Ask assistant</Link>
            </Button>
            <Button variant="outline" className="w-full justify-between" asChild>
              <Link to="/conversations">Open conversations</Link>
            </Button>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
