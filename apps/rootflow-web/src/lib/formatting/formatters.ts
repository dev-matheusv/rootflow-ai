import { type AppLocale } from "@/lib/i18n/types";

export function formatRelativeDate(input: string | Date, locale: AppLocale = "en"): string {
  const value = typeof input === "string" ? new Date(input) : input;
  const deltaMs = Date.now() - value.getTime();
  const deltaMinutes = Math.round(deltaMs / 60000);
  const isPortuguese = locale === "pt-BR";

  if (deltaMinutes < 1) {
    return isPortuguese ? "Agora mesmo" : "Just now";
  }

  if (deltaMinutes < 60) {
    return isPortuguese ? `há ${deltaMinutes} min` : `${deltaMinutes}m ago`;
  }

  const deltaHours = Math.round(deltaMinutes / 60);
  if (deltaHours < 24) {
    return isPortuguese ? `há ${deltaHours} h` : `${deltaHours}h ago`;
  }

  const deltaDays = Math.round(deltaHours / 24);
  if (deltaDays === 1) {
    return isPortuguese ? "Ontem" : "Yesterday";
  }

  return isPortuguese ? `há ${deltaDays} d` : `${deltaDays}d ago`;
}

export function formatAbsoluteDate(input: string | Date, locale: AppLocale = "en"): string {
  const value = typeof input === "string" ? new Date(input) : input;
  const intlLocale = locale === "pt-BR" ? "pt-BR" : "en-US";

  return new Intl.DateTimeFormat(intlLocale, {
    year: "numeric",
    month: "long",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(value);
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes / 1024;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex++;
  }

  return `${value.toFixed(value >= 10 ? 0 : 1)} ${units[unitIndex]}`;
}

export function getDocumentTypeLabel(fileName: string, contentType: string): string {
  const normalizedFileName = fileName.toLowerCase();
  const normalizedContentType = contentType.toLowerCase();

  const extensionMap: Record<string, string> = {
    ".pdf": "PDF",
    ".docx": "DOCX",
    ".doc": "DOC",
    ".txt": "TXT",
    ".md": "MD",
    ".markdown": "MD",
    ".rtf": "RTF",
  };

  for (const [extension, label] of Object.entries(extensionMap)) {
    if (normalizedFileName.endsWith(extension)) {
      return label;
    }
  }

  const contentTypeMap: Record<string, string> = {
    "application/pdf": "PDF",
    "application/msword": "DOC",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "DOCX",
    "text/plain": "TXT",
    "text/markdown": "MD",
    "text/x-markdown": "MD",
    "application/rtf": "RTF",
  };

  if (contentTypeMap[normalizedContentType]) {
    return contentTypeMap[normalizedContentType];
  }

  const subtype = normalizedContentType.split("/")[1];
  if (!subtype) {
    return "File";
  }

  return subtype.toUpperCase();
}

export function getDocumentStatusLabel(status: number, locale: AppLocale = "en"): string {
  const labels =
    locale === "pt-BR"
      ? {
          uploaded: "Enviado",
          processing: "Processando",
          processed: "Processado",
          failed: "Falhou",
          unknown: "Desconhecido",
        }
      : {
          uploaded: "Uploaded",
          processing: "Processing",
          processed: "Processed",
          failed: "Failed",
          unknown: "Unknown",
        };

  switch (status) {
    case 1:
      return labels.uploaded;
    case 2:
      return labels.processing;
    case 3:
      return labels.processed;
    case 4:
      return labels.failed;
    default:
      return labels.unknown;
  }
}
