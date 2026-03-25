/// <reference lib="webworker" />

import type {
  DecodedWsqDocument,
  NfiqAssessmentResult,
  NistFileInfo,
  NistFileInput,
  WsqFileInfo,
} from "@/lib/opennist-models";

type OpenNistWorkerExports = {
  GetVersion(): string;
  EncodeWsq(
    rawPixels: Uint8Array,
    width: number,
    height: number,
    pixelsPerInch?: number,
    bitRate?: number,
  ): Uint8Array;
  InspectWsq(wsqBytes: Uint8Array): string;
  DecodeWsq(wsqBytes: Uint8Array): Uint8Array;
  AssessNfiq(
    rawPixels: Uint8Array,
    width: number,
    height: number,
    pixelsPerInch: number,
  ): string;
  InspectNist(nistBytes: Uint8Array): string;
  EncodeNist(fileJson: string): Uint8Array;
};

type WorkerRequest =
  | { id: number; type: "getVersion" }
  | {
      id: number;
      type: "encodeWsq";
      rawPixels: Uint8Array;
      width: number;
      height: number;
      pixelsPerInch: number;
      bitRate: number;
    }
  | { id: number; type: "decodeWsq"; wsqBytes: Uint8Array }
  | {
      id: number;
      type: "assessNfiq";
      rawPixels: Uint8Array;
      width: number;
      height: number;
      pixelsPerInch: number;
    }
  | { id: number; type: "inspectNist"; nistBytes: Uint8Array }
  | { id: number; type: "encodeNist"; file: NistFileInput };

type WorkerResponse =
  | {
      id: number;
      type: "success";
      payload: string | Uint8Array | DecodedWsqDocument | NfiqAssessmentResult | NistFileInfo;
    }
  | { id: number; type: "error"; error: string };

let exportsPromise: Promise<OpenNistWorkerExports> | undefined;
const RUNTIME_BOOT_TIMEOUT_MS = 30000;

function isAbsoluteUrl(url: string): boolean {
  return /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(url);
}

