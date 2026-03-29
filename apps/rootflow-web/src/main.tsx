import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import "@fontsource-variable/manrope/index.css";
import "@fontsource-variable/sora/index.css";

import App from "@/App";
import { AppProviders } from "@/app/providers/app-providers";
import "@/index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <App />
    </AppProviders>
  </StrictMode>,
);
