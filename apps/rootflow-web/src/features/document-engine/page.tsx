import { Bot, Download, FileOutput, FileText, FilePlus, Loader2, Pencil, Sparkles, Upload } from "lucide-react";
import { useRef, useState } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { ApiError, apiRequest, apiRequestBlob } from "@/lib/api/client";
import { ErrorState } from "@/components/feedback/error-state";
import { LoadingState } from "@/components/feedback/loading-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/ui/page-header";
import {
  useCreateDocumentTemplateMutation,
  useDocumentTemplateQuery,
  useDocumentTemplatesQuery,
  useGenerateDocumentMutation,
} from "@/hooks/use-rootflow-data";
import type { DocumentTemplateSummary, TemplateFieldSummary } from "@/lib/api/contracts";
import { formatRelativeDate } from "@/lib/formatting/formatters";

interface TemplateDraft {
  name: string;
  body: string;
  fields: TemplateFieldSummary[];
}

type CreationMode = "pick" | "ai" | "import" | "manual" | "review";

export function DocumentEnginePage() {
  const { locale, t } = useI18n();
  const templatesQuery = useDocumentTemplatesQuery();
  const templates = templatesQuery.data ?? [];
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [creationMode, setCreationMode] = useState<CreationMode | null>(null);
  const [pendingDraft, setPendingDraft] = useState<TemplateDraft | null>(null);

  function openCreation() {
    setSelectedId(null);
    setCreationMode("pick");
    setPendingDraft(null);
  }

  function handleDraftReady(draft: TemplateDraft) {
    setPendingDraft(draft);
    setCreationMode("review");
  }

  function handleCreated(id: string) {
    setCreationMode(null);
    setPendingDraft(null);
    setSelectedId(id);
  }

  const showRight = creationMode !== null || selectedId !== null;

  return (
    <div className="space-y-5">
      <PageHeader
        title={t("documentEngine.title")}
        description={t("documentEngine.description")}
      />

      <section className="grid gap-3 xl:grid-cols-[0.82fr_1.18fr]">
        {/* Template list */}
        <Card className="min-w-0 border-border/80 bg-card/86">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t("documentEngine.templatesTitle")}</CardTitle>
              <div className="flex items-center gap-2">
                <Badge variant="secondary">{templates.length}</Badge>
                <Button size="sm" variant="outline" onClick={openCreation}>
                  <FilePlus className="size-3.5" />
                  {t("documentEngine.newTemplate")}
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {templatesQuery.isLoading ? (
              <LoadingState title={t("documentEngine.loadingTitle")} description={t("documentEngine.loadingDescription")} />
            ) : templatesQuery.isError ? (
              <ErrorState title={t("documentEngine.errorTitle")} description={t("documentEngine.errorDescription")} onRetry={() => templatesQuery.refetch()} />
            ) : templates.length === 0 && !creationMode ? (
              <div className="rounded-[18px] border border-dashed border-border/75 bg-card/56 px-4 py-3 text-sm text-muted-foreground">
                {t("documentEngine.emptyHint")}
              </div>
            ) : (
              <div className="overflow-hidden rounded-[22px] border border-border/75 bg-card/72">
                {templates.map((template) => {
                  const isActive = template.id === selectedId;
                  return (
                    <button
                      key={template.id}
                      type="button"
                      onClick={() => { setSelectedId(template.id); setCreationMode(null); }}
                      className={`w-full px-4 py-3.5 text-left transition-[transform,background-color] duration-200 [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/70 ${
                        isActive ? "bg-primary/[0.08]" : "hover:-translate-y-0.5 hover:bg-secondary/34"
                      }`}
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="min-w-0 space-y-0.5">
                          <div className="truncate text-sm font-semibold text-foreground">{template.name}</div>
                          {template.description && (
                            <p className="line-clamp-1 text-xs text-muted-foreground">{template.description}</p>
                          )}
                        </div>
                        <Badge variant="secondary" className="shrink-0">{template.fields.length} {t("documentEngine.fields")}</Badge>
                      </div>
                      <div className="mt-1.5 text-xs text-muted-foreground">
                        {t("common.labels.updated", { time: formatRelativeDate(template.updatedAtUtc, locale) })}
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Right panel */}
        <Card className="min-w-0 border-border/80 bg-card/86">
          <CardContent className="px-6 py-6">
            {!showRight ? (
              <EmptyRight t={t} onNew={openCreation} />
            ) : creationMode === "pick" ? (
              <CreationModePicker t={t} onPick={setCreationMode} onCancel={() => setCreationMode(null)} />
            ) : creationMode === "ai" ? (
              <AiCreateForm t={t} onDraft={handleDraftReady} onBack={() => setCreationMode("pick")} />
            ) : creationMode === "import" ? (
              <ImportFileForm t={t} onDraft={handleDraftReady} onBack={() => setCreationMode("pick")} />
            ) : creationMode === "manual" ? (
              <ManualCreateForm t={t} onCreated={handleCreated} onBack={() => setCreationMode("pick")} />
            ) : creationMode === "review" && pendingDraft ? (
              <ReviewAndSaveForm t={t} draft={pendingDraft} onCreated={handleCreated} onBack={() => setCreationMode("pick")} />
            ) : selectedId ? (
              <GeneratePanel templateId={selectedId} t={t} />
            ) : null}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

// ── Empty state ────────────────────────────────────────────────────────────

function EmptyRight({ t, onNew }: { t: (k: string) => string; onNew: () => void }) {
  return (
    <div className="flex h-full min-h-[240px] flex-col items-center justify-center gap-4 text-center">
      <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
        <FileOutput className="size-6" />
      </div>
      <p className="text-sm text-muted-foreground">{t("documentEngine.selectTemplateHint")}</p>
      <Button variant="outline" size="sm" onClick={onNew}>
        <FilePlus className="size-3.5" />
        {t("documentEngine.newTemplate")}
      </Button>
    </div>
  );
}

// ── Mode picker ────────────────────────────────────────────────────────────

function CreationModePicker({ t, onPick, onCancel }: { t: (k: string) => string; onPick: (m: CreationMode) => void; onCancel: () => void }) {
  const modes = [
    {
      id: "ai" as CreationMode,
      icon: <Bot className="size-6" />,
      label: t("documentEngine.modeAiTitle"),
      desc: t("documentEngine.modeAiDesc"),
    },
    {
      id: "import" as CreationMode,
      icon: <Upload className="size-6" />,
      label: t("documentEngine.modeImportTitle"),
      desc: t("documentEngine.modeImportDesc"),
    },
    {
      id: "manual" as CreationMode,
      icon: <Pencil className="size-6" />,
      label: t("documentEngine.modeManualTitle"),
      desc: t("documentEngine.modeManualDesc"),
    },
  ];

  return (
    <div className="space-y-5">
      <div>
        <Badge variant="secondary"><FilePlus className="size-3.5" />{t("documentEngine.newTemplate")}</Badge>
        <h2 className="mt-2 text-base font-semibold text-foreground">{t("documentEngine.howToCreate")}</h2>
      </div>
      <div className="grid gap-3">
        {modes.map((mode) => (
          <button
            key={mode.id}
            type="button"
            onClick={() => onPick(mode.id)}
            className="flex items-start gap-4 rounded-[20px] border border-border/80 bg-card/72 px-5 py-4 text-left transition-[transform,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-[0_8px_24px_-12px_rgba(66,116,194,0.18)]"
          >
            <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-primary/8 text-primary">
              {mode.icon}
            </div>
            <div>
              <div className="text-sm font-semibold text-foreground">{mode.label}</div>
              <p className="mt-0.5 text-sm text-muted-foreground">{mode.desc}</p>
            </div>
          </button>
        ))}
      </div>
      <Button variant="ghost" size="sm" onClick={onCancel}>{t("common.actions.cancel")}</Button>
    </div>
  );
}

// ── AI create form ─────────────────────────────────────────────────────────

function AiCreateForm({ t, onDraft, onBack }: { t: (k: string) => string; onDraft: (d: TemplateDraft) => void; onBack: () => void }) {
  const [description, setDescription] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const draft = await apiRequest<TemplateDraft>("/api/document-templates/ai-suggest", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ description }),
      });
      onDraft(draft);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("documentEngine.generateError"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div>
        <Badge><Bot className="size-3.5" />{t("documentEngine.modeAiTitle")}</Badge>
        <h2 className="mt-2 text-base font-semibold text-foreground">{t("documentEngine.aiDescribeTitle")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("documentEngine.aiDescribeHint")}</p>
      </div>
      <div className="space-y-2">
        <textarea
          className="min-h-[100px] w-full rounded-[14px] border border-input bg-transparent px-4 py-3 text-sm leading-6 text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          placeholder={t("documentEngine.aiDescribePlaceholder")}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          required
          disabled={loading}
        />
      </div>
      {error && <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{error}</p>}
      <div className="flex gap-3">
        <Button type="submit" className="flex-1 justify-between" disabled={loading || !description.trim()}>
          {loading ? <><Loader2 className="size-4 animate-spin" />{t("documentEngine.generating")}</> : <><Sparkles className="size-4" />{t("documentEngine.aiGenerateButton")}</>}
        </Button>
        <Button type="button" variant="outline" onClick={onBack} disabled={loading}>{t("documentEngine.back")}</Button>
      </div>
    </form>
  );
}

// ── Import file form ───────────────────────────────────────────────────────

function ImportFileForm({ t, onDraft, onBack }: { t: (k: string) => string; onDraft: (d: TemplateDraft) => void; onBack: () => void }) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const file = fileRef.current?.files?.[0];
    if (!file) return;
    setError(null);
    setLoading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const draft = await apiRequest<TemplateDraft>("/api/document-templates/import-file", {
        method: "POST",
        body: form,
      });
      onDraft(draft);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("documentEngine.generateError"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div>
        <Badge variant="secondary"><Upload className="size-3.5" />{t("documentEngine.modeImportTitle")}</Badge>
        <h2 className="mt-2 text-base font-semibold text-foreground">{t("documentEngine.importTitle")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("documentEngine.importHint")}</p>
      </div>
      <div
        onClick={() => fileRef.current?.click()}
        className="flex cursor-pointer flex-col items-center gap-3 rounded-[20px] border-2 border-dashed border-border/75 bg-card/56 px-6 py-8 text-center transition-colors hover:border-primary/40 hover:bg-primary/[0.03]"
      >
        <div className="flex size-10 items-center justify-center rounded-2xl bg-primary/8 text-primary">
          <FileText className="size-5" />
        </div>
        <div>
          <p className="text-sm font-medium text-foreground">{fileName ?? t("documentEngine.dropFileHint")}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">PDF, DOCX, TXT, MD</p>
        </div>
        <input
          ref={fileRef}
          type="file"
          accept=".pdf,.docx,.txt,.md"
          className="hidden"
          onChange={(e) => setFileName(e.target.files?.[0]?.name ?? null)}
          disabled={loading}
        />
      </div>
      {error && <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{error}</p>}
      <div className="flex gap-3">
        <Button type="submit" className="flex-1 justify-between" disabled={loading || !fileName}>
          {loading ? <><Loader2 className="size-4 animate-spin" />{t("documentEngine.generating")}</> : <><Upload className="size-4" />{t("documentEngine.importButton")}</>}
        </Button>
        <Button type="button" variant="outline" onClick={onBack} disabled={loading}>{t("documentEngine.back")}</Button>
      </div>
    </form>
  );
}

// ── Review & save form ─────────────────────────────────────────────────────

function ReviewAndSaveForm({ t, draft, onCreated, onBack }: { t: (k: string) => string; draft: TemplateDraft; onCreated: (id: string) => void; onBack: () => void }) {
  const createMutation = useCreateDocumentTemplateMutation();
  const [name, setName] = useState(draft.name);
  const [body, setBody] = useState(draft.body);
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const template = await createMutation.mutateAsync({
        name,
        description: description || null,
        body,
        fields: draft.fields,
      });
      onCreated(template.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("documentEngine.createError"));
    }
  }

  return (
    <form onSubmit={handleSave} className="space-y-4">
      <div>
        <Badge><Sparkles className="size-3.5" />{t("documentEngine.reviewTitle")}</Badge>
        <p className="mt-2 text-sm text-muted-foreground">{t("documentEngine.reviewHint")}</p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="rev-name">{t("documentEngine.templateName")}</Label>
        <Input id="rev-name" value={name} onChange={(e) => setName(e.target.value)} required disabled={createMutation.isPending} />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="rev-desc">{t("documentEngine.templateDescription")}</Label>
        <Input id="rev-desc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder={t("documentEngine.templateDescriptionPlaceholder")} disabled={createMutation.isPending} />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="rev-body">{t("documentEngine.templateBody")}</Label>
        <textarea
          id="rev-body"
          className="min-h-[180px] w-full rounded-[14px] border border-input bg-transparent px-4 py-3 text-sm leading-6 text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          required
          disabled={createMutation.isPending}
        />
        <p className="text-xs text-muted-foreground">{t("documentEngine.templateBodyHint")}</p>
      </div>
      {draft.fields.length > 0 && (
        <div className="rounded-[16px] border border-border/75 bg-card/56 px-4 py-3">
          <p className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted-foreground">{t("documentEngine.detectedFields")}</p>
          <div className="flex flex-wrap gap-2">
            {draft.fields.map((f) => (
              <span key={f.key} className="rounded-full border border-border/70 bg-card px-3 py-1 text-xs text-foreground">
                {`{{${f.key}}}`} — {f.label}
              </span>
            ))}
          </div>
        </div>
      )}
      {error && <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{error}</p>}
      <div className="flex gap-3">
        <Button type="submit" className="flex-1 justify-between" disabled={createMutation.isPending}>
          {createMutation.isPending ? t("documentEngine.creating") : t("documentEngine.createButton")}
          <FilePlus className="size-4" />
        </Button>
        <Button type="button" variant="outline" onClick={onBack} disabled={createMutation.isPending}>{t("documentEngine.back")}</Button>
      </div>
    </form>
  );
}

