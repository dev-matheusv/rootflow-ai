import { Clock3, MessageCircleMore, Pin, Sparkles } from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";

const sessions = [
  { title: "Remote work policy", summary: "Policy clarification with citations", time: "2m ago", active: true },
  { title: "Billing cancellation notice", summary: "Answered with finance source blocks", time: "19m ago", active: false },
  { title: "Enterprise password reset", summary: "Operational runbook walkthrough", time: "Yesterday", active: false },
] as const;

const transcript = [
  { role: "User", content: "How do support leads reset an enterprise customer password?" },
  {
    role: "Assistant",
    content:
      "Open the Admin Portal, go to Users, select the customer record, then click Reset Password and confirm the action. Only support leads can perform this for enterprise accounts. [1]",
  },
] as const;

export function ConversationsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Conversations"
        title="Keep every answer trail readable and easy to revisit."
        description="A serious product needs session history that feels polished for teams, not a raw log viewer. This area should support review, trust, and handoff."
      />

      <section className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <Card>
          <CardHeader>
            <CardTitle>Recent sessions</CardTitle>
            <CardDescription>Polished conversation navigation for business users.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {sessions.map((session) => (
              <div
                key={session.title}
                className={`rounded-[24px] border p-4 transition-colors ${
                  session.active ? "border-primary/18 bg-primary/8" : "border-border/70 bg-background/60 hover:bg-secondary/35"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-foreground">{session.title}</div>
                    <p className="text-sm leading-6 text-muted-foreground">{session.summary}</p>
                  </div>
                  {session.active ? <Pin className="size-4 text-primary" /> : null}
                </div>
                <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                  <Clock3 className="size-3.5" />
                  {session.time}
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Conversation detail</CardTitle>
                <CardDescription>Structured to show both the answer and the decision trail behind it.</CardDescription>
              </div>
              <Badge>
                <Sparkles className="size-3.5" />
                Source-backed
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {transcript.map((message) => (
              <div key={`${message.role}-${message.content}`} className="flex gap-4 rounded-[28px] border border-border/70 bg-background/60 p-4">
                <Avatar className="size-11">
                  <AvatarFallback>{message.role === "User" ? "U" : "AI"}</AvatarFallback>
                </Avatar>
                <div className="space-y-2">
                  <div className="flex items-center gap-2">
                    <div className="text-sm font-semibold text-foreground">{message.role}</div>
                    <Badge variant={message.role === "Assistant" ? "default" : "secondary"}>
                      {message.role === "Assistant" ? "Grounded" : "Question"}
                    </Badge>
                  </div>
                  <p className="text-sm leading-7 text-muted-foreground">{message.content}</p>
                </div>
              </div>
            ))}

            <div className="rounded-[24px] border border-dashed border-border/80 bg-secondary/25 p-4">
              <div className="flex items-start gap-3">
                <div className="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                  <MessageCircleMore className="size-[18px]" />
                </div>
                <div className="space-y-1">
                  <div className="text-sm font-semibold text-foreground">Ready for live history integration</div>
                  <p className="text-sm leading-6 text-muted-foreground">
                    This page maps naturally to the existing conversation endpoint and can later support filters, favorites, and shared review.
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
