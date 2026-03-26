import { AlertTriangle, LoaderCircle, ScanSearch } from "lucide-react"
import { startTransition, useCallback, useEffect, useState } from "react"

import { InspectorMetric, InspectorNotice, InspectorPanel, InspectorSection } from "@/components/workspace-inspector"
import { WorkspaceSidebarToggleGroup, useWorkspaceSidebars } from "@/components/workspace-sidebars"
import { type NfiqWorkspaceDocument, getFileFingerprint } from "@/lib/codecs-document"
import { exportRawImage, normalizeImageFile } from "@/lib/ffmpeg-wasm"
import { assessNfiqRawImage, decodeWsqAndAssessNfiq } from "@/lib/opennist-wasm"
import type { WorkspaceFileIntake } from "@/lib/workspace-file-intake"
import { setWorkspaceNfiqDocument, useWorkspaceSession } from "@/lib/workspace-session"

type NfiqWorkspaceError = {
  fileName: string
  message: string
}

function bytesToObjectUrl(bytes: Uint8Array, mimeType: string): string {
  return URL.createObjectURL(new Blob([Uint8Array.from(bytes)], { type: mimeType }))
}

function formatByteSize(byteCount: number): string {
  if (byteCount < 1024) {
    return `${byteCount} B`
  }

  if (byteCount < 1024 * 1024) {
    return `${(byteCount / 1024).toFixed(1)} KB`
  }

  return `${(byteCount / (1024 * 1024)).toFixed(2)} MB`
}

function formatBoolean(value: boolean): string {
  return value ? "Yes" : "No"
}

function formatMeasureLabel(label: string): string {
  const normalized = label.startsWith("QB_") ? label.slice(3) : label

  const friendlyLabels: Record<string, string> = {
    UniformImage: "Uniform Image Check",
    EmptyImageOrContrastTooLow: "Contrast Check",
    FingerprintImageWithMinutiae: "Detected Minutiae Count",
    SufficientFingerprintForeground: "Fingerprint Foreground Coverage",
    FDA_Bin10_Mean: "Ridge Frequency Strength",
    FDA_Bin10_StdDev: "Ridge Frequency Variability",
    FingerJetFX_MinutiaeCount: "Detected Minutiae Count",
    FingerJetFX_MinCount_COMMinRect200x200: "Central Minutiae Count",
    ImgProcROIArea_Mean: "Fingerprint Area Coverage",
    MMB: "Average Block Brightness",
    OrientationMap_ROIFilter_CoherenceSum: "Ridge Flow Consistency",
    OCL_Bin10_Mean: "Orientation Certainty",
    LCS_Bin10_Mean: "Local Clarity",
    OF_Bin10_Mean: "Ridge Flow Direction",
    RVUP_Bin10_Mean: "Ridge and Valley Uniformity"
  }

  return friendlyLabels[normalized] ?? normalized.replaceAll(/[._]/g, " ").replaceAll(/\s+/g, " ").trim()
}

function formatMeasureValue(value: number | null | undefined): string {
  if (typeof value !== "number" || Number.isNaN(value)) {
    return "Unavailable"
  }

  if (Number.isInteger(value)) {
    return value.toString()
  }

  return value.toFixed(Math.abs(value) >= 10 ? 2 : 3)
}

function pickMeasureRows(
  measures: Record<string, number | null>,
  limit: number,
  emptyLabel: string,
  getDescription?: (key: string) => string | undefined
): Array<{ label: string; value: string; description?: string }> {
  const rows = Object.entries(measures)
    .filter(([, value]) => value !== null)
    .sort(([left], [right]) => left.localeCompare(right))
    .slice(0, limit)
    .map(([label, value]) => ({
      label: formatMeasureLabel(label),
      value: formatMeasureValue(value),
      description: getDescription?.(label)
    }))

  if (rows.length > 0) {
    return rows
  }

  return [{ label: emptyLabel, value: "Awaiting file input" }]
}

function describeActionableFeedbackMeasure(key: string): string | undefined {
  const descriptions: Record<string, string> = {
    UniformImage:
      "This is grayscale spread, also called sigma. A value of 0 means a completely flat image, and values below 1.0 are treated as nearly uniform, which usually means little or no usable ridge detail.",
    EmptyImageOrContrastTooLow:
      "This is average brightness on a 0-255 grayscale scale. Values above 250 are treated as near-empty or too bright for reliable ridge extraction.",
    FingerprintImageWithMinutiae:
      "This is the count of detected ridge endings and splits. The minimum is 0. There is no fixed upper limit here, but higher counts usually mean the image contains more usable fingerprint detail.",
    SufficientFingerprintForeground:
      "This is the number of pixels marked as fingerprint foreground instead of blank background. The minimum is 0, and the practical upper limit depends on the image size."
  }

  return descriptions[key]
}

