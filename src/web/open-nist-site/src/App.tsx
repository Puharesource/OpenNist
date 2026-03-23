import { useEffect, useId, useRef, useState, startTransition, useDeferredValue } from "react";
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  ChevronDown,
  FileArchive,
  Image as ImageIcon,
  LoaderCircle,
  Workflow,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import openNistLogo from "@/assets/opennist-logo.svg";
import { exportRawImage, normalizeImageFile } from "@/lib/ffmpeg-wasm";
import { downloadBytes, replaceExtension } from "@/lib/download";
import {
  decodeWsqBytes,
  encodeRawToWsqBytes,
  getOpenNistVersion,
  type WsqCommentInfo,
  type WsqFileInfo,
} from "@/lib/opennist-wasm";

type ExportFormat = "png" | "jpeg" | "tiff" | "bmp" | "webp";
type SourceKind = "WSQ" | "Image";

type WorkspaceDocument = {
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

const EXPORT_MIME_TYPES: Record<ExportFormat, string> = {
  png: "image/png",
  jpeg: "image/jpeg",
  tiff: "image/tiff",
  bmp: "image/bmp",
  webp: "image/webp",
};

const BIT_RATE_OPTIONS = [
  { label: "0.75 bpp", value: "0.75" },
  { label: "2.25 bpp", value: "2.25" },
] as const;

const EXPORT_OPTIONS = [
  { label: "PNG", value: "png" },
  { label: "JPEG", value: "jpeg" },
  { label: "TIFF", value: "tiff" },
  { label: "BMP", value: "bmp" },
  { label: "WEBP", value: "webp" },
] as const;

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

function App() {
  const [document, setDocument] = useState<WorkspaceDocument | null>(null);
  const deferredDocument = useDeferredValue(document);
  const [bitRate, setBitRate] = useState<(typeof BIT_RATE_OPTIONS)[number]["value"]>("2.25");
  const [exportFormat, setExportFormat] = useState<ExportFormat>("png");
  const [isBusy, setIsBusy] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    logRuntime("OpenNist.Wasm and ffmpeg.wasm are lazy-loaded on first conversion.");

    void getOpenNistVersion()
      .then((version) => {
        logRuntime(`OpenNist.Wasm runtime ready (${version}).`);
      })
      .catch((error) => {
        logRuntime("OpenNist.Wasm runtime version is unavailable.", error);
      });
  }, []);

  useEffect(() => {
    return () => {
      if (document) {
        URL.revokeObjectURL(document.previewUrl);
      }
    };
  }, [document]);

  const canRunActions = Boolean(document) && !isBusy;
  const canExportImage = document?.sourceKind === "WSQ" && !isBusy;
  const wsqActionLabel = document?.sourceKind === "Image" ? "Convert To WSQ" : "Download WSQ";

  function updateDocument(next: WorkspaceDocument) {
    startTransition(() => {
      setDocument((current) => {
        if (current) {
          URL.revokeObjectURL(current.previewUrl);
        }

        return next;
      });
    });
  }

  async function withBusy<T>(message: string, work: () => Promise<T>): Promise<T> {
    setIsBusy(true);
    logRuntime(message);

    try {
      const result = await work();
      return result;
    } finally {
      setIsBusy(false);
    }
  }

  async function handleFile(file: File) {
    const fileName = file.name;

    try {
      if (fileName.toLowerCase().endsWith(".wsq")) {
        const loadedDocument = await withBusy("Decoding WSQ with OpenNist.Wasm and rendering a browser preview.", async () => {
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
        });

        updateDocument(loadedDocument);
        logRuntime(`Decoded ${fileName} into ${loadedDocument.width}×${loadedDocument.height} grayscale pixels.`, loadedDocument.wsqInfo);
        return;
      }

      const loadedDocument = await withBusy("Normalizing the source image through ffmpeg.wasm for WSQ-safe grayscale output.", async () => {
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
      });

      updateDocument(loadedDocument);
      logRuntime(`Prepared ${fileName} as a grayscale ${loadedDocument.width}×${loadedDocument.height} working raster.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "The file could not be processed.";
      logRuntime(`Error: ${message}`, error);
    }
  }

  async function onDownloadWsq() {
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

      setDocument((current) => (current ? { ...current, wsqBytes } : current));
      downloadBytes(wsqBytes, replaceExtension(document.fileName, ".wsq"), "image/x-wsq");
      logRuntime(`WSQ downloaded at ${bitRate} bpp.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "WSQ encoding failed.";
      logRuntime(`Error: ${message}`, error);
    }
  }

  async function onExportImage() {
    if (!document) {
      return;
    }

    try {
      const imageBytes = await withBusy(`Rendering a ${exportFormat.toUpperCase()} export through ffmpeg.wasm.`, async () =>
        exportRawImage(document.rawPixels, document.width, document.height, exportFormat),
      );

      downloadBytes(
        imageBytes,
        replaceExtension(document.fileName, `.${exportFormat === "jpeg" ? "jpg" : exportFormat}`),
        EXPORT_MIME_TYPES[exportFormat],
      );
      logRuntime(`${exportFormat.toUpperCase()} export ready.`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Image export failed.";
      logRuntime(`Error: ${message}`, error);
    }
  }

  function onBrowseClick() {
    fileInputRef.current?.click();
  }

  function onFileInputChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    void handleFile(file);
    event.target.value = "";
  }

  function onDrop(event: React.DragEvent<HTMLButtonElement>) {
    event.preventDefault();
    if (isBusy) {
      return;
    }

    const file = event.dataTransfer.files?.[0];
    if (!file) {
      return;
    }

    void handleFile(file);
  }

  function onDragOver(event: React.DragEvent<HTMLButtonElement>) {
    event.preventDefault();
    event.dataTransfer.dropEffect = isBusy ? "none" : "copy";
  }

  return (
    <div className="relative overflow-hidden">
      <div className="mx-auto flex min-h-screen w-full max-w-[1440px] flex-col px-6 pb-10 pt-6 md:px-10">
        <header className="glass-panel ghost-outline sticky top-6 z-20 flex items-center justify-between rounded-[var(--radius-lg)] px-5 py-4">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-[var(--radius-md)] bg-[var(--color-surface-container-high)] shadow-[var(--effect-modal-shadow)]">
              <img src={openNistLogo} alt="OpenNist logo" className="size-7" />
            </div>
            <div>
              <p className="font-display text-base font-semibold tracking-[-0.03em] text-foreground">OpenNist</p>
            </div>
          </div>
        </header>

        <main className="flex flex-1 flex-col gap-10 pt-10">
          <section className="grid gap-8 lg:grid-cols-[minmax(0,1.35fr)_minmax(360px,0.9fr)]">
            <div className="space-y-6">
              <Card className="surface-module glass-panel border-0 shadow-[var(--effect-modal-shadow)]">
                <CardHeader className="pb-2">
                  <CardTitle className="font-display text-[1.2rem] tracking-[-0.03em]">Drop Zone</CardTitle>
                  <CardDescription>
                    Click to browse or drag a WSQ, PNG, JPEG, TIFF, BMP, HEIC, GIF, or WebP file into the workspace.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <button
                    type="button"
                    onClick={onBrowseClick}
                    onDrop={onDrop}
                    onDragOver={onDragOver}
                    className="pixel-grid-overlay ghost-outline relative flex min-h-[340px] w-full flex-col items-center justify-center gap-5 overflow-hidden rounded-[var(--radius-lg)] bg-[var(--color-surface-container-low)] px-8 py-10 text-left transition-transform hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary-fixed)]"
                  >
                    {deferredDocument ? (
                      <>
                        <img
                          src={deferredDocument.previewUrl}
                          alt={deferredDocument.fileName}
                          className="max-h-[280px] w-auto max-w-full rounded-[var(--radius-md)] object-contain shadow-[var(--effect-modal-shadow)]"
                        />
                        <div className="flex flex-wrap items-center justify-center gap-2">
                          <Badge className="bg-[var(--color-surface-container-high)] px-3 py-1 text-[var(--color-on-surface)]">
                            {deferredDocument.sourceKind}
                          </Badge>
                          <Badge className="bg-[var(--color-secondary-container)] px-3 py-1 text-[var(--color-secondary)]">
                            {deferredDocument.width} × {deferredDocument.height}
                          </Badge>
                          <Badge className="bg-[var(--color-tertiary-fixed)] px-3 py-1 text-[var(--color-primary)]">
                            {deferredDocument.pixelsPerInch} ppi
                          </Badge>
                        </div>
                      </>
                    ) : (
                      <>
                        <div className="flex size-20 items-center justify-center rounded-full bg-[var(--color-surface-container-highest)] text-[var(--color-primary)]">
                          <ArrowUpFromLine className="size-8" />
                        </div>
                        <div className="space-y-2 text-center">
                          <p className="font-display text-2xl tracking-[-0.04em] text-foreground">Open a working file</p>
                          <p className="mx-auto max-w-md text-sm leading-6 text-[var(--color-on-surface-variant)]">
                            Every non-WSQ image is normalized with ffmpeg.wasm before it touches the WSQ encoder.
                          </p>
                        </div>
                      </>
                    )}
                  </button>

                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".wsq,image/*,.bmp,.gif,.heic,.heif,.jpeg,.jpg,.png,.tif,.tiff,.webp"
                    className="hidden"
                    onChange={onFileInputChange}
                  />
                </CardContent>
              </Card>
            </div>

            <div className="grid gap-6">
              <Card className="surface-module border-0 shadow-[var(--effect-modal-shadow)]">
                <CardHeader>
                  <CardTitle className="font-display text-[1.15rem] tracking-[-0.03em]">Conversion Controls</CardTitle>
                  <CardDescription>Minimal controls, explicit output.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="space-y-2">
                      <p className="font-mono text-[0.72rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                        WSQ bitrate
                      </p>
                      <PopoverFieldSelect
                        ariaLabel="WSQ bitrate"
                        value={bitRate}
                        options={BIT_RATE_OPTIONS}
                        onChange={(value) => setBitRate(value as (typeof BIT_RATE_OPTIONS)[number]["value"])}
                      />
                    </div>

                    {document?.sourceKind === "WSQ" ? (
                      <div className="space-y-2">
                        <p className="font-mono text-[0.72rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                          Image export
                        </p>
                        <PopoverFieldSelect
                          ariaLabel="Image export format"
                          value={exportFormat}
                          options={EXPORT_OPTIONS}
                          onChange={(value) => setExportFormat(value as ExportFormat)}
                        />
                      </div>
                    ) : (
                      <div className="space-y-2">
                        <p className="font-mono text-[0.72rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                          Output mode
                        </p>
                        <div className="flex h-11 items-center rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 text-sm text-[var(--color-on-surface)]">
                          WSQ only
                        </div>
                      </div>
                    )}
                  </div>

                  <div className="grid gap-3">
                    <Button
                      size="lg"
                      className="h-12 rounded-[var(--radius-lg)] border-0 hover:opacity-95"
                      style={{
                        background: "var(--color-primary)",
                        color: "var(--on-primary)",
                      }}
                      disabled={!canRunActions}
                      onClick={() => void onDownloadWsq()}
                    >
                      {isBusy ? <LoaderCircle className="size-4 animate-spin" /> : <FileArchive className="size-4" />}
                      {wsqActionLabel}
                    </Button>
                    {document?.sourceKind === "Image" ? (
                      <p className="px-1 text-sm text-[var(--color-on-surface-variant)]">
                        Standard image uploads can only be converted into WSQ output.
                      </p>
                    ) : null}
                    {document?.sourceKind === "WSQ" ? (
                      <Button
                        variant="outline"
                        size="lg"
                        className="h-12 rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-high)]"
                        disabled={!canExportImage}
                        onClick={() => void onExportImage()}
                      >
                        {isBusy ? <LoaderCircle className="size-4 animate-spin" /> : <ArrowDownToLine className="size-4" />}
                        Export {exportFormat.toUpperCase()}
                      </Button>
                    ) : null}
                  </div>

                  <Separator className="bg-[color:var(--effect-ghost-border)]" />

                  <div className="grid gap-3 sm:grid-cols-3">
                    <MetricCard label="Source" value={document?.sourceKind ?? "Idle"} icon={<ImageIcon className="size-4" />} />
                    <MetricCard
                      label="Pixels"
                      value={document ? `${document.width * document.height}` : "0"}
                      icon={<Workflow className="size-4" />}
                    />
                    <MetricCard
                      label="WSQ Ready"
                      value={document?.wsqBytes ? "Yes" : "Pending"}
                      icon={<FileArchive className="size-4" />}
                    />
                  </div>
                </CardContent>
              </Card>

              <Card className="surface-module border-0 shadow-[var(--effect-modal-shadow)]">
                <CardHeader>
                  <CardTitle className="font-display text-[1.15rem] tracking-[-0.03em]">
                    {document?.sourceKind === "WSQ" ? "WSQ Details" : "Working Raster"}
                  </CardTitle>
                  <CardDescription>
                    {document?.sourceKind === "WSQ"
                      ? "Container and frame metadata read directly from the WSQ file."
                      : "Current browser-side raster prepared for WSQ encoding."}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <FileDetailsPanel document={document} bitRate={bitRate} />
                </CardContent>
              </Card>
            </div>
          </section>
        </main>
      </div>
    </div>
  );
}

