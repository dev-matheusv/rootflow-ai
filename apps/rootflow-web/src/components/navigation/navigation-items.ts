import {
  Bot,
  CreditCard,
  LayoutDashboard,
  MessagesSquare,
  Settings2,
  Sparkles,
} from "lucide-react";

export const navigationItems = [
  {
    id: "dashboard",
    to: "/dashboard",
    icon: LayoutDashboard,
    labelKey: "nav.dashboard",
    captionKey: "nav.dashboardCaption",
  },
  {
    id: "knowledge-base",
    to: "/knowledge-base",
    icon: Sparkles,
    labelKey: "nav.knowledgeBase",
    captionKey: "nav.knowledgeBaseCaption",
  },
  {
    id: "assistant",
    to: "/assistant",
    icon: Bot,
    labelKey: "nav.assistant",
    captionKey: "nav.assistantCaption",
  },
  {
    id: "conversations",
    to: "/conversations",
    icon: MessagesSquare,
    labelKey: "nav.conversations",
    captionKey: "nav.conversationsCaption",
  },
  {
    id: "billing",
    to: "/billing",
    icon: CreditCard,
    labelKey: "nav.billing",
    captionKey: "nav.billingCaption",
  },
  {
    id: "settings",
    to: "/settings",
    icon: Settings2,
    labelKey: "nav.settings",
    captionKey: "nav.settingsCaption",
  },
] as const;
