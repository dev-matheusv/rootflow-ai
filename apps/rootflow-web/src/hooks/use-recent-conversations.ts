import { useCallback, useMemo, useState } from "react";

const storageKey = "rootflow.recent-conversations";

export interface RecentConversationRecord {
  id: string;
  title: string;
  preview: string;
  updatedAt: string;
}

function readStorage(): RecentConversationRecord[] {
  if (typeof window === "undefined") {
    return [];
  }

  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter(isConversationRecord);
  } catch {
    return [];
  }
}

function writeStorage(items: RecentConversationRecord[]) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(storageKey, JSON.stringify(items));
}

function isConversationRecord(value: unknown): value is RecentConversationRecord {
  return (
    typeof value === "object" &&
    value !== null &&
    "id" in value &&
    typeof value.id === "string" &&
    "title" in value &&
    typeof value.title === "string" &&
    "preview" in value &&
    typeof value.preview === "string" &&
    "updatedAt" in value &&
    typeof value.updatedAt === "string"
  );
}

export function useRecentConversations() {
  const [items, setItems] = useState<RecentConversationRecord[]>(() => readStorage());

  const upsertConversation = useCallback((record: RecentConversationRecord) => {
    setItems((current) => {
      const next = [record, ...current.filter((item) => item.id !== record.id)].slice(0, 12);
      writeStorage(next);
      return next;
    });
  }, []);

  const clearConversations = useCallback(() => {
    writeStorage([]);
    setItems([]);
  }, []);

  return useMemo(
    () => ({
      items,
      upsertConversation,
      clearConversations,
    }),
    [clearConversations, items, upsertConversation],
  );
}
