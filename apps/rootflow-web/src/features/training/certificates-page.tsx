import { Award, Download, Loader2 } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { PageHeader } from "@/components/ui/page-header";
import { useTrainingCertificatesQuery } from "@/hooks/use-rootflow-data";
import { rootflowApi } from "@/lib/api/rootflow-api";
import { ApiError } from "@/lib/api/client";

export function TrainingCertificatesPage() {
  const query = useTrainingCertificatesQuery();
  const certs = query.data ?? [];

  return (
    <div className="space-y-5">
      <PageHeader
        title="Meus certificados"
        description="Programas que você concluiu. Cada certificado tem URL pública de verificação."
      />

      {query.isLoading ? (
        <LoadingState title="Carregando" description="Buscando seus certificados..." />
      ) : query.isError ? (
        <ErrorState title="Erro" description="Não foi possível listar." onRetry={() => query.refetch()} />
      ) : certs.length === 0 ? (
        <Card className="border-border/80 bg-card/86">
          <CardContent className="flex flex-col items-center gap-4 px-6 py-12 text-center">
            <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
              <Award className="size-6" />
            </div>
            <p className="text-sm text-muted-foreground">
              Você ainda não concluiu nenhum programa. Quando passar em todos os módulos de uma trilha, o
              certificado aparece aqui.
            </p>
          </CardContent>
        </Card>
      ) : (
        <section className="grid gap-3 md:grid-cols-2">
          {certs.map((cert) => (
            <CertificateCard key={cert.id} cert={cert} />
          ))}
        </section>
      )}
    </div>
  );
}

function CertificateCard({ cert }: { cert: { id: string; programName: string; issuedAtUtc: string; code: string; verificationUrl: string } }) {
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleDownload() {
    setError(null);
    setDownloading(true);
    try {
      const blob = await rootflowApi.downloadTrainingCertificatePdf(cert.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${cert.programName}-${cert.code}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível baixar o PDF.");
    } finally {
      setDownloading(false);
    }
  }

  return (
    <Card className="border-border/80 bg-card/86">
      <CardContent className="space-y-3 px-5 py-4">
        <div className="flex items-start gap-3">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/8 text-primary">
            <Award className="size-5" />
          </div>
          <div className="min-w-0 flex-1 space-y-0.5">
            <p className="truncate text-sm font-semibold text-foreground">{cert.programName}</p>
            <p className="text-xs text-muted-foreground">
              Emitido em {new Intl.DateTimeFormat("pt-BR").format(new Date(cert.issuedAtUtc))}
            </p>
          </div>
        </div>
        <div className="rounded-[14px] border border-border bg-background/72 px-3 py-2">
          <div className="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">Código</div>
          <div className="font-mono text-sm tracking-[0.15em] text-foreground">{cert.code}</div>
          <a href={cert.verificationUrl} target="_blank" rel="noreferrer" className="mt-1 inline-block text-[11px] text-primary hover:underline">
            {cert.verificationUrl}
          </a>
        </div>
        {error ? <p className="text-xs text-destructive">{error}</p> : null}
        <Button size="sm" onClick={handleDownload} disabled={downloading}>
          {downloading ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
          Baixar PDF
        </Button>
      </CardContent>
    </Card>
  );
}
