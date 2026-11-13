import { Clock3, MailPlus, ShieldCheck, UserMinus } from "lucide-react";
import { useState, type FormEvent } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/features/auth/auth-provider";
import {
  useChangeWorkspaceMemberRoleMutation,
  useInviteWorkspaceMemberMutation,
  useRemoveWorkspaceMemberMutation,
  useWorkspaceMembersQuery,
} from "@/hooks/use-rootflow-data";
import type { WorkspaceMember, WorkspaceRole } from "@/lib/api/contracts";
import { ApiError } from "@/lib/api/client";
import { cn } from "@/lib/utils";

// RF-11: the owner role is never granted by invite, and admins invite members only (enforced by the API too).
function getInviteRoles(currentRole: WorkspaceRole): WorkspaceRole[] {
  return currentRole === "Owner" ? ["Member", "Admin"] : ["Member"];
}

// Mirrors the API rules: nobody manages themselves or the owner; admins manage members only.
function canManageMember(currentRole: WorkspaceRole, member: WorkspaceMember): boolean {
  if (member.isCurrentUser || member.role === "Owner") return false;
  if (currentRole === "Owner") return true;
  return currentRole === "Admin" && member.role === "Member";
}

export function WorkspaceCollaborationPanel() {
  const { intlLocale, t } = useI18n();
  const { session } = useAuth();
  const workspaceId = session?.workspace.id;
  const currentRole = session?.role ?? "Member";
  const canInvite = currentRole === "Owner" || currentRole === "Admin";
  const membersQuery = useWorkspaceMembersQuery(workspaceId);
  const inviteMutation = useInviteWorkspaceMemberMutation(workspaceId);
  const changeRoleMutation = useChangeWorkspaceMemberRoleMutation(workspaceId);
  const removeMutation = useRemoveWorkspaceMemberMutation(workspaceId);
  const inviteRoles = getInviteRoles(currentRole);
  const [memberActionError, setMemberActionError] = useState<string | null>(null);
  const [memberActionNotice, setMemberActionNotice] = useState<string | null>(null);
  const [pendingMemberId, setPendingMemberId] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<WorkspaceRole>("Member");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const currentRoleLabel =
    currentRole === "Owner"
      ? t("common.labels.owner")
      : currentRole === "Admin"
        ? t("common.labels.admin")
        : t("common.labels.member");

  async function handleChangeRole(member: WorkspaceMember, nextRole: WorkspaceRole) {
    setMemberActionError(null);
    setMemberActionNotice(null);
    setPendingMemberId(member.userId);
    try {
      await changeRoleMutation.mutateAsync({ userId: member.userId, role: nextRole });
    } catch (error) {
      setMemberActionError(error instanceof ApiError ? error.message : t("settings.memberActionErrorFallback"));
    } finally {
      setPendingMemberId(null);
    }
  }

  async function handleRemove(member: WorkspaceMember) {
    if (!window.confirm(t("settings.removeMemberConfirm", { name: member.fullName }))) return;
    setMemberActionError(null);
    setMemberActionNotice(null);
    setPendingMemberId(member.userId);
    try {
      await removeMutation.mutateAsync(member.userId);
      setMemberActionNotice(t("settings.memberRemoved", { name: member.fullName }));
    } catch (error) {
      setMemberActionError(error instanceof ApiError ? error.message : t("settings.memberActionErrorFallback"));
    } finally {
      setPendingMemberId(null);
    }
  }

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
      setErrorMessage(error instanceof ApiError ? error.message : t("settings.inviteErrorFallback"));
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <Badge variant="secondary" className="min-w-0 truncate" title={session?.workspace.name}>
          {session?.workspace.name}
        </Badge>
        <Badge variant="secondary" className="min-w-0 max-w-full truncate" title={session?.workspace.slug}>
          @{session?.workspace.slug}
        </Badge>
        <Badge variant="success">{currentRoleLabel}</Badge>
        <Badge variant="secondary">
          <Clock3 className="size-3.5" />
          {t("common.helper.timedInvites")}
        </Badge>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <CardTitle>{t("settings.inviteTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {canInvite ? (
              <form className="space-y-5" onSubmit={handleInviteSubmit}>
                <div className="space-y-2">
                  <label className="text-sm font-semibold text-foreground" htmlFor="invite-email">
                    {t("common.labels.workEmail")}
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
                  <div className="text-sm font-semibold text-foreground">{t("common.labels.role")}</div>
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
                        {inviteRole === "Owner" ? t("common.labels.owner") : inviteRole === "Admin" ? t("common.labels.admin") : t("common.labels.member")}
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
                  {inviteMutation.isPending ? t("settings.preparingInvite") : t("common.actions.sendInvite")}
                  <MailPlus />
                </Button>
              </form>
            ) : (
              <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm text-muted-foreground">
                {t("settings.yourRoleReviewMembers", { role: currentRoleLabel })}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="border-border/70 bg-background/72 shadow-none">
          <CardHeader>
            <div className="flex items-center gap-2">
              <ShieldCheck className="size-4 text-primary" />
              <CardTitle>{t("settings.membersTitle")}</CardTitle>
            </div>
          </CardHeader>
          <CardContent>
            {membersQuery.isLoading ? (
              <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm text-muted-foreground">
                {t("settings.membersLoading")}
              </div>
            ) : membersQuery.error ? (
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-4 text-sm text-destructive">
                {membersQuery.error instanceof ApiError
                  ? membersQuery.error.message
                  : t("settings.membersErrorFallback")}
              </div>
            ) : membersQuery.data && membersQuery.data.length > 0 ? (
              <div className="space-y-3">
                {memberActionError ? (
                  <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                    {memberActionError}
                  </div>
                ) : null}
                {memberActionNotice ? (
                  <div className="rounded-[22px] border border-primary/18 bg-primary/8 px-4 py-3 text-sm text-muted-foreground">
                    {memberActionNotice}
                  </div>
                ) : null}
                {membersQuery.data.map((member) => (
                  <div
                    key={member.userId}
                    className="flex flex-col gap-3 rounded-[22px] border border-border/70 bg-card/72 p-4 sm:flex-row sm:items-center sm:justify-between"
                  >
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="truncate text-sm font-semibold text-foreground">{member.fullName}</span>
                        {member.isCurrentUser ? <Badge variant="secondary">{t("common.labels.you")}</Badge> : null}
                      </div>
                      <div className="mt-1 text-sm text-muted-foreground [overflow-wrap:anywhere]">{member.email}</div>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={member.role === "Owner" ? "default" : member.role === "Admin" ? "success" : "secondary"}>
                        {member.role === "Owner" ? t("common.labels.owner") : member.role === "Admin" ? t("common.labels.admin") : t("common.labels.member")}
                      </Badge>
                      <div className="text-xs text-muted-foreground">{t("common.labels.joined", { date: new Date(member.createdAtUtc).toLocaleDateString(intlLocale) })}</div>
                      {canManageMember(currentRole, member) ? (
                        <div className="flex flex-wrap items-center gap-2">
                          {currentRole === "Owner" ? (
                            <Button
                              type="button"
                              variant="outline"
                              size="sm"
                              disabled={pendingMemberId === member.userId}
                              onClick={() => handleChangeRole(member, member.role === "Admin" ? "Member" : "Admin")}
                            >
                              {member.role === "Admin" ? t("settings.makeMember") : t("settings.makeAdmin")}
                            </Button>
                          ) : null}
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            className="text-destructive hover:text-destructive"
                            disabled={pendingMemberId === member.userId}
                            onClick={() => handleRemove(member)}
                          >
                            <UserMinus />
                            {t("settings.removeMember")}
                          </Button>
                        </div>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="rounded-[22px] border border-border/70 bg-card/72 px-4 py-4 text-sm text-muted-foreground">
                {t("settings.noMembersYet")}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
