export function VideoSection() {
  return (
    <section className="relative overflow-hidden py-24 sm:py-32">
      {/* Background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute left-1/2 top-1/2 h-[600px] w-[800px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-[#0f63ec]/8 blur-[160px]" />
      </div>

      <div className="relative mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mx-auto mb-12 max-w-2xl text-center">
          <p className="mb-3 text-sm font-semibold tracking-widest text-[#06b6d4] uppercase">Veja funcionando</p>
          <h2 className="font-display mb-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Do documento à resposta em segundos
          </h2>
          <p className="text-base text-white/50">
            Assista como o RootFlow transforma um PDF em um assistente que responde perguntas da sua equipe.
          </p>
        </div>

        {/* Video embed */}
        <div className="overflow-hidden rounded-2xl border border-white/[0.08] shadow-[0_32px_80px_rgba(0,0,0,0.55)]">
          <img
            style={{ width: "100%", margin: "auto", display: "block" }}
            className="vidyard-player-embed"
            src="https://play.vidyard.com/THbPGmj3iosyb7LZX43PPm.jpg"
            data-uuid="THbPGmj3iosyb7LZX43PPm"
            data-v="4"
            data-type="inline"
            alt="Demonstração do RootFlow"
          />
        </div>
      </div>
    </section>
  );
}
