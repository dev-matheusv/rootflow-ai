import { Bot, CornerDownLeft, MessageSquareQuote, Microscope, SendHorizonal } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Textarea } from "@/components/ui/textarea";

const sources = [
  {
    title: "employee-handbook-2026.pdf",
    section: "Remote Work Policy - Paragraph 2",
    excerpt: "Employees may work remotely up to three days per week with manager approval.",
  },
  {
    title: "support-escalation-policy.docx",
    section: "Enterprise Access - Paragraph 1",
    excerpt: "Only support leads can perform password resets for enterprise accounts.",
  },
] as const;

export function AssistantPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Assistant"
        title="Make the answer experience feel calm, precise, and trustworthy."
        description="The assistant surface should feel like the center of the product: comfortable for daily business use, elegant for demos, and explicit about where each answer comes from."
        actions={
          <>
            <Button>
              <Bot />
              New session
            </Button>
            <Button variant="outline">
              <Microscope />
              Review retrieval
            </Button>
          </>
        }
      />

      <section className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(32,110,255,0.12),transparent_38%,rgba(130,203,255,0.16))]" />
            <div className="relative flex flex-col gap-6 p-6 md:p-8">
              <div className="flex flex-wrap items-center gap-2">
                <Badge>Grounded chat</Badge>
                <Badge variant="secondary">Sources visible</Badge>
                <Badge variant="secondary">Debug-ready</Badge>
              </div>
              <div className="space-y-3">
                <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">A premium chat workspace built for confident business answers.</h2>
                <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">
                  Answer quality is only part of the product. The surrounding experience should make grounded responses feel believable, legible, and easy to inspect.
                </p>
              </div>

              <div className="grid gap-4">
                <div className="ml-auto max-w-[80%] rounded-[28px] rounded-br-md border border-primary/12 bg-primary px-5 py-4 text-sm leading-7 text-primary-foreground shadow-[0_18px_40px_-26px_rgba(21,91,255,0.72)]">
                  How many remote days are allowed each week for employees?
                </div>
                <div className="max-w-[86%] rounded-[28px] rounded-bl-md border border-border/70 bg-background/82 px-5 py-4 text-sm leading-7 text-foreground shadow-[0_20px_44px_-32px_rgba(12,39,84,0.4)]">
                  Employees may work remotely up to three days per week with manager approval. [1]
                </div>
              </div>

              <div className="rounded-[28px] border border-border/70 bg-background/78 p-4">
                <div className="mb-3 flex items-center justify-between">
                  <div className="text-sm font-semibold text-foreground">Ask a business question</div>
                  <div className="text-xs text-muted-foreground">Prepared for live API binding</div>
                </div>
                <Textarea
                  className="min-h-[120px] resize-none border-none bg-transparent px-0 py-0 shadow-none focus-visible:ring-0"
                  placeholder="What can I help your team answer today?"
                  readOnly
                  value=""
                />
                <div className="mt-4 flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <CornerDownLeft className="size-4" />
                    Compose with grounded context and citations
                  </div>
                  <Button>
                    <SendHorizonal />
                    Send
                  </Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Supporting sources</CardTitle>
            <CardDescription>The right-side panel should make answer provenance obvious at a glance.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {sources.map((source, index) => (
              <div key={source.title} className="rounded-[24px] border border-border/70 bg-secondary/30 p-4">
                <div className="flex items-center justify-between gap-3">
                  <Badge variant="secondary">[{index + 1}]</Badge>
                  <div className="text-xs uppercase tracking-[0.18em] text-muted-foreground">Source block</div>
                </div>
                <div className="mt-3 text-sm font-semibold text-foreground">{source.title}</div>
                <div className="mt-1 text-sm text-primary">{source.section}</div>
                <p className="mt-3 text-sm leading-7 text-muted-foreground">{source.excerpt}</p>
              </div>
            ))}

            <div className="rounded-[24px] border border-dashed border-border/80 bg-background/55 p-4">
              <div className="flex items-start gap-3">
                <div className="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <MessageSquareQuote className="size-[18px]" />
                </div>
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-foreground">Future debug drawer</div>
                  <p className="text-sm leading-6 text-muted-foreground">
                    Retrieval signals and ranking reasons can surface here for operators without cluttering the normal client view.
                  </p>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
