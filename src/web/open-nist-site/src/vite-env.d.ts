/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_FFMPEG_CLASS_WORKER_URL?: string
  readonly VITE_FFMPEG_CORE_URL?: string
  readonly VITE_FFMPEG_WASM_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
