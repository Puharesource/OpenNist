import { startTransition, useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "@tanstack/react-router";
import { createPortal } from "react-dom";
import {
  AlertTriangle,
  Binary,
  Check,
  ChevronDown,
  ChevronRight,
  CloudDownload,
  Copy,
  ExternalLink,
  FileArchive,
  FileImage,
  FileSignature,
  FileText,
  Fingerprint,
  FolderTree,
  Hash,
  ImageIcon,
  ListTree,
  LoaderCircle,
  Maximize2,
  ScanFace,
  ScanSearch,
  X,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { InspectorInfoButton, InspectorNotice, InspectorPanel, InspectorSection } from "@/components/workspace-inspector";
import { WorkspaceSidebarToggleGroup, useWorkspaceSidebars } from "@/components/workspace-sidebars";
import { type NistWorkspaceDocument, getFileFingerprint } from "@/lib/codecs-document";
import { downloadBytes } from "@/lib/download";
import { exportRawImage, normalizeImageFile } from "@/lib/ffmpeg-wasm";
import { getNistFieldMetadata, getNistRecordMetadata } from "@/lib/nist-metadata";
import { type NistFieldInfo, type NistRecordInfo, decodeWsqBytes, inspectNistBytes } from "@/lib/opennist-wasm";
import { isNistTransactionFileName, type WorkspaceFileIntake } from "@/lib/workspace-file-intake";
import { setWorkspaceActiveFile, setWorkspaceNistDocument, useWorkspaceSession } from "@/lib/workspace-session";

type NistWorkspaceError = {
  fileName: string;
  message: string;
};

type NistSelection =
  | { kind: "record"; recordIndex: number }
  | { kind: "field"; recordIndex: number; fieldIndex: number };

type ResolvedNistSelection =
  | { kind: "record"; record: NistRecordInfo; recordIndex: number }
  | { kind: "field"; record: NistRecordInfo; field: NistDisplayField; recordIndex: number; fieldIndex: number };

type NistDisplayField = NistFieldInfo & {
  fieldNumber: number | null;
  key: string;
  source: "parsed" | "binary-header" | "binary-payload";
  subfieldItems: string[][];
  binaryBytes?: Uint8Array;
};

type NistPreviewState =
  | { status: "idle" }
  | { status: "loading"; message: string }
  | { status: "unsupported"; message: string }
  | { status: "error"; message: string }
  | { status: "ready"; objectUrl: string; alt: string; details: string };

type NistBinaryAsset = {
  bytes: Uint8Array;
  fileName: string;
  mimeType: string;
};

const MAX_BINARY_PREVIEW_PIXELS = 4_000_000;
const MAX_BINARY_PREVIEW_BYTES = 12 * 1024 * 1024;

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

function buildRecordKey(recordIndex: number): string {
  return `record:${recordIndex}`;
}

function buildSubfieldKey(recordIndex: number, fieldKey: string, subfieldIndex: number): string {
  return `subfield:${recordIndex}:${fieldKey}:${subfieldIndex}`;
}

function buildFieldKey(recordIndex: number, fieldKey: string): string {
  return `field:${recordIndex}:${fieldKey}`;
}

function canRenderOpaqueRecordPreview(record: NistRecordInfo): boolean {
  return record.type === 4 || record.type === 7 || record.type === 8;
}

function getInitialCollapsedRecordKeys(document: NistWorkspaceDocument | null): Record<string, boolean> {
  if (!document) {
    return {};
  }

  return Object.fromEntries(document.fileInfo.records.map((_, recordIndex) => [buildRecordKey(recordIndex), true]));
}

function normalizeFieldPreview(value: string, limit = 96): string {
  const normalized = value
    .replaceAll(String.fromCharCode(0x1f), " | ")
    .replaceAll(String.fromCharCode(0x1e), " / ")
    .replaceAll(/\s+/g, " ")
    .trim();

  if (normalized.length <= limit) {
    return normalized || "(empty)";
  }

  return `${normalized.slice(0, Math.max(0, limit - 1))}\u2026`;
}

function tryParseFieldNumber(tag: string): number | null {
  const separatorIndex = tag.indexOf(".");
  if (separatorIndex < 0) {
    return null;
  }

  const fieldNumber = Number.parseInt(tag.slice(separatorIndex + 1), 10);
  return Number.isFinite(fieldNumber) ? fieldNumber : null;
}

function toLatin1Bytes(value: string): Uint8Array {
  const bytes = new Uint8Array(value.length);

  for (let index = 0; index < value.length; index += 1) {
    bytes[index] = value.charCodeAt(index) & 0xff;
  }

  return bytes;
}

function createObjectUrl(bytes: Uint8Array, mimeType: string): string {
  return URL.createObjectURL(new Blob([Uint8Array.from(bytes)], { type: mimeType }));
}

function normalizeAssetBaseName(fileName: string): string {
  const extensionIndex = fileName.lastIndexOf(".");
  return extensionIndex < 0 ? fileName : fileName.slice(0, extensionIndex);
}

function getRecordBytes(document: NistWorkspaceDocument, record: NistRecordInfo): Uint8Array {
  return document.sourceBytes.subarray(record.byteOffset, record.byteOffset + record.encodedByteCount);
}

function createDisplayField(
  tag: string,
  value: string,
  source: NistDisplayField["source"],
  occurrence: number,
): NistDisplayField {
  return {
    tag,
    value,
    subfieldCount: 1,
    itemCount: 1,
    fieldNumber: tryParseFieldNumber(tag),
    key: `${source}:${tag}:${occurrence}`,
    source,
    subfieldItems: [[value]],
  };
}

function createBinaryPayloadField(tag: string, bytes: Uint8Array, occurrence: number): NistDisplayField {
  return {
    tag,
    value: "",
    subfieldCount: 1,
    itemCount: 1,
    fieldNumber: tryParseFieldNumber(tag),
    key: `binary-payload:${tag}:${occurrence}`,
    source: "binary-payload",
    subfieldItems: [["Binary image payload"]],
    binaryBytes: bytes,
  };
}

function parseFieldSubfieldItems(value: string): string[][] {
  if (value.length === 0) {
    return [[value]];
  }

  return value.split(String.fromCharCode(0x1e)).map((subfield) => subfield.split(String.fromCharCode(0x1f)));
}

function shouldShowSubfieldItems(field: NistDisplayField): boolean {
  return field.subfieldItems.length > 1 || field.subfieldItems.some((items) => items.length > 1);
}

function getSubfieldPreviewLabel(items: string[]): string {
  return items.map((item) => normalizeFieldPreview(item, 48)).join(" · ");
}

function getRecordIcon(recordType: number): LucideIcon {
  switch (recordType) {
    case 4:
    case 5:
    case 6:
    case 13:
    case 14:
    case 15:
      return Fingerprint;
    case 8:
      return FileSignature;
    case 10:
      return ScanFace;
    case 17:
      return ScanSearch;
    case 7:
    case 16:
      return ImageIcon;
    case 1:
    case 2:
      return FileText;
    default:
      return FileArchive;
  }
}

function getFieldIcon(recordType: number, field: NistDisplayField): LucideIcon {
  if (field.source === "binary-header") {
    return Binary;
  }

  if (isBinaryLikeField(recordType, field)) {
    return FileImage;
  }

  const fieldMetadata = field.fieldNumber ? getNistFieldMetadata(recordType, field.fieldNumber) : null;
  const valueType = fieldMetadata?.valueType?.toLowerCase() ?? "";
  if (
    valueType.includes("integer") ||
    valueType.includes("count") ||
    valueType.includes("pixel") ||
    valueType.includes("code") ||
    valueType.includes("resolution")
  ) {
    return Hash;
  }

  return FileText;
}

function getBinaryBytes(field: NistFieldInfo): Uint8Array | undefined {
  if ("binaryBytes" in field) {
    return (field as NistDisplayField).binaryBytes;
  }

  return undefined;
}

function formatType4FingerPositions(bytes: Uint8Array): string {
  const positions = Array.from(bytes).filter((value) => value !== 0 && value !== 255);
  return positions.length > 0 ? positions.join(", ") : "None encoded";
}

function getType4HeaderFields(document: NistWorkspaceDocument, record: NistRecordInfo): NistDisplayField[] {
  const recordBytes = getRecordBytes(document, record);
  if (recordBytes.length < 18) {
    return [];
  }

  const width = (recordBytes[13] << 8) | recordBytes[14];
  const height = (recordBytes[15] << 8) | recordBytes[16];
  const fingerPositionBytes = recordBytes.subarray(6, 12);

  return [
    createDisplayField("4.002", `${recordBytes[4]}`, "binary-header", 0),
    createDisplayField("4.003", `${recordBytes[5]}`, "binary-header", 0),
    createDisplayField("4.004", formatType4FingerPositions(fingerPositionBytes), "binary-header", 0),
    createDisplayField("4.005", `${recordBytes[12]}`, "binary-header", 0),
    createDisplayField("4.006", `${width}`, "binary-header", 0),
    createDisplayField("4.007", `${height}`, "binary-header", 0),
    createDisplayField("4.008", `${recordBytes[17]}`, "binary-header", 0),
  ];
}

function getType7HeaderFields(document: NistWorkspaceDocument, record: NistRecordInfo): NistDisplayField[] {
  const recordBytes = getRecordBytes(document, record);
  if (recordBytes.length < 18) {
    return [];
  }

  const width = (recordBytes[13] << 8) | recordBytes[14];
  const height = (recordBytes[15] << 8) | recordBytes[16];
  const fingerPositionBytes = recordBytes.subarray(6, 12);

  return [
    createDisplayField("7.002", `${recordBytes[4]}`, "binary-header", 0),
    createDisplayField("7.003", `${recordBytes[5]}`, "binary-header", 0),
    createDisplayField("7.004", formatType4FingerPositions(fingerPositionBytes), "binary-header", 0),
    createDisplayField("7.005", `${recordBytes[12]}`, "binary-header", 0),
    createDisplayField("7.006", `${width}`, "binary-header", 0),
    createDisplayField("7.007", `${height}`, "binary-header", 0),
    createDisplayField("7.008", `${recordBytes[17]}`, "binary-header", 0),
  ];
}

function getType8HeaderFields(document: NistWorkspaceDocument, record: NistRecordInfo): NistDisplayField[] {
  const recordBytes = getRecordBytes(document, record);
  if (recordBytes.length < 12) {
    return [];
  }

  const width = (recordBytes[8] << 8) | recordBytes[9];
  const height = (recordBytes[10] << 8) | recordBytes[11];

  return [
    createDisplayField("8.002", `${recordBytes[4]}`, "binary-header", 0),
    createDisplayField("8.003", `${recordBytes[5]}`, "binary-header", 0),
    createDisplayField("8.004", `${recordBytes[6]}`, "binary-header", 0),
    createDisplayField("8.005", `${recordBytes[7]}`, "binary-header", 0),
    createDisplayField("8.006", `${width}`, "binary-header", 0),
    createDisplayField("8.007", `${height}`, "binary-header", 0),
  ];
}

function getDisplayFields(document: NistWorkspaceDocument, record: NistRecordInfo): NistDisplayField[] {
  if (record.isOpaqueBinaryRecord) {
    if (record.type === 4) {
      const recordBytes = getRecordBytes(document, record);
      const payload = recordBytes.length >= 18 ? recordBytes.subarray(18) : new Uint8Array();
      return [...getType4HeaderFields(document, record), createBinaryPayloadField("4.999", payload, 0)];
    }

    if (record.type === 7) {
      const recordBytes = getRecordBytes(document, record);
      const payload = recordBytes.length >= 18 ? recordBytes.subarray(18) : new Uint8Array();
      return [...getType7HeaderFields(document, record), createBinaryPayloadField("7.999", payload, 0)];
    }

    if (record.type === 8) {
      const recordBytes = getRecordBytes(document, record);
      const payload = recordBytes.length >= 12 ? recordBytes.subarray(12) : new Uint8Array();
      return [...getType8HeaderFields(document, record), createBinaryPayloadField("8.999", payload, 0)];
    }

    return [];
  }

  return record.fields.map((field, fieldIndex) => ({
    ...field,
    fieldNumber: tryParseFieldNumber(field.tag),
    key: `parsed:${field.tag}:${fieldIndex}`,
    source: "parsed" as const,
    subfieldItems: parseFieldSubfieldItems(field.value),
  }));
}

function getRecordImageField(document: NistWorkspaceDocument, record: NistRecordInfo): NistDisplayField | null {
  for (const field of getDisplayFields(document, record)) {
    const metadata = field.fieldNumber ? getNistFieldMetadata(record.type, field.fieldNumber) : null;
    if (metadata?.imagePayload || (field.fieldNumber === 999 && isBinaryLikeField(record.type, field))) {
      return field;
    }
  }

  return null;
}

function buildBinaryAssetFromSelection(
  document: NistWorkspaceDocument,
  selection: ResolvedNistSelection,
): NistBinaryAsset | null {
  const baseName = normalizeAssetBaseName(document.fileName);

  if (selection.kind === "record" && !selection.record.isOpaqueBinaryRecord) {
    const imageField = getRecordImageField(document, selection.record);
    if (!imageField) {
      return null;
    }

    const bytes = imageField.binaryBytes ?? toLatin1Bytes(imageField.value);
    const assetBytes = detectImageFormat(bytes) ? bytes : extractEmbeddedImageBytes(bytes) ?? bytes;
    const format = detectImageFormat(assetBytes);
    if (!format) {
      return null;
    }

    return {
      bytes: assetBytes,
      fileName: `${baseName}-type${selection.record.type}-${selection.recordIndex + 1}.${format.extension}`,
      mimeType: format.kind === "direct" ? format.mimeType : "application/octet-stream",
    };
  }

  if (selection.kind === "field") {
    if (!isBinaryLikeField(selection.record.type, selection.field)) {
      return null;
    }

    if (selection.field.source === "binary-payload" && selection.record.isOpaqueBinaryRecord) {
      if (selection.record.type === 4 || selection.record.type === 7) {
        const recordBytes = getRecordBytes(document, selection.record);
        if (recordBytes.length < 18) {
          return null;
        }

        const compressionCode = recordBytes[17];
        if (compressionCode === 1) {
          return {
            bytes: selection.field.binaryBytes ?? new Uint8Array(),
            fileName: `${baseName}-${selection.field.tag.replaceAll(".", "_")}.wsq`,
            mimeType: "application/octet-stream",
          };
        }
      }

      return null;
    }

    const bytes = selection.field.binaryBytes ?? toLatin1Bytes(selection.field.value);
    const assetBytes = detectImageFormat(bytes) ? bytes : extractEmbeddedImageBytes(bytes) ?? bytes;
    const format = detectImageFormat(assetBytes);
    if (!format) {
      return null;
    }

    const extension = format.extension;
    const mimeType = format.kind === "direct" ? format.mimeType : "application/octet-stream";

    return {
      bytes: assetBytes,
      fileName: `${baseName}-${selection.field.tag.replaceAll(".", "_")}.${extension}`,
      mimeType,
    };
  }

  if (!selection.record.isOpaqueBinaryRecord || (selection.record.type !== 4 && selection.record.type !== 7)) {
    return null;
  }

  const recordBytes = getRecordBytes(document, selection.record);
  if (recordBytes.length < 18) {
    return null;
  }

  const compressionCode = recordBytes[17];
  const imagePayload = recordBytes.subarray(18);
  if (compressionCode !== 1) {
    return null;
  }

  return {
    bytes: imagePayload,
    fileName: `${baseName}-type${selection.record.type}-${selection.recordIndex + 1}.wsq`,
    mimeType: "application/octet-stream",
  };
}

function getDefaultSelection(document: NistWorkspaceDocument | null): NistSelection | null {
  if (!document) {
    return null;
  }

  for (let recordIndex = 0; recordIndex < document.fileInfo.records.length; recordIndex += 1) {
    const record = document.fileInfo.records[recordIndex];
    const firstField = getDisplayFields(document, record)[0];

    if (firstField) {
      return { kind: "field", recordIndex, fieldIndex: 0 };
    }

    return { kind: "record", recordIndex };
  }

  return null;
}

function resolveSelection(document: NistWorkspaceDocument | null, selection: NistSelection | null): ResolvedNistSelection | null {
  if (!document || !selection) {
    return null;
  }

  const record = document.fileInfo.records[selection.recordIndex];
  if (!record) {
    return null;
  }

  if (selection.kind === "record") {
    return { kind: "record", record, recordIndex: selection.recordIndex };
  }

  const field = getDisplayFields(document, record)[selection.fieldIndex];
  if (!field) {
    return null;
  }

  return { kind: "field", record, field, recordIndex: selection.recordIndex, fieldIndex: selection.fieldIndex };
}

function isBinaryLikeField(recordType: number, field: NistFieldInfo): boolean {
  const binaryBytes = getBinaryBytes(field);
  if (binaryBytes) {
    return true;
  }

  const fieldNumber = tryParseFieldNumber(field.tag);
  const fieldMetadata = fieldNumber ? getNistFieldMetadata(recordType, fieldNumber) : null;

  if (fieldMetadata?.imagePayload) {
    return true;
  }

  let controlCount = 0;

  for (let index = 0; index < field.value.length; index += 1) {
    const code = field.value.charCodeAt(index);

    if (code === 0) {
      return true;
    }

    if (code < 0x20 && code !== 0x09 && code !== 0x0a && code !== 0x0d && code !== 0x1e && code !== 0x1f) {
      controlCount += 1;
    }
  }

  return controlCount > 0;
}

function getFieldPreview(recordType: number, field: NistFieldInfo): string {
  if (isBinaryLikeField(recordType, field)) {
    const byteLength = getBinaryBytes(field)?.length ?? field.value.length;
    return `Binary payload · ${formatByteSize(byteLength)}`;
  }

  return normalizeFieldPreview(field.value);
}

function detectImageFormat(bytes: Uint8Array):
  | { kind: "direct"; mimeType: string; extension: string }
  | { kind: "wsq"; extension: string }
  | { kind: "ffmpeg"; extension: string }
  | null {
  if (bytes.length >= 4) {
    if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) {
      return { kind: "direct", mimeType: "image/png", extension: "png" };
    }

    if (bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff) {
      return { kind: "direct", mimeType: "image/jpeg", extension: "jpg" };
    }

    if (bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46) {
      return { kind: "direct", mimeType: "image/gif", extension: "gif" };
    }

    if (bytes[0] === 0x42 && bytes[1] === 0x4d) {
      return { kind: "direct", mimeType: "image/bmp", extension: "bmp" };
    }

    if (bytes[0] === 0xff && bytes[1] === 0xa0 && bytes[2] === 0xff && bytes[3] === 0xa8) {
      return { kind: "wsq", extension: "wsq" };
    }

    if (bytes[0] === 0xff && bytes[1] === 0x4f && bytes[2] === 0xff && bytes[3] === 0x51) {
      return { kind: "ffmpeg", extension: "j2k" };
    }
  }

  if (bytes.length >= 12) {
    if (
      bytes[0] === 0x52 &&
      bytes[1] === 0x49 &&
      bytes[2] === 0x46 &&
      bytes[3] === 0x46 &&
      bytes[8] === 0x57 &&
      bytes[9] === 0x45 &&
      bytes[10] === 0x42 &&
      bytes[11] === 0x50
    ) {
      return { kind: "direct", mimeType: "image/webp", extension: "webp" };
    }

    if (
      bytes[0] === 0x00 &&
      bytes[1] === 0x00 &&
      bytes[2] === 0x00 &&
      bytes[3] === 0x0c &&
      bytes[4] === 0x6a &&
      bytes[5] === 0x50 &&
      bytes[6] === 0x20 &&
      bytes[7] === 0x20
    ) {
      return { kind: "ffmpeg", extension: "jp2" };
    }
  }

  if (bytes.length >= 4) {
    if (
      (bytes[0] === 0x49 && bytes[1] === 0x49 && bytes[2] === 0x2a && bytes[3] === 0x00) ||
      (bytes[0] === 0x4d && bytes[1] === 0x4d && bytes[2] === 0x00 && bytes[3] === 0x2a)
    ) {
      return { kind: "direct", mimeType: "image/tiff", extension: "tif" };
    }
  }

  return null;
}

function extractEmbeddedImageBytes(bytes: Uint8Array): Uint8Array | null {
  const signatures = [
    Uint8Array.from([0x89, 0x50, 0x4e, 0x47]),
    Uint8Array.from([0xff, 0xd8, 0xff]),
    Uint8Array.from([0x47, 0x49, 0x46]),
    Uint8Array.from([0x42, 0x4d]),
    Uint8Array.from([0xff, 0xa0, 0xff, 0xa8]),
    Uint8Array.from([0xff, 0x4f, 0xff, 0x51]),
    Uint8Array.from([0x00, 0x00, 0x00, 0x0c, 0x6a, 0x50, 0x20, 0x20]),
    Uint8Array.from([0x49, 0x49, 0x2a, 0x00]),
    Uint8Array.from([0x4d, 0x4d, 0x00, 0x2a]),
  ];

  let earliestOffset = -1;

  for (const signature of signatures) {
    for (let offset = 1; offset <= bytes.length - signature.length; offset += 1) {
      let matched = true;

      for (let index = 0; index < signature.length; index += 1) {
        if (bytes[offset + index] !== signature[index]) {
          matched = false;
          break;
        }
      }

      if (matched && (earliestOffset < 0 || offset < earliestOffset)) {
        earliestOffset = offset;
      }
    }
  }

  return earliestOffset > 0 ? bytes.subarray(earliestOffset) : null;
}

async function canvasToPngBytes(canvas: HTMLCanvasElement | OffscreenCanvas): Promise<Uint8Array> {
  const blob =
    "convertToBlob" in canvas
      ? await canvas.convertToBlob({ type: "image/png" })
      : await new Promise<Blob>((resolve, reject) => {
          canvas.toBlob((value) => {
            if (value) {
              resolve(value);
              return;
            }

            reject(new Error("Canvas preview export failed."));
          }, "image/png");
        });

  return new Uint8Array(await blob.arrayBuffer());
}

function createPreviewCanvas(width: number, height: number): HTMLCanvasElement | OffscreenCanvas {
  if ("OffscreenCanvas" in globalThis) {
    return new OffscreenCanvas(width, height);
  }

  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  return canvas;
}

async function renderRasterPreviewBytes(
  width: number,
  height: number,
  pixelBuilder: (imageData: ImageData) => void,
): Promise<Uint8Array> {
  const canvas = createPreviewCanvas(width, height);
  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Canvas preview context could not be created.");
  }

  const imageData = context.createImageData(width, height);
  pixelBuilder(imageData);
  context.putImageData(imageData, 0, 0);
  return canvasToPngBytes(canvas);
}

