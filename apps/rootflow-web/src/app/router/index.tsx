import { createBrowserRouter, Navigate } from "react-router-dom";

import { AppShell } from "@/app/layouts/app-shell";
import { AssistantPage } from "@/features/assistant/page";
import { RedirectIfAuthenticated } from "@/features/auth/components/redirect-if-authenticated";
import { RequireAuth } from "@/features/auth/components/require-auth";
import { AcceptInvitePage } from "@/features/auth/pages/accept-invite-page";
import { ForgotPasswordPage } from "@/features/auth/pages/forgot-password-page";
import { LoginPage } from "@/features/auth/pages/login-page";
import { ResetPasswordPage } from "@/features/auth/pages/reset-password-page";
import { SignUpPage } from "@/features/auth/pages/sign-up-page";
import { ConversationsPage } from "@/features/conversations/page";
import { DashboardPage } from "@/features/dashboard/page";
import { KnowledgeBasePage } from "@/features/knowledge-base/page";
import { SettingsPage } from "@/features/settings/page";

export const router = createBrowserRouter([
  {
    path: "/",
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
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
      {
        path: "settings",
        element: <SettingsPage />,
        handle: {
          title: "Settings",
          subtitle: "Workspace and access controls are evolving into a real SaaS operating surface.",
        },
      },
    ],
  },
  {
    path: "/auth/login",
    element: (
      <RedirectIfAuthenticated>
        <LoginPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: "/auth/signup",
    element: (
      <RedirectIfAuthenticated>
        <SignUpPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: "/auth/forgot-password",
    element: (
      <RedirectIfAuthenticated>
        <ForgotPasswordPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: "/auth/reset-password",
    element: (
      <RedirectIfAuthenticated>
        <ResetPasswordPage />
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: "/auth/invite",
    element: (
      <RedirectIfAuthenticated>
        <AcceptInvitePage />
      </RedirectIfAuthenticated>
    ),
  },
]);
