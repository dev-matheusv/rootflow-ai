import {
  ArrowLeft,
  CheckCircle2,
  Loader2,
  Pencil,
  Plus,
  Sparkles,
  Trash2,
} from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import { LoadingState } from "@/components/feedback/loading-state";
import { useAuth } from "@/features/auth/auth-provider";
import {
  useAddTrainingModuleMutation,
  useDeleteTrainingModuleMutation,
  useDeleteTrainingQuestionMutation,
  useDocumentsQuery,
  useGenerateTrainingQuizMutation,
  usePublishTrainingProgramMutation,
  usePublishTrainingQuestionMutation,
  useTrainingModuleQuestionsQuery,
  useTrainingProgramQuery,
  useUnpublishTrainingProgramMutation,
  useUpdateTrainingModuleMutation,
  useUpdateTrainingProgramMutation,
  useUpdateTrainingQuestionMutation,
} from "@/hooks/use-rootflow-data";
import { ApiError } from "@/lib/api/client";
import type {
  TrainingModuleSummary,
  TrainingProgramSummary,
  TrainingQuestion,
  TrainingQuestionType,
} from "@/lib/api/contracts";
import { cn } from "@/lib/utils";

export function TrainingAdminEditPage() {
  const { programId } = useParams<{ programId: string }>();
  const { session } = useAuth();
  const isAdmin = session?.role === "Owner" || session?.role === "Admin";

  if (!isAdmin) {
    return (
      <div className="space-y-5">
        <PageHeader title="Acesso restrito" description="Apenas Owner e Admins editam programas de treinamento." />
      </div>
    );
  }

  if (!programId) {
    return <PageHeader title="Programa não encontrado" />;
  }

  return <EditorContent programId={programId} />;
}

function EditorContent({ programId }: { programId: string }) {
  const programQuery = useTrainingProgramQuery(programId);

  if (programQuery.isLoading) {
    return <LoadingState title="Carregando programa" description="Aguarde..." />;
  }

  if (programQuery.isError || !programQuery.data) {
    return (
      <div className="space-y-5">
        <PageHeader
          title="Não foi possível carregar"
          description="O programa não existe, foi removido, ou o workspace não tem o treinamento habilitado."
          actions={
            <Button variant="outline" asChild>
              <Link to="/training/manage">
                <ArrowLeft className="size-4" />
                Voltar
              </Link>
            </Button>
          }
        />
      </div>
    );
  }

  const { program, modules } = programQuery.data;
  return <Editor program={program} modules={modules} />;
}

function Editor({
  program,
  modules,
}: {
  program: TrainingProgramSummary;
  modules: TrainingModuleSummary[];
}) {
  const [selectedModuleId, setSelectedModuleId] = useState<string | null>(modules[0]?.id ?? null);
  const [showNewModule, setShowNewModule] = useState(false);

  return (
    <div className="space-y-5">
      <PageHeader
        title={program.name}
        description={program.description ?? "Programa em edição."}
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" asChild>
              <Link to="/training/manage">
                <ArrowLeft className="size-4" />
                Voltar
              </Link>
            </Button>
            <PublishToggle program={program} modules={modules} />
          </div>
        }
      />

      <ProgramDetailsCard program={program} />

      <section className="grid gap-3 xl:grid-cols-[0.55fr_1fr]">
        <Card className="min-w-0 border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Módulos</CardTitle>
              <Button size="sm" variant="outline" onClick={() => setShowNewModule(true)}>
                <Plus className="size-3.5" />
                Novo
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            {showNewModule ? (
              <NewModuleForm
                programId={program.id}
                nextOrderIndex={modules.length}
                onCancel={() => setShowNewModule(false)}
                onCreated={(moduleId) => {
                  setShowNewModule(false);
                  setSelectedModuleId(moduleId);
                }}
              />
            ) : null}

            {modules.length === 0 && !showNewModule ? (
              <p className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                Nenhum módulo ainda. Adicione um pra começar — depois você gera o quiz com IA.
              </p>
            ) : null}

            <div className="mt-2 space-y-1.5">
              {modules.map((module) => {
                const isActive = module.id === selectedModuleId;
                const ready = module.publishedQuestionCount >= 3;
                return (
                  <button
                    key={module.id}
                    type="button"
                    onClick={() => setSelectedModuleId(module.id)}
                    className={cn(
                      "w-full rounded-[18px] border px-4 py-3 text-left transition-[transform,border-color,background-color,box-shadow] duration-200 hover:-translate-y-0.5",
                      isActive
                        ? "border-primary/30 bg-primary/[0.06] shadow-[0_18px_34px_-26px_rgba(18,72,166,0.22)]"
                        : "border-border/80 bg-card/72 hover:border-primary/24",
                    )}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 space-y-0.5">
                        <div className="truncate text-sm font-semibold text-foreground">
                          {module.orderIndex + 1}. {module.title}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {module.publishedQuestionCount}/{module.questionCount} perguntas publicadas
                        </div>
                      </div>
                      {ready ? (
                        <Badge>Pronto</Badge>
                      ) : (
                        <Badge variant="secondary">{3 - module.publishedQuestionCount} faltam</Badge>
                      )}
                    </div>
                  </button>
                );
              })}
            </div>
          </CardContent>
        </Card>

        <div className="min-w-0">
          {selectedModuleId ? (
            <ModulePanel
              programId={program.id}
              module={modules.find((m) => m.id === selectedModuleId)!}
            />
          ) : (
            <Card className="border-border/80 bg-card/86">
              <CardContent className="px-6 py-12 text-center">
                <p className="text-sm text-muted-foreground">
                  Selecione ou crie um módulo pra editar suas perguntas.
                </p>
              </CardContent>
            </Card>
          )}
        </div>
      </section>
    </div>
  );
}

