import { Activity, ArrowUpRight, BookOpenText, Bot, DatabaseZap, MessagesSquare } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Separator } from "@/components/ui/separator";

const metrics = [
  { label: "Knowledge sources", value: "24", note: "+3 imported this week", icon: BookOpenText },
  { label: "Answer success", value: "92%", note: "Grounded responses with citations", icon: Bot },
  { label: "Active sessions", value: "18", note: "Live conversations across teams", icon: MessagesSquare },
  { label: "Retrieval health", value: "Strong", note: "Signals aligned across ranking layers", icon: DatabaseZap },
] as const;

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Command Center"
        title="Build a knowledge assistant that feels enterprise-ready from day one."
        description="RootFlow is positioned as a premium AI workspace for grounded business answers, reusable knowledge operations, and client-facing demos that already look polished."
        actions={
          <>
            <Button>Open assistant</Button>
            <Button variant="outline">Review documents</Button>
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
                  ["Fast onboarding", "Clear product shell and route system"],
                  ["Trustworthy answers", "Citations and debug visibility ready for operators"],
                  ["Scalable foundation", "Feature folders, providers, and typed contracts"],
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
              ["API integration", "Next", "Pages are structured to bind to the current .NET endpoints."],
            ].map(([title, status, detail]) => (
              <div key={title} className="space-y-2 rounded-[22px] border border-border/70 bg-secondary/35 p-4">
                <div className="flex items-center justify-between">
                  <div className="text-sm font-semibold text-foreground">{title}</div>
                  <Badge variant={status === "Complete" ? "success" : status === "Next" ? "secondary" : "warning"}>
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
        {metrics.map((metric) => {
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
        })}
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <CardTitle>Product flow</CardTitle>
            <CardDescription>How the RootFlow experience is organized for business users.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              ["1. Capture knowledge", "Upload documents, policies, runbooks, or help-center content into a single workspace."],
              ["2. Ground the assistant", "Process chunks, evaluate retrieval quality, and make sources visible inside the answer flow."],
              ["3. Operate confidently", "Give teams a premium interface where they can trust the answer trail, not just the response."],
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
            <CardTitle>What this shell already solves</CardTitle>
            <CardDescription>High-signal foundations that make the product immediately presentable.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {[
              "Blue-first premium visual identity with intentional light and dark themes",
              "Separate frontend architecture with feature-driven organization",
              "Dashboard, knowledge, assistant, and conversations areas mapped from the start",
              "Future auth routes prepared without adding backend auth complexity yet",
            ].map((item, index) => (
              <div key={item} className="flex items-start gap-3">
                <div className="mt-1 flex size-6 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                  {index + 1}
                </div>
                <p className="text-sm leading-7 text-muted-foreground">{item}</p>
              </div>
            ))}
            <Separator />
            <Button variant="outline" className="w-full justify-between">
              View page architecture
              <ArrowUpRight />
            </Button>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
