import { AlertTriangle, CheckCircle2, Clock3, Upload } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { getDocumentStatusLabel } from "@/lib/formatting/formatters";

interface StatusBadgeProps {
  status: number;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  if (status === 3) {
    return (
      <Badge variant="success">
        <CheckCircle2 className="size-3.5" />
        {getDocumentStatusLabel(status)}
      </Badge>
    );
  }

  if (status === 4) {
    return (
      <Badge variant="warning">
        <AlertTriangle className="size-3.5" />
        {getDocumentStatusLabel(status)}
      </Badge>
    );
  }

  if (status === 2) {
    return (
      <Badge variant="secondary">
        <Clock3 className="size-3.5" />
        {getDocumentStatusLabel(status)}
      </Badge>
    );
  }

  return (
    <Badge variant="secondary">
      <Upload className="size-3.5" />
      {getDocumentStatusLabel(status)}
    </Badge>
  );
}
