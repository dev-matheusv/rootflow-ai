import { ArrowRight, CheckCircle2, KeyRound, LogIn, MailPlus, ShieldCheck, UserRoundPlus } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";

import { useI18n } from "@/app/providers/i18n-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuth } from "@/features/auth/auth-provider";
import { AuthScaffold } from "@/features/auth/components/auth-scaffold";
import { ApiError } from "@/lib/api/client";
import { rootflowApi } from "@/lib/api/rootflow-api";
import type { InviteLookupResult } from "@/lib/api/contracts";

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
  const { t } = useI18n();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [acceptedWorkspaceName, setAcceptedWorkspaceName] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [inviteLookup, setInviteLookup] = useState<InviteLookupResult | null>(null);
  const [isLookingUp, setIsLookingUp] = useState(false);
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const isMissingToken = token.length === 0;
  const authRedirect = useMemo(() => encodeURIComponent(redirectTo), [redirectTo]);
  const requiresDifferentAccount = Boolean(errorMessage && /different email address/i.test(errorMessage));

  useEffect(() => {
    if (isMissingToken || isAuthenticated) return;

    setIsLookingUp(true);
    rootflowApi
      .lookupInvite(token)
      .then(setInviteLookup)
      .catch(() => setInviteLookup(null))
      .finally(() => setIsLookingUp(false));
  }, [token, isMissingToken, isAuthenticated]);

  async function handleAcceptInvite() {
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const response = await rootflowApi.acceptWorkspaceInvite({ token });
      applyAuthResponse(response);
      setAcceptedWorkspaceName(response.session.workspace.name);
      setSuccessMessage(t("auth.acceptInvite.successMessage", { workspace: response.session.workspace.name }));
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("auth.acceptInvite.fallbackError"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSignupViaInvite(e: React.FormEvent) {
    e.preventDefault();
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const response = await rootflowApi.signupViaInvite({ fullName, password, token });
      applyAuthResponse(response);
      setAcceptedWorkspaceName(response.session.workspace.name);
      setSuccessMessage(t("auth.acceptInvite.successMessage", { workspace: response.session.workspace.name }));
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("auth.acceptInvite.fallbackError"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthScaffold
      badge={
        <>
          <MailPlus className="size-3.5" />
          {t("auth.acceptInvite.badge")}
        </>
      }
      title={t("auth.acceptInvite.title")}
      description={t("auth.acceptInvite.description")}
      highlights={[
        {
          title: t("auth.acceptInvite.highlightOneTitle"),
          description: t("auth.acceptInvite.highlightOneDescription"),
        },
        {
          title: t("auth.acceptInvite.highlightTwoTitle"),
          description: t("auth.acceptInvite.highlightTwoDescription"),
        },
      ]}
    >
      <Card className="border-border/0 bg-transparent shadow-none">
        <CardHeader className="px-0 pt-0">
          <Badge className="w-fit">
            <ShieldCheck className="size-3.5" />
            {t("common.labels.workspaceAccess")}
          </Badge>
          <CardTitle>{t("auth.acceptInvite.cardTitle")}</CardTitle>
          <CardDescription>{t("auth.acceptInvite.cardDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="px-0 pb-0">
          {status === "loading" ? (
            <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
              {t("auth.acceptInvite.restoringSession")}
            </div>
          ) : successMessage ? (
            <div className="space-y-5">
              <div className="rounded-[26px] border border-primary/18 bg-primary/8 px-5 py-4">
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="mt-0.5 size-5 text-primary" />
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">{t("auth.acceptInvite.workspaceJoined")}</p>
                    <p className="text-sm leading-6 text-muted-foreground">{successMessage}</p>
                  </div>
                </div>
              </div>

              <Button className="w-full justify-between" asChild>
                <Link to="/dashboard">
                  {t("auth.acceptInvite.openWorkspace", { workspace: acceptedWorkspaceName ?? t("common.labels.workspace").toLowerCase() })}
                  <ArrowRight />
                </Link>
              </Button>
            </div>
          ) : isMissingToken ? (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                {t("auth.acceptInvite.incompleteLink")}
              </div>
              <Button variant="outline" className="w-full" asChild>
                <Link to="/auth/login">{t("common.actions.backToLogin")}</Link>
              </Button>
            </div>
          ) : !isAuthenticated ? (
            isLookingUp ? (
              <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
                Verificando convite...
              </div>
            ) : inviteLookup && !inviteLookup.isValid ? (
              <div className="space-y-5">
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {inviteLookup.errorMessage ?? t("auth.acceptInvite.incompleteLink")}
                </div>
                <Button variant="outline" className="w-full" asChild>
                  <Link to="/auth/login">{t("common.actions.backToLogin")}</Link>
                </Button>
              </div>
            ) : inviteLookup?.isExistingUser ? (
              <div className="space-y-5">
                <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
                  O convite é para <strong>{inviteLookup.email}</strong>. Entre com essa conta para aceitar.
                </div>
                <Button className="w-full justify-between" asChild>
                  <Link to={`/auth/login?redirect=${authRedirect}`}>
                    {t("common.actions.signInToContinue")}
                    <LogIn />
                  </Link>
                </Button>
              </div>
            ) : inviteLookup && !inviteLookup.isExistingUser ? (
              <form onSubmit={handleSignupViaInvite} className="space-y-5">
                {inviteLookup.workspaceName && (
                  <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
                    Você foi convidado para <strong>{inviteLookup.workspaceName}</strong> por <strong>{inviteLookup.inviterName}</strong>. Crie sua senha para entrar.
                  </div>
                )}
                <div className="space-y-2">
                  <Label htmlFor="fullName">Nome completo</Label>
                  <Input
                    id="fullName"
                    type="text"
                    placeholder="Seu nome"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    required
                    disabled={isSubmitting}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="password">Senha</Label>
                  <Input
                    id="password"
                    type="password"
                    placeholder="Mínimo 8 caracteres"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    disabled={isSubmitting}
                  />
                </div>
                {errorMessage && (
                  <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                    {errorMessage}
                  </div>
                )}
                <Button type="submit" className="w-full justify-between" disabled={isSubmitting}>
                  {isSubmitting ? "Criando conta..." : "Criar conta e entrar"}
                  <UserRoundPlus />
                </Button>
              </form>
            ) : (
              <div className="space-y-5">
                <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-3 text-sm text-muted-foreground">
                  {t("auth.acceptInvite.signInFirst")}
                </div>
                <div className="flex flex-col gap-3">
                  <Button className="w-full justify-between" asChild>
                    <Link to={`/auth/login?redirect=${authRedirect}`}>
                      {t("common.actions.signInToContinue")}
                      <LogIn />
                    </Link>
                  </Button>
                </div>
              </div>
            )
          ) : (
            <div className="space-y-5">
              <div className="rounded-[22px] border border-border/75 bg-background/80 px-4 py-4">
                <div className="text-sm font-semibold text-foreground">{session?.user.fullName}</div>
                <p className="mt-1 text-sm leading-6 text-muted-foreground">
                  {t("auth.acceptInvite.useEmail", { email: session?.user.email ?? "" })}
                </p>
              </div>

              {errorMessage ? (
                <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
                  {errorMessage}
                </div>
              ) : null}

              <div className="flex flex-col gap-3">
                <Button className="w-full justify-between" onClick={handleAcceptInvite} disabled={isSubmitting}>
                  {isSubmitting ? t("auth.acceptInvite.joiningWorkspace") : t("common.actions.acceptInvite")}
                  <ArrowRight />
                </Button>
                {requiresDifferentAccount ? (
                  <Button variant="outline" className="w-full justify-between" onClick={logout}>
                    {t("auth.acceptInvite.signOutAndTryAnother")}
                    <LogIn />
                  </Button>
                ) : (
                  <Button variant="outline" className="w-full" asChild>
                    <Link to="/dashboard">{t("common.actions.cancel")}</Link>
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