async function renderGray8PreviewBytes(pixels: Uint8Array, width: number, height: number): Promise<Uint8Array> {
  return renderRasterPreviewBytes(width, height, (imageData) => {
    const destination = imageData.data;

    for (let index = 0; index < pixels.length; index += 1) {
      const value = pixels[index];
      const offset = index * 4;
      destination[offset] = value;
      destination[offset + 1] = value;
      destination[offset + 2] = value;
      destination[offset + 3] = 255;
    }
  });
}

async function renderRgb24PreviewBytes(pixels: Uint8Array, width: number, height: number): Promise<Uint8Array> {
  return renderRasterPreviewBytes(width, height, (imageData) => {
    const destination = imageData.data;

    for (let sourceOffset = 0, destinationOffset = 0; sourceOffset < pixels.length; sourceOffset += 3, destinationOffset += 4) {
      destination[destinationOffset] = pixels[sourceOffset];
      destination[destinationOffset + 1] = pixels[sourceOffset + 1];
      destination[destinationOffset + 2] = pixels[sourceOffset + 2];
      destination[destinationOffset + 3] = 255;
    }
  });
}

async function renderBitonalPreviewBytes(pixels: Uint8Array, width: number, height: number): Promise<Uint8Array> {
  const rowStride = Math.ceil(width / 8);

  return renderRasterPreviewBytes(width, height, (imageData) => {
    const destination = imageData.data;

    for (let y = 0; y < height; y += 1) {
      for (let x = 0; x < width; x += 1) {
        const byte = pixels[(y * rowStride) + (x >> 3)];
        const bit = (byte >> (7 - (x & 7))) & 1;
        const value = bit === 1 ? 255 : 0;
        const offset = (y * width + x) * 4;
        destination[offset] = value;
        destination[offset + 1] = value;
        destination[offset + 2] = value;
        destination[offset + 3] = 255;
      }
    }
  });
}

