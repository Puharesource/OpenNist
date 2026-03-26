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
  rawPixels: Uint8Array,
  width: number,
  height: number,
  pixelsPerInch: number,
  bitRate: number
): Promise<Uint8Array> {
  return callWorker<Uint8Array>({
    type: "encodeWsq",
    rawPixels,
    width,
    height,
    pixelsPerInch,
    bitRate
  })
}

export async function decodeWsqBytes(wsqBytes: Uint8Array): Promise<DecodedWsqDocument> {
  return callWorker<DecodedWsqDocument>({
    type: "decodeWsq",
    wsqBytes
  })
}

export async function decodeWsqAndAssessNfiq(
  wsqBytes: Uint8Array,
  options?: { transferOwnership?: boolean }
): Promise<DecodedWsqNfiqDocument> {
  return callWorkerWithTransfers<DecodedWsqNfiqDocument>(
    {
      type: "decodeWsqAndAssessNfiq",
      wsqBytes
    },
    options?.transferOwnership ? [wsqBytes.buffer] : []
  )
}

export async function assessNfiqRawImage(
  rawPixels: Uint8Array,
  width: number,
  height: number,
  pixelsPerInch: number,
  options?: { transferOwnership?: boolean }
): Promise<NfiqAssessmentResult> {
  return callWorkerWithTransfers<NfiqAssessmentResult>(
    {
      type: "assessNfiq",
      rawPixels,
      width,
      height,
      pixelsPerInch
    },
    options?.transferOwnership ? [rawPixels.buffer] : []
  )
}

export async function inspectNistBytes(nistBytes: Uint8Array): Promise<NistFileInfo> {
  return callWorker<NistFileInfo>({
    type: "inspectNist",
    nistBytes
  })
}

export async function encodeNistFile(file: NistFileInput): Promise<Uint8Array> {
  return callWorker<Uint8Array>({
    type: "encodeNist",
    fileJson: JSON.stringify(file)
  })
}
