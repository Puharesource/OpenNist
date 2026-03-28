import type { DecodedWsqDocument, NfiqAssessmentResult, NistFileInfo, NistFileInput } from "@/lib/opennist-models"

export type {
  DecodedWsqDocument,
  NfiqAssessmentResult,
  NistFieldInfo,
  NistFileInfo,
  NistFileInput,
  NistRecordInfo,
  WsqCommentInfo,
  WsqFileInfo
} from "@/lib/opennist-models"

type GetVersionRequest = { id: number; type: "getVersion" }
type EncodeWsqRequest = {
  id: number
  type: "encodeWsq"
  rawPixels: Uint8Array
  width: number
  height: number
  pixelsPerInch: number
  bitRate: number
}
type DecodeWsqRequest = { id: number; type: "decodeWsq"; wsqBytes: Uint8Array }
type DecodeWsqAndAssessNfiqRequest = { id: number; type: "decodeWsqAndAssessNfiq"; wsqBytes: Uint8Array }
type AssessNfiqRequest = {
  id: number
  type: "assessNfiq"
  rawPixels: Uint8Array
  width: number
  height: number
  pixelsPerInch: number
}
type InspectNistRequest = {
  id: number
  type: "inspectNist"
  nistBytes: Uint8Array
}
type EncodeNistRequest = {
  id: number
  type: "encodeNist"
  fileJson: string
}
type WorkerRequestInput =
  | Omit<GetVersionRequest, "id">
  | Omit<EncodeWsqRequest, "id">
  | Omit<DecodeWsqRequest, "id">
  | Omit<DecodeWsqAndAssessNfiqRequest, "id">
  | Omit<AssessNfiqRequest, "id">
  | Omit<InspectNistRequest, "id">
  | Omit<EncodeNistRequest, "id">

export type OpenNistBinarySource =
  | Uint8Array
  | ArrayBuffer
  | ArrayBufferView
  | Blob
  | Response
  | ReadableStream<Uint8Array>
  | AsyncIterable<Uint8Array>

type DecodedWsqNfiqDocument = DecodedWsqDocument & {
  assessment: NfiqAssessmentResult
}

type WorkerResponse =
  | {
      id: number
      type: "success"
      payload: string | Uint8Array | DecodedWsqDocument | DecodedWsqNfiqDocument | NfiqAssessmentResult | NistFileInfo
    }
  | { id: number; type: "error"; error: string }

const WORKER_BUILD_ID = "2026-03-26-1"
const DEBUG_LOGGING = import.meta.env.DEV
let workerPromise: Promise<Worker> | undefined
let nextRequestId = 1
const WORKER_REQUEST_TIMEOUT_MS = 45000
const pendingRequests = new Map<
  number,
  {
    resolve: (value: unknown) => void
    reject: (error: Error) => void
  }
>()

function isReadableStream(value: unknown): value is ReadableStream<Uint8Array> {
  return typeof ReadableStream !== "undefined" && value instanceof ReadableStream
}

function isAsyncIterable(value: unknown): value is AsyncIterable<Uint8Array> {
  return !!value && typeof (value as AsyncIterable<Uint8Array>)[Symbol.asyncIterator] === "function"
}

async function concatenateBinaryChunks(chunks: AsyncIterable<Uint8Array>): Promise<Uint8Array> {
  const normalizedChunks: Uint8Array[] = []
  let totalLength = 0

  for await (const chunk of chunks) {
    normalizedChunks.push(chunk)
    totalLength += chunk.byteLength
  }

  const bytes = new Uint8Array(totalLength)
  let offset = 0

  for (const chunk of normalizedChunks) {
    bytes.set(chunk, offset)
    offset += chunk.byteLength
  }

  return bytes
}

export async function readOpenNistBinarySource(source: OpenNistBinarySource): Promise<Uint8Array> {
  if (source instanceof Uint8Array) {
    return source
  }

  if (source instanceof ArrayBuffer) {
    return new Uint8Array(source)
  }

  if (ArrayBuffer.isView(source)) {
    return new Uint8Array(source.buffer, source.byteOffset, source.byteLength)
  }

  if (typeof Blob !== "undefined" && source instanceof Blob) {
    return readOpenNistBinarySource(source.stream())
  }

  if (typeof Response !== "undefined" && source instanceof Response) {
    if (!source.body) {
      throw new Error("The supplied Response does not have a readable body.")
    }

    return readOpenNistBinarySource(source.body)
  }

  if (isReadableStream(source)) {
    const reader = source.getReader()

    return concatenateBinaryChunks({
      async *[Symbol.asyncIterator]() {
        while (true) {
          const { done, value } = await reader.read()
          if (done) {
            return
          }

          yield value
        }
      }
    })
  }

  if (isAsyncIterable(source)) {
    return concatenateBinaryChunks(source)
  }

  throw new TypeError("Unsupported OpenNist binary source.")
}

function toTransferableUint8Array(bytes: Uint8Array): Uint8Array {
  if (bytes.byteOffset === 0 && bytes.byteLength === bytes.buffer.byteLength) {
    return bytes
  }

  return bytes.slice()
}

