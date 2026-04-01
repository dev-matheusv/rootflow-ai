import {
  Bot,
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
    label: "Dashboard",
    caption: "Overview and product signals",
  },
  {
    id: "knowledge-base",
    to: "/knowledge-base",
    icon: Sparkles,
    label: "Knowledge Base",
    caption: "Documents and ingestion status",
  },
  {
    id: "assistant",
    to: "/assistant",
    icon: Bot,
    label: "Assistant",
    caption: "Grounded answers and sources",
  },
  {
    id: "conversations",
    to: "/conversations",
    icon: MessagesSquare,
    label: "Conversations",
    caption: "Stored sessions and trails",
  },
  {
    id: "settings",
    to: "/settings",
    icon: Settings2,
    label: "Settings",
    caption: "Workspace controls",
  },
] as const;
