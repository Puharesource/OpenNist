import { useNavigate } from "@tanstack/react-router"
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type ClipboardEvent as ReactClipboardEvent,
  type DragEvent
} from "react"

import { getFileFingerprint } from "@/lib/codecs-document"
import type { WorkspaceView } from "@/lib/site-content"
import { setWorkspaceActiveFile } from "@/lib/workspace-session"

export const ACCEPTED_FILES = ".nist,.nst,.an2,.eft,.wsq,.jp2,.j2k,.j2c,.png,.jpg,.jpeg,.tif,.tiff,.bmp,.webp,.gif"

const OPEN_PICKER_TYPES = [
  {
    description: "Biometric transaction and image files",
    accept: {
      "application/octet-stream": [".nist", ".nst", ".an2", ".eft", ".wsq", ".jp2", ".j2k", ".j2c"],
      "image/png": [".png"],
      "image/jpeg": [".jpg", ".jpeg"],
      "image/tiff": [".tif", ".tiff"],
      "image/bmp": [".bmp"],
      "image/webp": [".webp"],
      "image/gif": [".gif"]
    }
  }
] as const

type FilePickerWindow = Window & {
  showOpenFilePicker?: (options?: {
    multiple?: boolean
    excludeAcceptAllOption?: boolean
    types?: ReadonlyArray<{
      description?: string
      accept: Record<string, readonly string[]>
    }>
  }) => Promise<Array<{ getFile(): Promise<File> }>>
}

export function isNistTransactionFileName(fileName: string): boolean {
  const normalizedName = fileName.toLowerCase()
  return (
    normalizedName.endsWith(".nist") ||
    normalizedName.endsWith(".nst") ||
    normalizedName.endsWith(".an2") ||
    normalizedName.endsWith(".eft")
  )
}

export function routeForFileName(
  fileName: string,
  currentView: WorkspaceView
): "/app/nist" | "/app/codecs" | "/app/nfiq" {
  if (isNistTransactionFileName(fileName)) {
    return "/app/nist"
  }

  return currentView === "nfiq" ? "/app/nfiq" : "/app/codecs"
}

export type WorkspaceFileIntake = {
  fileInputRef: React.RefObject<HTMLInputElement | null>
  isDragActive: boolean
  openPicker(): void
  handleInputChange(event: ChangeEvent<HTMLInputElement>): void
  handleDrop(event: DragEvent<HTMLElement>): void
  handlePaste(event: ReactClipboardEvent<HTMLElement>): void
  activateDrag(): void
  deactivateDrag(): void
}

export function useWorkspaceFileIntake(currentView: WorkspaceView): WorkspaceFileIntake {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [isDragActive, setIsDragActive] = useState(false)

  const handleResolvedFile = useCallback(
    (file: File) => {
      const targetRoute = routeForFileName(file.name, currentView)
      setWorkspaceActiveFile(file, getFileFingerprint(file))

      if (targetRoute === "/app/codecs") {
        if (currentView !== "codecs") {
          void navigate({ to: "/app/codecs" })
        }

        return
      }

      if (targetRoute === "/app/nfiq") {
        if (currentView !== "nfiq") {
          void navigate({ to: "/app/nfiq" })
        }

        return
      }

      if (currentView !== "nist") {
        void navigate({ to: "/app/nist" })
      }
    },
    [currentView, navigate]
  )

  const handleFiles = useCallback(
    (files: ArrayLike<File> | null | undefined) => {
      const file = files ? Array.from(files)[0] : undefined

      if (!file) {
        return
      }

      handleResolvedFile(file)
    },
    [handleResolvedFile]
  )

  const handleInputChange = useCallback(
    (event: ChangeEvent<HTMLInputElement>) => {
      handleFiles(event.target.files)
      event.target.value = ""
    },
    [handleFiles]
  )

  const handleDrop = useCallback(
    (event: DragEvent<HTMLElement>) => {
      event.preventDefault()
      setIsDragActive(false)
      handleFiles(event.dataTransfer.files)
    },
    [handleFiles]
  )

  const handlePasteData = useCallback(
    (clipboardData: DataTransfer | null) => {
      if (!clipboardData) {
        return
      }

      if (clipboardData.files.length > 0) {
        handleFiles(clipboardData.files)
        return
      }

      for (const item of Array.from(clipboardData.items)) {
        if (item.kind !== "file") {
          continue
        }

        const file = item.getAsFile()

        if (file) {
          handleFiles([file])
          return
        }
      }
    },
    [handleFiles]
  )

  useEffect(() => {
    function handleWindowPaste(event: ClipboardEvent) {
      handlePasteData(event.clipboardData)
    }

    window.addEventListener("paste", handleWindowPaste)

    return () => {
      window.removeEventListener("paste", handleWindowPaste)
    }
  }, [handlePasteData])

  return {
    fileInputRef,
    isDragActive,
    openPicker: () => {
      const nativeWindow = window as FilePickerWindow

      if (!nativeWindow.showOpenFilePicker) {
        fileInputRef.current?.click()
        return
      }

      void nativeWindow
        .showOpenFilePicker({
          multiple: false,
          types: OPEN_PICKER_TYPES
        })
        .then(async (handles) => {
          const file = await handles[0]?.getFile()
          if (file) {
            handleResolvedFile(file)
          }
        })
        .catch((error: unknown) => {
          if (error instanceof DOMException && error.name === "AbortError") {
            return
          }

          fileInputRef.current?.click()
        })
    },
    handleInputChange,
    handleDrop,
    handlePaste: (event) => handlePasteData(event.clipboardData),
    activateDrag: () => setIsDragActive(true),
    deactivateDrag: () => setIsDragActive(false)
  }
}
