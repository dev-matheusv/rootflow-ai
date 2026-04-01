import { ArrowUpRight, BellDot, LockKeyhole, Search, SlidersHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { cn } from "@/lib/utils";

const settingsSections = [
  {
    id: "workspace",
    title: "Workspace defaults",
    description: "Review the foundations for workspace naming, answer preferences, and future tenant-wide behavior.",
    detail: "This page now sits on top of a real workspace identity instead of a demo-only shell.",
    icon: SlidersHorizontal,
  },
  {
    id: "notifications",
    title: "Notifications",
    description: "Reserve space for alerts, digest preferences, and future delivery channels.",
    detail: "The notification control in the topbar now lands here instead of pointing to a dead end.",
    icon: BellDot,
  },
  {
    id: "search",
    title: "Search and answer controls",
    description: "Future controls for retrieval tuning, citation preferences, and answer framing can live here.",
    detail: "The assistant already runs inside the workspace boundary, so search controls can evolve safely from here.",
    icon: Search,
  },
  {
    id: "access",
    title: "Access and authentication",
    description: "JWT auth, memberships, and tenant context are live; invites and billing can layer on later.",
    detail: "Owner-scoped workspace access is active now, with room for admin/member workflows in later phases.",
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
        description="This surface stays intentionally focused: the routes are real, the entry points are consistent, and deeper controls can layer in without redesigning the shell."
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

      <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <Card>
          <CardHeader>
            <CardTitle>Available settings areas</CardTitle>
            <CardDescription>Each entry already has a live route so nothing in the interface feels misleading.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {settingsSections.map((section) => {
              const Icon = section.icon;
              const isActive = activeSection === section.id;

              return (
                <Link
                  key={section.id}
                  to={`/settings?section=${section.id}`}
                  className={cn(
                    "flex items-start gap-3 rounded-[24px] border p-4 transition-[border-color,background-color,transform,box-shadow] duration-200 hover:-translate-y-[1px]",
                    isActive
                      ? "border-primary/18 bg-primary/[0.07] shadow-[0_18px_34px_-30px_rgba(18,72,166,0.22)]"
                      : "border-border/75 bg-background/72 hover:border-primary/14 hover:bg-secondary/44",
                  )}
                >
                  <div className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <Icon className="size-5" />
                  </div>
                  <div className="min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-foreground">{section.title}</span>
                      {isActive ? <Badge>Current</Badge> : null}
                    </div>
                    <p className="text-sm leading-6 text-muted-foreground">{section.description}</p>
                  </div>
                </Link>
              );
            })}
          </CardContent>
        </Card>

        <Card className="overflow-hidden">
          <CardContent className="relative p-0">
            <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(37,99,235,0.08),transparent_46%,rgba(191,219,254,0.18))] dark:bg-[linear-gradient(135deg,rgba(138,180,255,0.12),transparent_46%,rgba(58,78,112,0.18))]" />
            <div className="relative flex flex-col gap-6 p-6 md:p-8">
              <div className="space-y-6">
                <div className="flex items-start gap-4">
                  <div className="flex size-14 shrink-0 items-center justify-center rounded-[26px] bg-primary/10 text-primary">
                    <SelectedIcon className="size-6" />
                  </div>
                  <div className="space-y-2">
                    <Badge>{selectedSection.title}</Badge>
                    <h2 className="font-display text-3xl tracking-[-0.05em] text-foreground">{selectedSection.title}</h2>
                    <p className="max-w-2xl text-sm leading-7 text-muted-foreground sm:text-base">{selectedSection.detail}</p>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-[24px] border border-border/75 bg-background/74 p-5">
                    <div className="text-sm font-semibold text-foreground">Why this exists now</div>
                    <p className="mt-2 text-sm leading-6 text-muted-foreground">
                      Users now arrive here from an authenticated workspace instead of a placeholder-only shell.
                    </p>
                  </div>
                  <div className="rounded-[24px] border border-border/75 bg-background/74 p-5">
                    <div className="text-sm font-semibold text-foreground">Future implementation path</div>
                    <p className="mt-2 text-sm leading-6 text-muted-foreground">
                      Backend-connected forms, preference persistence, and account-level controls can be added here without redesigning the shell.
                    </p>
                  </div>
                </div>

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
              </div>
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
