import { GraduationCap, Loader2, Plus, ShieldAlert } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { useAuth } from "@/features/auth/auth-provider";
import { useCreateTrainingProgramMutation, useTrainingProgramsQuery } from "@/hooks/use-rootflow-data";
import { ApiError } from "@/lib/api/client";

export function TrainingAdminListPage() {
  const { session } = useAuth();
  const isAdmin = session?.role === "Owner" || session?.role === "Admin";

  if (!isAdmin) {
    return (
      <div className="space-y-5">
        <PageHeader title="Treinamentos" description="Esta área é restrita ao Owner e Admins do workspace." />
        <Card className="border-border/80 bg-card/86">
          <CardContent className="flex items-start gap-4 p-6">
            <ShieldAlert className="size-6 text-muted-foreground" />
            <div>
              <p className="text-sm font-medium text-foreground">Acesso restrito.</p>
              <p className="mt-1 text-sm text-muted-foreground">
                A área de gerenciamento de treinamentos é visível para o Owner e Admins do workspace. Se você
                precisa criar ou editar trilhas, peça pra um admin te promover.
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return <AdminListContent />;
}

function AdminListContent() {
  const programsQuery = useTrainingProgramsQuery();
  const [showCreate, setShowCreate] = useState(false);

  const programs = programsQuery.data ?? [];
  const publishedCount = programs.filter((p) => p.isPublished).length;
  const draftCount = programs.length - publishedCount;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Treinamentos"
        description="Crie programas com módulos baseados nos seus documentos. A IA gera o quiz; você revisa antes de publicar."
        actions={
          <Button onClick={() => setShowCreate(true)}>
            <Plus className="size-4" />
            Novo programa
          </Button>
        }
      />

      {showCreate ? (
        <CreateProgramCard onCancel={() => setShowCreate(false)} onCreated={() => setShowCreate(false)} />
      ) : null}

      <section className="grid gap-3 sm:grid-cols-3">
        <StatCard label="Total" value={programs.length} />
        <StatCard label="Publicados" value={publishedCount} />
        <StatCard label="Rascunhos" value={draftCount} />
      </section>

      <Card className="border-border/80 bg-card/86">
        <CardHeader>
          <div className="flex items-center justify-between gap-3">
            <CardTitle>Programas</CardTitle>
            <Badge variant="secondary">{programs.length}</Badge>
          </div>
        </CardHeader>
        <CardContent>
          {programsQuery.isLoading ? (
            <LoadingState title="Carregando" description="Buscando programas do workspace..." />
          ) : programsQuery.isError ? (
            <ErrorState
              title="Não foi possível carregar"
              description="Tente novamente ou confira se o treinamento está habilitado no seu workspace."
              onRetry={() => programsQuery.refetch()}
            />
          ) : programs.length === 0 ? (
            <EmptyState onCreate={() => setShowCreate(true)} />
          ) : (
            <div className="space-y-2">
              {programs.map((program) => (
                <Link
                  key={program.id}
                  to={`/training/manage/${program.id}`}
                  className="block rounded-[22px] border border-border/80 bg-card/72 px-4 py-3.5 transition-[transform,border-color,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:bg-card/92 hover:shadow-[0_18px_36px_-26px_rgba(18,38,74,0.18)]"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-sm font-semibold text-foreground">{program.name}</span>
                        {program.isPublished ? (
                          <Badge>Publicado</Badge>
                        ) : (
                          <Badge variant="secondary">Rascunho</Badge>
                        )}
                      </div>
                      {program.description ? (
                        <p className="line-clamp-1 text-xs text-muted-foreground">{program.description}</p>
                      ) : null}
                      <div className="text-xs text-muted-foreground">@{program.slug}</div>
                    </div>
                    <div className="shrink-0 text-right text-xs text-muted-foreground">
                      Aprov. mín: {program.passingScore}%
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <Card className="border-border/80 bg-card/86">
      <CardContent className="px-5 py-4">
        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{label}</div>
        <div className="mt-1 text-2xl font-semibold tracking-[-0.04em] text-foreground">{value}</div>
      </CardContent>
    </Card>
  );
}

function EmptyState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex flex-col items-center gap-4 rounded-[22px] border border-dashed border-border/80 bg-card/56 px-6 py-12 text-center">
      <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
        <GraduationCap className="size-6" />
      </div>
      <div className="space-y-1">
        <p className="text-sm font-semibold text-foreground">Nenhum programa ainda</p>
        <p className="text-sm text-muted-foreground">
          Crie sua primeira trilha apontando pros documentos da base de conhecimento. A IA gera o quiz pra você.
        </p>
      </div>
      <Button onClick={onCreate}>
        <Plus className="size-4" />
        Criar primeiro programa
      </Button>
    </div>
  );
}

function CreateProgramCard({ onCancel, onCreated }: { onCancel: () => void; onCreated: () => void }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const createMutation = useCreateTrainingProgramMutation();

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await createMutation.mutateAsync({
        name: name.trim(),
        slug: null,
        description: description.trim() || null,
      });
      onCreated();
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível criar o programa.");
    }
  }

  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <CardTitle>Novo programa</CardTitle>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div className="space-y-1.5">
            <Label htmlFor="program-name">Nome</Label>
            <Input
              id="program-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Onboarding 2026"
              required
              disabled={createMutation.isPending}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="program-description">Descrição (opcional)</Label>
            <textarea
              id="program-description"
              className="min-h-[80px] w-full rounded-[14px] border border-border bg-transparent px-4 py-3 text-sm leading-6 text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Trilha inicial pra novos funcionários"
              disabled={createMutation.isPending}
            />
          </div>
          {error ? (
            <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <div className="flex gap-3">
            <Button type="submit" disabled={createMutation.isPending || !name.trim()}>
              {createMutation.isPending ? (
                <>
                  <Loader2 className="size-4 animate-spin" />
                  Criando...
                </>
              ) : (
                "Criar programa"
              )}
            </Button>
            <Button type="button" variant="outline" onClick={onCancel} disabled={createMutation.isPending}>
              Cancelar
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