function describeQualityMeasure(key: string): string | undefined {
  const normalized = key.startsWith("QB_") ? key.slice(3) : key
  const descriptions: Record<string, string> = {
    FDA_Bin10_Mean:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-1 measure. Higher values usually mean the ridge spacing pattern is stronger and more stable.",
    FDA_Bin10_StdDev:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-1 variability measure. Lower values usually mean ridge spacing stays more consistent from block to block, while higher values mean it varies more across the image.",
    FingerJetFX_MinutiaeCount:
      "Displayed here on a 0-100 scale. It is based on detected minutiae count and capped at 100, so values near 100 usually mean the extractor found plenty of ridge detail.",
    FingerJetFX_MinCount_COMMinRect200x200:
      "Displayed here on a 0-100 scale. It is based on minutiae count inside a 200x200 region around the print's center and capped at 100, so higher values usually mean the core capture area contains usable detail.",
    ImgProcROIArea_Mean:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-255 measure. Higher values usually mean more of the frame is occupied by fingerprint foreground rather than empty background.",
    MMB: "Displayed here as a normalized 0-100 score, derived from a raw 0-255 brightness measure. Mid-range values are usually healthier; very low or very high brightness can make ridge detail harder to read.",
    OrientationMap_ROIFilter_CoherenceSum:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-3150 coherence sum. Higher values usually mean ridge flow stays more consistent across the usable fingerprint area.",
    OCL_Bin10_Mean:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-1 measure. Higher values usually mean the local ridge direction is easier to determine.",
    LCS_Bin10_Mean:
      "Displayed here as a normalized 0-100 score, derived from a raw 0-1 measure. Higher values usually mean ridges and valleys are more distinctly separated.",
    OF_Bin10_Mean:
      "Displayed here as a normalized 0-100 score. Higher values usually mean ridge direction changes more smoothly across the print.",
    RVUP_Bin10_Mean:
      "Displayed here as a normalized 0-100 score. Higher values usually suggest more regular spacing between dark ridges and light valleys."
  }

  return descriptions[normalized]
}

async function waitForUiPaint(): Promise<void> {
  await new Promise<void>((resolve) => {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => resolve())
    })
  })
}

function logRuntime(message: string, detail?: unknown): void {
  if (detail instanceof Error) {
    console.error(`[OpenNist] ${message}`, detail)
    return
  }

  if (typeof detail !== "undefined") {
    console.info(`[OpenNist] ${message}`, detail)
    return
  }

  console.info(`[OpenNist] ${message}`)
}

