const assemblyName = "OpenNist.Wasm";
let startPromise;
let runtimePromise;

async function ensureRuntime() {
  if (globalThis.Blazor) {
    return;
  }

  if (!runtimePromise) {
    runtimePromise = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = new URL("./_framework/blazor.webassembly.js", import.meta.url).toString();
      script.setAttribute("autostart", "false");
      script.onload = () => resolve();
      script.onerror = () => reject(new Error("OpenNist.Wasm could not load the Blazor runtime."));
      document.head.appendChild(script);
    });
  }

  await runtimePromise;
}

async function ensureStarted() {
  await ensureRuntime();

  if (!startPromise) {
    startPromise = Blazor.start();
  }

  await startPromise;
}

async function invoke(methodName, ...args) {
  await ensureStarted();
  return DotNet.invokeMethodAsync(assemblyName, methodName, ...args);
}

export async function getVersion() {
  return invoke("openNist_getVersion");
}

export async function encodeWsq(rawPixelsBase64, width, height, pixelsPerInch = 500, bitRate = 2.25) {
  return invoke("openNist_encodeWsq", rawPixelsBase64, width, height, pixelsPerInch, bitRate);
}

export async function decodeWsq(wsqBase64) {
  const json = await invoke("openNist_decodeWsq", wsqBase64);
  return JSON.parse(json);
}

window.OpenNistWasm = {
  getVersion,
  encodeWsq,
  decodeWsq,
};
