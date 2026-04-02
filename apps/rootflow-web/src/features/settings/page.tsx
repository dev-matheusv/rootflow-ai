import { BellDot, LockKeyhole, Search, SlidersHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/features/auth/auth-provider";
import { WorkspaceCollaborationPanel } from "@/features/settings/components/workspace-collaboration-panel";
import { cn } from "@/lib/utils";

const settingsSections = [
  {
    id: "workspace",
    title: "Workspace",
    description: "Identity and defaults",
    icon: SlidersHorizontal,
    upcoming: ["Name", "Slug", "Defaults"],
  },
  {
    id: "notifications",
    title: "Notifications",
    description: "Email and mention routing",
    icon: BellDot,
    upcoming: ["Email", "Digest", "Mentions"],
  },
  {
    id: "search",
    title: "Search",
    description: "Retrieval and answer behavior",
    icon: Search,
    upcoming: ["Retrieval", "Citations", "Answer style"],
  },
  {
    id: "access",
    title: "Access",
    description: "Members, invites, roles",
    icon: LockKeyhole,
    upcoming: ["Members", "Invites", "Roles"],
  },
] as const;

export function SettingsPage() {
  const { session } = useAuth();
  const [searchParams] = useSearchParams();
  const selectedSection =
    settingsSections.find((section) => section.id === searchParams.get("section")) ?? settingsSections[0];
  const activeSection = selectedSection.id;
  const SelectedIcon = selectedSection.icon;
  const sectionSignals =
    activeSection === "workspace"
      ? [
          { label: "Name", value: session?.workspace.name ?? "Workspace" },
          { label: "Handle", value: `@${session?.workspace.slug ?? "workspace"}` },
          { label: "Role", value: session?.role ?? "Member" },
        ]
      : activeSection === "notifications"
        ? [
            { label: "Invites", value: "Email" },
            { label: "Digests", value: "Soon" },
            { label: "Mentions", value: "Soon" },
          ]
        : [
            { label: "Grounding", value: "Active" },
            { label: "Sources", value: "Available" },
            { label: "Tuning", value: "Later" },
          ];

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
        <Card className="border-border/80 bg-card/86">
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
                    "flex items-start gap-3 rounded-[20px] border p-3.5 transition-[border-color,background-color] duration-200",
                    isActive
                      ? "border-primary/24 bg-primary/[0.08]"
                      : "border-border/75 bg-card/74 hover:border-primary/18 hover:bg-card/92",
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
                    <div className="mt-1 text-xs text-muted-foreground">{section.description}</div>
                  </div>
                </Link>
              );
            })}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-card/86">
          <CardContent className="flex flex-col gap-6 p-6 md:p-8">
            <div className="flex items-start gap-4">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-[22px] bg-primary/10 text-primary">
                <SelectedIcon className="size-5" />
              </div>
              <div className="space-y-1">
                <Badge variant="secondary">{selectedSection.title}</Badge>
                <h2 className="font-display text-[1.35rem] tracking-[-0.045em] text-foreground">{selectedSection.title}</h2>
                <p className="text-sm text-muted-foreground">{selectedSection.description}</p>
              </div>
            </div>

            {activeSection === "access" ? (
              <WorkspaceCollaborationPanel />
            ) : (
              <div className="space-y-4">
                <div className="rounded-[22px] border border-border/75 bg-card/72 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="text-sm font-semibold text-foreground">Current view</div>
                    <Badge variant="secondary">Staged</Badge>
                  </div>
                  <div className="mt-3 grid gap-2 sm:grid-cols-3">
                    {sectionSignals.map((item) => (
                      <div key={item.label} className="rounded-[18px] border border-border/75 bg-background/84 px-3 py-2.5">
                        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{item.label}</div>
                        <div className="mt-1 truncate text-sm font-medium text-foreground">{item.value}</div>
                      </div>
                    ))}
                  </div>
                </div>
                <div className="rounded-[22px] border border-border/75 bg-card/72 p-4">
                  <div className="text-sm font-semibold text-foreground">Next</div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {selectedSection.upcoming.map((item) => (
                      <Badge key={item} variant="secondary">
                        {item}
                      </Badge>
                    ))}
                  </div>
                  <p className="mt-3 text-sm text-muted-foreground">
                    Controls for {selectedSection.title.toLowerCase()} will land here next.
                  </p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
