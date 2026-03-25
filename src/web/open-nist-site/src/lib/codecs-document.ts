import type { NfiqAssessmentResult, WsqFileInfo } from "@/lib/opennist-models";

export type SourceKind = "WSQ" | "Image";

export type CodecsWorkspaceDocument = {
  fileName: string;
  sourceKind: SourceKind;
  sourceByteCount: number;
  width: number;
  height: number;
  pixelsPerInch: number;
  rawPixels: Uint8Array;
  previewUrl: string;
  wsqInfo?: WsqFileInfo;
  wsqBytes?: Uint8Array;
};

export type NfiqWorkspaceDocument = {
  fileName: string;
  sourceKind: SourceKind;
  sourceByteCount: number;
  width: number;
  height: number;
  pixelsPerInch: number;
  previewUrl: string;
  assessment: NfiqAssessmentResult;
};

export function getFileFingerprint(file: File | null): string | null {
  if (!file) {
    return null;
  }

  return [file.name, file.size, file.lastModified, file.type].join(":");
}