// ── Manual create form ─────────────────────────────────────────────────────

interface FieldDraft { key: string; label: string; type: string; isRequired: boolean; }

function ManualCreateForm({ t, onCreated, onBack }: { t: (k: string) => string; onCreated: (id: string) => void; onBack: () => void }) {
  const createMutation = useCreateDocumentTemplateMutation();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [body, setBody] = useState("");
  const [fields, setFields] = useState<FieldDraft[]>([{ key: "", label: "", type: "Text", isRequired: true }]);
  const [error, setError] = useState<string | null>(null);

  function addField() { setFields((p) => [...p, { key: "", label: "", type: "Text", isRequired: false }]); }
  function updateField(i: number, patch: Partial<FieldDraft>) { setFields((p) => p.map((f, idx) => idx === i ? { ...f, ...patch } : f)); }
  function removeField(i: number) { setFields((p) => p.filter((_, idx) => idx !== i)); }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const template = await createMutation.mutateAsync({ name, description: description || null, body, fields: fields.filter((f) => f.key && f.label) });
      onCreated(template.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("documentEngine.createError"));
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <Badge variant="secondary"><Pencil className="size-3.5" />{t("documentEngine.modeManualTitle")}</Badge>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="m-name">{t("documentEngine.templateName")}</Label>
          <Input id="m-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={t("documentEngine.templateNamePlaceholder")} required disabled={createMutation.isPending} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="m-desc">{t("documentEngine.templateDescription")}</Label>
          <Input id="m-desc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder={t("documentEngine.templateDescriptionPlaceholder")} disabled={createMutation.isPending} />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="m-body">{t("documentEngine.templateBody")}</Label>
        <textarea
          id="m-body"
          className="min-h-[140px] w-full rounded-[14px] border border-input bg-transparent px-4 py-3 text-sm leading-6 text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
          value={body} onChange={(e) => setBody(e.target.value)}
          placeholder={t("documentEngine.templateBodyPlaceholder")} required disabled={createMutation.isPending}
        />
        <p className="text-xs text-muted-foreground">{t("documentEngine.templateBodyHint")}</p>
      </div>
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>{t("documentEngine.templateFields")}</Label>
          <Button type="button" variant="outline" size="sm" onClick={addField}>{t("documentEngine.addField")}</Button>
        </div>
        {fields.map((field, i) => (
          <div key={i} className="grid grid-cols-[1fr_1fr_auto_auto] items-end gap-2">
            <div className="space-y-1"><Label className="text-xs">{t("documentEngine.fieldKey")}</Label>
              <Input value={field.key} onChange={(e) => updateField(i, { key: e.target.value })} placeholder="nome_funcionario" disabled={createMutation.isPending} /></div>
            <div className="space-y-1"><Label className="text-xs">{t("documentEngine.fieldLabel")}</Label>
              <Input value={field.label} onChange={(e) => updateField(i, { label: e.target.value })} placeholder="Nome do Funcionário" disabled={createMutation.isPending} /></div>
            <select className="h-10 rounded-[10px] border border-input bg-transparent px-3 text-sm text-foreground" value={field.type} onChange={(e) => updateField(i, { type: e.target.value })} disabled={createMutation.isPending}>
              <option value="Text">Texto</option><option value="Date">Data</option><option value="Number">Número</option>
            </select>
            <Button type="button" variant="ghost" size="sm" onClick={() => removeField(i)} disabled={createMutation.isPending || fields.length === 1}>✕</Button>
          </div>
        ))}
      </div>
      {error && <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{error}</p>}
      <div className="flex gap-3">
        <Button type="submit" className="flex-1 justify-between" disabled={createMutation.isPending}>
          {createMutation.isPending ? t("documentEngine.creating") : t("documentEngine.createButton")}
          <FilePlus className="size-4" />
        </Button>
        <Button type="button" variant="outline" onClick={onBack} disabled={createMutation.isPending}>{t("documentEngine.back")}</Button>
      </div>
    </form>
  );
}