export function NfiqWorkspace({
  currentLabel,
  intake,
  incomingFile
}: {
  currentLabel: string
  intake: WorkspaceFileIntake
  incomingFile: File | null
}) {
  const { nfiqDocument, nfiqDocumentFingerprint, activeFileFingerprint } = useWorkspaceSession()
  const [document, setDocument] = useState<NfiqWorkspaceDocument | null>(nfiqDocument)
  const [busyMode, setBusyMode] = useState<"preview" | null>(null)
  const [errorState, setErrorState] = useState<NfiqWorkspaceError | null>(null)

  const updateDocument = useCallback((next: NfiqWorkspaceDocument, fingerprint: string | null) => {
    setWorkspaceNfiqDocument(next, fingerprint)

    startTransition(() => {
      setDocument(next)
    })
  }, [])

  const withBusy = useCallback(async <T,>(message: string, work: () => Promise<T>): Promise<T> => {
    setBusyMode("preview")
    logRuntime(message)
    await waitForUiPaint()

    try {
      return await work()
    } finally {
      setBusyMode(null)
    }
  }, [])

  const handleFile = useCallback(
    async (file: File) => {
      const fileName = file.name
      const fingerprint = getFileFingerprint(file)
      setErrorState(null)

      try {
        if (fileName.toLowerCase().endsWith(".wsq")) {
          const loadedDocument = await withBusy("Decoding WSQ and computing its NFIQ 2 score.", async () => {
            const wsqBytes = new Uint8Array(await file.arrayBuffer())
            const decoded = await decodeWsqAndAssessNfiq(wsqBytes, { transferOwnership: true })
            const previewBytes = await exportRawImage(decoded.rawPixels, decoded.width, decoded.height, "png")

            return {
              fileName,
              sourceKind: "WSQ" as const,
              sourceByteCount: wsqBytes.length,
              width: decoded.width,
              height: decoded.height,
              pixelsPerInch: decoded.pixelsPerInch,
              previewUrl: bytesToObjectUrl(previewBytes, "image/png"),
              assessment: decoded.assessment
            }
          })

          updateDocument(loadedDocument, fingerprint)
          setErrorState(null)
          logRuntime(`Scored ${fileName} with NFIQ 2 score ${loadedDocument.assessment.qualityScore}.`)
          return
        }

        const loadedDocument = await withBusy(
          "Normalizing the source image and computing its NFIQ 2 score.",
          async () => {
            const normalized = await normalizeImageFile(file)
            const assessment = await assessNfiqRawImage(
              normalized.rawPixels,
              normalized.width,
              normalized.height,
              500,
              { transferOwnership: true }
            )

            return {
              fileName,
              sourceKind: "Image" as const,
              sourceByteCount: file.size,
              width: normalized.width,
              height: normalized.height,
              pixelsPerInch: 500,
              previewUrl: bytesToObjectUrl(normalized.previewBytes, "image/png"),
              assessment
            }
          }
        )

        updateDocument(loadedDocument, fingerprint)
        setErrorState(null)
        logRuntime(`Scored ${fileName} with NFIQ 2 score ${loadedDocument.assessment.qualityScore}.`)
      } catch (error) {
        const message = error instanceof Error ? error.message : "The file could not be scored."
        setErrorState({ fileName, message })
        logRuntime(`Error: ${message}`, error)
      }
    },
    [updateDocument, withBusy]
  )

  useEffect(() => {
    if (!nfiqDocument) {
      return
    }

    setDocument(nfiqDocument)
  }, [nfiqDocument])

  useEffect(() => {
    if (!incomingFile) {
      return
    }

    const fingerprint = getFileFingerprint(incomingFile)
    if (
      nfiqDocument &&
      nfiqDocumentFingerprint &&
      nfiqDocumentFingerprint === fingerprint &&
      activeFileFingerprint === fingerprint
    ) {
      setDocument(nfiqDocument)
      return
    }

    void handleFile(incomingFile)
  }, [activeFileFingerprint, handleFile, incomingFile, nfiqDocument, nfiqDocumentFingerprint])

  const showPreviewOverlay = busyMode === "preview" && Boolean(document)
  const showInitialLoader = busyMode === "preview" && !document
  const showInspector = Boolean(document)
  const { rightDocked, rightInlineVisible, rightOverlayVisible } = useWorkspaceSidebars()
  const showRightSidebar = showInspector && (rightInlineVisible || rightOverlayVisible)

  return (
    <>
      <div
        className={`flex min-w-0 flex-1 flex-col overflow-hidden ${
          showRightSidebar && rightInlineVisible ? "border-r border-[color:var(--effect-ghost-border)]" : ""
        }`}
      >
        <div className="border-b border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] px-6 py-5">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="font-display text-3xl font-semibold tracking-[-0.05em] text-[var(--color-primary)]">
                {currentLabel}
              </h1>
              <p className="mt-1 max-w-xl text-xs leading-5 text-[var(--color-on-surface-variant)]">
                Review a fingerprint image and its NFIQ 2 result.
              </p>
            </div>
            <WorkspaceSidebarToggleGroup showRightToggle={showInspector} />
          </div>
        </div>

        <div className="flex-1 overflow-auto px-6 py-6">
          <button
            type="button"
            className={`surface-module flex min-h-full w-full cursor-pointer flex-col overflow-hidden rounded-[var(--radius-xl)] border-0 bg-white text-left shadow-none ring-1 ring-[color:var(--effect-ghost-border)] transition-colors ${
              intake.isDragActive ? "bg-[var(--color-primary-fixed)]/10 ring-[var(--color-primary)]/30" : ""
            }`}
            onClick={intake.openPicker}
            onDragEnter={intake.activateDrag}
            onDragOver={(event) => {
              event.preventDefault()
              intake.activateDrag()
            }}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
                intake.deactivateDrag()
              }
            }}
            onDrop={intake.handleDrop}
            onPaste={intake.handlePaste}
          >
            <div className="flex min-h-[560px] items-center justify-center bg-white p-6">
              {showInitialLoader ? (
                <div className="mx-auto flex max-w-[420px] items-center justify-center px-8">
                  <div className="space-y-5 text-center">
                    <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                      <LoaderCircle className="size-11 animate-spin" />
                    </div>
                    <div>
                      <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                        Scoring image
                      </p>
                      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                        Preparing the image and running NFIQ 2 in the worker.
                      </p>
                    </div>
                  </div>
                </div>
              ) : document ? (
                <div className="relative inline-flex max-h-[500px] max-w-full items-center justify-center">
                  <img
                    src={document.previewUrl}
                    alt={document.fileName}
                    className="max-h-[500px] w-auto max-w-full object-contain"
                  />
                  {showPreviewOverlay ? (
                    <div className="absolute inset-0 flex items-center justify-center bg-white/45 backdrop-blur-sm">
                      <div className="flex size-16 items-center justify-center rounded-full bg-white/85 text-[var(--color-primary)] shadow-[var(--effect-modal-shadow)]">
                        <LoaderCircle className="size-8 animate-spin" />
                      </div>
                    </div>
                  ) : null}
                </div>
              ) : errorState ? (
                <div className="mx-auto flex max-w-[520px] items-center justify-center px-8">
                  <div className="space-y-5 text-center">
                    <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-amber-100 text-amber-700 shadow-[var(--effect-modal-shadow)]">
                      <AlertTriangle className="size-11" />
                    </div>
                    <div>
                      <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                        NFIQ analysis failed
                      </p>
                      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                        {errorState.message}
                      </p>
                      <p className="mt-4 font-mono text-[0.68rem] uppercase tracking-[0.2em] text-[var(--color-secondary)]">
                        Try a tighter crop or a smaller 500 ppi fingerprint image
                      </p>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="mx-auto flex max-w-[520px] items-center justify-center px-8">
                  <div className="space-y-5 text-center">
                    <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                      <ScanSearch className="size-11" />
                    </div>
                    <div>
                      <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                        NFIQ inspection canvas
                      </p>
                      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                        Open a fingerprint image or WSQ file to run NFIQ 2 scoring in the browser worker.
                      </p>
                      <p className="mt-4 font-mono text-[0.68rem] uppercase tracking-[0.2em] text-[var(--color-secondary)]">
                        Click to open, drag and drop, or paste a file
                      </p>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </button>
        </div>
      </div>

      {showRightSidebar ? (
        <NfiqInspector
          document={document!}
          errorState={errorState}
          rightDocked={rightDocked}
          rightOverlayVisible={rightOverlayVisible}
        />
      ) : null}
    </>
  )
}

