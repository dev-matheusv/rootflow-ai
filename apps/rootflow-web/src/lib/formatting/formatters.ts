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
