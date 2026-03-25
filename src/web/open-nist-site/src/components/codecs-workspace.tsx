import { startTransition, useCallback, useEffect, useId, useRef, useState } from "react";
import {
  ArrowDownToLine,
  Check,
  ChevronDown,
  FileArchive,
  Fingerprint,
  LoaderCircle,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { InspectorPanel, InspectorSection } from "@/components/workspace-inspector";
import { WorkspaceSidebarToggleGroup, useWorkspaceSidebars } from "@/components/workspace-sidebars";
import { type CodecsWorkspaceDocument, getFileFingerprint } from "@/lib/codecs-document";
import { downloadBytes, replaceExtension } from "@/lib/download";
import { exportRawImage, normalizeImageFile } from "@/lib/ffmpeg-wasm";
import { type WorkspaceFileIntake } from "@/lib/workspace-file-intake";
import {
  decodeWsqBytes,
  encodeRawToWsqBytes,
  type WsqCommentInfo,
  type WsqFileInfo,
} from "@/lib/opennist-wasm";
import { setWorkspaceCodecsDocument, useWorkspaceSession } from "@/lib/workspace-session";

type ExportFormat = "png" | "jpeg" | "tiff" | "bmp" | "webp";
type OutputFormat = "wsq" | ExportFormat;

const EXPORT_MIME_TYPES: Record<ExportFormat, string> = {
  png: "image/png",
  jpeg: "image/jpeg",
  tiff: "image/tiff",
  bmp: "image/bmp",
  webp: "image/webp",
};

const BIT_RATE_OPTIONS = [
  { label: "0.75", value: 0.75 },
  { label: "2.25", value: 2.25 },
] as const;

const EXPORT_OPTIONS: ExportFormat[] = ["png", "jpeg", "tiff", "bmp", "webp"];

function bytesToObjectUrl(bytes: Uint8Array, mimeType: string): string {
  return URL.createObjectURL(new Blob([Uint8Array.from(bytes)], { type: mimeType }));
}

function formatByteSize(byteCount: number): string {
  if (byteCount < 1024) {
    return `${byteCount} B`;
  }

  if (byteCount < 1024 * 1024) {
    return `${(byteCount / 1024).toFixed(1)} KB`;
  }

  return `${(byteCount / (1024 * 1024)).toFixed(2)} MB`;
}

async function waitForUiPaint(): Promise<void> {
  await new Promise<void>((resolve) => {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => resolve());
    });
  });
}

function logRuntime(message: string, detail?: unknown): void {
  if (detail instanceof Error) {
    console.error(`[OpenNist] ${message}`, detail);
    return;
  }

  if (typeof detail !== "undefined") {
    console.info(`[OpenNist] ${message}`, detail);
    return;
  }

  console.info(`[OpenNist] ${message}`);
}

