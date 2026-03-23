export function downloadBytes(bytes: Uint8Array, fileName: string, mimeType: string): void {
  const objectUrl = URL.createObjectURL(new Blob([Uint8Array.from(bytes)], { type: mimeType }));
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(objectUrl);
}

export function replaceExtension(fileName: string, extension: string): string {
  const extensionIndex = fileName.lastIndexOf(".");
  if (extensionIndex < 0) {
    return `${fileName}${extension}`;
  }

  return `${fileName.slice(0, extensionIndex)}${extension}`;
}