function resolveBootResourceUrl(defaultUri: string): string {
  if (!defaultUri || isAbsoluteUrl(defaultUri)) {
    return defaultUri;
  }

  const frameworkBaseUrl = new URL("/opennist-wasm/_framework/", self.location.origin);
  return new URL(defaultUri.replace(/^\.\//, ""), frameworkBaseUrl).toString();
}

function pickBootResourcePath(name: string, defaultUri: string): string {
  if (!defaultUri) {
    return name;
  }

  if (isAbsoluteUrl(defaultUri)) {
    return defaultUri;
  }

  if (defaultUri.startsWith("../_content/") || defaultUri.startsWith("_content/") || defaultUri.startsWith("./_content/")) {
    return defaultUri;
  }

  return name || defaultUri;
}

async function loadExports(): Promise<OpenNistWorkerExports> {
  exportsPromise ??= (async () => {
    console.info("[OpenNist worker] Booting .NET runtime.");
    const workerGlobal = globalThis as any;

    workerGlobal.document ??= {
      baseURI: self.location.href,
      location: self.location,
      currentScript: undefined,
    };

    const dotnetModuleUrl = new URL("/opennist-wasm/_framework/dotnet.js", self.location.origin).toString();
    const { dotnet } = await import(/* @vite-ignore */ dotnetModuleUrl);
    const configuredDotnet = dotnet as {
      withConfig?: (config: Record<string, unknown>) => typeof dotnet;
      withDiagnosticTracing?: (enabled: boolean) => typeof dotnet;
      withModuleConfig?: (config: Record<string, unknown>) => typeof dotnet;
    };

    console.info("[OpenNist worker] Host capabilities.", {
      withConfig: typeof configuredDotnet.withConfig,
      withDiagnosticTracing: typeof configuredDotnet.withDiagnosticTracing,
      withModuleConfig: typeof configuredDotnet.withModuleConfig,
    });

    configuredDotnet.withDiagnosticTracing?.(true);
    configuredDotnet.withConfig?.({
      diagnosticTracing: true,
      runtimeConfig: {
        runtimeOptions: {
          configProperties: {
            "System.GC.Server": false,
          },
        },
      },
    });

    const dotnetHost = configuredDotnet.withModuleConfig?.({
      ENVIRONMENT_IS_PTHREAD: false,
      mainScriptUrlOrBlob: dotnetModuleUrl,
      preInit: () => {
        console.info("[OpenNist worker] Emscripten preInit.");
      },
      preRun: () => {
        console.info("[OpenNist worker] Emscripten preRun.");
      },
      onRuntimeInitialized: () => {
        console.info("[OpenNist worker] Emscripten onRuntimeInitialized.");
      },
      postRun: () => {
        console.info("[OpenNist worker] Emscripten postRun.");
      },
      onAbort: (reason: unknown) => {
        console.error("[OpenNist worker] Emscripten abort.", reason);
      },
      onDownloadResourceProgress: (loaded: number, total: number) => {
        console.info("[OpenNist worker] Download progress.", { loaded, total });
      },
      monitorRunDependencies: (count: number) => {
        console.info("[OpenNist worker] Run dependencies.", { count });
      },
      print: (...args: unknown[]) => {
        console.info("[OpenNist worker][stdout]", ...args);
      },
      printErr: (...args: unknown[]) => {
        console.warn("[OpenNist worker][stderr]", ...args);
      },
    }) ?? dotnet;
    console.info("[OpenNist worker] Calling dotnet.create().");
    const runtime = await Promise.race([
      dotnetHost
        .withResourceLoader((type: string, name: string, defaultUri: string) => {
          const resolvedUrl = resolveBootResourceUrl(pickBootResourcePath(name, defaultUri));
          console.info("[OpenNist worker] Boot resource.", { type, name, defaultUri, resolvedUrl });
          return resolvedUrl;
        })
        .create(),
      new Promise<never>((_, reject) => {
        self.setTimeout(() => {
          reject(new Error("OpenNist worker runtime boot timed out."));
        }, RUNTIME_BOOT_TIMEOUT_MS);
      }),
    ]);
    console.info("[OpenNist worker] .NET runtime ready.");
    let exports = await runtime.getAssemblyExports("OpenNist.Wasm.dll");

    for (const segment of ["OpenNist", "Wasm", "OpenNistWasmExports"]) {
      exports = exports?.[segment];
    }

    if (!exports) {
      throw new Error("OpenNist.Wasm exports could not be resolved.");
    }

    return exports as OpenNistWorkerExports;
  })();

  return exportsPromise;
}

function postSuccess(
  id: number,
  payload: string | Uint8Array | DecodedWsqDocument | NfiqAssessmentResult | NistFileInfo,
  transfer: Transferable[] = [],
): void {
  const response: WorkerResponse = { id, type: "success", payload };
  self.postMessage(response, transfer);
}

function postError(id: number, error: unknown): void {
  const message = error instanceof Error ? error.message : "OpenNist worker request failed.";
  const response: WorkerResponse = { id, type: "error", error: message };
  self.postMessage(response);
}

self.addEventListener("message", async (event: MessageEvent<WorkerRequest>) => {
  const request = event.data;
  console.info("[OpenNist worker] Request received.", { id: request.id, type: request.type });

  try {
    const exports = await loadExports();

    switch (request.type) {
      case "getVersion": {
        postSuccess(request.id, exports.GetVersion());
        return;
      }

      case "encodeWsq": {
        const wsqBytes = exports.EncodeWsq(
          request.rawPixels,
          request.width,
          request.height,
          request.pixelsPerInch,
          request.bitRate,
        );

        postSuccess(request.id, wsqBytes, [wsqBytes.buffer]);
        return;
      }

      case "decodeWsq": {
        const fileInfo = JSON.parse(exports.InspectWsq(request.wsqBytes)) as WsqFileInfo;
        const rawPixels = exports.DecodeWsq(request.wsqBytes);
        const payload: DecodedWsqDocument = {
          width: fileInfo.width,
          height: fileInfo.height,
          bitsPerPixel: fileInfo.bitsPerPixel,
          pixelsPerInch: fileInfo.pixelsPerInch,
          rawPixels,
          fileInfo,
        };

        postSuccess(request.id, payload, [rawPixels.buffer]);
        return;
      }

      case "assessNfiq": {
        const payload = JSON.parse(
          exports.AssessNfiq(request.rawPixels, request.width, request.height, request.pixelsPerInch),
        ) as NfiqAssessmentResult;

        postSuccess(request.id, payload);
        return;
      }

      case "inspectNist": {
        const payload = JSON.parse(exports.InspectNist(request.nistBytes)) as NistFileInfo;
        postSuccess(request.id, payload);
        return;
      }

      case "encodeNist": {
        const encoded = exports.EncodeNist(JSON.stringify(request.file));
        postSuccess(request.id, encoded, [encoded.buffer]);
        return;
      }
    }
  } catch (error) {
    postError(request.id, error);
  }
});

export {};