export function CodecsWorkspace({
  currentLabel,
  intake,
  incomingFile,
}: {
  currentLabel: string;
  intake: WorkspaceFileIntake;
  incomingFile: File | null;
}) {
  const { codecsDocument, codecsDocumentFingerprint, activeFileFingerprint } = useWorkspaceSession();
  const [document, setDocument] = useState<CodecsWorkspaceDocument | null>(codecsDocument);
  const [bitRate, setBitRate] = useState<(typeof BIT_RATE_OPTIONS)[number]["value"]>(2.25);
  const [outputFormat, setOutputFormat] = useState<OutputFormat>("wsq");
  const [busyMode, setBusyMode] = useState<"preview" | "action" | null>(null);

  const updateDocument = useCallback((next: CodecsWorkspaceDocument, fingerprint: string | null) => {
    setWorkspaceCodecsDocument(next, fingerprint);

    startTransition(() => {
      setDocument(next);
    });
  }, []);

  const withBusy = useCallback(
    async <T,>(message: string, work: () => Promise<T>, mode: "preview" | "action" = "action"): Promise<T> => {
      setBusyMode(mode);
      logRuntime(message);
      await waitForUiPaint();

      try {
        return await work();
      } finally {
        setBusyMode(null);
      }
    },
    [],
  );
  const isBusy = busyMode !== null;
  const showPreviewOverlay = busyMode === "preview" && Boolean(document);
  const showInitialLoader = busyMode === "preview" && !document;

  const handleFile = useCallback(
    async (file: File) => {
      const fileName = file.name;
      const fingerprint = getFileFingerprint(file);

      try {
        if (fileName.toLowerCase().endsWith(".wsq")) {
          const loadedDocument = await withBusy(
            "Decoding WSQ with OpenNist.Wasm and rendering a browser preview.",
            async () => {
              const wsqBytes = new Uint8Array(await file.arrayBuffer());
              const decoded = await decodeWsqBytes(wsqBytes);
              const previewBytes = await exportRawImage(decoded.rawPixels, decoded.width, decoded.height, "png");

              return {
                fileName,
                sourceKind: "WSQ" as const,
                sourceByteCount: wsqBytes.length,
                width: decoded.width,
                height: decoded.height,
                pixelsPerInch: decoded.pixelsPerInch,
                rawPixels: decoded.rawPixels,
                previewUrl: bytesToObjectUrl(previewBytes, "image/png"),
                wsqInfo: decoded.fileInfo,
                wsqBytes,
              };
            },
            "preview",
          );

          updateDocument(loadedDocument, fingerprint);
          logRuntime(
            `Decoded ${fileName} into ${loadedDocument.width}×${loadedDocument.height} grayscale pixels.`,
            loadedDocument.wsqInfo,
          );
          return;
        }

        const loadedDocument = await withBusy(
          "Normalizing the source image through ffmpeg.wasm for WSQ-safe grayscale output.",
          async () => {
            const normalized = await normalizeImageFile(file);

            return {
              fileName,
              sourceKind: "Image" as const,
              sourceByteCount: file.size,
              width: normalized.width,
              height: normalized.height,
              pixelsPerInch: 500,
              rawPixels: normalized.rawPixels,
              previewUrl: bytesToObjectUrl(normalized.previewBytes, "image/png"),
            };
          },
          "preview",
        );

        updateDocument(loadedDocument, fingerprint);
        logRuntime(`Prepared ${fileName} as a grayscale ${loadedDocument.width}×${loadedDocument.height} working raster.`);
      } catch (error) {
        const message = error instanceof Error ? error.message : "The file could not be processed.";
        logRuntime(`Error: ${message}`, error);
      }
    },
    [updateDocument, withBusy],
  );

  useEffect(() => {
    if (!codecsDocument) {
      return;
    }

    setDocument(codecsDocument);
  }, [codecsDocument]);

  useEffect(() => {
    if (!incomingFile) {
      return;
    }

    const fingerprint = getFileFingerprint(incomingFile);

    if (
      codecsDocument &&
      codecsDocumentFingerprint &&
      codecsDocumentFingerprint === fingerprint &&
      activeFileFingerprint === fingerprint
    ) {
      setDocument(codecsDocument);
      return;
    }

    void handleFile(incomingFile);
  }, [activeFileFingerprint, codecsDocument, codecsDocumentFingerprint, handleFile, incomingFile]);

  useEffect(() => {
    if (!document) {
      setOutputFormat("wsq");
      return;
    }

    if (document.sourceKind === "WSQ") {
      setOutputFormat((current) => (current === "wsq" ? "png" : current));
      return;
    }

    setOutputFormat("wsq");
  }, [document]);

  const activeExportFormat = outputFormat === "wsq" ? "png" : outputFormat;

  const onConvertToWsq = useCallback(async () => {
    if (!document) {
      return;
    }

    try {
      const wsqBytes = await withBusy("Encoding the active raster to WSQ.", async () =>
        encodeRawToWsqBytes(
          document.rawPixels,
          document.width,
          document.height,
          document.pixelsPerInch,
          Number(bitRate),
        ),
      );

      setDocument((current) => {
        if (!current) {
          return current;
        }

        const nextDocument = { ...current, wsqBytes };
        setWorkspaceCodecsDocument(nextDocument, activeFileFingerprint);
        return nextDocument;
      });
      downloadBytes(wsqBytes, replaceExtension(document.fileName, ".wsq"), "image/x-wsq");
      logRuntime(`Converted ${document.fileName} to WSQ at ${bitRate} bpp.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "WSQ encoding failed.";
      logRuntime(`Error: ${message}`, error);
    }
  }, [activeFileFingerprint, bitRate, document, withBusy]);

  const onExportImage = useCallback(async () => {
    if (!document) {
      return;
    }

    try {
      const imageBytes = await withBusy(`Rendering a ${activeExportFormat.toUpperCase()} image.`, async () =>
        exportRawImage(document.rawPixels, document.width, document.height, activeExportFormat),
      );

      downloadBytes(
        imageBytes,
        replaceExtension(document.fileName, `.${activeExportFormat === "jpeg" ? "jpg" : activeExportFormat}`),
        EXPORT_MIME_TYPES[activeExportFormat],
      );
      logRuntime(`Exported ${document.fileName} as ${activeExportFormat.toUpperCase()}.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Image export failed.";
      logRuntime(`Error: ${message}`, error);
    }
  }, [activeExportFormat, document, withBusy]);

  const canRunActions = Boolean(document) && !isBusy;
  const showInspector = Boolean(document);
  const { rightDocked, rightInlineVisible, rightOverlayVisible } = useWorkspaceSidebars();
  const showRightSidebar = showInspector && (rightInlineVisible || rightOverlayVisible);
  const actionLabel = outputFormat === "wsq" ? "Convert to WSQ" : `Export ${outputFormat.toUpperCase()}`;
  const handlePrimaryAction = useCallback(() => {
    if (outputFormat === "wsq") {
      void onConvertToWsq();
      return;
    }

    void onExportImage();
  }, [onConvertToWsq, onExportImage, outputFormat]);

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
                Convert between WSQ, JPEG2000, and image formats.
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
              event.preventDefault();
              intake.activateDrag();
            }}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
                intake.deactivateDrag();
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
                        Opening file
                      </p>
                      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                        Preparing the image for preview and conversion.
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
              ) : (
                <div className="mx-auto flex max-w-[520px] items-center justify-center px-8">
                  <div className="space-y-5 text-center">
                    <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                      <Fingerprint className="size-11" />
                    </div>
                    <div>
                      <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                        WSQ and JPEG2000 preview surface
                      </p>
                      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                        Open a biometric image or WSQ file to decode it, inspect it, or create a WSQ version.
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
        <CodecsInspector
          document={document!}
          bitRate={bitRate}
          outputFormat={outputFormat}
          isBusy={isBusy}
          canRunActions={canRunActions}
          onSelectBitRate={setBitRate}
          onSelectOutputFormat={setOutputFormat}
          onPrimaryAction={handlePrimaryAction}
          actionLabel={actionLabel}
          rightDocked={rightDocked}
          rightOverlayVisible={rightOverlayVisible}
        />
      ) : null}
    </>
  );
}

