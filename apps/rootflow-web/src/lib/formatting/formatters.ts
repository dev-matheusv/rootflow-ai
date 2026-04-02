export function formatRelativeDate(input: string | Date): string {
  const value = typeof input === "string" ? new Date(input) : input;
  const deltaMs = Date.now() - value.getTime();
  const deltaMinutes = Math.round(deltaMs / 60000);

  if (deltaMinutes < 1) {
    return "Just now";
  }

  if (deltaMinutes < 60) {
    return `${deltaMinutes}m ago`;
  }

  const deltaHours = Math.round(deltaMinutes / 60);
  if (deltaHours < 24) {
    return `${deltaHours}h ago`;
  }

  const deltaDays = Math.round(deltaHours / 24);
  if (deltaDays === 1) {
    return "Yesterday";
  }

  return `${deltaDays}d ago`;
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

export function getDocumentStatusLabel(status: number): string {
  switch (status) {
    case 1:
      return "Uploaded";
    case 2:
      return "Processing";
    case 3:
      return "Processed";
    case 4:
      return "Failed";
    default:
      return "Unknown";
  }
}
