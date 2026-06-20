import { ArrowLeft, CheckCircle2, Loader2, XCircle } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { PageHeader } from "@/components/ui/page-header";
import {
  useStartTrainingAttemptMutation,
  useSubmitTrainingAnswerMutation,
  useSubmitTrainingAttemptMutation,
} from "@/hooks/use-rootflow-data";
import { ApiError } from "@/lib/api/client";
import type { AttemptResult, ConsumerQuestion } from "@/lib/api/contracts";
import { cn } from "@/lib/utils";

export function TrainingQuizAttemptPage() {
  const { programId, moduleId } = useParams<{ programId: string; moduleId: string }>();
  const startMutation = useStartTrainingAttemptMutation();
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [questions, setQuestions] = useState<ConsumerQuestion[]>([]);
  const [passingScore, setPassingScore] = useState<number>(70);
  const [startError, setStartError] = useState<string | null>(null);

  useEffect(() => {
    if (!moduleId || attemptId || startMutation.isPending) return;
    setStartError(null);
    startMutation.mutate(moduleId, {
      onSuccess: (result) => {
        setAttemptId(result.attemptId);
        setQuestions(result.questions);
        setPassingScore(result.passingScore);
      },
      onError: (error) =>
        setStartError(error instanceof ApiError ? error.message : "Não foi possível iniciar a tentativa."),
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [moduleId]);

  if (!programId || !moduleId) {
    return <PageHeader title="Endereço inválido" />;
  }

  if (startError) {
    return (
      <div className="space-y-5">
        <PageHeader
          title="Não foi possível iniciar"
          description={startError}
          actions={
            <Button variant="outline" asChild>
              <Link to={`/training/programs/${programId}`}>
                <ArrowLeft className="size-4" />
                Voltar
              </Link>
            </Button>
          }
        />
      </div>
    );
  }

  if (!attemptId) {
    return <LoadingState title="Preparando" description="Carregando perguntas..." />;
  }

  return (
    <QuizPlayer
      programId={programId}
      moduleId={moduleId}
      attemptId={attemptId}
      questions={questions}
      passingScore={passingScore}
    />
  );
}

function QuizPlayer({
  programId,
  attemptId,
  questions,
  passingScore,
}: {
  programId: string;
  moduleId: string;
  attemptId: string;
  questions: ConsumerQuestion[];
  passingScore: number;
}) {
  const [answers, setAnswers] = useState<Record<string, number[]>>({});
  const [submittedQuestions, setSubmittedQuestions] = useState<Set<string>>(new Set());
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const answerMutation = useSubmitTrainingAnswerMutation(attemptId);
  const submitAttemptMutation = useSubmitTrainingAttemptMutation(programId);

  const total = questions.length;
  const answeredCount = useMemo(
    () => questions.filter((q) => (answers[q.id]?.length ?? 0) > 0).length,
    [answers, questions],
  );
  const canSubmit = answeredCount === total && !result;

  function toggleAnswer(question: ConsumerQuestion, index: number) {
    if (result) return;
    setAnswers((prev) => {
      const current = prev[question.id] ?? [];
      if (question.type === "MultiChoice") {
        const next = current.includes(index) ? current.filter((i) => i !== index) : [...current, index];
        return { ...prev, [question.id]: next };
      }
      // SingleChoice / TrueFalse
      return { ...prev, [question.id]: [index] };
    });
  }

  async function persistAnswer(question: ConsumerQuestion, indices: number[]) {
    if (submittedQuestions.has(question.id)) {
      // Already saved on first selection — backend upserts on conflict so we re-save anyway
    }
    try {
      await answerMutation.mutateAsync({ questionId: question.id, selectedIndices: indices });
      setSubmittedQuestions((prev) => new Set(prev).add(question.id));
    } catch {
      // We don't surface per-answer errors aggressively; final submit will reveal real issues.
    }
  }

  async function handleSubmit() {
    setSubmitError(null);
    // Make sure the latest selection is persisted for every question before submitting.
    for (const q of questions) {
      const selected = answers[q.id] ?? [];
      if (selected.length > 0) {
        await persistAnswer(q, selected);
      }
    }
    try {
      const final = await submitAttemptMutation.mutateAsync(attemptId);
      setResult(final);
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : "Não foi possível finalizar a tentativa.");
    }
  }

  if (result) {
    return <QuizResult programId={programId} result={result} />;
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Quiz"
        description={`Responda todas as perguntas e clique em "Finalizar". Aprovação: ${passingScore}%.`}
        actions={
          <Button variant="outline" asChild>
            <Link to={`/training/programs/${programId}`}>
              <ArrowLeft className="size-4" />
              Sair (perde progresso)
            </Link>
          </Button>
        }
      />

      <Card className="border-border/80 bg-card/86">
        <CardContent className="flex items-center justify-between gap-3 px-5 py-3">
          <div className="text-sm">
            <span className="font-semibold text-foreground">{answeredCount}</span>
            <span className="text-muted-foreground"> / {total} respondidas</span>
          </div>
          <div className="h-1.5 w-40 overflow-hidden rounded-full bg-muted/70">
            <div
              className="h-full bg-primary transition-[width] duration-200"
              style={{ width: total === 0 ? "0%" : `${(answeredCount / total) * 100}%` }}
            />
          </div>
        </CardContent>
      </Card>

      <div className="space-y-3">
        {questions.map((question, qIndex) => {
          const selected = answers[question.id] ?? [];
          return (
            <Card key={question.id} className="border-border/80 bg-card/86">
              <CardHeader>
                <div className="flex items-start justify-between gap-3">
                  <CardTitle>
                    {qIndex + 1}. {question.prompt}
                  </CardTitle>
                  <Badge variant="secondary">{labelForType(question.type)}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                {question.options.map((option, index) => {
                  const isSelected = selected.includes(index);
                  return (
                    <button
                      key={index}
                      type="button"
                      onClick={() => {
                        toggleAnswer(question, index);
                        const newSelected = question.type === "MultiChoice"
                          ? isSelected
                            ? selected.filter((i) => i !== index)
                            : [...selected, index]
                          : [index];
                        void persistAnswer(question, newSelected);
                      }}
                      className={cn(
                        "flex w-full items-center gap-3 rounded-[16px] border px-4 py-3 text-left transition-[transform,border-color,background-color,box-shadow] duration-200",
                        isSelected
                          ? "-translate-y-0.5 border-primary/35 bg-primary/[0.08] shadow-[0_16px_28px_-22px_rgba(18,72,166,0.22)]"
                          : "border-border/80 bg-card/72 hover:-translate-y-0.5 hover:border-primary/22 hover:bg-card/92",
                      )}
                    >
                      <div
                        className={cn(
                          "flex size-7 shrink-0 items-center justify-center rounded-full border",
                          isSelected ? "border-primary bg-primary text-primary-foreground" : "border-border bg-background",
                        )}
                      >
                        {isSelected ? <CheckCircle2 className="size-4" /> : null}
                      </div>
                      <span className="text-sm text-foreground">{option}</span>
                    </button>
                  );
                })}
              </CardContent>
            </Card>
          );
        })}
      </div>

      {submitError ? (
        <ErrorState title="Erro ao finalizar" description={submitError} />
      ) : null}

      <Card className="border-border/80 bg-card/86">
        <CardContent className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
          <p className="text-sm text-muted-foreground">
            Pode revisar suas respostas antes de finalizar — depois disso o score é calculado.
          </p>
          <Button onClick={handleSubmit} disabled={!canSubmit || submitAttemptMutation.isPending}>
            {submitAttemptMutation.isPending ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Finalizando...
              </>
            ) : (
              "Finalizar tentativa"
            )}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

function QuizResult({ programId, result }: { programId: string; result: AttemptResult }) {
  const passed = result.status === "Passed";
  const navigate = useNavigate();

  return (
    <div className="space-y-5">
      <PageHeader
        title={passed ? "Aprovado!" : "Resultado"}
        description={
          passed
            ? "Você passou nesse módulo. Continue pra completar o programa e ganhar o certificado."
            : "Você não alcançou a aprovação mínima dessa vez. Pode tentar de novo."
        }
      />

      <Card className={cn("border-border/80", passed ? "bg-primary/[0.06]" : "bg-card/86")}>
        <CardContent className="grid gap-4 px-6 py-6 sm:grid-cols-4">
          <ResultCell label="Status" value={passed ? "Aprovado" : "Reprovado"} icon={passed ? CheckCircle2 : XCircle} iconColor={passed ? "text-primary" : "text-destructive"} />
          <ResultCell label="Score" value={`${result.score}%`} />
          <ResultCell label="Aprovação mínima" value={`${result.passingScore}%`} />
          <ResultCell label="Acertos" value={`${result.correctAnswerCount}/${result.totalQuestionCount}`} />
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-2">
        <Button onClick={() => navigate(`/training/programs/${programId}`)}>
          Voltar ao programa
        </Button>
        {!passed ? (
          <Button variant="outline" onClick={() => window.location.reload()}>
            Tentar de novo
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function ResultCell({
  label,
  value,
  icon: Icon,
  iconColor,
}: {
  label: string;
  value: string;
  icon?: React.ComponentType<{ className?: string }>;
  iconColor?: string;
}) {
  return (
    <div className="flex items-center gap-3">
      {Icon ? <Icon className={cn("size-6", iconColor)} /> : null}
      <div>
        <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{label}</div>
        <div className="text-base font-semibold text-foreground">{value}</div>
      </div>
    </div>
  );
}

function labelForType(type: ConsumerQuestion["type"]): string {
  switch (type) {
    case "SingleChoice":
      return "1 correta";
    case "MultiChoice":
      return "Múltiplas corretas";
    case "TrueFalse":
      return "V ou F";
  }
}