function CodecsInspector({
  document,
  bitRate,
  outputFormat,
  isBusy,
  canRunActions,
  onSelectBitRate,
  onSelectOutputFormat,
  onPrimaryAction,
  actionLabel,
  rightDocked,
  rightOverlayVisible,
}: {
  document: CodecsWorkspaceDocument;
  bitRate: (typeof BIT_RATE_OPTIONS)[number]["value"];
  outputFormat: OutputFormat;
  isBusy: boolean;
  canRunActions: boolean;
  onSelectBitRate(value: (typeof BIT_RATE_OPTIONS)[number]["value"]): void;
  onSelectOutputFormat(value: OutputFormat): void;
  onPrimaryAction(): void;
  actionLabel: string;
  rightDocked: boolean;
  rightOverlayVisible: boolean;
}) {
  const popoverId = useId();
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [isPopoverOpen, setIsPopoverOpen] = useState(false);
  const availableFormats: OutputFormat[] = document.sourceKind === "WSQ" ? EXPORT_OPTIONS : ["wsq"];
  const outputLabel = outputFormat === "wsq" ? `WSQ · ${bitRate.toFixed(2)} bpp` : outputFormat.toUpperCase();

  const positionPopover = useCallback(() => {
    const trigger = triggerRef.current;
    const popover = popoverRef.current;

    if (!trigger || !popover) {
      return;
    }

    const triggerRect = trigger.getBoundingClientRect();
    const gutter = 16;
    const width = Math.max(triggerRect.width, 224);
    const left = Math.min(
      Math.max(triggerRect.left, gutter),
      Math.max(gutter, window.innerWidth - width - gutter),
    );

    popover.style.position = "fixed";
    popover.style.inset = "auto";
    popover.style.margin = "0";
    popover.style.width = `${width}px`;
    popover.style.left = `${left}px`;

    const popoverHeight = popover.offsetHeight;
    const top = Math.min(
      triggerRect.bottom + 8,
      Math.max(gutter, window.innerHeight - popoverHeight - gutter),
    );

    popover.style.top = `${top}px`;
  }, []);

  useEffect(() => {
    const popover = popoverRef.current;

    if (!popover) {
      return;
    }

    const handleToggle = () => {
      const open = popover.matches(":popover-open");
      setIsPopoverOpen(open);

      if (open) {
        requestAnimationFrame(positionPopover);
      }
    };

    popover.addEventListener("toggle", handleToggle);
    return () => {
      popover.removeEventListener("toggle", handleToggle);
    };
  }, [positionPopover]);

  useEffect(() => {
    if (!isPopoverOpen) {
      return;
    }

    const reposition = () => {
      requestAnimationFrame(positionPopover);
    };

    window.addEventListener("resize", reposition);
    window.addEventListener("scroll", reposition, true);

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, true);
    };
  }, [isPopoverOpen, positionPopover]);

  return (
    <InspectorPanel
      title={document.fileName}
      summary="Conversion settings, image details, and WSQ metadata shown with plain-language explanations."
      rightDocked={rightDocked}
      rightOverlayVisible={rightOverlayVisible}
    >
          <div className="space-y-3">
            <SectionTitle>Output</SectionTitle>
            <p className="text-sm leading-6 text-[var(--color-on-surface-variant)]">
              Choose the format you want to download. WSQ is the fingerprint-specific compression format; the other options are regular image files.
            </p>
            <div className="space-y-3">
              <div className="relative">
                <Button
                  ref={triggerRef}
                  type="button"
                  variant="outline"
                  className="w-full justify-between rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
                  onClick={() => {
                    const popover = popoverRef.current;

                    if (!popover) {
                      return;
                    }

                    if (popover.matches(":popover-open")) {
                      popover.hidePopover();
                      return;
                    }

                    positionPopover();
                    popover.showPopover();
                  }}
                >
                  <span>{outputLabel}</span>
                  <ChevronDown className="size-4 opacity-70" />
                </Button>
                <div
                  ref={popoverRef}
                  id={popoverId}
                  popover="auto"
                  className="rounded-[var(--radius-xl)] border border-[color:var(--effect-ghost-border)] bg-white p-2 text-[var(--color-on-surface)] shadow-[var(--effect-modal-shadow)] backdrop:bg-black/20"
                >
                  <div className="space-y-3">
                    <div className="space-y-1">
                      <p className="px-3 pt-1 font-mono text-[0.62rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                        Format
                      </p>
                      {availableFormats.map((option) => (
                        <button
                          key={option}
                          type="button"
                          className={`flex w-full items-center justify-between rounded-[var(--radius-lg)] px-3 py-2 text-left text-sm transition-colors ${
                            outputFormat === option
                              ? "bg-[var(--color-primary-fixed)]/30 text-[var(--color-primary)]"
                              : "hover:bg-[var(--color-surface-container-low)]"
                          }`}
                          onClick={() => {
                            onSelectOutputFormat(option);

                            if (option !== "wsq") {
                              popoverRef.current?.hidePopover();
                            }
                          }}
                        >
                          <span>{option === "wsq" ? "WSQ" : option.toUpperCase()}</span>
                          {outputFormat === option ? <Check className="size-4" /> : null}
                        </button>
                      ))}
                    </div>

                    {outputFormat === "wsq" ? (
                      <div className="space-y-1 border-t border-[color:var(--effect-ghost-border)] pt-2">
                        <p className="px-3 pt-1 font-mono text-[0.62rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                          WSQ bitrate
                        </p>
                        {BIT_RATE_OPTIONS.map((option) => (
                          <button
                            key={option.value}
                            type="button"
                            className={`flex w-full items-center justify-between rounded-[var(--radius-lg)] px-3 py-2 text-left text-sm transition-colors ${
                              bitRate === option.value
                                ? "bg-[var(--color-primary-fixed)]/30 text-[var(--color-primary)]"
                                : "hover:bg-[var(--color-surface-container-low)]"
                            }`}
                            onClick={() => {
                              onSelectBitRate(option.value);
                              popoverRef.current?.hidePopover();
                            }}
                          >
                            <span>{option.label} bpp</span>
                            {bitRate === option.value ? <Check className="size-4" /> : null}
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </div>
                </div>
              </div>

              <Button
                type="button"
                className="w-full rounded-[var(--radius-lg)] bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
                disabled={!canRunActions}
                onClick={onPrimaryAction}
              >
                {isBusy ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : outputFormat === "wsq" ? (
                  <FileArchive className="size-4" />
                ) : (
                  <ArrowDownToLine className="size-4" />
                )}
                {actionLabel}
              </Button>
            </div>
          </div>

          <InspectorSection
            title="File details"
            description="Core information about the image currently shown in the preview."
            items={[
              {
                label: "Format",
                value: document.sourceKind === "WSQ" ? "WSQ" : "Image",
                description: "WSQ is a fingerprint-focused compression format. Image means a regular raster file like PNG, TIFF, or JPEG2000.",
              },
              {
                label: "Original file size",
                value: formatByteSize(document.sourceByteCount),
                description: "Size of the source file before any conversion or decoding.",
              },
              {
                label: "Image size",
                value: `${document.width} × ${document.height}`,
                description: "Pixel width and height of the grayscale working image shown in the app.",
              },
              {
                label: "Resolution",
                value: `${document.pixelsPerInch} ppi`,
                description: "Pixels per inch. Fingerprint workflows commonly expect 500 ppi images.",
              },
            ]}
          />

          {document.wsqInfo ? <WsqInfoSection wsqInfo={document.wsqInfo} /> : null}
    </InspectorPanel>
  );
}

function WsqInfoSection({ wsqInfo }: { wsqInfo: WsqFileInfo }) {
  return (
    <div className="space-y-7">
      <InspectorSection
        title="WSQ details"
        description="Technical details from the WSQ file header. These are mainly useful when debugging a compression workflow."
        items={[
          {
            label: "Bits per pixel",
            value: `${wsqInfo.bitsPerPixel}`,
            description: "Gray depth stored for each pixel after decoding the image.",
          },
          {
            label: "Black and white levels",
            value: `${wsqInfo.black} / ${wsqInfo.white}`,
            description: "Tone endpoints used by the encoder for the darkest and lightest grayscale values.",
          },
          {
            label: "Wavelet filter sizes",
            value: `${wsqInfo.highPassFilterLength} / ${wsqInfo.lowPassFilterLength}`,
            description: "The high-pass and low-pass filter lengths used by WSQ compression.",
          },
          {
            label: "Encoded block count",
            value: `${wsqInfo.blockCount}`,
            description: "How many compressed data blocks were written into the WSQ stream.",
          },
          {
            label: "Compressed payload",
            value: `${wsqInfo.encodedBlockByteCount} bytes`,
            description: "Byte size of the compressed image data inside the WSQ file.",
          },
          {
            label: "Embedded comments",
            value: `${wsqInfo.commentCount}`,
            description: "Number of text metadata entries stored alongside the image.",
          },
        ]}
      />

      {wsqInfo.comments.length > 0 ? (
        <div className="space-y-3">
          <SectionTitle>Comments</SectionTitle>
          <p className="text-sm leading-6 text-[var(--color-on-surface-variant)]">
            Free-form metadata embedded in the WSQ file. Different tools may use these comments differently.
          </p>
          {wsqInfo.comments.map((comment, index) => (
            <CommentCard key={`${index}-${comment.text}`} comment={comment} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function CommentCard({ comment }: { comment: WsqCommentInfo }) {
  const fields = Object.entries(comment.fields);

  return (
    <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-low)] p-4">
      <p className="text-sm leading-7 text-[var(--color-on-surface)]">{comment.text || "WSQ comment"}</p>
      {fields.length > 0 ? (
        <div className="mt-3 grid gap-2">
          {fields.map(([key, value]) => (
            <div key={key} className="flex items-start justify-between gap-3">
              <p className="font-mono text-[0.62rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                {key}
              </p>
              <p className="text-right text-sm text-[var(--color-on-surface)]">{value}</p>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function SectionTitle({ children }: { children: string }) {
  return (
    <p className="border-b border-[color:var(--effect-ghost-border)] pb-2 font-mono text-[0.66rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
      {children}
    </p>
  );
}