async function renderPreviewFromBinaryBytes(
  bytes: Uint8Array,
  baseName: string,
): Promise<
  | { status: "ready"; objectUrl: string; alt: string; details: string }
  | { status: "unsupported"; message: string }
> {
  const sourceBytes = detectImageFormat(bytes) ? bytes : extractEmbeddedImageBytes(bytes) ?? bytes;
  const detectedFormat = detectImageFormat(sourceBytes);

  if (!detectedFormat) {
    return {
      status: "unsupported",
      message: "This binary payload is not a browser-renderable image format that the workspace currently recognizes.",
    };
  }

  if (detectedFormat.kind === "direct") {
    return {
      status: "ready",
      objectUrl: createObjectUrl(sourceBytes, detectedFormat.mimeType),
      alt: `${baseName} preview`,
      details: `${detectedFormat.extension.toUpperCase()} image · ${formatByteSize(sourceBytes.length)}`,
    };
  }

  if (detectedFormat.kind === "wsq") {
    const decoded = await decodeWsqBytes(sourceBytes);
    const previewBytes = await exportRawImage(decoded.rawPixels, decoded.width, decoded.height, "png");

    return {
      status: "ready",
      objectUrl: createObjectUrl(previewBytes, "image/png"),
      alt: `${baseName} preview`,
      details: `WSQ image · ${decoded.width}×${decoded.height} · ${decoded.pixelsPerInch} ppi`,
    };
  }

  const file = new File([Uint8Array.from(sourceBytes)], `${baseName}.${detectedFormat.extension}`);
  const normalized = await normalizeImageFile(file);

  return {
    status: "ready",
    objectUrl: createObjectUrl(normalized.previewBytes, "image/png"),
    alt: `${baseName} preview`,
    details: `${detectedFormat.extension.toUpperCase()} image · ${normalized.width}×${normalized.height}`,
  };
}

