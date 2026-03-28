const assemblyName = "OpenNist.Wasm";
const assemblyFileName = `${assemblyName}.dll`;
const wasmAssetBaseUrl = new URL("./", import.meta.url);
const frameworkBaseUrl = new URL("./_framework/", import.meta.url);
const exportPath = ["OpenNist", "Wasm", "OpenNistWasmExports"];
let runtimePromise;
let exportsPromise;

function isUint8Array(value) {
  return value instanceof Uint8Array;
}

function isArrayBuffer(value) {
  return value instanceof ArrayBuffer;
}

function isArrayBufferView(value) {
  return ArrayBuffer.isView(value);
}

function isBlobLike(value) {
  return typeof Blob !== "undefined" && value instanceof Blob;
}

function isResponseLike(value) {
  return typeof Response !== "undefined" && value instanceof Response;
}

function isReadableStreamLike(value) {
  return typeof ReadableStream !== "undefined" && value instanceof ReadableStream;
}

function isAsyncIterable(value) {
  return value && typeof value[Symbol.asyncIterator] === "function";
}

async function readChunksToUint8Array(chunks) {
  let totalLength = 0;
  const normalizedChunks = [];

  for await (const chunk of chunks) {
    const bytes = await readBinarySource(chunk);
    normalizedChunks.push(bytes);
    totalLength += bytes.byteLength;
  }

  const combined = new Uint8Array(totalLength);
  let offset = 0;

  for (const chunk of normalizedChunks) {
    combined.set(chunk, offset);
    offset += chunk.byteLength;
  }

  return combined;
}

export async function readBinarySource(source) {
  if (isUint8Array(source)) {
    return source;
  }

  if (isArrayBuffer(source)) {
    return new Uint8Array(source);
  }

  if (isArrayBufferView(source)) {
    return new Uint8Array(source.buffer, source.byteOffset, source.byteLength);
  }

  if (isBlobLike(source)) {
    return readBinarySource(source.stream());
  }

  if (isResponseLike(source)) {
    if (!source.body) {
      throw new Error("The supplied Response does not have a readable body.");
    }

    return readBinarySource(source.body);
  }

  if (isReadableStreamLike(source) || isAsyncIterable(source)) {
    return readChunksToUint8Array(source);
  }

  throw new TypeError("Unsupported OpenNist binary source. Expected bytes, a Blob/File, Response, or readable stream.");
}

function isAbsoluteUrl(url) {
  return /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(url);
}

function resolveBootResourceUrl(defaultUri) {
  if (typeof defaultUri !== "string" || defaultUri.length === 0 || isAbsoluteUrl(defaultUri)) {
    return defaultUri;
  }

  if (defaultUri.startsWith("../_content/")) {
    return new URL(defaultUri, frameworkBaseUrl).toString();
  }

  if (defaultUri.startsWith("_content/") || defaultUri.startsWith("./_content/")) {
    return new URL(defaultUri.replace(/^\.\//, ""), wasmAssetBaseUrl).toString();
  }

  return defaultUri;
}

function pickBootResourcePath(name, defaultUri) {
  if (typeof defaultUri !== "string" || defaultUri.length === 0) {
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

async function ensureRuntime() {
  if (!runtimePromise) {
    runtimePromise = (async () => {
      const dotnetModuleUrl = new URL("./_framework/dotnet.js", import.meta.url).toString();
      const { dotnet } = await import(/* @vite-ignore */ dotnetModuleUrl);

      return dotnet
        .withResourceLoader((_type, name, defaultUri) => resolveBootResourceUrl(pickBootResourcePath(name, defaultUri)))
        .create();
    })();
  }

  return runtimePromise;
}

async function ensureExports() {
  if (!exportsPromise) {
    exportsPromise = (async () => {
      const runtime = await ensureRuntime();
      const { getAssemblyExports } = runtime;
      let exports = await getAssemblyExports(assemblyFileName);

      for (const segment of exportPath) {
        exports = exports?.[segment];
      }

      if (!exports) {
        throw new Error(`OpenNist.Wasm exports could not be resolved from '${assemblyFileName}'.`);
      }

      return exports;
    })();
  }

  return exportsPromise;
}

async function invoke(methodName, ...args) {
  const exports = await ensureExports();
  return exports[methodName](...args);
}

export async function getVersion() {
  return invoke("GetVersion");
}

export async function encodeWsq(rawPixels, width, height, pixelsPerInch = 500, bitRate = 2.25) {
  return invoke("EncodeWsq", await readBinarySource(rawPixels), width, height, pixelsPerInch, bitRate);
}

export async function inspectWsq(wsqBytes) {
  const json = await invoke("InspectWsq", await readBinarySource(wsqBytes));
  return JSON.parse(json);
}

export async function decodeWsq(wsqBytes) {
  return invoke("DecodeWsq", await readBinarySource(wsqBytes));
}

export async function assessNfiq(rawPixels, width, height, pixelsPerInch = 500) {
  const json = await invoke("AssessNfiq", await readBinarySource(rawPixels), width, height, pixelsPerInch);
  return JSON.parse(json);
}

window.OpenNistWasm = {
  getVersion,
  readBinarySource,
  encodeWsq,
  inspectWsq,
  decodeWsq,
  assessNfiq,
};