// ── Generate panel ─────────────────────────────────────────────────────────

function GeneratePanel({ templateId, t }: { templateId: string; t: (k: string) => string }) {
  const templateQuery = useDocumentTemplateQuery(templateId);
  const generateMutation = useGenerateDocumentMutation();
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const template = templateQuery.data;

  async function handleGenerate() {
    if (!template) return;
    setErrorMessage(null);
    try {
      const blob = await generateMutation.mutateAsync({ templateId, payload: { fieldValues } });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = `${template.slug}-${Date.now()}.pdf`; a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setErrorMessage(err instanceof ApiError ? err.message : t("documentEngine.generateError"));
    }
  }

  if (templateQuery.isLoading) return <LoadingState title={t("documentEngine.loadingTitle")} description={t("documentEngine.loadingDescription")} />;
  if (templateQuery.isError || !template) return <ErrorState title={t("documentEngine.errorTitle")} description={t("documentEngine.errorDescription")} />;

  return (
    <div className="space-y-5">
      <div>
        <Badge><Sparkles className="size-3.5" />{t("documentEngine.generateLabel")}</Badge>
        <h2 className="mt-2 text-base font-semibold text-foreground">{template.name}</h2>
        {template.description && <p className="mt-1 text-sm text-muted-foreground">{template.description}</p>}
      </div>
      <div className="space-y-3">
        {template.fields.map((field) => (
          <div key={field.key} className="space-y-1.5">
            <Label htmlFor={field.key}>
              {field.label}{field.isRequired && <span className="ml-1 text-destructive">*</span>}
            </Label>
            <Input
              id={field.key}
              type={field.type === "Date" ? "date" : field.type === "Number" ? "number" : "text"}
              placeholder={field.label}
              value={fieldValues[field.key] ?? ""}
              onChange={(e) => setFieldValues((p) => ({ ...p, [field.key]: e.target.value }))}
              disabled={generateMutation.isPending}
            />
          </div>
        ))}
      </div>
      {errorMessage && <p className="rounded-[14px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">{errorMessage}</p>}
      <Button className="w-full justify-between" onClick={handleGenerate} disabled={generateMutation.isPending}>
        {generateMutation.isPending
          ? <><Loader2 className="size-4 animate-spin" />{t("documentEngine.generating")}</>
          : <>{t("documentEngine.generateButton")}<Download className="size-4" /></>}
      </Button>
    </div>
  );
}
