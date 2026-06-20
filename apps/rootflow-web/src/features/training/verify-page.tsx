import { Award, CheckCircle2, XCircle } from "lucide-react";
import { useParams } from "react-router-dom";

import { Card, CardContent } from "@/components/ui/card";
import { LoadingState } from "@/components/feedback/loading-state";
import { useTrainingCertificateVerificationQuery } from "@/hooks/use-rootflow-data";

// Public page — no auth required. Reached via the URL printed on every
// certificate PDF. Renders a confirmation that the certificate is real.
export function TrainingVerifyPage() {
  const { code } = useParams<{ code: string }>();
  const query = useTrainingCertificateVerificationQuery(code ?? null);

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex min-h-screen max-w-2xl flex-col items-center justify-center px-6 py-12">
        <div className="mb-8 flex items-center gap-3">
          <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
            <Award className="size-6" />
          </div>
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              RootFlow
            </div>
            <h1 className="text-xl font-semibold tracking-[-0.04em] text-foreground">Verificação de certificado</h1>
          </div>
        </div>

        {query.isLoading ? (
          <LoadingState title="Verificando" description={`Conferindo o código ${code}...`} />
        ) : !query.data?.isValid ? (
          <Card className="w-full border-destructive/40 bg-destructive/5">
            <CardContent className="space-y-3 px-6 py-6 text-center">
              <XCircle className="mx-auto size-10 text-destructive" />
              <h2 className="text-lg font-semibold text-foreground">Certificado não encontrado</h2>
              <p className="text-sm text-muted-foreground">
                O código <span className="font-mono">{code}</span> não corresponde a nenhum certificado emitido
                pelo RootFlow. Confira se digitou corretamente.
              </p>
            </CardContent>
          </Card>
        ) : (
          <Card className="w-full border-primary/30 bg-primary/[0.04]">
            <CardContent className="space-y-4 px-6 py-6">
              <div className="flex items-center justify-center gap-3 text-center">
                <CheckCircle2 className="size-10 text-primary" />
                <div className="text-left">
                  <h2 className="text-lg font-semibold text-foreground">Certificado válido</h2>
                  <p className="text-xs text-muted-foreground">Verificação realizada com sucesso.</p>
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Funcionário" value={query.data.employeeName ?? "—"} />
                <Field label="Programa" value={query.data.programName ?? "—"} />
                <Field label="Workspace" value={query.data.workspaceName ?? "—"} />
                <Field
                  label="Emitido em"
                  value={
                    query.data.issuedAtUtc
                      ? new Intl.DateTimeFormat("pt-BR").format(new Date(query.data.issuedAtUtc))
                      : "—"
                  }
                />
              </div>

              <div className="rounded-[14px] border border-border bg-background/72 px-4 py-3">
                <div className="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                  Código de verificação
                </div>
                <div className="font-mono text-base tracking-[0.18em] text-foreground">{query.data.code}</div>
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[14px] border border-border bg-background/72 px-4 py-3">
      <div className="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{label}</div>
      <div className="text-sm font-medium text-foreground">{value}</div>
    </div>
  );
}
