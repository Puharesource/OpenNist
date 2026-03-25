const assemblyName = "OpenNist.Wasm";
const assemblyFileName = `${assemblyName}.dll`;
const wasmAssetBaseUrl = new URL("./", import.meta.url);
const frameworkBaseUrl = new URL("./_framework/", import.meta.url);
const exportPath = ["OpenNist", "Wasm", "OpenNistWasmExports"];
let runtimePromise;
let exportsPromise;

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
  return invoke("EncodeWsq", rawPixels, width, height, pixelsPerInch, bitRate);
}

export async function inspectWsq(wsqBytes) {
  const json = await invoke("InspectWsq", wsqBytes);
  return JSON.parse(json);
}

export async function decodeWsq(wsqBytes) {
  return invoke("DecodeWsq", wsqBytes);
}

export async function assessNfiq(rawPixels, width, height, pixelsPerInch = 500) {
  const json = await invoke("AssessNfiq", rawPixels, width, height, pixelsPerInch);
  return JSON.parse(json);
}

window.OpenNistWasm = {
  getVersion,
  encodeWsq,
  inspectWsq,
  decodeWsq,
  assessNfiq,
};
