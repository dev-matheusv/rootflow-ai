import { Download, FileOutput, FilePlus, Loader2, Sparkles } from "lucide-react";
import { useState } from "react";

import { useI18n } from "@/app/providers/i18n-provider";
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
import type { DocumentTemplateSummary } from "@/lib/api/contracts";
import { ApiError } from "@/lib/api/client";
import { formatRelativeDate } from "@/lib/formatting/formatters";

export function DocumentEnginePage() {
  const { locale, t } = useI18n();
  const templatesQuery = useDocumentTemplatesQuery();
  const templates = templatesQuery.data ?? [];
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showNewForm, setShowNewForm] = useState(false);

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
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setShowNewForm(true);
                    setSelectedId(null);
                  }}
                >
                  <FilePlus className="size-3.5" />
                  {t("documentEngine.newTemplate")}
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {templatesQuery.isLoading ? (
              <LoadingState title={t("documentEngine.loadingTitle")} description={t("documentEngine.loadingDescription")} />
            ) : templatesQuery.isError ? (
              <ErrorState
                title={t("documentEngine.errorTitle")}
                description={t("documentEngine.errorDescription")}
                onRetry={() => templatesQuery.refetch()}
              />
            ) : templates.length === 0 && !showNewForm ? (
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
                      onClick={() => {
                        setSelectedId(template.id);
                        setShowNewForm(false);
                      }}
                      className={`w-full px-4 py-3.5 text-left transition-[transform,background-color,border-color,box-shadow] duration-200 [&:not(:last-child)]:border-b [&:not(:last-child)]:border-border/70 ${
                        isActive
                          ? "bg-primary/[0.08]"
                          : "hover:-translate-y-0.5 hover:bg-secondary/34 hover:shadow-[inset_0_1px_0_rgba(255,255,255,0.2)]"
                      }`}
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="min-w-0 space-y-1">
                          <div className="truncate text-sm font-semibold text-foreground">{template.name}</div>
                          {template.description && (
                            <p className="line-clamp-1 text-sm leading-6 text-muted-foreground">{template.description}</p>
                          )}
                        </div>
                        <Badge variant="secondary">{template.fields.length} {t("documentEngine.fields")}</Badge>
                      </div>
                      <div className="mt-2 text-xs text-muted-foreground">
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
            {showNewForm ? (
              <NewTemplateForm
                onCancel={() => setShowNewForm(false)}
                onCreated={(id) => {
                  setShowNewForm(false);
                  setSelectedId(id);
                }}
              />
            ) : selectedId ? (
              <GeneratePanel templateId={selectedId} />
            ) : (
              <div className="flex h-full min-h-[240px] flex-col items-center justify-center gap-3 text-center">
                <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/8 text-primary">
                  <FileOutput className="size-6" />
                </div>
                <p className="text-sm text-muted-foreground">{t("documentEngine.selectTemplateHint")}</p>
              </div>
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

// ── Generate panel ─────────────────────────────────────────────────────────

function GeneratePanel({ templateId }: { templateId: string }) {
  const { t } = useI18n();
  const templateQuery = useDocumentTemplateQuery(templateId);
  const generateMutation = useGenerateDocumentMutation();
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const template = templateQuery.data;

  function handleFieldChange(key: string, value: string) {
    setFieldValues((prev) => ({ ...prev, [key]: value }));
  }

  async function handleGenerate() {
    if (!template) return;
    setErrorMessage(null);

    try {
      const blob = await generateMutation.mutateAsync({ templateId, payload: { fieldValues } });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${template.slug}-${Date.now()}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("documentEngine.generateError"));
    }
  }

  if (templateQuery.isLoading) {
    return <LoadingState title={t("documentEngine.loadingTitle")} description={t("documentEngine.loadingDescription")} />;
  }

  if (templateQuery.isError || !template) {
    return <ErrorState title={t("documentEngine.errorTitle")} description={t("documentEngine.errorDescription")} />;
  }

  return (
    <div className="space-y-5">
      <div>
        <div className="flex items-center gap-2">
          <Badge>
            <Sparkles className="size-3.5" />
            {t("documentEngine.generateLabel")}
          </Badge>
        </div>
        <h2 className="mt-2 text-base font-semibold text-foreground">{template.name}</h2>
        {template.description && (
          <p className="mt-1 text-sm text-muted-foreground">{template.description}</p>
        )}
      </div>

      <div className="space-y-3">
        {template.fields.map((field) => (
          <div key={field.key} className="space-y-1.5">
            <Label htmlFor={field.key}>
              {field.label}
              {field.isRequired && <span className="ml-1 text-destructive">*</span>}
            </Label>
            <Input
              id={field.key}
              type={field.type === "Date" ? "date" : field.type === "Number" ? "number" : "text"}
              placeholder={field.label}
              value={fieldValues[field.key] ?? ""}
              onChange={(e) => handleFieldChange(field.key, e.target.value)}
              disabled={generateMutation.isPending}
            />
          </div>
        ))}
      </div>

      {errorMessage && (
        <div className="rounded-[18px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
          {errorMessage}
        </div>
      )}

      <Button
        className="w-full justify-between"
        onClick={handleGenerate}
        disabled={generateMutation.isPending}
      >
        {generateMutation.isPending ? (
          <>
            <Loader2 className="size-4 animate-spin" />
            {t("documentEngine.generating")}
          </>
        ) : (
          <>
            {t("documentEngine.generateButton")}
            <Download className="size-4" />
          </>
        )}
      </Button>
    </div>
  );
}

// ── New template form ──────────────────────────────────────────────────────

interface NewTemplateFormProps {
  onCancel: () => void;
  onCreated: (id: string) => void;
}

interface FieldDraft {
  key: string;
  label: string;
  type: string;
  isRequired: boolean;
}

function NewTemplateForm({ onCancel, onCreated }: NewTemplateFormProps) {
  const { t } = useI18n();
  const createMutation = useCreateDocumentTemplateMutation();
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [description, setDescription] = useState("");
  const [body, setBody] = useState("");
  const [fields, setFields] = useState<FieldDraft[]>([
    { key: "", label: "", type: "Text", isRequired: true },
  ]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  function handleNameChange(value: string) {
    setName(value);
    if (!slug) {
      setSlug(value.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, ""));
    }
  }

  function addField() {
    setFields((prev) => [...prev, { key: "", label: "", type: "Text", isRequired: false }]);
  }

  function updateField(index: number, patch: Partial<FieldDraft>) {
    setFields((prev) => prev.map((f, i) => (i === index ? { ...f, ...patch } : f)));
  }

  function removeField(index: number) {
    setFields((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErrorMessage(null);

    try {
      const template = await createMutation.mutateAsync({
        name,
        slug,
        description: description || null,
        body,
        fields: fields.filter((f) => f.key && f.label),
      });
      onCreated(template.id);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("documentEngine.createError"));
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div>
        <Badge variant="secondary">
          <FilePlus className="size-3.5" />
          {t("documentEngine.newTemplate")}
        </Badge>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="tpl-name">{t("documentEngine.templateName")}</Label>
          <Input
            id="tpl-name"
            value={name}
            onChange={(e) => handleNameChange(e.target.value)}
            placeholder={t("documentEngine.templateNamePlaceholder")}
            required
            disabled={createMutation.isPending}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="tpl-slug">{t("documentEngine.templateSlug")}</Label>
          <Input
            id="tpl-slug"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            placeholder="hr-certificate"
            required
            disabled={createMutation.isPending}
          />
        </div>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="tpl-desc">{t("documentEngine.templateDescription")}</Label>
        <Input
          id="tpl-desc"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder={t("documentEngine.templateDescriptionPlaceholder")}
          disabled={createMutation.isPending}
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="tpl-body">{t("documentEngine.templateBody")}</Label>
        <textarea
          id="tpl-body"
          className="min-h-[140px] w-full rounded-[12px] border border-input bg-transparent px-4 py-3 text-sm leading-6 text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder={t("documentEngine.templateBodyPlaceholder")}
          required
          disabled={createMutation.isPending}
        />
        <p className="text-xs text-muted-foreground">{t("documentEngine.templateBodyHint")}</p>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <Label>{t("documentEngine.templateFields")}</Label>
          <Button type="button" variant="outline" size="sm" onClick={addField}>
            {t("documentEngine.addField")}
          </Button>
        </div>
        {fields.map((field, index) => (
          <div key={index} className="grid grid-cols-[1fr_1fr_auto_auto] items-end gap-2">
            <div className="space-y-1">
              <Label className="text-xs">{t("documentEngine.fieldKey")}</Label>
              <Input
                value={field.key}
                onChange={(e) => updateField(index, { key: e.target.value })}
                placeholder="employee_name"
                disabled={createMutation.isPending}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">{t("documentEngine.fieldLabel")}</Label>
              <Input
                value={field.label}
                onChange={(e) => updateField(index, { label: e.target.value })}
                placeholder="Nome do funcionário"
                disabled={createMutation.isPending}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">{t("documentEngine.fieldType")}</Label>
              <select
                className="h-10 rounded-[10px] border border-input bg-transparent px-3 text-sm text-foreground"
                value={field.type}
                onChange={(e) => updateField(index, { type: e.target.value })}
                disabled={createMutation.isPending}
              >
                <option value="Text">Text</option>
                <option value="Date">Date</option>
                <option value="Number">Number</option>
              </select>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="mb-0.5"
              onClick={() => removeField(index)}
              disabled={createMutation.isPending || fields.length === 1}
            >
              ✕
            </Button>
          </div>
        ))}
      </div>

      {errorMessage && (
        <div className="rounded-[18px] border border-destructive/20 bg-destructive/8 px-4 py-3 text-sm text-destructive">
          {errorMessage}
        </div>
      )}

      <div className="flex gap-3">
        <Button
          type="submit"
          className="flex-1 justify-between"
          disabled={createMutation.isPending}
        >
          {createMutation.isPending ? t("documentEngine.creating") : t("documentEngine.createButton")}
          <FilePlus className="size-4" />
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={createMutation.isPending}>
          {t("common.actions.cancel")}
        </Button>
      </div>
    </form>
  );
}
