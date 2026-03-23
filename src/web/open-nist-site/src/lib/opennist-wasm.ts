import { base64ToBytes, bytesToBase64 } from "@/lib/base64";

type OpenNistWasmModule = {
  getVersion(): Promise<string>;
  encodeWsq(
    rawPixelsBase64: string,
    width: number,
    height: number,
    pixelsPerInch?: number,
    bitRate?: number,
  ): Promise<string>;
  decodeWsq(wsqBase64: string): Promise<{
    width: number;
    height: number;
    bitsPerPixel: number;
    pixelsPerInch: number;
    rawPixelsBase64: string;
    fileInfo: WsqFileInfo;
  }>;
};

export type WsqCommentInfo = {
  text: string;
  fields: Record<string, string>;
};

export type WsqFileInfo = {
  width: number;
  height: number;
  bitsPerPixel: number;
  pixelsPerInch: number;
  black: number;
  white: number;
  shift: number;
  scale: number;
  wsqEncoder: number;
  softwareImplementationNumber: number;
  highPassFilterLength: number;
  lowPassFilterLength: number;
  quantizationBinCenter: number;
  huffmanTableIds: number[];
  blockCount: number;
  encodedBlockByteCount: number;
  commentCount: number;
  nistCommentCount: number;
  comments: WsqCommentInfo[];
};

type DecodedWsqDocument = {
  width: number;
  height: number;
  bitsPerPixel: number;
  pixelsPerInch: number;
  rawPixels: Uint8Array;
  fileInfo: WsqFileInfo;
};

let modulePromise: Promise<OpenNistWasmModule> | undefined;

declare global {
  interface Window {
    OpenNistWasm?: OpenNistWasmModule;
  }
}

async function loadModule(): Promise<OpenNistWasmModule> {
  modulePromise ??= (async () => {
    if (window.OpenNistWasm) {
      return window.OpenNistWasm;
    }

    await new Promise<void>((resolve, reject) => {
      const existingScript = document.querySelector<HTMLScriptElement>(
        'script[data-opennist-wasm="true"]',
      );

      if (existingScript) {
        existingScript.addEventListener("load", () => resolve(), { once: true });
        existingScript.addEventListener("error", () => reject(new Error("OpenNist.Wasm could not be loaded.")), {
          once: true,
        });
        return;
      }

      const script = document.createElement("script");
      script.type = "module";
      script.src = "/opennist-wasm/opennist.interop.js";
      script.dataset.opennistWasm = "true";
      script.onload = () => resolve();
      script.onerror = () => reject(new Error("OpenNist.Wasm could not be loaded."));
      document.head.appendChild(script);
    });

    if (!window.OpenNistWasm) {
      throw new Error("OpenNist.Wasm loaded, but no browser API was registered.");
    }

    return window.OpenNistWasm;
  })();

  return modulePromise;
}

export async function getOpenNistVersion(): Promise<string> {
  const module = await loadModule();
  return module.getVersion();
}

export async function encodeRawToWsqBytes(
  rawPixels: Uint8Array,
  width: number,
  height: number,
  pixelsPerInch: number,
  bitRate: number,
): Promise<Uint8Array> {
  const module = await loadModule();
  const wsqBase64 = await module.encodeWsq(bytesToBase64(rawPixels), width, height, pixelsPerInch, bitRate);
  return base64ToBytes(wsqBase64);
}

export async function decodeWsqBytes(wsqBytes: Uint8Array): Promise<DecodedWsqDocument> {
  const module = await loadModule();
  const decoded = await module.decodeWsq(bytesToBase64(wsqBytes));

  return {
    width: decoded.width,
    height: decoded.height,
    bitsPerPixel: decoded.bitsPerPixel,
    pixelsPerInch: decoded.pixelsPerInch,
    rawPixels: base64ToBytes(decoded.rawPixelsBase64),
    fileInfo: decoded.fileInfo,
  };
}
