import { BookOpenText, Bot, DatabaseZap, MessagesSquare } from "lucide-react";
import { Link } from "react-router-dom";

import { EmptyState } from "@/components/feedback/empty-state";
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
  const latestConversation = conversations[0];

  const metrics = [
    { label: "Documents", value: String(documents.length), note: "Total", icon: BookOpenText },
    { label: "Ready", value: String(processedCount), note: "Processed", icon: Bot },
    { label: "Conversations", value: String(conversations.length), note: "Saved", icon: MessagesSquare },
    {
      label: "API",
      value: healthQuery.data?.status === "healthy" ? "Healthy" : "Checking",
      note: "Status",
      icon: DatabaseZap,
    },
  ] as const;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Overview"
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

            return (
              <Card key={metric.label} className="border-border/70 bg-background/72 shadow-none">
                <CardContent className="space-y-2.5 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <div className="flex size-9 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                      <Icon className="size-5" />
                    </div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{metric.note}</div>
                  </div>
                  <div className="space-y-1.5">
                    <div className="text-sm text-muted-foreground">{metric.label}</div>
                    <div className="font-display text-[1.75rem] tracking-[-0.045em] text-foreground">{metric.value}</div>
                  </div>
                </CardContent>
              </Card>
            );
          })
        )}
      </section>

      <section className="grid gap-3 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Workspace</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{session?.workspace.name ?? "Workspace"}</Badge>
              <Badge variant="secondary">@{session?.workspace.slug ?? "workspace"}</Badge>
              <Badge variant="secondary">{session?.role ?? "Member"}</Badge>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="text-sm font-semibold text-foreground">Latest conversation</div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {latestConversation
                    ? `${latestConversation.title} updated ${formatRelativeDate(latestConversation.updatedAtUtc)}`
                    : "No conversations yet"}
                </p>
              </div>
              <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="text-sm font-semibold text-foreground">Processing</div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {processingCount === 0
                    ? "No documents in progress"
                    : `${processingCount} document${processingCount === 1 ? "" : "s"} in progress`}
                </p>
              </div>
            </div>

            {documents.length === 0 ? (
              <EmptyState
                icon={BookOpenText}
                title="No documents"
                description="Upload a document to start retrieval."
              />
            ) : null}
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Next</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
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