function NfiqInspector({
  document,
  errorState,
  rightDocked,
  rightOverlayVisible
}: {
  document: NfiqWorkspaceDocument
  errorState: NfiqWorkspaceError | null
  rightDocked: boolean
  rightOverlayVisible: boolean
}) {
  const summaryRows = [
    {
      label: "Overall quality score",
      value: `${document.assessment.qualityScore}`,
      description:
        "A single NFIQ 2 score from 0 to 100. Higher scores usually mean the print should be easier to match."
    },
    {
      label: "Input type",
      value: document.sourceKind,
      description: "Whether this result came from a WSQ fingerprint image or a regular uploaded image."
    },
    {
      label: "Resolution",
      value: `${document.pixelsPerInch} ppi`,
      description: "Pixels per inch. Fingerprint quality tools commonly expect 500 ppi input."
    },
    {
      label: "Image size",
      value: `${document.width} × ${document.height}`,
      description: "Pixel dimensions used for scoring after the image was prepared for analysis."
    },
    {
      label: "Original file size",
      value: formatByteSize(document.sourceByteCount),
      description: "Size of the file you uploaded before any decoding or normalization happened."
    },
    {
      label: "Gray levels reduced",
      value: formatBoolean(document.assessment.quantized),
      description: "Shows whether the image had to be simplified to a smaller grayscale palette before scoring."
    },
    {
      label: "Image resized",
      value: formatBoolean(document.assessment.resampled),
      description: "Shows whether the image had to be resized to fit the scoring pipeline requirements."
    }
  ]

  const actionableRows = pickMeasureRows(
    document.assessment.actionableFeedback,
    4,
    "Actionable feedback",
    describeActionableFeedbackMeasure
  )
  const qualityMeasureRows = pickMeasureRows(
    document.assessment.mappedQualityMeasures,
    4,
    "Quality measures",
    describeQualityMeasure
  )

  return (
    <InspectorPanel
      title={document.fileName}
      summary="NFIQ 2 score, image details, and plain-language clues about what may be helping or hurting quality."
      rightDocked={rightDocked}
      rightOverlayVisible={rightOverlayVisible}
    >
      <InspectorMetric
        eyebrow="Overall quality score"
        value={`${document.assessment.qualityScore}`}
        meta="NFIQ 2"
        description="NFIQ 2 scores run from 0 to 100. Higher scores usually mean the fingerprint image should be easier for matching systems to use."
      />

      {errorState ? <InspectorNotice title="Analysis failed" message={errorState.message} tone="error" /> : null}

      {document.assessment.optionalError ? (
        <InspectorNotice title="Analysis note" message={document.assessment.optionalError} />
      ) : null}

      <InspectorSection
        title="Score summary"
        description="Core facts about the scored image and any prep work the pipeline had to do before producing the result."
        items={summaryRows}
      />
      <InspectorSection
        title="Actionable feedback"
        description="Hints about what may be limiting fingerprint quality, useful when deciding whether to capture or crop again."
        items={actionableRows}
      />
      <InspectorSection
        title="Supporting quality measures"
        description="Additional measurements that help explain how the final quality score was derived."
        items={qualityMeasureRows}
      />
    </InspectorPanel>
  )
}
