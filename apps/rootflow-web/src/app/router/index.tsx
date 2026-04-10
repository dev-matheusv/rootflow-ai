import { createBrowserRouter, Navigate } from "react-router-dom";

import { AppShell } from "@/app/layouts/app-shell";
import { AdminPage } from "@/features/admin/page";
import { AssistantPage } from "@/features/assistant/page";
import { RedirectIfAuthenticated } from "@/features/auth/components/redirect-if-authenticated";
import { RequireAuth } from "@/features/auth/components/require-auth";
import { RequirePlatformAdmin } from "@/features/auth/components/require-platform-admin";
import { AcceptInvitePage } from "@/features/auth/pages/accept-invite-page";
import { ForgotPasswordPage } from "@/features/auth/pages/forgot-password-page";
import { LoginPage } from "@/features/auth/pages/login-page";
import { ResetPasswordPage } from "@/features/auth/pages/reset-password-page";
import { SignUpPage } from "@/features/auth/pages/sign-up-page";
import { BillingPage } from "@/features/billing/page";
import { ConversationsPage } from "@/features/conversations/page";
import { ConversationPrintPage } from "@/features/conversations/print-page";
import { DocumentEnginePage } from "@/features/document-engine/page";
import { DashboardPage } from "@/features/dashboard/page";
import { KnowledgeBasePage } from "@/features/knowledge-base/page";
import { LandingPage } from "@/features/landing/landing-page";
import { SettingsPage } from "@/features/settings/page";

export const router = createBrowserRouter([
  // Public landing page
  {
    path: "/",
    element: <LandingPage />,
  },

  // Protected app shell
  {
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
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
        path: "document-engine",
        element: <DocumentEnginePage />,
        handle: {
          title: "Documents",
          subtitle: "Generate professional PDFs from reusable templates.",
        },
      },
      {
        path: "billing",
        element: <BillingPage />,
        handle: {
          title: "Billing",
          subtitle: "Credits visibility and upgrade path placeholder.",
        },
      },
      {
        path: "admin",
        element: (
          <RequirePlatformAdmin>
            <AdminPage />
          </RequirePlatformAdmin>
        ),
        handle: {
          title: "Admin",
          subtitle: "Internal platform operations and billing telemetry.",
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

  // Conversation print page (auth required, no AppShell)
  {
    path: "/conversations/print",
    element: (
      <RequireAuth>
        <ConversationPrintPage />
      </RequireAuth>
    ),
  },

  // Auth routes (redirect if already authenticated)
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
    element: <AcceptInvitePage />,
  },

  // Fallback redirect
  {
    path: "*",
    element: <Navigate replace to="/" />,
  },
]);