async function renderOpaqueRecordPreview(
  document: NistWorkspaceDocument,
  record: NistRecordInfo,
): Promise<
  | { status: "ready"; objectUrl: string; alt: string; details: string }
  | { status: "unsupported"; message: string }
> {
  if (record.type !== 4 && record.type !== 7 && record.type !== 8) {
    return {
      status: "unsupported",
      message: "Binary preview is currently available for Type-4, Type-7, and Type-8 image records, plus fielded image payloads.",
    };
  }

  if (record.encodedByteCount > MAX_BINARY_PREVIEW_BYTES) {
    return {
      status: "unsupported",
      message: `This Type-${record.type} record is ${formatByteSize(record.encodedByteCount)}, which is too large for an in-browser preview.`,
    };
  }

  const recordBytes = document.sourceBytes.subarray(record.byteOffset, record.byteOffset + record.encodedByteCount);

  if (record.type === 8) {
    if (recordBytes.length < 12) {
      return {
        status: "unsupported",
        message: "This Type-8 record was shorter than its fixed binary header.",
      };
    }

    const width = (recordBytes[8] << 8) | recordBytes[9];
    const height = (recordBytes[10] << 8) | recordBytes[11];
    const imagePayload = recordBytes.subarray(12);

    if (width <= 0 || height <= 0) {
      return {
        status: "unsupported",
        message: "This Type-8 record did not contain a valid image size in its binary header.",
      };
    }

    if (width * height > MAX_BINARY_PREVIEW_PIXELS) {
      return {
        status: "unsupported",
        message: `This Type-8 image is ${width}×${height}, which is larger than the in-browser preview limit.`,
      };
    }

    const expectedBitonalLength = Math.ceil(width / 8) * height;
    const expectedGrayLength = width * height;
    const expectedRgbLength = width * height * 3;

    if (imagePayload.length === expectedBitonalLength) {
      const previewBytes = await renderBitonalPreviewBytes(imagePayload, width, height);
      return {
        status: "ready",
        objectUrl: createObjectUrl(previewBytes, "image/png"),
        alt: "Type-8 preview",
        details: `Type-8 bitonal signature image · ${width}×${height}`,
      };
    }

    if (imagePayload.length === expectedGrayLength) {
      const previewBytes = await exportRawImage(imagePayload, width, height, "png");
      return {
        status: "ready",
        objectUrl: createObjectUrl(previewBytes, "image/png"),
        alt: "Type-8 preview",
        details: `Type-8 grayscale signature image · ${width}×${height}`,
      };
    }

    if (imagePayload.length === expectedRgbLength) {
      const previewBytes = await renderRgb24PreviewBytes(imagePayload, width, height);
      return {
        status: "ready",
        objectUrl: createObjectUrl(previewBytes, "image/png"),
        alt: "Type-8 preview",
        details: `Type-8 RGB signature image · ${width}×${height}`,
      };
    }

    return {
      status: "unsupported",
      message: `This Type-8 record has ${formatByteSize(imagePayload.length)} of payload data, which did not match the expected bitonal, grayscale, or RGB layouts for ${width}×${height}.`,
    };
  }

  if (recordBytes.length < 18) {
    return {
      status: "unsupported",
      message: `This Type-${record.type} record was shorter than its fixed binary header.`,
    };
  }

  const width = (recordBytes[13] << 8) | recordBytes[14];
  const height = (recordBytes[15] << 8) | recordBytes[16];
  const compressionCode = recordBytes[17];
  const imagePayload = recordBytes.subarray(18);

  if (record.type === 7) {
    const embeddedImageBytes = extractEmbeddedImageBytes(imagePayload);
    if (embeddedImageBytes) {
      return renderPreviewFromBinaryBytes(embeddedImageBytes, `type-${record.type}`);
    }
  }

  if (width <= 0 || height <= 0) {
    return {
      status: "unsupported",
      message: `This Type-${record.type} record did not contain a valid image size in its binary header.`,
    };
  }

  if (width * height > MAX_BINARY_PREVIEW_PIXELS) {
    return {
      status: "unsupported",
      message: `This Type-${record.type} image is ${width}×${height}, which is larger than the in-browser preview limit.`,
    };
  }

  if (compressionCode === 0) {
    const expectedPixelCount = width * height;
    if (imagePayload.length < expectedPixelCount) {
      return {
        status: "unsupported",
        message: `The Type-${record.type} record was shorter than its declared raw grayscale pixel dimensions.`,
      };
    }

    const previewBytes = await exportRawImage(imagePayload.subarray(0, expectedPixelCount), width, height, "png");
    return {
      status: "ready",
      objectUrl: createObjectUrl(previewBytes, "image/png"),
      alt: `Type-${record.type} preview`,
      details: `Type-${record.type} raw grayscale image · ${width}×${height}`,
    };
  }

  if (compressionCode === 1) {
    const decoded = await decodeWsqBytes(imagePayload);
    const previewBytes = await renderGray8PreviewBytes(decoded.rawPixels, decoded.width, decoded.height);

    return {
      status: "ready",
      objectUrl: createObjectUrl(previewBytes, "image/png"),
      alt: `Type-${record.type} preview`,
      details: `Type-${record.type} WSQ-compressed image · ${decoded.width}×${decoded.height} · ${decoded.pixelsPerInch} ppi`,
    };
  }

  return {
    status: "unsupported",
    message: `This Type-${record.type} record uses compression code ${compressionCode}, which the workspace does not render yet.`,
  };
}