function PublishToggle({
  program,
  modules,
}: {
  program: TrainingProgramSummary;
  modules: TrainingModuleSummary[];
}) {
  const publishMutation = usePublishTrainingProgramMutation(program.id);
  const unpublishMutation = useUnpublishTrainingProgramMutation(program.id);
  const [error, setError] = useState<string | null>(null);

  const allModulesReady = modules.length > 0 && modules.every((m) => m.publishedQuestionCount >= 3);
  const isPending = publishMutation.isPending || unpublishMutation.isPending;

  async function handleClick() {
    setError(null);
    try {
      if (program.isPublished) {
        await unpublishMutation.mutateAsync();
      } else {
        await publishMutation.mutateAsync();
      }
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível atualizar o programa.");
    }
  }

  if (program.isPublished) {
    return (
      <Button variant="outline" onClick={handleClick} disabled={isPending}>
        {isPending ? <Loader2 className="size-4 animate-spin" /> : null}
        Despublicar
      </Button>
    );
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      <Button onClick={handleClick} disabled={isPending || !allModulesReady}>
        {isPending ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 className="size-4" />}
        Publicar programa
      </Button>
      {!allModulesReady ? (
        <span className="text-xs text-muted-foreground">
          {modules.length === 0
            ? "Adicione pelo menos um módulo."
            : "Cada módulo precisa de no mínimo 3 perguntas publicadas."}
        </span>
      ) : null}
      {error ? <span className="text-xs text-destructive">{error}</span> : null}
    </div>
  );
}

