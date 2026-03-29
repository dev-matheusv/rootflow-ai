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
import { useConversationsQuery, useHealthQuery, useDocumentsQuery } from "@/hooks/use-rootflow-data";
import { formatRelativeDate } from "@/lib/formatting/formatters";

export function DashboardPage() {
  const healthQuery = useHealthQuery();
  const documentsQuery = useDocumentsQuery({ autoRefreshProcessing: true });
  const conversationsQuery = useConversationsQuery();

  const documents = documentsQuery.data ?? [];
  const conversations = conversationsQuery.data ?? [];
  const processedCount = documents.filter((document) => document.status === 3).length;
  const processingCount = documents.filter((document) => document.status === 2).length;
  const latestConversation = conversations[0];

  const metrics = [
    { label: "Knowledge sources", value: String(documents.length), note: "Live count from /api/documents", icon: BookOpenText },
    { label: "Processed documents", value: String(processedCount), note: "Ready for grounded retrieval", icon: Bot },
    { label: "Stored sessions", value: String(conversations.length), note: "Live count from /api/conversations", icon: MessagesSquare },
    {
      label: "API health",
      value: healthQuery.data?.status === "healthy" ? "Healthy" : "Checking",
      note: "Derived from the RootFlow health endpoint",
      icon: DatabaseZap,
    },
  ] as const;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Command Center"
        title="Build a knowledge assistant that feels enterprise-ready from day one."
        description="RootFlow is positioned as a premium AI workspace for grounded business answers, reusable knowledge operations, and client-facing demos that already look polished."
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

      <section className="grid gap-4 xl:grid-cols-[1.4fr_0.9fr]">
        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(36,103,255,0.14),transparent_45%,rgba(129,207,255,0.18))]" />
            <div className="relative flex flex-col gap-8 p-6 md:p-8">
              <div className="flex flex-wrap items-center gap-2">
                <Badge>Premium SaaS shell</Badge>
                <Badge variant="secondary">Light + dark intentional</Badge>
                <Badge variant="secondary">Prepared for auth later</Badge>
              </div>
              <div className="max-w-2xl space-y-3">
                <h2 className="font-display text-3xl leading-tight tracking-[-0.05em] text-foreground md:text-[2.6rem]">
                  A calm blue interface designed for demos, daily use, and future commercialization.
                </h2>
                <p className="text-base leading-8 text-muted-foreground">
                  The frontend foundation emphasizes confidence, clarity, and visual trust. It is structured to support the current MVP flow now, and authentication, team features, and SaaS expansion later.
                </p>
              </div>
              <div className="grid gap-3 sm:grid-cols-3">
                {[
                  ["Fast onboarding", "The frontend shell and navigation are live and ready for demos."],
                  ["Trustworthy answers", "The assistant now talks to the real backend and returns live sources."],
                  ["Scalable foundation", "Feature folders, query hooks, and typed contracts support growth."],
                ].map(([title, detail]) => (
                  <div key={title} className="rounded-[24px] border border-border/70 bg-background/72 p-4 backdrop-blur-sm">
                    <div className="text-sm font-semibold text-foreground">{title}</div>
                    <p className="mt-2 text-sm leading-6 text-muted-foreground">{detail}</p>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Operational signal</CardTitle>
            <CardDescription>A focused summary for product demos and internal review.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            {[
              ["Frontend shell", "Complete", "The design system and app shell are live and consistent."],
              ["RAG quality", "Improving", "Retrieval grounding and evaluation are already in place."],
              ["API integration", "Live", "Documents, chat, and conversation history now connect to real endpoints."],
            ].map(([title, status, detail]) => (
              <div key={title} className="space-y-2 rounded-[22px] border border-border/70 bg-secondary/35 p-4">
                <div className="flex items-center justify-between">
                  <div className="text-sm font-semibold text-foreground">{title}</div>
                  <Badge variant={status === "Complete" || status === "Live" ? "success" : "warning"}>
                    {status}
                  </Badge>
                </div>
                <p className="text-sm leading-6 text-muted-foreground">{detail}</p>
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
              <Card key={metric.label}>
                <CardContent className="space-y-4 p-6">
                  <div className="flex items-center justify-between">
                    <div className="flex size-11 items-center justify-center rounded-2xl bg-primary/10 text-primary">
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

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Product flow</CardTitle>
            <CardDescription>How the RootFlow experience is organized for business users.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              ["1. Capture knowledge", "Upload real documents into the current workspace from the Knowledge Base page."],
              ["2. Ground the assistant", "Ask live questions and inspect the source-backed answer flow in the Assistant page."],
              ["3. Operate confidently", "Review stored session history in Conversations without losing the premium client-facing experience."],
            ].map(([title, description], index) => (
              <div key={title} className="flex gap-4 rounded-[24px] border border-border/70 bg-background/60 p-4">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-secondary text-secondary-foreground">{index + 1}</div>
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-foreground">{title}</div>
                  <p className="text-sm leading-6 text-muted-foreground">{description}</p>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Live operating summary</CardTitle>
            <CardDescription>Real signals derived from the current RootFlow API without local-only session shortcuts.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[24px] border border-border/70 bg-background/60 p-4">
              <div className="text-sm font-semibold text-foreground">API status</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {healthQuery.data?.status === "healthy"
                  ? "The backend health endpoint is responding normally."
                  : "Waiting for the health endpoint response."}
              </p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-background/60 p-4">
              <div className="text-sm font-semibold text-foreground">Documents still processing</div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {processingCount} document{processingCount === 1 ? "" : "s"} currently reported as processing.
              </p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-background/60 p-4">
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
                description="Upload documents first so the rest of the product can demonstrate grounded AI behavior with real data."
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
