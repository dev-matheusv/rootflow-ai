import { Quote, Star } from "lucide-react";

// TODO: Replace with real testimonials as they come in
const TESTIMONIALS = [
  {
    quote:
      "Antes, qualquer dúvida sobre processo parava no mesmo gerente. Com o RootFlow, o time encontra a resposta sozinho. O impacto no onboarding foi imediato.",
    author: "Diretora de Operações",
    company: "Agência de Marketing Digital",
    initials: "CM",
  },
  {
    quote:
      "Subimos o manual de atendimento e os scripts de venda. Agora o time consulta antes de perguntar. As perguntas repetidas caíram absurdamente.",
    author: "Head de CS",
    company: "Consultoria B2B",
    initials: "RL",
  },
  {
    quote:
      "Tentei outras ferramentas antes e sempre precisava de alguém técnico pra configurar. O RootFlow foi o único que funcionou no mesmo dia.",
    author: "Sócio-fundador",
    company: "Empresa de Serviços",
    initials: "FT",
  },
];

const STATS = [
  { value: "70%", label: "menos dúvidas repetidas" },
  { value: "3x", label: "mais rápido no onboarding" },
  { value: "< 5min", label: "para subir e usar" },
];


export function SocialProofSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-[#060d15] via-[#070e19] to-[#060d15]" />
        <div className="absolute left-1/2 top-0 h-px w-3/4 -translate-x-1/2 bg-gradient-to-r from-transparent via-white/10 to-transparent" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-16 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Prova social</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Equipes já estão usando o RootFlow
          </h2>
          <p className="text-base text-white/50">
            Empresas que centralizaram seu conhecimento e pararam de repetir respostas todo dia.
          </p>
        </div>

        {/* Stats */}
        <div className="mb-16 grid grid-cols-3 gap-4 sm:gap-8">
          {STATS.map((s, i) => (
            <div key={i} className="text-center">
              <p className="font-display mb-1 text-3xl font-bold text-white sm:text-4xl">{s.value}</p>
              <p className="text-xs text-white/40 sm:text-sm">{s.label}</p>
            </div>
          ))}
        </div>

        <div className="grid gap-8 lg:grid-cols-2 lg:items-start">
          {/* Testimonials */}
          <div className="flex flex-col gap-4">
            {TESTIMONIALS.map((t, i) => (
              <div
                key={i}
                className="relative rounded-2xl border border-white/[0.07] bg-white/[0.025] p-6 transition-all hover:border-white/12"
              >
                <Quote className="mb-3 h-5 w-5 text-[#0f63ec]/60" />
                <p className="mb-4 text-sm leading-relaxed text-white/70 sm:text-base">"{t.quote}"</p>
                <div className="flex items-center gap-3">
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[#0f63ec]/20 text-xs font-bold text-[#3b87f5]">
                    {t.initials}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-white/80">{t.author}</p>
                    <p className="text-xs text-white/40">{t.company}</p>
                  </div>
                  <div className="ml-auto flex gap-0.5">
                    {[...Array(5)].map((_, j) => (
                      <Star key={j} className="h-3.5 w-3.5 fill-[#f59e0b] text-[#f59e0b]" />
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Product screenshot — conversa real */}
          {/* Salve o screenshot como: apps/rootflow-web/public/product-screenshot.png */}
          <div className="sticky top-24">
            <div className="overflow-hidden rounded-2xl border border-white/[0.08] shadow-[0_24px_60px_rgba(0,0,0,0.55)]">
              {/* Fake browser chrome */}
              <div className="flex items-center gap-2 border-b border-white/[0.06] bg-[#0d1520] px-4 py-2.5">
                <div className="h-2.5 w-2.5 rounded-full bg-[#ff5f57]" />
                <div className="h-2.5 w-2.5 rounded-full bg-[#ffbd2e]" />
                <div className="h-2.5 w-2.5 rounded-full bg-[#28c840]" />
                <span className="ml-2 text-xs text-white/25">app.rootflow.com.br/assistant</span>
                <span className="ml-auto flex items-center gap-1 text-xs text-[#06b6d4]">
                  <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-[#06b6d4]" />
                  ao vivo
                </span>
              </div>
              <img
                src="/product-screenshot.png"
                alt="RootFlow assistente respondendo em tempo real"
                className="block w-full object-cover object-top"
                loading="lazy"
                decoding="async"
              />
            </div>
            <p className="mt-3 text-center text-xs text-white/25">
              Resposta real gerada com base nos documentos da empresa
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
