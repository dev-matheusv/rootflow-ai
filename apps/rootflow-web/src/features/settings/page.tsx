import { ArrowUpRight, BellDot, LockKeyhole, Search, SlidersHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { WorkspaceCollaborationPanel } from "@/features/settings/components/workspace-collaboration-panel";
import { cn } from "@/lib/utils";

const settingsSections = [
  {
    id: "workspace",
    title: "Workspace",
    icon: SlidersHorizontal,
    upcoming: ["Name", "Slug", "Defaults"],
  },
  {
    id: "notifications",
    title: "Notifications",
    icon: BellDot,
    upcoming: ["Email", "Digest", "Mentions"],
  },
  {
    id: "search",
    title: "Search",
    icon: Search,
    upcoming: ["Retrieval", "Citations", "Answer style"],
  },
  {
    id: "access",
    title: "Access",
    icon: LockKeyhole,
    upcoming: ["Members", "Invites", "Roles"],
  },
] as const;

export function SettingsPage() {
  const [searchParams] = useSearchParams();
  const selectedSection =
    settingsSections.find((section) => section.id === searchParams.get("section")) ?? settingsSections[0];
  const activeSection = selectedSection.id;
  const SelectedIcon = selectedSection.icon;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Settings"
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

      <section className="grid gap-3 xl:grid-cols-[300px_minmax(0,1fr)]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Sections</CardTitle>
              <Badge variant="secondary">{settingsSections.length}</Badge>
            </div>
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
                    "flex items-center gap-3 rounded-[20px] border p-3.5 transition-[border-color,background-color] duration-200",
                    isActive
                      ? "border-primary/18 bg-primary/[0.06]"
                      : "border-border/70 bg-card/70 hover:border-primary/14 hover:bg-card/88",
                  )}
                >
                  <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <Icon className="size-5" />
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-foreground">{section.title}</span>
                      {isActive ? <Badge variant="secondary">Current</Badge> : null}
                    </div>
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
              <div className="space-y-1">
                <Badge variant="secondary">{selectedSection.title}</Badge>
                <h2 className="font-display text-[1.55rem] tracking-[-0.045em] text-foreground">{selectedSection.title}</h2>
              </div>
            </div>

            {activeSection === "access" ? (
              <WorkspaceCollaborationPanel />
            ) : (
              <div className="space-y-4">
                <div className="rounded-[22px] border border-border/60 bg-background/54 p-4">
                  <div className="text-sm font-semibold text-foreground">Coming next</div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {selectedSection.upcoming.map((item) => (
                      <Badge key={item} variant="secondary">
                        {item}
                      </Badge>
                    ))}
                  </div>
                </div>
                <div className="rounded-[22px] border border-dashed border-border/65 bg-background/40 p-4 text-sm text-muted-foreground">
                  Controls for {selectedSection.title.toLowerCase()} will appear here.
                </div>
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