export function NistWorkspace({
  currentLabel,
  intake,
  incomingFile,
}: {
  currentLabel: string;
  intake: WorkspaceFileIntake;
  incomingFile: File | null;
}) {
  const navigate = useNavigate();
  const { nistDocument, nistDocumentFingerprint, activeFileFingerprint } = useWorkspaceSession();
  const [document, setDocument] = useState<NistWorkspaceDocument | null>(nistDocument);
  const [busy, setBusy] = useState(false);
  const [errorState, setErrorState] = useState<NistWorkspaceError | null>(null);
  const [selection, setSelection] = useState<NistSelection | null>(getDefaultSelection(nistDocument));
  const [collapsedRecordKeys, setCollapsedRecordKeys] = useState<Record<string, boolean>>(
    getInitialCollapsedRecordKeys(nistDocument),
  );
  const [collapsedFieldKeys, setCollapsedFieldKeys] = useState<Record<string, boolean>>({});
  const [collapsedSubfieldKeys, setCollapsedSubfieldKeys] = useState<Record<string, boolean>>({});
  const [previewState, setPreviewState] = useState<NistPreviewState>({ status: "idle" });

  const updateDocument = useCallback((next: NistWorkspaceDocument, fingerprint: string | null) => {
    setWorkspaceNistDocument(next, fingerprint);

    startTransition(() => {
      setDocument(next);
    });
  }, []);

  const handleFile = useCallback(
    async (file: File) => {
      const fileName = file.name;
      const fingerprint = getFileFingerprint(file);

      if (!isNistTransactionFileName(fileName)) {
        return;
      }

      setErrorState(null);
      setBusy(true);
      logRuntime("Inspecting NIST file structure in OpenNist.Wasm.");
      await waitForUiPaint();

      try {
        const nistBytes = new Uint8Array(await file.arrayBuffer());
        const fileInfo = await inspectNistBytes(nistBytes);
        const loadedDocument: NistWorkspaceDocument = {
          fileName,
          sourceByteCount: nistBytes.length,
          sourceBytes: nistBytes,
          fileInfo,
        };

        updateDocument(loadedDocument, fingerprint);
        setSelection(getDefaultSelection(loadedDocument));
        setCollapsedRecordKeys(getInitialCollapsedRecordKeys(loadedDocument));
        setCollapsedFieldKeys({});
        setCollapsedSubfieldKeys({});
        logRuntime(`Parsed ${fileName} into ${fileInfo.recordCount} logical records.`, fileInfo);
      } catch (error) {
        const message = error instanceof Error ? error.message : "The file could not be parsed.";
        setErrorState({ fileName, message });
        logRuntime(`Error: ${message}`, error);
      } finally {
        setBusy(false);
      }
    },
    [updateDocument],
  );

  useEffect(() => {
    if (!nistDocument) {
      return;
    }

    setDocument(nistDocument);
    setCollapsedRecordKeys(getInitialCollapsedRecordKeys(nistDocument));
    setCollapsedFieldKeys({});
    setCollapsedSubfieldKeys({});
  }, [nistDocument]);

  useEffect(() => {
    if (!incomingFile) {
      return;
    }

    const fingerprint = getFileFingerprint(incomingFile);
    if (
      nistDocument &&
      nistDocumentFingerprint &&
      nistDocumentFingerprint === fingerprint &&
      activeFileFingerprint === fingerprint
    ) {
      setDocument(nistDocument);
      setCollapsedRecordKeys(getInitialCollapsedRecordKeys(nistDocument));
      setCollapsedFieldKeys({});
      setCollapsedSubfieldKeys({});
      return;
    }

    void handleFile(incomingFile);
  }, [activeFileFingerprint, handleFile, incomingFile, nistDocument, nistDocumentFingerprint]);

  useEffect(() => {
    const nextDefaultSelection = getDefaultSelection(document);

    setSelection((current) => {
      if (!document) {
        return null;
      }

      return resolveSelection(document, current) ? current : nextDefaultSelection;
    });
  }, [document]);

  const resolvedSelection = useMemo(() => resolveSelection(document, selection), [document, selection]);
  const binaryAsset = useMemo(
    () => (document && resolvedSelection ? buildBinaryAssetFromSelection(document, resolvedSelection) : null),
    [document, resolvedSelection],
  );

  useEffect(() => {
    let objectUrlToRevoke: string | null = null;
    let disposed = false;

    async function loadPreview() {
      if (!document || !resolvedSelection) {
        setPreviewState({ status: "idle" });
        return;
      }

      if (resolvedSelection.kind === "record") {
        if (resolvedSelection.record.isOpaqueBinaryRecord) {
          if (!canRenderOpaqueRecordPreview(resolvedSelection.record)) {
            setPreviewState({
              status: "unsupported",
              message: "This binary record is preserved exactly, but the workspace only renders previews for Type-4, Type-7, and Type-8 image records right now.",
            });
            return;
          }

          setPreviewState({ status: "loading", message: "Rendering binary record preview." });

          try {
            const result = await renderOpaqueRecordPreview(document, resolvedSelection.record);
            if (disposed) {
              if (result.status === "ready") {
                URL.revokeObjectURL(result.objectUrl);
              }

              return;
            }

            if (result.status === "ready") {
              objectUrlToRevoke = result.objectUrl;
            }

            setPreviewState(result);
          } catch (error) {
            if (!disposed) {
              setPreviewState({
                status: "error",
                message: error instanceof Error ? error.message : "Binary preview failed.",
              });
            }
          }

          return;
        }

        const imageField = getRecordImageField(document, resolvedSelection.record);
        if (!imageField) {
          setPreviewState({
            status: "unsupported",
            message: "Select a field to inspect its value, or select a record that contains an image payload to preview it.",
          });
          return;
        }

        setPreviewState({ status: "loading", message: "Rendering record image preview." });

        try {
          const result = await renderPreviewFromBinaryBytes(
            imageField.binaryBytes ?? toLatin1Bytes(imageField.value),
            `${document.fileName}-type${resolvedSelection.record.type}-${resolvedSelection.recordIndex + 1}`,
          );
          if (disposed) {
            if (result.status === "ready") {
              URL.revokeObjectURL(result.objectUrl);
            }

            return;
          }

          if (result.status === "ready") {
            objectUrlToRevoke = result.objectUrl;
          }

          setPreviewState(result);
        } catch (error) {
          if (!disposed) {
            setPreviewState({
              status: "error",
              message: error instanceof Error ? error.message : "Image preview failed.",
            });
          }
        }

        return;
      }

      if (!isBinaryLikeField(resolvedSelection.record.type, resolvedSelection.field)) {
        setPreviewState({
          status: "unsupported",
          message: "This field contains structured text, not a renderable binary payload.",
        });
        return;
      }

      if (
        resolvedSelection.field.source === "binary-payload" &&
        resolvedSelection.record.isOpaqueBinaryRecord &&
        canRenderOpaqueRecordPreview(resolvedSelection.record)
      ) {
        setPreviewState({ status: "loading", message: "Rendering embedded image preview." });

        try {
          const result = await renderOpaqueRecordPreview(document, resolvedSelection.record);
          if (disposed) {
            if (result.status === "ready") {
              URL.revokeObjectURL(result.objectUrl);
            }

            return;
          }

          if (result.status === "ready") {
            objectUrlToRevoke = result.objectUrl;
          }

          setPreviewState(result);
        } catch (error) {
          if (!disposed) {
            setPreviewState({
              status: "error",
              message: error instanceof Error ? error.message : "Image preview failed.",
            });
          }
        }

        return;
      }

      setPreviewState({ status: "loading", message: "Rendering embedded image preview." });

      try {
        const result = await renderPreviewFromBinaryBytes(
          resolvedSelection.field.binaryBytes ?? toLatin1Bytes(resolvedSelection.field.value),
          `${document.fileName}-${resolvedSelection.field.tag.replaceAll(".", "_")}`,
        );
        if (disposed) {
          if (result.status === "ready") {
            URL.revokeObjectURL(result.objectUrl);
          }

          return;
        }

        if (result.status === "ready") {
          objectUrlToRevoke = result.objectUrl;
        }

        setPreviewState(result);
      } catch (error) {
        if (!disposed) {
          setPreviewState({
            status: "error",
            message: error instanceof Error ? error.message : "Image preview failed.",
          });
        }
      }
    }

    void loadPreview();

    return () => {
      disposed = true;

      if (objectUrlToRevoke) {
        URL.revokeObjectURL(objectUrlToRevoke);
      }
    };
  }, [document, resolvedSelection]);

  const showInspector = Boolean(document && resolvedSelection);
  const { rightDocked, rightInlineVisible, rightOverlayVisible } = useWorkspaceSidebars();
  const showRightSidebar = showInspector && (rightInlineVisible || rightOverlayVisible);

  const toggleRecordCollapsed = useCallback((recordIndex: number) => {
    const recordKey = buildRecordKey(recordIndex);
    setCollapsedRecordKeys((current) => ({ ...current, [recordKey]: !current[recordKey] }));
  }, []);

  const toggleFieldCollapsed = useCallback((recordIndex: number, fieldKey: string) => {
    const key = buildFieldKey(recordIndex, fieldKey);
    setCollapsedFieldKeys((current) => ({ ...current, [key]: !current[key] }));
  }, []);

  const toggleSubfieldCollapsed = useCallback((recordIndex: number, fieldKey: string, subfieldIndex: number) => {
    const subfieldKey = buildSubfieldKey(recordIndex, fieldKey, subfieldIndex);
    setCollapsedSubfieldKeys((current) => ({ ...current, [subfieldKey]: !current[subfieldKey] }));
  }, []);

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
                Open a NIST transaction file and inspect its logical records directly in the browser.
              </p>
            </div>
            <WorkspaceSidebarToggleGroup showRightToggle={showInspector}>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/75 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] backdrop-blur-sm hover:bg-[var(--color-surface-container-low)]"
                onClick={intake.openPicker}
              >
                <CloudDownload className="size-4" />
                Open
              </Button>
            </WorkspaceSidebarToggleGroup>
          </div>
        </div>

        <div className="flex-1 overflow-auto px-6 py-6">
          {document ? (
            <section
              className={`surface-module flex min-h-full w-full flex-col overflow-hidden rounded-[var(--radius-xl)] border-0 bg-white text-left shadow-none ring-1 ring-[color:var(--effect-ghost-border)] transition-colors ${
                intake.isDragActive ? "bg-[var(--color-primary-fixed)]/10 ring-[var(--color-primary)]/30" : ""
              }`}
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
              aria-label="NIST file structure view"
            >
              <div className="flex min-h-[560px] flex-1 flex-col bg-white p-6">
                {busy ? (
                  <div className="mx-auto flex min-h-[500px] max-w-[420px] items-center justify-center px-8">
                    <div className="space-y-5 text-center">
                      <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                        <LoaderCircle className="size-11 animate-spin" />
                      </div>
                      <div>
                        <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                          Parsing NIST file
                        </p>
                        <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                          Reading logical records and preparing the tree view in the browser worker.
                        </p>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="flex min-h-[500px] flex-col">
                    <div className="rounded-[var(--radius-lg)] bg-[var(--color-primary-fixed)]/30 px-4 py-3">
                      <div className="flex flex-wrap items-center gap-3">
                        <div className="flex items-center gap-3">
                          <FileArchive className="size-4 text-[var(--color-primary)]" />
                          <span className="font-mono text-sm font-semibold text-[var(--color-primary)]">{document.fileName}</span>
                        </div>
                        <span className="rounded-full bg-white/70 px-2 py-0.5 text-[0.62rem] uppercase tracking-[0.14em] text-[var(--color-on-surface-variant)]">
                          {document.fileInfo.recordCount} records
                        </span>
                        <span className="rounded-full bg-white/70 px-2 py-0.5 text-[0.62rem] uppercase tracking-[0.14em] text-[var(--color-on-surface-variant)]">
                          {formatByteSize(document.sourceByteCount)}
                        </span>
                      </div>
                    </div>

                    <div className="custom-scrollbar mt-4 flex-1 overflow-auto pr-1">
                      <div className="space-y-5">
                        {document.fileInfo.records.map((record, recordIndex) => {
                          const recordMetadata = getNistRecordMetadata(record.type);
                          const RecordIcon = getRecordIcon(record.type);
                          const recordKey = buildRecordKey(recordIndex);
                          const displayFields = getDisplayFields(document, record);
                          const isCollapsed = Boolean(collapsedRecordKeys[recordKey]);
                          const isRecordSelected =
                            resolvedSelection?.kind === "record" && resolvedSelection.recordIndex === recordIndex;

                          return (
                            <div key={`${record.type}-${recordIndex}`} className="ml-4 border-l-2 border-[color:var(--effect-ghost-border)]">
                              <div className="flex items-center gap-2 rounded-[var(--radius-lg)] px-2 py-1.5">
                                <button
                                  type="button"
                                  className="flex size-7 items-center justify-center rounded-[var(--radius-md)] text-[var(--color-outline)] hover:bg-[var(--color-surface-container-low)]"
                                  onClick={(event) => {
                                    event.stopPropagation();
                                    toggleRecordCollapsed(recordIndex);
                                  }}
                                  aria-label={isCollapsed ? "Expand record" : "Collapse record"}
                                >
                                  {isCollapsed ? <ChevronRight className="size-4" /> : <ChevronDown className="size-4" />}
                                </button>
                                <button
                                  type="button"
                                  className={`flex min-w-0 flex-1 items-center gap-3 rounded-[var(--radius-lg)] px-3 py-2.5 text-left ${
                                    isRecordSelected
                                      ? "border-l-2 border-[var(--color-secondary)] bg-[var(--color-secondary)]/6"
                                      : "hover:bg-[var(--color-surface-container-low)]"
                                  }`}
                                  onClick={() => setSelection({ kind: "record", recordIndex })}
                                >
                                  <RecordIcon className="size-4 shrink-0 text-[var(--color-primary)]" />
                                  <div className="min-w-0">
                                    <div className="text-sm font-medium text-[var(--color-on-surface)]">
                                      {`Type-${record.type} · ${recordMetadata.name}`}
                                    </div>
                                    <div className="mt-1 text-xs leading-5 text-[var(--color-on-surface-variant)]">
                                      {record.isOpaqueBinaryRecord
                                        ? `${formatByteSize(record.encodedByteCount)} binary record`
                                        : `${record.fieldCount} fields · ${recordMetadata.description}`}
                                    </div>
                                  </div>
                                </button>
                              </div>
                              {!isCollapsed ? (
                                <div className="ml-6 mt-1 space-y-1">
                                  {displayFields.length === 0 ? (
                                    <div className="rounded-[var(--radius-lg)] px-3 py-2 text-xs leading-5 text-[var(--color-on-surface-variant)]">
                                      This record is stored as opaque binary data. Select the record header to inspect or preview it.
                                    </div>
                                  ) : null}
                                  {displayFields.map((field, fieldIndex) => {
                                    const fieldMetadata = field.fieldNumber
                                      ? getNistFieldMetadata(record.type, field.fieldNumber)
                                      : { label: "Unknown field" };
                                    const FieldIcon = getFieldIcon(record.type, field);
                                    const hasSubfields = shouldShowSubfieldItems(field);
                                    const fieldCollapseKey = buildFieldKey(recordIndex, field.key);
                                    const isFieldCollapsed = collapsedFieldKeys[fieldCollapseKey] ?? true;
                                    const isFieldSelected =
                                      resolvedSelection?.kind === "field" &&
                                      resolvedSelection.recordIndex === recordIndex &&
                                      resolvedSelection.fieldIndex === fieldIndex;

                                    return (
                                      <div key={`${recordIndex}:${field.key}`} className="space-y-1">
                                        <div
                                          className={`flex w-full items-start gap-3 rounded-[var(--radius-lg)] px-3 py-2 text-left ${
                                            isFieldSelected
                                              ? "border-l-2 border-[var(--color-secondary)] bg-[var(--color-secondary)]/6"
                                              : "hover:bg-[var(--color-surface-container-low)]"
                                          }`}
                                        >
                                          {hasSubfields ? (
                                            <button
                                              type="button"
                                              className="mt-0.5 flex size-4 shrink-0 items-center justify-center text-[var(--color-outline)]"
                                              onClick={(event) => {
                                                event.stopPropagation();
                                                toggleFieldCollapsed(recordIndex, field.key);
                                              }}
                                              aria-label={isFieldCollapsed ? "Expand field" : "Collapse field"}
                                            >
                                              {isFieldCollapsed ? (
                                                <ChevronRight className="size-4" />
                                              ) : (
                                                <ChevronDown className="size-4" />
                                              )}
                                            </button>
                                          ) : (
                                            <span className="size-4 shrink-0" aria-hidden="true" />
                                          )}
                                          <FieldIcon
                                            className={`mt-0.5 size-4 shrink-0 ${
                                              isFieldSelected ? "text-[var(--color-secondary)]" : "text-[var(--color-outline)]"
                                            }`}
                                          />
                                          <button
                                            type="button"
                                            className="min-w-0 flex-1 text-left"
                                            onClick={(event) => {
                                              event.stopPropagation();
                                              setSelection({ kind: "field", recordIndex, fieldIndex });
                                            }}
                                          >
                                            <div className="text-xs text-[var(--color-on-surface)]">
                                              <span className="font-mono">{field.tag}</span>
                                              <span className="mx-1 text-[var(--color-outline)]">·</span>
                                              <span className="font-medium">{fieldMetadata.label}</span>
                                              {"mnemonic" in fieldMetadata && fieldMetadata.mnemonic ? (
                                                <span className="ml-2 font-mono uppercase tracking-[0.14em] text-[var(--color-secondary)]">
                                                  {fieldMetadata.mnemonic}
                                                </span>
                                              ) : null}
                                            </div>
                                            <div className="mt-1 text-xs leading-5 text-[var(--color-on-surface-variant)]">
                                              {getFieldPreview(record.type, field)}
                                            </div>
                                            {field.source === "binary-header" ? (
                                              <div className="mt-1 font-mono text-[0.62rem] uppercase tracking-[0.14em] text-[var(--color-secondary)]">
                                                Derived from binary header
                                              </div>
                                            ) : null}
                                            {"valueType" in fieldMetadata && fieldMetadata.valueType ? (
                                              <div className="mt-1 font-mono text-[0.62rem] uppercase tracking-[0.14em] text-[var(--color-outline)]">
                                                {fieldMetadata.valueType}
                                              </div>
                                            ) : null}
                                          </button>
                                        </div>
                                        {hasSubfields && !isFieldCollapsed ? (
                                          <div className="ml-7 space-y-1 border-l border-[color:var(--effect-ghost-border)] pl-3">
                                            {field.subfieldItems.map((items, subfieldIndex) => (
                                              (() => {
                                                const subfieldKey = buildSubfieldKey(recordIndex, field.key, subfieldIndex);
                                                const isSubfieldCollapsed = collapsedSubfieldKeys[subfieldKey] ?? true;

                                                return (
                                                  <div key={subfieldKey} className="space-y-1">
                                                    <button
                                                      type="button"
                                                      className="flex w-full items-start gap-3 rounded-[var(--radius-lg)] px-3 py-2 text-left hover:bg-[var(--color-surface-container-low)]"
                                                      onClick={(event) => {
                                                        event.stopPropagation();
                                                        toggleSubfieldCollapsed(recordIndex, field.key, subfieldIndex);
                                                      }}
                                                      aria-label={isSubfieldCollapsed ? "Expand subfield" : "Collapse subfield"}
                                                    >
                                                      {isSubfieldCollapsed ? (
                                                        <ChevronRight className="mt-0.5 size-4 shrink-0 text-[var(--color-outline)]" />
                                                      ) : (
                                                        <ChevronDown className="mt-0.5 size-4 shrink-0 text-[var(--color-outline)]" />
                                                      )}
                                                      <ListTree className="mt-0.5 size-4 shrink-0 text-[var(--color-outline)]" />
                                                      <div className="min-w-0">
                                                        <div className="text-[0.72rem] text-[var(--color-on-surface)]">
                                                          <span className="font-medium">{`Subfield ${subfieldIndex + 1}`}</span>
                                                          <span className="ml-2 font-mono uppercase tracking-[0.14em] text-[var(--color-outline)]">
                                                            {items.length === 1 ? "1 item" : `${items.length} items`}
                                                          </span>
                                                        </div>
                                                        <div className="mt-1 break-words font-mono text-[0.72rem] leading-5 text-[var(--color-on-surface-variant)]">
                                                          {getSubfieldPreviewLabel(items)}
                                                        </div>
                                                      </div>
                                                    </button>
                                                    {!isSubfieldCollapsed ? (
                                                      <div className="ml-7 space-y-1 border-l border-[color:var(--effect-ghost-border)] pl-3">
                                                        {items.map((item, itemIndex) => (
                                                          <div
                                                            key={`${subfieldKey}:item:${itemIndex}`}
                                                            className="flex items-start gap-3 rounded-[var(--radius-lg)] px-3 py-2 text-left"
                                                          >
                                                            <FileText className="mt-0.5 size-4 shrink-0 text-[var(--color-outline)]" />
                                                            <div className="min-w-0">
                                                              <div className="text-[0.72rem] text-[var(--color-on-surface)]">
                                                                <span className="font-medium">{`Item ${itemIndex + 1}`}</span>
                                                              </div>
                                                              <div className="mt-1 break-words font-mono text-[0.72rem] leading-5 text-[var(--color-on-surface-variant)]">
                                                                {normalizeFieldPreview(item, 256)}
                                                              </div>
                                                            </div>
                                                          </div>
                                                        ))}
                                                      </div>
                                                    ) : null}
                                                  </div>
                                                );
                                              })()
                                            ))}
                                          </div>
                                        ) : null}
                                      </div>
                                    );
                                  })}
                                </div>
                              ) : null}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </section>
          ) : (
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
              <div className="flex min-h-[560px] flex-1 flex-col bg-white p-6">
                {busy ? (
                  <div className="mx-auto flex min-h-[500px] max-w-[420px] items-center justify-center px-8">
                    <div className="space-y-5 text-center">
                      <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                        <LoaderCircle className="size-11 animate-spin" />
                      </div>
                      <div>
                        <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                          Parsing NIST file
                        </p>
                        <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                          Reading logical records and preparing the tree view in the browser worker.
                        </p>
                      </div>
                    </div>
                  </div>
                ) : errorState ? (
                  <div className="mx-auto flex min-h-[500px] max-w-[520px] items-center justify-center px-8">
                    <div className="space-y-5 text-center">
                      <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-amber-100 text-amber-700 shadow-[var(--effect-modal-shadow)]">
                        <AlertTriangle className="size-11" />
                      </div>
                      <div>
                        <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                          NIST parsing failed
                        </p>
                        <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                          {errorState.message}
                        </p>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="mx-auto flex min-h-[500px] max-w-[520px] items-center justify-center px-8">
                    <div className="space-y-5 text-center">
                      <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-[var(--color-primary)] text-[var(--on-primary)] shadow-[var(--effect-modal-shadow)]">
                        <FolderTree className="size-11" />
                      </div>
                      <div>
                        <p className="font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]">
                          NIST structure explorer
                        </p>
                        <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface-variant)]">
                          Open a `.nist`, `.an2`, or `.eft` file to inspect logical records, field meanings, and image payloads.
                        </p>
                        <p className="mt-4 font-mono text-[0.68rem] uppercase tracking-[0.2em] text-[var(--color-secondary)]">
                          Click to open, drag and drop, or paste a file
                        </p>
                        <span
                          className="mt-5 inline-flex items-center gap-2 rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white/80 px-4 py-2 text-sm font-medium text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)]"
                          aria-hidden="true"
                        >
                          <CloudDownload className="size-4" />
                          Open file
                        </span>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </button>
          )}
        </div>
      </div>

      {showRightSidebar && resolvedSelection ? (
        <NistInspector
          document={document!}
          selection={resolvedSelection}
          previewState={previewState}
          errorState={errorState}
          binaryAsset={binaryAsset}
          onDownloadBinaryAsset={() => {
            if (!binaryAsset) {
              return;
            }

            downloadBytes(binaryAsset.bytes, binaryAsset.fileName, binaryAsset.mimeType);
          }}
          onOpenBinaryAssetInCodecs={() => {
            if (!binaryAsset) {
              return;
            }

            const file = new File([Uint8Array.from(binaryAsset.bytes)], binaryAsset.fileName, {
              type: binaryAsset.mimeType,
            });
            setWorkspaceActiveFile(file, getFileFingerprint(file));
            void navigate({ to: "/app/codecs" });
          }}
          rightDocked={rightDocked}
          rightOverlayVisible={rightOverlayVisible}
        />
      ) : null}
    </>
  );
}

function NistInspector({
  document,
  selection,
  previewState,
  errorState,
  binaryAsset,
  onDownloadBinaryAsset,
  onOpenBinaryAssetInCodecs,
  rightDocked,
  rightOverlayVisible,
}: {
  document: NistWorkspaceDocument;
  selection: ResolvedNistSelection;
  previewState: NistPreviewState;
  errorState: NistWorkspaceError | null;
  binaryAsset: NistBinaryAsset | null;
  onDownloadBinaryAsset(): void;
  onOpenBinaryAssetInCodecs(): void;
  rightDocked: boolean;
  rightOverlayVisible: boolean;
}) {
  const recordMetadata = getNistRecordMetadata(selection.record.type);
  const fieldMetadata =
    selection.kind === "field"
      ? getNistFieldMetadata(selection.record.type, selection.field.fieldNumber ?? -1)
      : null;
  const inspectorTitle =
    selection.kind === "field" ? selection.field.tag : `Type-${selection.record.type}`;
  const inspectorSummary =
    selection.kind === "field"
      ? (
          <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
            {fieldMetadata?.mnemonic ? (
              <span className="font-mono text-[0.68rem] uppercase tracking-[0.16em] text-[var(--color-secondary)]">
                {fieldMetadata.mnemonic}
              </span>
            ) : null}
            <span>{fieldMetadata?.label ?? recordMetadata.name}</span>
          </span>
        )
      : recordMetadata.name;
  const fieldInfoDescription =
    selection.kind === "field"
      ? [
          fieldMetadata?.description ? `What it is\n${fieldMetadata.description}` : null,
          `Expected value\n${fieldMetadata?.valueType ?? "Profile-defined"}`,
          selection.field.source === "binary-header"
            ? "Source\nDerived binary header. This value is decoded from the fixed binary header for inspection without modifying the original record bytes."
            : selection.field.source === "binary-payload"
              ? "Source\nDerived binary payload. This value is exposed from the record payload so you can inspect or preview it without changing the original record bytes."
              : "Source\nParsed field value. This value comes directly from the parsed ANSI/NIST field content.",
        ]
          .filter(Boolean)
          .join("\n\n")
      : null;
  const isPreviewableBinarySelection =
    (selection.kind === "record" &&
      (canRenderOpaqueRecordPreview(selection.record) || Boolean(getRecordImageField(document, selection.record)))) ||
    (selection.kind === "field" &&
      ((selection.field.source === "binary-payload" && canRenderOpaqueRecordPreview(selection.record)) ||
        (selection.field.source !== "binary-payload" && isBinaryLikeField(selection.record.type, selection.field))));
  const showBinaryPreviewSection =
    isPreviewableBinarySelection && (previewState.status === "loading" || previewState.status === "ready" || previewState.status === "error");

  const recordRows = [
    {
      label: "Record length",
      value: selection.record.logicalRecordLength ? `${selection.record.logicalRecordLength}` : `${selection.record.encodedByteCount}`,
      description: selection.record.isOpaqueBinaryRecord
        ? "Size of the opaque binary record in bytes."
        : "The LEN field value when present. It is the byte length of the logical record in the file.",
    },
    {
      label: "Storage form",
      value: selection.record.isOpaqueBinaryRecord ? "Binary" : "Fielded text",
      description: selection.record.isOpaqueBinaryRecord
        ? "The record is stored as an opaque binary payload rather than individual textual fields."
        : "The record uses ANSI/NIST textual tag/value fields.",
    },
  ];

  const fieldRows =
    selection.kind === "field"
      ? [
          {
            label: "Raw length",
            value: selection.field.binaryBytes ? formatByteSize(selection.field.binaryBytes.length) : `${selection.field.value.length} chars`,
            description: selection.field.binaryBytes
              ? "Byte length of the binary payload exposed as a derived data field."
              : "Character count of the raw field value after the tag and colon.",
          },
          ...(selection.field.subfieldCount > 1
            ? [
                {
                  label: "Subfields",
                  value: `${selection.field.subfieldCount}`,
                  description: "Number of subfield groups split on the record separator character.",
                },
                {
                  label: "Items",
                  value: `${selection.field.itemCount}`,
                  description: "Number of item values across all subfields.",
                },
              ]
            : []),
        ]
      : [];

  return (
    <InspectorPanel
      title={inspectorTitle}
      summary={inspectorSummary}
      headerActions={
        fieldInfoDescription
          ? <InspectorInfoButton label="Field information" description={fieldInfoDescription} />
          : undefined
      }
      rightDocked={rightDocked}
      rightOverlayVisible={rightOverlayVisible}
    >
      {errorState ? <InspectorNotice title="Parsing failed" message={errorState.message} tone="error" /> : null}

      {selection.kind === "field" ? (
        <>
          {showBinaryPreviewSection ? (
            <NistPreviewSection
              title="Field value"
              binaryAsset={binaryAsset}
              previewState={previewState}
              onDownloadBinaryAsset={onDownloadBinaryAsset}
              onOpenBinaryAssetInCodecs={onOpenBinaryAssetInCodecs}
            />
          ) : !selection.field.binaryBytes ? (
            <NistFieldValueSection value={selection.field.value} />
          ) : null}
          <InspectorSection
            title="Field details"
            items={fieldRows}
          />
        </>
      ) : (
        <InspectorSection title="Selected record" items={recordRows} />
      )}
      {selection.kind === "record" && showBinaryPreviewSection ? (
        <NistPreviewSection
          binaryAsset={binaryAsset}
          previewState={previewState}
          onDownloadBinaryAsset={onDownloadBinaryAsset}
          onOpenBinaryAssetInCodecs={onOpenBinaryAssetInCodecs}
        />
      ) : null}
    </InspectorPanel>
  );
}

function NistFieldValueSection({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) {
      return;
    }

    const timeoutId = window.setTimeout(() => {
      setCopied(false);
    }, 1600);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [copied]);

  const copyValue = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
  };

  return (
    <div className="space-y-3">
      <div className="space-y-2 border-b border-[color:var(--effect-ghost-border)] pb-2">
        <p className="font-mono text-[0.66rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
          Field value
        </p>
      </div>
      <div className="flex items-center gap-2 rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white p-3 shadow-[var(--effect-subtle-shadow)]">
        <Input
          readOnly
          value={value}
          className="h-9 border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] font-mono text-sm text-[var(--color-on-surface)]"
          aria-label="Field value"
          title={value}
        />
        <Button
          type="button"
          variant="outline"
          size="icon-sm"
          className="shrink-0 rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/80 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] hover:bg-[var(--color-surface-container-low)]"
          onClick={() => {
            void copyValue();
          }}
          aria-label={copied ? "Copied field value" : "Copy field value"}
          title={copied ? "Copied" : "Copy"}
        >
          {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
        </Button>
      </div>
    </div>
  );
}

function NistPreviewSection({
  title,
  binaryAsset,
  previewState,
  onDownloadBinaryAsset,
  onOpenBinaryAssetInCodecs,
}: {
  title?: string;
  binaryAsset: NistBinaryAsset | null;
  previewState: NistPreviewState;
  onDownloadBinaryAsset(): void;
  onOpenBinaryAssetInCodecs(): void;
}) {
  const [isPreviewMaximized, setIsPreviewMaximized] = useState(false);

  useEffect(() => {
    if (previewState.status !== "ready") {
      setIsPreviewMaximized(false);
    }
  }, [previewState.status]);

  useEffect(() => {
    if (!isPreviewMaximized) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsPreviewMaximized(false);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isPreviewMaximized]);

  return (
    <>
      <div className="space-y-3">
      {title ? (
        <div className="space-y-2 border-b border-[color:var(--effect-ghost-border)] pb-2">
          <p className="font-mono text-[0.66rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
            {title}
          </p>
        </div>
      ) : null}
      {previewState.status === "idle" ? null : previewState.status === "loading" ? (
        <div className="rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white p-4 shadow-[var(--effect-subtle-shadow)]">
          <div className="flex items-center gap-3 text-sm text-[var(--color-on-surface-variant)]">
            <LoaderCircle className="size-4 animate-spin text-[var(--color-primary)]" />
            <span>{previewState.message}</span>
          </div>
        </div>
      ) : previewState.status === "ready" ? (
        <div className="rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white p-4 shadow-[var(--effect-subtle-shadow)]">
          <div className="relative overflow-hidden rounded-[var(--radius-md)] bg-[var(--color-surface-container-low)]">
            <img src={previewState.objectUrl} alt={previewState.alt} className="block h-auto max-h-[280px] w-full object-contain" />
            <Button
              type="button"
              variant="outline"
              size="icon-sm"
              className="absolute right-3 top-3 rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/88 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] backdrop-blur-sm hover:bg-white"
              onClick={() => setIsPreviewMaximized(true)}
              aria-label="Maximize image preview"
              title="Maximize preview"
            >
              <Maximize2 className="size-4" />
            </Button>
          </div>
          <div className="mt-3 flex items-center gap-2 text-xs text-[var(--color-on-surface-variant)]">
            <FileImage className="size-4 text-[var(--color-primary)]" />
            <span>{previewState.details}</span>
          </div>
        </div>
      ) : null}

      {binaryAsset ? (
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/80 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] hover:bg-[var(--color-surface-container-low)]"
            onClick={onDownloadBinaryAsset}
          >
            <CloudDownload className="size-4" />
            Download original
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/80 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] hover:bg-[var(--color-surface-container-low)]"
            onClick={onOpenBinaryAssetInCodecs}
          >
            <ExternalLink className="size-4" />
            Open in Codecs
          </Button>
        </div>
      ) : null}
      </div>

      {previewState.status === "ready" && isPreviewMaximized
        ? createPortal(
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/72 p-4 backdrop-blur-sm">
          <button
            type="button"
            className="absolute inset-0"
            aria-label="Close maximized preview"
            onClick={() => setIsPreviewMaximized(false)}
          />
          <div className="relative z-10 flex max-h-full w-full max-w-6xl flex-col overflow-hidden rounded-[var(--radius-xl)] border border-white/10 bg-[var(--color-surface)] shadow-[var(--effect-modal-shadow)]">
            <div className="flex items-center justify-between gap-3 border-b border-[color:var(--effect-ghost-border)] px-4 py-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-[var(--color-on-surface)]">{previewState.alt}</p>
                <p className="truncate text-xs text-[var(--color-on-surface-variant)]">{previewState.details}</p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="icon-sm"
                className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/80 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] hover:bg-[var(--color-surface-container-low)]"
                onClick={() => setIsPreviewMaximized(false)}
                aria-label="Close maximized preview"
              >
                <X className="size-4" />
              </Button>
            </div>
            <div className="flex min-h-0 flex-1 items-center justify-center bg-[var(--color-surface-container-low)] p-4">
              <img
                src={previewState.objectUrl}
                alt={previewState.alt}
                className="block max-h-[calc(100vh-9rem)] w-auto max-w-full object-contain"
              />
            </div>
          </div>
        </div>,
        document.body,
      )
        : null}
    </>
  );
}