function FileDetailsPanel({
  document,
  bitRate,
}: {
  document: WorkspaceDocument | null;
  bitRate: string;
}) {
  if (!document) {
    return (
      <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 py-4 text-sm text-[var(--color-on-surface-variant)]">
        Open a file to inspect its working raster and any available WSQ metadata.
      </div>
    );
  }

  const pixelCount = document.width * document.height;
  const wsqByteCount =
    document.wsqBytes?.length ?? (document.sourceKind === "WSQ" ? document.sourceByteCount : undefined);
  const effectiveBitsPerPixel = wsqByteCount ? ((wsqByteCount * 8) / pixelCount).toFixed(3) : null;
  const compressionRatio = wsqByteCount ? (pixelCount / wsqByteCount).toFixed(2) : null;

  const primaryDetails: Array<{ label: string; value: string }> = [
    { label: "File", value: document.fileName },
    { label: "Source", value: document.sourceKind },
    { label: "File Size", value: formatByteSize(document.sourceByteCount) },
    { label: "Dimensions", value: `${document.width} × ${document.height}` },
    { label: "Pixels", value: pixelCount.toLocaleString() },
    { label: "Resolution", value: `${document.pixelsPerInch} ppi` },
    { label: "Target Bitrate", value: `${bitRate} bpp` },
  ];

  if (effectiveBitsPerPixel) {
    primaryDetails.push({ label: "Encoded Bitrate", value: `${effectiveBitsPerPixel} bpp` });
  }

  if (compressionRatio) {
    primaryDetails.push({ label: "Compression Ratio", value: `${compressionRatio}:1` });
  }

  const wsqInfo = document.wsqInfo;

  return (
    <div className="space-y-4">
      <DetailGrid items={primaryDetails} />

      {wsqInfo ? (
        <>
          <Separator className="bg-[color:var(--effect-ghost-border)]" />
          <DetailGrid
            items={[
              { label: "Black", value: wsqInfo.black.toString() },
              { label: "White", value: wsqInfo.white.toString() },
              { label: "Shift", value: wsqInfo.shift.toFixed(6) },
              { label: "Scale", value: wsqInfo.scale.toFixed(6) },
              { label: "Encoder", value: wsqInfo.wsqEncoder.toString() },
              { label: "Software Impl.", value: wsqInfo.softwareImplementationNumber.toString() },
              { label: "Low-pass Filter", value: wsqInfo.lowPassFilterLength.toString() },
              { label: "High-pass Filter", value: wsqInfo.highPassFilterLength.toString() },
              { label: "Bin Center", value: wsqInfo.quantizationBinCenter.toFixed(6) },
              { label: "Huffman Tables", value: wsqInfo.huffmanTableIds.join(", ") || "None" },
              { label: "Block Count", value: wsqInfo.blockCount.toString() },
              { label: "Encoded Blocks", value: formatByteSize(wsqInfo.encodedBlockByteCount) },
              { label: "Comments", value: wsqInfo.commentCount.toString() },
              { label: "NIST Comments", value: wsqInfo.nistCommentCount.toString() },
            ]}
          />

          {wsqInfo.comments.length > 0 ? (
            <div className="space-y-3">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
                WSQ Comment Segments
              </p>
              <div className="grid gap-3">
                {wsqInfo.comments.map((comment, index) => (
                  <CommentCard key={`${index}-${comment.text}`} comment={comment} />
                ))}
              </div>
            </div>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

function DetailGrid({
  items,
}: {
  items: ReadonlyArray<{ label: string; value: string }>;
}) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {items.map((item) => (
        <div
          key={`${item.label}-${item.value}`}
          className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 py-3"
        >
          <p className="font-mono text-[0.68rem] uppercase tracking-[0.16em] text-[var(--color-on-surface-variant)]">
            {item.label}
          </p>
          <p className="mt-2 break-words text-sm text-[var(--color-on-surface)]">{item.value}</p>
        </div>
      ))}
    </div>
  );
}

function CommentCard({ comment }: { comment: WsqCommentInfo }) {
  const fieldEntries = Object.entries(comment.fields);

  return (
    <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 py-4">
      {fieldEntries.length > 0 ? (
        <div className="grid gap-2 sm:grid-cols-2">
          {fieldEntries.map(([key, value]) => (
            <div key={`${key}-${value}`}>
              <p className="font-mono text-[0.68rem] uppercase tracking-[0.16em] text-[var(--color-on-surface-variant)]">
                {key}
              </p>
              <p className="mt-1 break-words text-sm text-[var(--color-on-surface)]">{value}</p>
            </div>
          ))}
        </div>
      ) : (
        <p className="break-words text-sm text-[var(--color-on-surface)]">{comment.text}</p>
      )}
    </div>
  );
}

function PopoverFieldSelect<TValue extends string>({
  ariaLabel,
  value,
  options,
  onChange,
}: {
  ariaLabel: string;
  value: TValue;
  options: ReadonlyArray<{ label: string; value: TValue }>;
  onChange: (value: TValue) => void;
}) {
  const popoverId = useId();
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number; width: number }>({
    top: 0,
    left: 0,
    width: 0,
  });

  const selectedOption = options.find((option) => option.value === value) ?? options[0];

  function syncPosition() {
    if (!buttonRef.current) {
      return;
    }

    const bounds = buttonRef.current.getBoundingClientRect();
    setPosition({
      top: bounds.bottom + 8,
      left: bounds.left,
      width: bounds.width,
    });
  }

  function togglePopover() {
    const popover = popoverRef.current;
    if (!popover) {
      return;
    }

    syncPosition();
    popover.togglePopover();
  }

  function closePopover() {
    const popover = popoverRef.current;
    if (!popover) {
      return;
    }

    if (popover.matches(":popover-open")) {
      popover.hidePopover();
    }
  }

  useEffect(() => {
    const popover = popoverRef.current;
    if (!popover) {
      return;
    }

    const handleToggle = (event: Event) => {
      const toggleEvent = event as ToggleEvent;
      const nextOpen = toggleEvent.newState === "open";
      setIsOpen(nextOpen);

      if (nextOpen) {
        syncPosition();
      }
    };

    popover.addEventListener("toggle", handleToggle);
    return () => {
      popover.removeEventListener("toggle", handleToggle);
    };
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handleViewportChange = () => {
      syncPosition();
    };

    window.addEventListener("resize", handleViewportChange);
    window.addEventListener("scroll", handleViewportChange, true);

    return () => {
      window.removeEventListener("resize", handleViewportChange);
      window.removeEventListener("scroll", handleViewportChange, true);
    };
  }, [isOpen]);

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        aria-label={ariaLabel}
        aria-expanded={isOpen}
        aria-controls={popoverId}
        className="flex h-11 w-full items-center justify-between rounded-[var(--radius-lg)] border-0 bg-[var(--color-surface-container-high)] px-4 text-sm text-[var(--color-on-surface)] outline-none transition-colors hover:bg-[var(--color-surface-container-highest)] focus-visible:ring-2 focus-visible:ring-[var(--color-primary-fixed)]"
        onClick={togglePopover}
      >
        <span>{selectedOption.label}</span>
        <ChevronDown className={`size-4 text-[var(--color-on-surface-variant)] transition-transform ${isOpen ? "rotate-180" : ""}`} />
      </button>

      <div
        id={popoverId}
        ref={popoverRef}
        popover="auto"
        className="m-0 rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-lowest)] p-2 text-[var(--color-on-surface)] shadow-[var(--effect-modal-shadow)] backdrop:bg-transparent"
        style={{
          inset: "auto auto auto 0",
          left: `${position.left}px`,
          top: `${position.top}px`,
          width: `${position.width}px`,
        }}
      >
        <div className="grid gap-1">
          {options.map((option) => {
            const isSelected = option.value === value;

            return (
              <button
                key={option.value}
                type="button"
                className={`flex w-full items-center rounded-[var(--radius-md)] px-3 py-2 text-left text-sm transition-colors ${
                  isSelected
                    ? "bg-[var(--color-secondary-container)] text-[var(--color-secondary)]"
                    : "text-[var(--color-on-surface)] hover:bg-[var(--color-surface-container-high)]"
                }`}
                onClick={() => {
                  onChange(option.value);
                  closePopover();
                }}
              >
                {option.label}
              </button>
            );
          })}
        </div>
      </div>
    </>
  );
}

function MetricCard({
  label,
  value,
  icon,
}: {
  label: string;
  value: string;
  icon: React.ReactNode;
}) {
  return (
    <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 py-4">
      <div className="mb-3 flex items-center gap-2 text-[var(--color-on-surface-variant)]">{icon}<span className="font-mono text-[0.72rem] uppercase tracking-[0.18em]">{label}</span></div>
      <p className="font-display text-xl tracking-[-0.04em] text-foreground">{value}</p>
    </div>
  );
}

export default App;
