import { Activity, ArrowUpRight, BookOpenText, Bot, DatabaseZap, MessagesSquare } from "lucide-react";
import { Link } from "react-router-dom";

import { EmptyState } from "@/components/feedback/empty-state";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Separator } from "@/components/ui/separator";
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
    { label: "Knowledge sources", value: String(documents.length), note: "Current workspace documents", icon: BookOpenText },
    { label: "Processed documents", value: String(processedCount), note: "Ready for grounded retrieval", icon: Bot },
    { label: "Stored sessions", value: String(conversations.length), note: "Saved assistant conversations", icon: MessagesSquare },
    {
      label: "API health",
      value: healthQuery.data?.status === "healthy" ? "Healthy" : "Checking",
      note: "Live backend status",
      icon: DatabaseZap,
    },
  ] as const;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Overview"
        title="A calmer view of the active workspace."
        description="Track knowledge readiness, assistant activity, and workspace health without unnecessary visual competition."
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

      <section className="grid gap-4 xl:grid-cols-[1.18fr_0.82fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardContent className="flex h-full flex-col gap-6 p-6 md:p-8">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{session?.workspace.name ?? "Workspace"}</Badge>
              <Badge variant="secondary">{session?.role ?? "Member"}</Badge>
            </div>
            <div className="max-w-2xl space-y-3">
              <h2 className="font-display text-3xl leading-tight tracking-[-0.05em] text-foreground md:text-[2.5rem]">
                The essentials for this workspace, kept close at hand.
              </h2>
              <p className="text-base leading-7 text-muted-foreground">
                Documents, grounded answers, and conversations stay scoped to the active workspace so the product surface can stay focused.
              </p>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="text-sm font-semibold text-foreground">Workspace context</div>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {session
                    ? `${session.workspace.name} is active as ${session.role}.`
                    : "The authenticated workspace is being restored."}
                </p>
              </div>
              <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="text-sm font-semibold text-foreground">Latest activity</div>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {latestConversation
                    ? `${latestConversation.title} updated ${formatRelativeDate(latestConversation.updatedAtUtc)}.`
                    : "No assistant conversations have been stored yet."}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Workspace snapshot</CardTitle>
            <CardDescription>The few signals worth checking first.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              ["API status", healthQuery.data?.status === "healthy" ? "Healthy" : "Checking", "Live backend health."],
              [
                "Documents processing",
                `${processingCount}`,
                processingCount === 1 ? "One document is still processing." : `${processingCount} documents are still processing.`,
              ],
              [
                "Stored conversations",
                `${conversations.length}`,
                conversations.length === 1 ? "One conversation is stored." : `${conversations.length} conversations are stored.`,
              ],
            ].map(([title, value, detail]) => (
              <div key={title} className="rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <div className="text-sm font-semibold text-foreground">{title}</div>
                    <p className="mt-1 text-sm leading-6 text-muted-foreground">{detail}</p>
                  </div>
                  <div className="text-sm font-semibold text-foreground">{value}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {documentsQuery.isLoading || healthQuery.isLoading || conversationsQuery.isLoading ? (
          <div className="md:col-span-2 xl:col-span-4">
            <LoadingState
              title="Loading product metrics"
              description="Gathering live dashboard signals from the current RootFlow API."
            />
          </div>
        ) : documentsQuery.isError || healthQuery.isError || conversationsQuery.isError ? (
          <div className="md:col-span-2 xl:col-span-4">
            <ErrorState
              title="Could not load dashboard metrics"
              description="The dashboard needs the health, document, and conversation endpoints available to display live product data."
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
                <CardContent className="space-y-3 p-6">
                  <div className="flex items-center justify-between">
                    <div className="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                      <Icon className="size-5" />
                    </div>
                    <Activity className="size-4 text-muted-foreground" />
                  </div>
                  <div className="space-y-1.5">
                    <div className="text-sm text-muted-foreground">{metric.label}</div>
                    <div className="font-display text-3xl tracking-[-0.05em] text-foreground">{metric.value}</div>
                  </div>
                  <p className="text-sm leading-6 text-muted-foreground">{metric.note}</p>
                </CardContent>
              </Card>
            );
          })
        )}
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Core workflow</CardTitle>
            <CardDescription>The product path remains intentionally simple.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              ["Enter a workspace", "Sign in to a JWT-backed workspace with the correct membership role."],
              ["Capture knowledge", "Upload documents into the active workspace from the Knowledge Base."],
              ["Ground the assistant", "Ask questions against workspace-scoped documents and stored conversation history."],
            ].map(([title, description], index) => (
              <div key={title} className="flex gap-4 rounded-[22px] border border-border/65 bg-card/72 p-4">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-secondary text-secondary-foreground">{index + 1}</div>
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-foreground">{title}</div>
                  <p className="text-sm leading-6 text-muted-foreground">{description}</p>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Live operating summary</CardTitle>
            <CardDescription>Current state from the active RootFlow API and workspace session.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
              <div className="text-sm font-semibold text-foreground">API status</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {healthQuery.data?.status === "healthy"
                  ? "The backend health endpoint is responding normally."
                  : "Waiting for the health endpoint response."}
              </p>
            </div>
            <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
              <div className="text-sm font-semibold text-foreground">Current workspace</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {session
                  ? `${session.workspace.name} is signed in as ${session.role}.`
                  : "The authenticated workspace is being restored."}
              </p>
            </div>
            <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
              <div className="text-sm font-semibold text-foreground">Documents still processing</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {processingCount} document{processingCount === 1 ? "" : "s"} currently reported as processing.
              </p>
            </div>
            <div className="rounded-[22px] border border-border/65 bg-card/72 p-4">
              <div className="text-sm font-semibold text-foreground">Latest stored conversation</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {latestConversation
                  ? `${latestConversation.title} was updated ${formatRelativeDate(latestConversation.updatedAtUtc)} and contains ${latestConversation.messageCount} messages.`
                  : "No assistant sessions have been stored by the backend yet."}
              </p>
              {latestConversation?.lastMessagePreview ? (
                <p className="mt-3 text-sm leading-6 text-muted-foreground">{latestConversation.lastMessagePreview}</p>
              ) : null}
            </div>
            {documents.length === 0 ? (
              <EmptyState
                icon={BookOpenText}
                title="No live documents yet"
                description="Upload documents first so the assistant can answer against real workspace knowledge."
              />
            ) : null}
            <Separator />
            <Button variant="outline" className="w-full justify-between" asChild>
              <Link to="/knowledge-base">
                Open live knowledge base
                <ArrowUpRight />
              </Link>
            </Button>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
