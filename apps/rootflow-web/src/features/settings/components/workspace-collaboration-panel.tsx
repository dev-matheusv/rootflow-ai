import { Clock3, MailPlus, ShieldCheck } from "lucide-react";
import { useState, type FormEvent } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/features/auth/auth-provider";
import { useInviteWorkspaceMemberMutation, useWorkspaceMembersQuery } from "@/hooks/use-rootflow-data";
import type { WorkspaceRole } from "@/lib/api/contracts";
import { ApiError } from "@/lib/api/client";
import { cn } from "@/lib/utils";

const inviteRoles: WorkspaceRole[] = ["Member", "Admin", "Owner"];

export function WorkspaceCollaborationPanel() {
  const { session } = useAuth();
  const workspaceId = session?.workspace.id;
  const currentRole = session?.role ?? "Member";
  const canInvite = currentRole === "Owner" || currentRole === "Admin";
  const membersQuery = useWorkspaceMembersQuery(workspaceId);
  const inviteMutation = useInviteWorkspaceMemberMutation(workspaceId);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<WorkspaceRole>("Member");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  async function handleInviteSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      const response = await inviteMutation.mutateAsync({
        email,
        role,
      });

      setSuccessMessage(response.message);
      setEmail("");
      setRole("Member");
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "We could not send the invite right now.");
    }
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-4 xl:grid-cols-[0.88fr_1.12fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Workspace context</CardTitle>
            <CardDescription>Access stays scoped to the active workspace and membership role.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[22px] border border-border/70 bg-card/72 p-4">
              <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
                <ShieldCheck className="size-4 text-primary" />
                Current workspace
              </div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                <span className="font-semibold text-foreground">{session?.workspace.name}</span> is active for your current JWT session.
              </p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <Badge variant="secondary">@{session?.workspace.slug}</Badge>
                <Badge variant="success">{currentRole}</Badge>
              </div>
            </div>

            <div className="rounded-[22px] border border-border/70 bg-card/72 p-4">
              <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
                <Clock3 className="size-4 text-primary" />
                Invite behavior
              </div>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Invite links are time-bound and single-use. When outbound email is configured, RootFlow sends them directly and preserves a safe local fallback in development.
              </p>
            </div>
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>Invite collaborators</CardTitle>
            <CardDescription>
              {canInvite
                ? "Send a collaborator invite into this workspace without changing the existing auth or session flow."
                : "Only owners and admins can invite new members into this workspace."}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {canInvite ? (
              <form className="space-y-5" onSubmit={handleInviteSubmit}>
                <div className="space-y-2">
                  <label className="text-sm font-semibold text-foreground" htmlFor="invite-email">
                    Work email
                  </label>
                  <Input
                    id="invite-email"
                    type="email"
                    autoComplete="email"
                    placeholder="teammate@company.com"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    disabled={inviteMutation.isPending}
                  />
                </div>

                <div className="space-y-3">
                  <div className="text-sm font-semibold text-foreground">Role</div>
                  <div className="flex flex-wrap gap-2">
                    {inviteRoles.map((inviteRole) => (
                      <button
                        key={inviteRole}
                        type="button"
                        className={cn(
                          "rounded-full border px-3 py-2 text-sm font-semibold transition-[border-color,background-color,color]",
                          role === inviteRole
                            ? "border-primary/30 bg-primary/10 text-primary"
                            : "border-border/75 bg-background/84 text-muted-foreground hover:border-primary/20 hover:text-foreground",
                        )}
                        onClick={() => setRole(inviteRole)}
                      >
                        {inviteRole}
                      </button>
                    ))}
                  </div>
                </div>

                {errorMessage ? (
                  <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                    {errorMessage}
                  </div>
                ) : null}

                {successMessage ? (
                  <div className="rounded-[22px] border border-primary/18 bg-primary/8 px-4 py-3 text-sm text-muted-foreground">
                    {successMessage}
                  </div>
                ) : null}

                <Button
                  type="submit"
                  className="w-full justify-between"
                  disabled={inviteMutation.isPending || email.trim().length === 0}
                >
                  {inviteMutation.isPending ? "Preparing invite..." : "Send invite"}
                  <MailPlus />
                </Button>
              </form>
            ) : (
              <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm leading-6 text-muted-foreground">
                Your current role is <span className="font-semibold text-foreground">{currentRole}</span>. You can still review the current
                workspace members below.
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card className="border-border/70 bg-background/72 shadow-none">
        <CardHeader>
          <CardTitle>Workspace members</CardTitle>
          <CardDescription>Everyone listed here can collaborate inside the current workspace boundary.</CardDescription>
        </CardHeader>
        <CardContent>
          {membersQuery.isLoading ? (
            <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm text-muted-foreground">
              Loading workspace members...
            </div>
          ) : membersQuery.error ? (
            <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-4 text-sm text-destructive">
              {membersQuery.error instanceof ApiError
                ? membersQuery.error.message
                : "We could not load workspace members right now."}
            </div>
          ) : membersQuery.data && membersQuery.data.length > 0 ? (
            <div className="space-y-3">
              {membersQuery.data.map((member) => (
                <div
                  key={member.userId}
                  className="flex flex-col gap-3 rounded-[22px] border border-border/70 bg-card/72 p-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate text-sm font-semibold text-foreground">{member.fullName}</span>
                      {member.isCurrentUser ? <Badge variant="secondary">You</Badge> : null}
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">{member.email}</div>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={member.role === "Owner" ? "default" : member.role === "Admin" ? "success" : "secondary"}>
                      {member.role}
                    </Badge>
                    <div className="text-xs text-muted-foreground">Joined {new Date(member.createdAtUtc).toLocaleDateString()}</div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm text-muted-foreground">
              No members are attached to this workspace yet.
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
