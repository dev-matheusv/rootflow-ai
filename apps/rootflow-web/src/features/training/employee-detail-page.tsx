import { ArrowLeft, CheckCircle2, Circle, PlayCircle, XCircle } from "lucide-react";
import { Link, useParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { LoadingState } from "@/components/feedback/loading-state";
import { PageHeader } from "@/components/ui/page-header";
import { useAvailableTrainingProgramQuery } from "@/hooks/use-rootflow-data";
import type { ConsumerModuleStatus } from "@/lib/api/contracts";
import { cn } from "@/lib/utils";

export function TrainingEmployeeDetailPage() {
  const { programId } = useParams<{ programId: string }>();
  const query = useAvailableTrainingProgramQuery(programId);

  if (query.isLoading) {
    return <LoadingState title="Carregando" description="Buscando módulos do programa..." />;
  }

  if (query.isError || !query.data) {
    return (
      <div className="space-y-5">
        <PageHeader
          title="Programa não encontrado"
          description="O programa pode ter sido despublicado ou removido."
          actions={
            <Button variant="outline" asChild>
              <Link to="/training">
                <ArrowLeft className="size-4" />
                Voltar
              </Link>
            </Button>
          }
        />
      </div>
    );
  }

  const program = query.data;
  const totalModules = program.modules.length;
  const passedModules = program.modules.filter((m) => m.status === "Passed").length;
  const completed = totalModules > 0 && passedModules >= totalModules;

  return (
    <div className="space-y-5">
      <PageHeader
        title={program.name}
        description={program.description ?? "Programa de treinamento."}
        actions={
          <Button variant="outline" asChild>
            <Link to="/training">
              <ArrowLeft className="size-4" />
              Voltar
            </Link>
          </Button>
        }
      />

      <Card className="border-border/80 bg-card/86">
        <CardContent className="flex items-center justify-between gap-3 px-5 py-4">
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Progresso
            </div>
            <div className="text-base font-semibold text-foreground">
              {passedModules}/{totalModules} módulos concluídos
            </div>
          </div>
          <div className="text-right">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Aprovação mínima
            </div>
            <div className="text-base font-semibold text-foreground">{program.passingScore}%</div>
          </div>
          {completed ? (
            <Badge>
              <CheckCircle2 className="size-3.5" />
              Concluído
            </Badge>
          ) : null}
        </CardContent>
      </Card>

      <Card className="border-border/80 bg-card/86">
        <CardHeader>
          <CardTitle>Módulos</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            {program.modules.map((module) => (
              <ModuleRow
                key={module.id}
                programId={program.id}
                moduleId={module.id}
                orderIndex={module.orderIndex}
                title={module.title}
                description={module.description}
                questionCount={module.questionCount}
                status={module.status}
                latestScore={module.latestScore ?? null}
              />
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function ModuleRow({
  programId,
  moduleId,
  orderIndex,
  title,
  description,
  questionCount,
  status,
  latestScore,
}: {
  programId: string;
  moduleId: string;
  orderIndex: number;
  title: string;
  description: string | null | undefined;
  questionCount: number;
  status: ConsumerModuleStatus;
  latestScore: number | null;
}) {
  const StatusIcon =
    status === "Passed" ? CheckCircle2 : status === "Failed" ? XCircle : status === "InProgress" ? PlayCircle : Circle;
  const iconColor =
    status === "Passed"
      ? "text-primary"
      : status === "Failed"
        ? "text-destructive"
        : status === "InProgress"
          ? "text-amber-500"
          : "text-muted-foreground";

  const ctaLabel =
    status === "Passed"
      ? "Refazer"
      : status === "Failed"
        ? "Tentar novamente"
        : status === "InProgress"
          ? "Continuar"
          : "Iniciar";

  return (
    <div className="flex items-center gap-4 rounded-[18px] border border-border/80 bg-card/72 px-4 py-3">
      <div className={cn("flex size-9 shrink-0 items-center justify-center rounded-full border border-border bg-background", iconColor)}>
        <StatusIcon className="size-5" />
      </div>
      <div className="min-w-0 flex-1 space-y-0.5">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm font-semibold text-foreground">
            {orderIndex + 1}. {title}
          </span>
          {latestScore !== null ? <Badge variant="secondary">{latestScore}%</Badge> : null}
        </div>
        {description ? <p className="line-clamp-1 text-xs text-muted-foreground">{description}</p> : null}
        <p className="text-[11px] text-muted-foreground">{questionCount} perguntas</p>
      </div>
      <Button asChild size="sm" disabled={questionCount === 0}>
        <Link to={`/training/programs/${programId}/modules/${moduleId}/attempt`}>{ctaLabel}</Link>
      </Button>
    </div>
  );
}