function ProgramDetailsCard({ program }: { program: TrainingProgramSummary }) {
  const [name, setName] = useState(program.name);
  const [description, setDescription] = useState(program.description ?? "");
  const [passingScore, setPassingScore] = useState(program.passingScore);
  const [error, setError] = useState<string | null>(null);
  const updateMutation = useUpdateTrainingProgramMutation(program.id);

  const dirty =
    name.trim() !== program.name ||
    (description.trim() || null) !== (program.description ?? null) ||
    passingScore !== program.passingScore;

  async function handleSave(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await updateMutation.mutateAsync({
        name: name.trim(),
        description: description.trim() || null,
        passingScore,
      });
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível salvar.");
    }
  }

  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <CardTitle>Detalhes do programa</CardTitle>
      </CardHeader>
      <CardContent>
        <form className="grid gap-4 md:grid-cols-[2fr_2fr_1fr] md:items-end" onSubmit={handleSave}>
          <div className="space-y-1.5">
            <Label htmlFor="program-name">Nome</Label>
            <Input
              id="program-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
              disabled={updateMutation.isPending}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="program-description">Descrição</Label>
            <Input
              id="program-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Trilha inicial pra novos funcionários"
              disabled={updateMutation.isPending}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="program-passing">Aprov. mín. (%)</Label>
            <Input
              id="program-passing"
              type="number"
              min={0}
              max={100}
              value={passingScore}
              onChange={(event) => setPassingScore(Number(event.target.value))}
              disabled={updateMutation.isPending}
            />
          </div>
          {error ? (
            <p className="md:col-span-3 rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <div className="md:col-span-3 flex justify-end">
            <Button type="submit" disabled={!dirty || updateMutation.isPending}>
              {updateMutation.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
              Salvar alterações
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function NewModuleForm({
  programId,
  nextOrderIndex,
  onCancel,
  onCreated,
}: {
  programId: string;
  nextOrderIndex: number;
  onCancel: () => void;
  onCreated: (moduleId: string) => void;
}) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [selectedDocs, setSelectedDocs] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const documentsQuery = useDocumentsQuery();
  const addMutation = useAddTrainingModuleMutation(programId);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      const created = await addMutation.mutateAsync({
        title: title.trim(),
        description: description.trim() || null,
        orderIndex: nextOrderIndex,
        sourceDocumentIds: Array.from(selectedDocs),
      });
      onCreated(created.id);
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível criar o módulo.");
    }
  }

  const documents = documentsQuery.data ?? [];

  return (
    <form className="space-y-3 rounded-[18px] border border-primary/24 bg-primary/[0.04] px-4 py-3" onSubmit={handleSubmit}>
      <div className="space-y-1.5">
        <Label htmlFor="module-title">Título</Label>
        <Input
          id="module-title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Boas-vindas e cultura"
          required
          disabled={addMutation.isPending}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="module-description">Descrição</Label>
        <Input
          id="module-description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          placeholder="Opcional"
          disabled={addMutation.isPending}
        />
      </div>
      <div className="space-y-1.5">
        <Label>Documentos-fonte</Label>
        <p className="text-xs text-muted-foreground">A IA vai gerar perguntas usando o conteúdo desses documentos.</p>
        <div className="max-h-40 overflow-y-auto rounded-[14px] border border-border bg-background/70">
          {documents.length === 0 ? (
            <p className="px-3 py-3 text-xs text-muted-foreground">
              Você não tem documentos na base de conhecimento ainda. Suba alguns antes pra usar a geração via IA.
            </p>
          ) : (
            documents.map((doc) => {
              const selected = selectedDocs.has(doc.id);
              return (
                <button
                  type="button"
                  key={doc.id}
                  onClick={() => {
                    setSelectedDocs((prev) => {
                      const next = new Set(prev);
                      if (next.has(doc.id)) next.delete(doc.id);
                      else next.add(doc.id);
                      return next;
                    });
                  }}
                  className={cn(
                    "flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-xs transition-colors",
                    selected ? "bg-primary/8 text-foreground" : "hover:bg-muted/50 text-muted-foreground",
                  )}
                >
                  <span className="truncate">{doc.originalFileName}</span>
                  {selected ? <CheckCircle2 className="size-4 shrink-0 text-primary" /> : null}
                </button>
              );
            })
          )}
        </div>
      </div>
      {error ? (
        <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
          {error}
        </p>
      ) : null}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={!title.trim() || addMutation.isPending}>
          {addMutation.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
          Criar módulo
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={onCancel} disabled={addMutation.isPending}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}

function ModulePanel({ programId, module }: { programId: string; module: TrainingModuleSummary }) {
  const questionsQuery = useTrainingModuleQuestionsQuery(module.id);
  const [count, setCount] = useState(5);
  const [generateError, setGenerateError] = useState<string | null>(null);
  const generateMutation = useGenerateTrainingQuizMutation(programId, module.id);
  const deleteModuleMutation = useDeleteTrainingModuleMutation(programId);
  const navigate = useNavigate();
  const [showEditModule, setShowEditModule] = useState(false);

  async function handleGenerate() {
    setGenerateError(null);
    try {
      await generateMutation.mutateAsync({ questionCount: count });
    } catch (exception) {
      setGenerateError(
        exception instanceof ApiError ? exception.message : "Não foi possível gerar o quiz. Confira se o módulo tem documentos-fonte.",
      );
    }
  }

  async function handleDelete() {
    if (!confirm(`Apagar o módulo "${module.title}"? Todas as perguntas e tentativas dele também serão removidas.`)) return;
    try {
      await deleteModuleMutation.mutateAsync(module.id);
      navigate(`/training/manage/${programId}`);
    } catch {
      // surface via list refresh; ignore
    }
  }

  const questions = questionsQuery.data ?? [];

  return (
    <Card className="border-border/80 bg-card/86">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <CardTitle>{module.title}</CardTitle>
            <p className="text-xs text-muted-foreground">
              {module.publishedQuestionCount}/{module.questionCount} perguntas publicadas · {module.sourceDocumentIds.length} documento(s) de origem
            </p>
          </div>
          <div className="flex shrink-0 gap-2">
            <Button size="sm" variant="outline" onClick={() => setShowEditModule((v) => !v)}>
              <Pencil className="size-3.5" />
              Editar
            </Button>
            <Button size="sm" variant="outline" onClick={handleDelete} disabled={deleteModuleMutation.isPending}>
              <Trash2 className="size-3.5" />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {showEditModule ? (
          <EditModuleForm
            programId={programId}
            module={module}
            onClose={() => setShowEditModule(false)}
          />
        ) : null}

        <div className="rounded-[18px] border border-primary/22 bg-primary/[0.05] px-4 py-3">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label htmlFor="quiz-count" className="text-xs">Quantas perguntas?</Label>
              <Input
                id="quiz-count"
                type="number"
                min={3}
                max={12}
                value={count}
                onChange={(event) => setCount(Number(event.target.value))}
                className="w-24"
                disabled={generateMutation.isPending}
              />
            </div>
            <Button onClick={handleGenerate} disabled={generateMutation.isPending || module.sourceDocumentIds.length === 0}>
              {generateMutation.isPending ? (
                <>
                  <Loader2 className="size-4 animate-spin" />
                  Gerando...
                </>
              ) : (
                <>
                  <Sparkles className="size-4" />
                  Gerar quiz com IA
                </>
              )}
            </Button>
            {module.sourceDocumentIds.length === 0 ? (
              <span className="text-xs text-muted-foreground">
                Adicione documentos-fonte ao módulo antes de gerar.
              </span>
            ) : null}
          </div>
          {generateError ? (
            <p className="mt-3 rounded-[12px] border border-destructive/20 bg-destructive/8 px-3 py-2 text-xs text-destructive">
              {generateError}
            </p>
          ) : null}
        </div>

        {questionsQuery.isLoading ? (
          <LoadingState title="Carregando perguntas" description="Aguarde..." />
        ) : questions.length === 0 ? (
          <p className="rounded-[18px] border border-dashed border-border/80 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
            Nenhuma pergunta ainda. Clique em "Gerar quiz com IA" pra criar um lote a partir dos documentos-fonte.
          </p>
        ) : (
          <div className="space-y-3">
            {questions.map((question) => (
              <QuestionEditor
                key={question.id}
                question={question}
                programId={programId}
                moduleId={module.id}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function EditModuleForm({
  programId,
  module,
  onClose,
}: {
  programId: string;
  module: TrainingModuleSummary;
  onClose: () => void;
}) {
  const [title, setTitle] = useState(module.title);
  const [description, setDescription] = useState(module.description ?? "");
  const [selectedDocs, setSelectedDocs] = useState<Set<string>>(new Set(module.sourceDocumentIds));
  const documentsQuery = useDocumentsQuery();
  const updateMutation = useUpdateTrainingModuleMutation(programId, module.id);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await updateMutation.mutateAsync({
        title: title.trim(),
        description: description.trim() || null,
        orderIndex: module.orderIndex,
        sourceDocumentIds: Array.from(selectedDocs),
      });
      onClose();
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível salvar.");
    }
  }

  const documents = documentsQuery.data ?? [];

  return (
    <form className="space-y-3 rounded-[18px] border border-border/80 bg-card/72 px-4 py-3" onSubmit={handleSubmit}>
      <div className="space-y-1.5">
        <Label htmlFor="edit-module-title">Título</Label>
        <Input
          id="edit-module-title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          required
          disabled={updateMutation.isPending}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="edit-module-description">Descrição</Label>
        <Input
          id="edit-module-description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          disabled={updateMutation.isPending}
        />
      </div>
      <div className="space-y-1.5">
        <Label>Documentos-fonte</Label>
        <div className="max-h-32 overflow-y-auto rounded-[14px] border border-border bg-background/70">
          {documents.length === 0 ? (
            <p className="px-3 py-2 text-xs text-muted-foreground">Nenhum documento disponível.</p>
          ) : (
            documents.map((doc) => {
              const selected = selectedDocs.has(doc.id);
              return (
                <button
                  type="button"
                  key={doc.id}
                  onClick={() => {
                    setSelectedDocs((prev) => {
                      const next = new Set(prev);
                      if (next.has(doc.id)) next.delete(doc.id);
                      else next.add(doc.id);
                      return next;
                    });
                  }}
                  className={cn(
                    "flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-xs transition-colors",
                    selected ? "bg-primary/8 text-foreground" : "hover:bg-muted/50 text-muted-foreground",
                  )}
                >
                  <span className="truncate">{doc.originalFileName}</span>
                  {selected ? <CheckCircle2 className="size-4 shrink-0 text-primary" /> : null}
                </button>
              );
            })
          )}
        </div>
      </div>
      {error ? (
        <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-3 py-2 text-xs text-destructive">{error}</p>
      ) : null}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={updateMutation.isPending}>
          {updateMutation.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
          Salvar
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={onClose} disabled={updateMutation.isPending}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}

function QuestionEditor({
  question,
  programId,
  moduleId,
}: {
  question: TrainingQuestion;
  programId: string;
  moduleId: string;
}) {
  const [editing, setEditing] = useState(false);

  if (!editing) {
    return <QuestionReadView question={question} onEdit={() => setEditing(true)} programId={programId} moduleId={moduleId} />;
  }

  return (
    <QuestionEditForm
      question={question}
      programId={programId}
      moduleId={moduleId}
      onClose={() => setEditing(false)}
    />
  );
}

function QuestionReadView({
  question,
  onEdit,
  programId,
  moduleId,
}: {
  question: TrainingQuestion;
  onEdit: () => void;
  programId: string;
  moduleId: string;
}) {
  const publishMutation = usePublishTrainingQuestionMutation(moduleId, programId);
  const deleteMutation = useDeleteTrainingQuestionMutation(moduleId, programId);

  async function handlePublish() {
    try {
      await publishMutation.mutateAsync(question.id);
    } catch {
      // ignore — list refresh will surface errors
    }
  }

  async function handleDelete() {
    if (!confirm("Apagar essa pergunta?")) return;
    try {
      await deleteMutation.mutateAsync(question.id);
    } catch {
      // ignore
    }
  }

  return (
    <div className="rounded-[18px] border border-border/80 bg-card/72 px-4 py-3.5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex items-center gap-2">
            <Badge variant="secondary">{question.type}</Badge>
            {question.status === "Published" ? (
              <Badge>Publicada</Badge>
            ) : (
              <Badge variant="secondary">Rascunho</Badge>
            )}
          </div>
          <p className="text-sm font-medium text-foreground">{question.prompt}</p>
        </div>
        <div className="flex shrink-0 gap-2">
          {question.status === "Draft" ? (
            <Button size="sm" onClick={handlePublish} disabled={publishMutation.isPending}>
              {publishMutation.isPending ? <Loader2 className="size-3.5 animate-spin" /> : <CheckCircle2 className="size-3.5" />}
              Publicar
            </Button>
          ) : null}
          <Button size="sm" variant="outline" onClick={onEdit}>
            <Pencil className="size-3.5" />
          </Button>
          <Button size="sm" variant="outline" onClick={handleDelete} disabled={deleteMutation.isPending}>
            <Trash2 className="size-3.5" />
          </Button>
        </div>
      </div>
      <ul className="mt-3 space-y-1">
        {question.options.map((option, index) => {
          const correct = question.correctAnswerIndices.includes(index);
          return (
            <li
              key={index}
              className={cn(
                "flex items-center gap-2 rounded-[12px] border px-3 py-1.5 text-xs",
                correct
                  ? "border-primary/30 bg-primary/[0.06] text-foreground"
                  : "border-border/70 bg-background/72 text-muted-foreground",
              )}
            >
              {correct ? <CheckCircle2 className="size-3.5 text-primary" /> : <span className="size-3.5" />}
              <span>{option}</span>
            </li>
          );
        })}
      </ul>
      {question.explanation ? (
        <p className="mt-2 text-xs italic text-muted-foreground">{question.explanation}</p>
      ) : null}
    </div>
  );
}

function QuestionEditForm({
  question,
  programId,
  moduleId,
  onClose,
}: {
  question: TrainingQuestion;
  programId: string;
  moduleId: string;
  onClose: () => void;
}) {
  const [prompt, setPrompt] = useState(question.prompt);
  const [type, setType] = useState<TrainingQuestionType>(question.type);
  const [options, setOptions] = useState<string[]>(question.options);
  const [correctIndices, setCorrectIndices] = useState<Set<number>>(new Set(question.correctAnswerIndices));
  const [explanation, setExplanation] = useState(question.explanation ?? "");
  const [error, setError] = useState<string | null>(null);
  const updateMutation = useUpdateTrainingQuestionMutation(moduleId, programId);

  function toggleCorrect(index: number) {
    setCorrectIndices((prev) => {
      const next = new Set(prev);
      if (type === "SingleChoice" || type === "TrueFalse") {
        next.clear();
        next.add(index);
      } else {
        if (next.has(index)) next.delete(index);
        else next.add(index);
      }
      return next;
    });
  }

  function updateOption(index: number, value: string) {
    setOptions((prev) => prev.map((option, i) => (i === index ? value : option)));
  }

  function addOption() {
    setOptions((prev) => [...prev, ""]);
  }

  function removeOption(index: number) {
    setOptions((prev) => prev.filter((_, i) => i !== index));
    setCorrectIndices((prev) => {
      const next = new Set<number>();
      prev.forEach((i) => {
        if (i < index) next.add(i);
        else if (i > index) next.add(i - 1);
      });
      return next;
    });
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await updateMutation.mutateAsync({
        questionId: question.id,
        payload: {
          prompt: prompt.trim(),
          type,
          options: options.map((o) => o.trim()).filter((o) => o.length > 0),
          correctAnswerIndices: Array.from(correctIndices).sort((a, b) => a - b),
          explanation: explanation.trim() || null,
        },
      });
      onClose();
    } catch (exception) {
      setError(exception instanceof ApiError ? exception.message : "Não foi possível salvar.");
    }
  }

  return (
    <form className="space-y-3 rounded-[18px] border border-primary/24 bg-primary/[0.04] px-4 py-3" onSubmit={handleSubmit}>
      <div className="space-y-1.5">
        <Label htmlFor="q-prompt">Pergunta</Label>
        <textarea
          id="q-prompt"
          className="min-h-[60px] w-full rounded-[12px] border border-border bg-transparent px-3 py-2 text-sm leading-6"
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          required
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="q-type">Tipo</Label>
        <select
          id="q-type"
          className="h-10 w-full rounded-[10px] border border-input bg-transparent px-3 text-sm text-foreground"
          value={type}
          onChange={(event) => setType(event.target.value as TrainingQuestionType)}
        >
          <option value="SingleChoice">Múltipla escolha (1 correta)</option>
          <option value="MultiChoice">Múltipla escolha (várias corretas)</option>
          <option value="TrueFalse">Verdadeiro/Falso</option>
        </select>
      </div>
      <div className="space-y-1.5">
        <Label>Opções (clique no círculo pra marcar como correta)</Label>
        <div className="space-y-1.5">
          {options.map((option, index) => {
            const isCorrect = correctIndices.has(index);
            return (
              <div key={index} className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => toggleCorrect(index)}
                  className={cn(
                    "flex size-7 shrink-0 items-center justify-center rounded-full border",
                    isCorrect ? "border-primary bg-primary text-primary-foreground" : "border-border bg-background",
                  )}
                  aria-label={isCorrect ? "Correta" : "Marcar como correta"}
                >
                  {isCorrect ? <CheckCircle2 className="size-4" /> : null}
                </button>
                <Input value={option} onChange={(event) => updateOption(index, event.target.value)} />
                {type !== "TrueFalse" && options.length > 2 ? (
                  <Button type="button" variant="outline" size="sm" onClick={() => removeOption(index)}>
                    <Trash2 className="size-3.5" />
                  </Button>
                ) : null}
              </div>
            );
          })}
        </div>
        {type !== "TrueFalse" ? (
          <Button type="button" variant="outline" size="sm" onClick={addOption}>
            <Plus className="size-3.5" />
            Adicionar opção
          </Button>
        ) : null}
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="q-explanation">Explicação (opcional)</Label>
        <Input
          id="q-explanation"
          value={explanation}
          onChange={(event) => setExplanation(event.target.value)}
          placeholder="Por que essa é a resposta correta"
        />
      </div>
      {error ? (
        <p className="rounded-[12px] border border-destructive/20 bg-destructive/8 px-3 py-2 text-xs text-destructive">{error}</p>
      ) : null}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={updateMutation.isPending}>
          {updateMutation.isPending ? <Loader2 className="size-4 animate-spin" /> : null}
          Salvar (volta a rascunho)
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={onClose} disabled={updateMutation.isPending}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
