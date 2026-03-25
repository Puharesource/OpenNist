import { useSyncExternalStore } from "react";
import type { CodecsWorkspaceDocument, NfiqWorkspaceDocument } from "@/lib/codecs-document";

type WorkspaceSessionState = {
  activeFile: File | null;
  activeFileFingerprint: string | null;
  codecsDocument: CodecsWorkspaceDocument | null;
  codecsDocumentFingerprint: string | null;
  nfiqDocument: NfiqWorkspaceDocument | null;
  nfiqDocumentFingerprint: string | null;
};

let state: WorkspaceSessionState = {
  activeFile: null,
  activeFileFingerprint: null,
  codecsDocument: null,
  codecsDocumentFingerprint: null,
  nfiqDocument: null,
  nfiqDocumentFingerprint: null,
};

const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) {
    listener();
  }
}

export function setWorkspaceActiveFile(file: File | null, fingerprint: string | null) {
  state = {
    ...state,
    activeFile: file,
    activeFileFingerprint: fingerprint,
  };

  emit();
}

export function setWorkspaceCodecsDocument(
  document: CodecsWorkspaceDocument | null,
  fingerprint: string | null,
) {
  const previousPreviewUrl = state.codecsDocument?.previewUrl;
  const nextPreviewUrl = document?.previewUrl;

  if (previousPreviewUrl && previousPreviewUrl !== nextPreviewUrl) {
    URL.revokeObjectURL(previousPreviewUrl);
  }

  state = {
    ...state,
    codecsDocument: document,
    codecsDocumentFingerprint: fingerprint,
  };

  emit();
}

export function setWorkspaceNfiqDocument(document: NfiqWorkspaceDocument | null, fingerprint: string | null) {
  const previousPreviewUrl = state.nfiqDocument?.previewUrl;
  const nextPreviewUrl = document?.previewUrl;

  if (previousPreviewUrl && previousPreviewUrl !== nextPreviewUrl) {
    URL.revokeObjectURL(previousPreviewUrl);
  }

  state = {
    ...state,
    nfiqDocument: document,
    nfiqDocumentFingerprint: fingerprint,
  };

  emit();
}

export function getWorkspaceSessionSnapshot(): WorkspaceSessionState {
  return state;
}

export function useWorkspaceSession() {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getWorkspaceSessionSnapshot,
    getWorkspaceSessionSnapshot,
  );
}
