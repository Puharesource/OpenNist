import { RouterProvider } from "@tanstack/react-router"
import { useEffect } from "react"

import { warmOpenNistWorker } from "@/lib/opennist-wasm"
import { router } from "@/router"

function App() {
  useEffect(() => {
    void warmOpenNistWorker().catch(() => {
      // Let the first explicit worker request surface the failure to the UI.
    })
  }, [])

  return <RouterProvider router={router} />
}

export default App
