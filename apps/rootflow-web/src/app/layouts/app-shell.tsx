import * as Dialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { useState } from "react";
import { Outlet } from "react-router-dom";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { ShellSidebar } from "@/components/navigation/shell-sidebar";
import { Topbar } from "@/components/navigation/topbar";
import { Button } from "@/components/ui/button";
import { useMediaQuery } from "@/hooks/use-media-query";
import { cn } from "@/lib/utils";

export function AppShell() {
  const isDesktop = useMediaQuery("(min-width: 1024px)");
  const [isDesktopSidebarCollapsed, setIsDesktopSidebarCollapsed] = useState(false);
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

  return (
    <div className="relative min-h-screen overflow-hidden bg-background text-foreground">
      <div className="pointer-events-none absolute inset-0 opacity-[0.42]">
        <div className="absolute left-[-10%] top-[-14%] h-[30rem] w-[30rem] rounded-full bg-[radial-gradient(circle,_rgba(66,132,255,0.08),transparent_64%)] blur-3xl" />
        <div className="absolute right-[-8%] top-[12%] h-[24rem] w-[24rem] rounded-full bg-[radial-gradient(circle,_rgba(168,217,255,0.1),transparent_60%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(54,74,110,0.12),transparent_60%)]" />
        <div className="absolute bottom-[-18%] left-[28%] h-[22rem] w-[22rem] rounded-full bg-[radial-gradient(circle,_rgba(65,104,186,0.05),transparent_66%)] blur-3xl dark:bg-[radial-gradient(circle,_rgba(43,62,94,0.09),transparent_66%)]" />
      </div>

      <div className="relative mx-auto flex min-h-screen max-w-[1680px]">
        {isDesktop ? (
          <aside
            className={cn(
              "hidden min-h-screen shrink-0 border-r border-sidebar-border/70 bg-sidebar/82 px-4 py-6 shadow-[14px_0_40px_-42px_rgba(15,37,79,0.16)] backdrop-blur-2xl lg:flex lg:flex-col",
              isDesktopSidebarCollapsed ? "w-[112px]" : "w-[292px]",
            )}
          >
            <ShellSidebar collapsed={isDesktopSidebarCollapsed} />
          </aside>
        ) : null}

        <div className="flex min-h-screen min-w-0 flex-1 flex-col">
          <Topbar
            isDesktop={isDesktop}
            isSidebarCollapsed={isDesktopSidebarCollapsed}
            onOpenNavigation={() => setIsMobileSidebarOpen(true)}
            onToggleSidebar={() => setIsDesktopSidebarCollapsed((value) => !value)}
          />
          <main className="flex-1 px-4 pb-8 pt-5 sm:px-6 sm:pb-10 sm:pt-6 lg:px-8 lg:pb-12 lg:pt-7">
            <div className="mx-auto max-w-[1320px] space-y-6">
              <Outlet />
            </div>
          </main>
        </div>
      </div>

      <Dialog.Root open={!isDesktop && isMobileSidebarOpen} onOpenChange={setIsMobileSidebarOpen}>
        <Dialog.Portal>
          <Dialog.Overlay className="fixed inset-0 z-40 bg-slate-950/32 backdrop-blur-sm lg:hidden" />
          <Dialog.Content className="fixed inset-y-0 left-0 z-50 flex w-full max-w-[320px] flex-col border-r border-border/75 bg-background/94 p-5 shadow-[0_24px_52px_-30px_rgba(15,37,79,0.24)] backdrop-blur-2xl focus:outline-none lg:hidden">
            <Dialog.Title className="sr-only">Navigation</Dialog.Title>
            <div className="flex items-center justify-between">
              <RootFlowBrand variant="logo" size="sm" className="h-9" />
              <Dialog.Close asChild>
                <Button variant="outline" size="icon" aria-label="Close navigation">
                  <X />
                </Button>
              </Dialog.Close>
            </div>

            <div className="mt-6 flex-1 overflow-y-auto">
              <ShellSidebar onNavigate={() => setIsMobileSidebarOpen(false)} showBrand={false} />
            </div>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog.Root>
    </div>
  );
}
