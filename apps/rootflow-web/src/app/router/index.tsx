import { createBrowserRouter, Navigate } from "react-router-dom";

import { AppShell } from "@/app/layouts/app-shell";
import { AssistantPage } from "@/features/assistant/page";
import { AcceptInvitePage } from "@/features/auth/pages/accept-invite-page";
import { ForgotPasswordPage } from "@/features/auth/pages/forgot-password-page";
import { LoginPage } from "@/features/auth/pages/login-page";
import { ResetPasswordPage } from "@/features/auth/pages/reset-password-page";
import { SignUpPage } from "@/features/auth/pages/sign-up-page";
import { ConversationsPage } from "@/features/conversations/page";
import { DashboardPage } from "@/features/dashboard/page";
import { KnowledgeBasePage } from "@/features/knowledge-base/page";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <AppShell />,
    children: [
      {
        index: true,
        element: <Navigate replace to="/dashboard" />,
      },
      {
        path: "dashboard",
        element: <DashboardPage />,
        handle: {
          title: "Dashboard",
          subtitle: "Premium command center for the RootFlow product workspace.",
        },
      },
      {
        path: "knowledge-base",
        element: <KnowledgeBasePage />,
        handle: {
          title: "Knowledge Base",
          subtitle: "Curate documents and ingestion states with a client-ready interface.",
        },
      },
      {
        path: "assistant",
        element: <AssistantPage />,
        handle: {
          title: "Assistant",
          subtitle: "Grounded answer experience with a premium conversation surface.",
        },
      },
      {
        path: "conversations",
        element: <ConversationsPage />,
        handle: {
          title: "Conversations",
          subtitle: "Readable answer trails and session history designed for business teams.",
        },
      },
    ],
  },
  {
    path: "/auth/login",
    element: <LoginPage />,
  },
  {
    path: "/auth/signup",
    element: <SignUpPage />,
  },
  {
    path: "/auth/forgot-password",
    element: <ForgotPasswordPage />,
  },
  {
    path: "/auth/reset-password",
    element: <ResetPasswordPage />,
  },
  {
    path: "/auth/invite",
    element: <AcceptInvitePage />,
  },
]);
