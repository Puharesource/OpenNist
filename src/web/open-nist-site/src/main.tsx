import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import "@oddbird/popover-polyfill"
import "./index.css"
import App from "./App.tsx"

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
)

if (import.meta.env.PROD && "serviceWorker" in navigator) {
  window.addEventListener("load", () => {
    void navigator.serviceWorker.register("/sw.js").catch(() => {
      // The app remains functional without offline install support.
    })
  })
}
