import { Award, GraduationCap, Settings2 } from "lucide-react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/features/auth/auth-provider";
import { useAvailableTrainingProgramsQuery } from "@/hooks/use-rootflow-data";

export function TrainingEmployeeListPage() {
  const { session } = useAuth();
  const isAdmin = session?.role === "Owner" || session?.role === "Admin";
  const programsQuery = useAvailableTrainingProgramsQuery();
  const programs = programsQuery.data ?? [];

  return (
    <div className="space-y-5">
      <PageHeader
        title="Treinamentos"
        description="Programas de treinamento disponíveis pra você. Conclua os módulos pra ganhar o certificado."
        actions={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link to="/training/certificates">
                <Award className="size-4" />
                Meus certificados
              </Link>
            </Button>
            {isAdmin ? (
              <Button variant="outline" asChild>
                <Link to="/training/manage">
                  <Settings2 className="size-4" />
                  Gerenciar
                </Link>
              </Button>
            ) : null}
          </div>
        }
      />

      {programsQuery.isLoading ? (
        <LoadingState title="Carregando" description="Buscando programas disponíveis..." />
      ) : programsQuery.isError ? (
        <ErrorState
          title="Não foi possível carregar"
          description="Os treinamentos podem não estar habilitados pro seu workspace ainda."
          onRetry={() => programsQuery.refetch()}
        />
      ) : programs.length === 0 ? (
        <Card className="border-border/80 bg-card/86">
          <CardContent className="flex flex-col items-center gap-4 px-6 py-12 text-center">
            <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
              <GraduationCap className="size-6" />
            </div>
            <div className="space-y-1">
              <p className="text-sm font-semibold text-foreground">Nenhum programa disponível ainda</p>
              <p className="text-sm text-muted-foreground">
                Quando seu workspace publicar trilhas, elas aparecem aqui.
              </p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <section className="grid gap-3 md:grid-cols-2">
          {programs.map((program) => {
            const completed = program.moduleCount > 0 && program.passedModuleCount >= program.moduleCount;
            const progress =
              program.moduleCount === 0
                ? 0
                : Math.round((program.passedModuleCount / program.moduleCount) * 100);
            return (
              <Link
                key={program.id}
                to={`/training/programs/${program.id}`}
                className="rounded-[22px] border border-border/80 bg-card/86 px-5 py-4 transition-[transform,border-color,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:bg-card/96 hover:shadow-[0_18px_38px_-26px_rgba(18,38,74,0.18)]"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1">
                    <p className="truncate text-sm font-semibold text-foreground">{program.name}</p>
                    {program.description ? (
                      <p className="line-clamp-2 text-xs text-muted-foreground">{program.description}</p>
                    ) : null}
                  </div>
                  {completed ? <Badge>Concluído</Badge> : null}
                </div>
                <div className="mt-3 space-y-1">
                  <div className="flex items-center justify-between text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    <span>Progresso</span>
                    <span>{program.passedModuleCount}/{program.moduleCount} módulos</span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-muted/70">
                    <div
                      className="h-full bg-primary transition-[width] duration-300"
                      style={{ width: `${progress}%` }}
                    />
                  </div>
                </div>
                <p className="mt-3 text-[11px] text-muted-foreground">
                  Aprovação mínima: <strong className="text-foreground">{program.passingScore}%</strong>
                </p>
              </Link>
            );
          })}
        </section>
      )}
    </div>
  );
}
