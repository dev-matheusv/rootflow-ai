import { ArrowRight, CheckCircle2, LogIn, MailPlus, ShieldCheck, UserRoundPlus } from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/features/auth/auth-provider";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";

export function AcceptInvitePage() {
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token")?.trim() ?? "";
  const redirectTo = `${location.pathname}${location.search}${location.hash}`;

  return <AcceptInviteContent token={token} redirectTo={redirectTo} />;
}

interface AcceptInviteContentProps {
  token: string;
  redirectTo: string;
}

function AcceptInviteContent({ token, redirectTo }: AcceptInviteContentProps) {
  const { applyAuthResponse, isAuthenticated, logout, session, status } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [acceptedWorkspaceName, setAcceptedWorkspaceName] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isMissingToken = token.length === 0;
  const authRedirect = useMemo(() => encodeURIComponent(redirectTo), [redirectTo]);
  const requiresDifferentAccount = Boolean(errorMessage && /different email address/i.test(errorMessage));

  async function handleAcceptInvite() {
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const response = await rootflowApi.acceptWorkspaceInvite({ token });
      applyAuthResponse(response);
      setAcceptedWorkspaceName(response.session.workspace.name);
      setSuccessMessage(`You now have access to ${response.session.workspace.name}.`);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "We could not accept this invite right now.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <MailPlus className="size-3.5" />
          Workspace invite
        </>
      }
      title="Join a shared RootFlow workspace."
      description="Accept the invite through the existing RootFlow auth flow, then switch directly into the invited workspace with a fresh workspace-scoped session."
      highlights={[
        {
          title: "Single-use secure invites",
          description: "Invite links expire and only work once, so workspace access stays explicit instead of being inferred from email alone.",
        },
        {
          title: "Same auth foundation",
          description: "Invite acceptance reuses the existing JWT session model instead of bolting on a second collaboration path.",
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            Workspace access
          </Badge>
          <CardTitle>Accept invite</CardTitle>
          <CardDescription>Continue with the account that matches the invited email address.</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {status === "loading" ? (
            <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
              Restoring your current session before RootFlow processes the invite.
            </div>
          ) : successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">Workspace joined</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <Button className="w-full justify-between" asChild>
                <Link to="/dashboard">
                  Open {acceptedWorkspaceName ?? "workspace"}
                  <ArrowRight />
                </Link>
              </Button>
            </div>
          ) : isMissingToken ? (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                This invite link is incomplete. Ask your workspace admin to send a fresh invitation.
              </div>
              <Button variant="outline" className="w-full" asChild>
                <Link to="/auth/login">Back to login</Link>
              </Button>
            </div>
          ) : !isAuthenticated ? (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
                Sign in first so RootFlow can attach this invite to the correct account and switch you into the invited workspace.
              </div>
              <div className="flex flex-col gap-3">
                <Button className="w-full justify-between" asChild>
                  <Link to={`/auth/login?redirect=${authRedirect}`}>
                    Sign in to continue
                    <LogIn />
                  </Link>
                </Button>
                <Button variant="outline" className="w-full justify-between" asChild>
                  <Link to={`/auth/signup?redirect=${authRedirect}`}>
                    Create account first
                    <UserRoundPlus />
                  </Link>
                </Button>
              </div>
            </div>
          ) : (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-4">
                <div className="text-sm font-semibold text-foreground">{session?.user.fullName}</div>
                <p className="mt-1 text-sm leading-6 text-muted-foreground">
                  RootFlow will use <span className="font-medium text-foreground">{session?.user.email}</span> to accept this invite.
                </p>
              </div>

              {errorMessage ? (
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {errorMessage}
                </div>
              ) : null}

              <div className="flex flex-col gap-3">
                <Button className="w-full justify-between" onClick={handleAcceptInvite} disabled={isSubmitting}>
                  {isSubmitting ? "Joining workspace..." : "Accept invite"}
                  <ArrowRight />
                </Button>
                {requiresDifferentAccount ? (
                  <Button variant="outline" className="w-full justify-between" onClick={logout}>
                    Sign out and try another account
                    <LogIn />
                  </Button>
                ) : (
                  <Button variant="outline" className="w-full" asChild>
                    <Link to="/dashboard">Cancel</Link>
                  </Button>
                )}
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </AuthScaffold>
  );
}
