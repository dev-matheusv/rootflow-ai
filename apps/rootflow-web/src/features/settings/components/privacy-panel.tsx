import { Download, UserX } from "lucide-react";
import { useState, type FormEvent } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PasswordInput } from "@/components/ui/password-input";
import { useAuth } from "@/features/auth/auth-provider";
import { ApiError, apiRequest, apiRequestBlob } from "@/lib/api/client";

// RF-13 (LGPD art. 18): the person can download a copy of their data and delete their account.
export function PrivacyPanel() {
  const { t } = useI18n();
  const { logout } = useAuth();
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [password, setPassword] = useState("");
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleExport() {
    setExportError(null);
    setIsExporting(true);
    try {
      const blob = await apiRequestBlob("/api/me/export");
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `rootflow-meus-dados-${new Date().toISOString().slice(0, 10)}.json`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setExportError(error instanceof ApiError ? error.message : t("settings.privacy.exportError"));
    } finally {
      setIsExporting(false);
    }
  }

  async function handleDelete(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!window.confirm(t("settings.privacy.deleteConfirm"))) {
      return;
    }

    setDeleteError(null);
    setIsDeleting(true);
    try {
      await apiRequest<void>("/api/me/delete", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ password }),
        parseAs: "none",
      });
      logout();
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : t("settings.privacy.deleteError"));
      setIsDeleting(false);
    }
  }

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <Card className="border-border/70 bg-background/72 shadow-none">
        <CardHeader>
          <CardTitle>{t("settings.privacy.exportTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">{t("settings.privacy.exportDescription")}</p>
          {exportError ? (
            <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{exportError}</div>
          ) : null}
          <Button type="button" variant="outline" onClick={handleExport} disabled={isExporting}>
            <Download />
            {isExporting ? t("settings.privacy.exporting") : t("settings.privacy.exportAction")}
          </Button>
        </CardContent>
      </Card>

      <Card className="border-destructive/25 bg-background/72 shadow-none">
        <CardHeader>
          <CardTitle>{t("settings.privacy.deleteTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={handleDelete}>
            <p className="text-sm text-muted-foreground">{t("settings.privacy.deleteDescription")}</p>
            <div className="space-y-2">
              <label className="text-sm font-semibold text-foreground" htmlFor="delete-account-password">
                {t("settings.privacy.passwordLabel")}
              </label>
              <PasswordInput
                id="delete-account-password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                disabled={isDeleting}
              />
            </div>
            {deleteError ? (
              <div className="rounded-[22px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{deleteError}</div>
            ) : null}
            <Button
              type="submit"
              variant="outline"
              className="text-destructive hover:text-destructive"
              disabled={isDeleting || password.length === 0}
            >
              <UserX />
              {isDeleting ? t("settings.privacy.deleting") : t("settings.privacy.deleteAction")}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
