import { authMessages } from "@/lib/i18n/messages/auth";
import { commonMessages } from "@/lib/i18n/messages/common";
import { productMessages } from "@/lib/i18n/messages/product";

export const messages = {
  en: {
    ...commonMessages.en,
    ...productMessages.en,
    ...authMessages.en,
  },
  "pt-BR": {
    ...commonMessages["pt-BR"],
    ...productMessages["pt-BR"],
    ...authMessages["pt-BR"],
  },
} as const;

export type TranslationTree = typeof messages.en;
