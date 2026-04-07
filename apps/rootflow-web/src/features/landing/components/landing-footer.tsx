import { Link } from "react-router-dom";

import { RootFlowBrand } from "@/components/branding/rootflow-brand";

const FOOTER_LINKS = [
  {
    title: "Produto",
    links: [
      { label: "Como funciona", href: "#como-funciona" },
      { label: "Casos de uso", href: "#casos-de-uso" },
      { label: "Preços", href: "#precos" },
      { label: "FAQ", href: "#faq" },
    ],
  },
  {
    title: "Conta",
    links: [
      { label: "Criar conta grátis", href: "/auth/signup" },
      { label: "Entrar", href: "/auth/login" },
    ],
  },
];

export function LandingFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="relative border-t border-white/[0.06] bg-[#060d15]">
      <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
        <div className="grid gap-10 sm:grid-cols-2 lg:grid-cols-4">
          {/* Brand */}
          <div className="lg:col-span-2">
            <Link to="/" className="mb-4 flex items-center gap-2">
              <RootFlowBrand variant="icon" size="sm" />
              <span className="font-display text-lg font-semibold text-white">RootFlow</span>
            </Link>
            <p className="max-w-xs text-sm leading-relaxed text-white/40">
              Transforme documentos em um assistente inteligente para sua equipe. Centralize o conhecimento.
              Reduza dúvidas. Acelere o crescimento.
            </p>
          </div>

          {/* Links */}
          {FOOTER_LINKS.map((group) => (
            <div key={group.title}>
              <p className="mb-4 text-xs font-semibold tracking-widest text-white/40 uppercase">{group.title}</p>
              <ul className="space-y-3">
                {group.links.map((link) => (
                  <li key={link.label}>
                    {link.href.startsWith("#") || link.href.startsWith("http") ? (
                      <a
                        href={link.href}
                        className="text-sm text-white/50 transition-colors hover:text-white/80"
                      >
                        {link.label}
                      </a>
                    ) : (
                      <Link
                        to={link.href}
                        className="text-sm text-white/50 transition-colors hover:text-white/80"
                      >
                        {link.label}
                      </Link>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        {/* Bottom bar */}
        <div className="mt-10 flex flex-col items-center justify-between gap-4 border-t border-white/[0.06] pt-8 sm:flex-row">
          <p className="text-xs text-white/25">© {year} RootFlow. Todos os direitos reservados.</p>
          <p className="text-xs text-white/20">Feito com cuidado para equipes brasileiras.</p>
        </div>
      </div>
    </footer>
  );
}
