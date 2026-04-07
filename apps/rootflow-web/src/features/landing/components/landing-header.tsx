import { Menu, X } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";
import { useAuth } from "@/features/auth/auth-provider";

const NAV_LINKS = [
  { label: "Como funciona", href: "#como-funciona" },
  { label: "Casos de uso", href: "#casos-de-uso" },
  { label: "Preços", href: "#precos" },
  { label: "FAQ", href: "#faq" },
];

export function LandingHeader() {
  const { isAuthenticated } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <header className="fixed top-0 right-0 left-0 z-40 border-b border-white/[0.06] bg-[#060d15]/90 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Logo */}
        <Link to="/" className="flex items-center gap-2 shrink-0" aria-label="RootFlow">
          <RootFlowBrand variant="icon" size="sm" />
          <span className="font-display text-lg font-semibold tracking-tight text-white">RootFlow</span>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden items-center gap-6 md:flex">
          {NAV_LINKS.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="text-sm font-medium text-white/60 transition-colors hover:text-white"
            >
              {link.label}
            </a>
          ))}
        </nav>

        {/* Desktop CTAs */}
        <div className="hidden items-center gap-3 md:flex">
          {isAuthenticated ? (
            <Link
              to="/dashboard"
              className="rounded-xl bg-[#0f63ec] px-4 py-2 text-sm font-semibold text-white transition-all hover:bg-[#0f63ec]/90 hover:-translate-y-0.5"
            >
              Ir para o App
            </Link>
          ) : (
            <>
              <Link
                to="/auth/login"
                className="text-sm font-medium text-white/70 transition-colors hover:text-white"
              >
                Entrar
              </Link>
              <Link
                to="/auth/signup"
                className="rounded-xl bg-[#0f63ec] px-4 py-2 text-sm font-semibold text-white shadow-[0_0_24px_rgba(15,99,236,0.35)] transition-all hover:bg-[#0f63ec]/90 hover:-translate-y-0.5 hover:shadow-[0_0_32px_rgba(15,99,236,0.5)]"
              >
                Começar grátis
              </Link>
            </>
          )}
        </div>

        {/* Mobile menu toggle */}
        <button
          className="flex items-center justify-center rounded-lg p-2 text-white/70 transition-colors hover:bg-white/10 hover:text-white md:hidden"
          onClick={() => setMobileOpen(!mobileOpen)}
          aria-label="Menu"
        >
          {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="border-t border-white/[0.06] bg-[#060d15]/98 px-4 py-4 md:hidden">
          <nav className="flex flex-col gap-1">
            {NAV_LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={() => setMobileOpen(false)}
                className="rounded-lg px-3 py-2.5 text-sm font-medium text-white/70 transition-colors hover:bg-white/8 hover:text-white"
              >
                {link.label}
              </a>
            ))}
          </nav>
          <div className="mt-4 flex flex-col gap-2 border-t border-white/[0.06] pt-4">
            {isAuthenticated ? (
              <Link
                to="/dashboard"
                className="rounded-xl bg-[#0f63ec] px-4 py-3 text-center text-sm font-semibold text-white"
              >
                Ir para o App
              </Link>
            ) : (
              <>
                <Link
                  to="/auth/login"
                  className="rounded-xl border border-white/10 px-4 py-3 text-center text-sm font-medium text-white/80"
                >
                  Entrar
                </Link>
                <Link
                  to="/auth/signup"
                  className="rounded-xl bg-[#0f63ec] px-4 py-3 text-center text-sm font-semibold text-white"
                >
                  Começar grátis
                </Link>
              </>
            )}
          </div>
        </div>
      )}
    </header>
  );
}
