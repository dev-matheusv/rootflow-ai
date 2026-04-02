import { ArrowUpRight, BellDot, LockKeyhole, Search, SlidersHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { WorkspaceCollaborationPanel } from "@/features/settings/components/workspace-collaboration-panel";
import { cn } from "@/lib/utils";

const settingsSections = [
  {
    id: "workspace",
    title: "Workspace defaults",
    description: "Foundations for workspace naming and future tenant-wide preferences.",
    detail: "This area now sits on top of a real workspace identity instead of a demo shell.",
    icon: SlidersHorizontal,
  },
  {
    id: "notifications",
    title: "Notifications",
    description: "Space for alerts, digests, and future delivery controls.",
    detail: "The topbar notification action now lands on a real settings surface.",
    icon: BellDot,
  },
  {
    id: "search",
    title: "Search and answer controls",
    description: "Future retrieval tuning and answer framing controls.",
    detail: "Search settings can evolve here without breaking workspace boundaries.",
    icon: Search,
  },
  {
    id: "access",
    title: "Access and authentication",
    description: "Workspace invites and memberships live on the same scoped session model.",
    detail: "Invited collaborators can accept access and move directly into the right workspace context.",
    icon: LockKeyhole,
  },
] as const;

export function SettingsPage() {
  const [searchParams] = useSearchParams();
  const selectedSection =
    settingsSections.find((section) => section.id === searchParams.get("section")) ?? settingsSections[0];
  const activeSection = selectedSection.id;
  const SelectedIcon = selectedSection.icon;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Settings"
        title="Keep workspace controls simple, clear, and ready to grow."
        description="The routes are real, the entry points are consistent, and deeper controls can layer in without redesigning the shell."
        actions={
          <>
            <Button asChild>
              <Link to="/assistant">Open assistant</Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to="/dashboard">Back to dashboard</Link>
            </Button>
          </>
        }
      />

      <section className="grid gap-4 xl:grid-cols-[320px_minmax(0,1fr)]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Areas</CardTitle>
            <CardDescription>Each section already has a live route.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {settingsSections.map((section) => {
              const Icon = section.icon;
              const isActive = activeSection === section.id;

              return (
                <Link
                  key={section.id}
                  to={`/settings?section=${section.id}`}
                  className={cn(
                    "flex items-start gap-3 rounded-[22px] border p-4 transition-[border-color,background-color] duration-200",
                    isActive
                      ? "border-primary/18 bg-primary/[0.06]"
                      : "border-border/70 bg-card/70 hover:border-primary/14 hover:bg-card/88",
                  )}
                >
                  <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <Icon className="size-5" />
                  </div>
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-foreground">{section.title}</span>
                      {isActive ? <Badge variant="secondary">Current</Badge> : null}
                    </div>
                    <p className="text-sm leading-6 text-muted-foreground">{section.description}</p>
                  </div>
                </Link>
              );
            })}
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardContent className="flex flex-col gap-6 p-6 md:p-8">
            <div className="flex items-start gap-4">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-[22px] bg-primary/10 text-primary">
                <SelectedIcon className="size-5" />
              </div>
              <div className="space-y-2">
                <Badge variant="secondary">{selectedSection.title}</Badge>
                <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">{selectedSection.title}</h2>
                <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">{selectedSection.detail}</p>
              </div>
            </div>

            {activeSection === "access" ? (
              <WorkspaceCollaborationPanel />
            ) : (
              <div className="rounded-[24px] border border-border/70 bg-card/72 p-5">
                <div className="text-sm font-semibold text-foreground">Planned here next</div>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  Backend-connected forms, preference persistence, and account-level controls can be added here without changing the shell structure again.
                </p>
              </div>
            )}

            <div className="flex flex-col gap-3 sm:flex-row">
              <Button asChild>
                <Link to="/assistant">
                  Continue in assistant
                  <ArrowUpRight />
                </Link>
              </Button>
              <Button variant="outline" asChild>
                <Link to="/dashboard">Back to dashboard</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
