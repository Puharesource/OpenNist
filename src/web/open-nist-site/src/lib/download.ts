type SaveFilePickerWindow = Window & {
  showSaveFilePicker?: (options?: {
    suggestedName?: string
    excludeAcceptAllOption?: boolean
    types?: Array<{
      description?: string
      accept: Record<string, string[]>
    }>
  }) => Promise<{
    createWritable(): Promise<{
      write(data: BlobPart): Promise<void>
      close(): Promise<void>
    }>
  }>
}

function fallbackDownloadBytes(bytes: Uint8Array, fileName: string, mimeType: string): void {
  const objectUrl = URL.createObjectURL(new Blob([Uint8Array.from(bytes)], { type: mimeType }))
  const anchor = document.createElement("a")
  anchor.href = objectUrl
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(objectUrl)
}

function getFileExtension(fileName: string): string {
  const extensionIndex = fileName.lastIndexOf(".")
  return extensionIndex < 0 ? "" : fileName.slice(extensionIndex).toLowerCase()
}

function getSavePickerTypes(
  fileName: string,
  mimeType: string
): Array<{ description: string; accept: Record<string, string[]> }> {
  const extension = getFileExtension(fileName)
  const accept: Record<string, string[]> = {}

  if (mimeType) {
    accept[mimeType] = extension ? [extension] : []
  } else if (extension) {
    accept["application/octet-stream"] = [extension]
  }

  return Object.keys(accept).length > 0
    ? [
        {
          description: "OpenNist export",
          accept
        }
      ]
    : []
}

export async function downloadBytes(bytes: Uint8Array, fileName: string, mimeType: string): Promise<void> {
  const nativeWindow = window as SaveFilePickerWindow

  if (nativeWindow.showSaveFilePicker) {
    try {
      const handle = await nativeWindow.showSaveFilePicker({
        suggestedName: fileName,
        types: getSavePickerTypes(fileName, mimeType)
      })
      const writable = await handle.createWritable()
      await writable.write(Uint8Array.from(bytes))
      await writable.close()
      return
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return
      }

      if (!(error instanceof DOMException) || !["SecurityError", "NotAllowedError"].includes(error.name)) {
        throw error
      }
    }
  }

  fallbackDownloadBytes(bytes, fileName, mimeType)
}

export function replaceExtension(fileName: string, extension: string): string {
  const extensionIndex = fileName.lastIndexOf(".")
  if (extensionIndex < 0) {
    return `${fileName}${extension}`
  }

  return `${fileName.slice(0, extensionIndex)}${extension}`
}