function createWorker(): Worker {
  const worker = new Worker(new URL("./opennist-wasm.worker.ts?v=2026-03-26-1", import.meta.url), { type: "module" })

  if (DEBUG_LOGGING) {
    console.info("[OpenNist] Starting codec worker.", { workerBuildId: WORKER_BUILD_ID })
  }

  worker.onmessage = (event: MessageEvent<WorkerResponse>) => {
    if (DEBUG_LOGGING) {
      console.info("[OpenNist] Worker response received.", { id: event.data.id, type: event.data.type })
    }
    const pending = pendingRequests.get(event.data.id)

    if (!pending) {
      return
    }

    pendingRequests.delete(event.data.id)

    if (event.data.type === "error") {
      pending.reject(new Error(event.data.error))
      return
    }

    pending.resolve(event.data.payload)
  }

  worker.onerror = (event) => {
    console.error("[OpenNist] Worker crashed.", event)
    const error = new Error(event.message || "OpenNist worker crashed.")

    for (const pending of pendingRequests.values()) {
      pending.reject(error)
    }

    pendingRequests.clear()
    workerPromise = undefined
  }

  return worker
}

async function getWorker(): Promise<Worker> {
  workerPromise ??= Promise.resolve(createWorker())
  return workerPromise
}

async function callWorker<T>(request: WorkerRequestInput): Promise<T> {
  return callWorkerWithTransfers<T>(request)
}

async function callWorkerWithTransfers<T>(request: WorkerRequestInput, transfer: Transferable[] = []): Promise<T> {
  const worker = await getWorker()
  const id = nextRequestId++

  if (DEBUG_LOGGING) {
    console.info("[OpenNist] Worker request posted.", { id, type: request.type })
  }

  return await new Promise<T>((resolve, reject) => {
    const timeoutId = window.setTimeout(() => {
      pendingRequests.delete(id)
      console.error("[OpenNist] Worker request timed out.", { id, type: request.type })
      reject(new Error("OpenNist worker request timed out."))
    }, WORKER_REQUEST_TIMEOUT_MS)

    pendingRequests.set(id, {
      resolve: (value) => {
        window.clearTimeout(timeoutId)
        resolve(value as T)
      },
      reject: (error) => {
        window.clearTimeout(timeoutId)
        reject(error)
      }
    })

    worker.postMessage({ ...request, id }, transfer)
  })
}

export async function warmOpenNistWorker(): Promise<void> {
  await getWorker()
}

export async function getOpenNistVersion(): Promise<string> {
  return callWorker<string>({ type: "getVersion" })
}

export async function encodeRawToWsqBytes(
  rawPixels: OpenNistBinarySource,
  width: number,
  height: number,
  pixelsPerInch: number,
  bitRate: number
): Promise<Uint8Array> {
  const resolvedPixels = await readOpenNistBinarySource(rawPixels)

  return callWorker<Uint8Array>({
    type: "encodeWsq",
    rawPixels: resolvedPixels,
    width,
    height,
    pixelsPerInch,
    bitRate
  })
}

export async function decodeWsqBytes(wsqBytes: OpenNistBinarySource): Promise<DecodedWsqDocument> {
  const resolvedBytes = await readOpenNistBinarySource(wsqBytes)

  return callWorker<DecodedWsqDocument>({
    type: "decodeWsq",
    wsqBytes: resolvedBytes
  })
}

export async function decodeWsqAndAssessNfiq(
  wsqBytes: OpenNistBinarySource,
  options?: { transferOwnership?: boolean }
): Promise<DecodedWsqNfiqDocument> {
  const resolvedBytes = await readOpenNistBinarySource(wsqBytes)
  const transferableBytes = options?.transferOwnership ? toTransferableUint8Array(resolvedBytes) : resolvedBytes

  return callWorkerWithTransfers<DecodedWsqNfiqDocument>(
    {
      type: "decodeWsqAndAssessNfiq",
      wsqBytes: transferableBytes
    },
    options?.transferOwnership ? [transferableBytes.buffer] : []
  )
}

export async function assessNfiqRawImage(
  rawPixels: OpenNistBinarySource,
  width: number,
  height: number,
  pixelsPerInch: number,
  options?: { transferOwnership?: boolean }
): Promise<NfiqAssessmentResult> {
  const resolvedPixels = await readOpenNistBinarySource(rawPixels)
  const transferablePixels = options?.transferOwnership ? toTransferableUint8Array(resolvedPixels) : resolvedPixels

  return callWorkerWithTransfers<NfiqAssessmentResult>(
    {
      type: "assessNfiq",
      rawPixels: transferablePixels,
      width,
      height,
      pixelsPerInch
    },
    options?.transferOwnership ? [transferablePixels.buffer] : []
  )
}

export async function inspectNistBytes(nistBytes: OpenNistBinarySource): Promise<NistFileInfo> {
  const resolvedBytes = await readOpenNistBinarySource(nistBytes)

  return callWorker<NistFileInfo>({
    type: "inspectNist",
    nistBytes: resolvedBytes
  })
}

export async function encodeNistFile(file: NistFileInput): Promise<Uint8Array> {
  return callWorker<Uint8Array>({
    type: "encodeNist",
    fileJson: JSON.stringify(file)
  })
}
