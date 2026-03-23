import { FFmpeg } from "@ffmpeg/ffmpeg";
import { fetchFile } from "@ffmpeg/util";
import classWorkerURL from "@ffmpeg/ffmpeg/worker?url";
import coreURL from "../../node_modules/@ffmpeg/core/dist/esm/ffmpeg-core.js?url";
import wasmURL from "../../node_modules/@ffmpeg/core/dist/esm/ffmpeg-core.wasm?url";

type NormalizedImageDocument = {
  width: number;
  height: number;
  rawPixels: Uint8Array;
  previewBytes: Uint8Array;
};

type ExportFormat = "png" | "jpeg" | "tiff" | "bmp" | "webp";

const PREVIEW_NAME = "preview.png";
const RAW_NAME = "working.raw";

let ffmpegPromise: Promise<FFmpeg> | undefined;

async function loadFfmpeg(): Promise<FFmpeg> {
  if (!ffmpegPromise) {
    ffmpegPromise = (async () => {
      const ffmpeg = new FFmpeg();
      await ffmpeg.load({
        classWorkerURL,
        coreURL,
        wasmURL,
      });

      return ffmpeg;
    })();
  }

  return ffmpegPromise;
}

function assertCommandSucceeded(exitCode: number, commandName: string): void {
  if (exitCode !== 0) {
    throw new Error(`${commandName} failed inside ffmpeg.wasm.`);
  }
}

function sanitizeName(fileName: string): string {
  return fileName.replaceAll(/[^a-zA-Z0-9._-]/g, "-");
}

async function deleteFiles(ffmpeg: FFmpeg, ...fileNames: string[]): Promise<void> {
  await Promise.allSettled(fileNames.map((fileName) => ffmpeg.deleteFile(fileName)));
}

async function resolvePreviewDimensions(previewBytes: Uint8Array): Promise<{ width: number; height: number }> {
  const previewBlob = new Blob([Uint8Array.from(previewBytes)], { type: "image/png" });

  if ("createImageBitmap" in globalThis) {
    const bitmap = await createImageBitmap(previewBlob);

    try {
      return {
        width: bitmap.width,
        height: bitmap.height,
      };
    } finally {
      bitmap.close();
    }
  }

  return await new Promise<{ width: number; height: number }>((resolve, reject) => {
    const previewUrl = URL.createObjectURL(previewBlob);
    const image = new Image();

    image.onload = () => {
      URL.revokeObjectURL(previewUrl);
      resolve({
        width: image.naturalWidth,
        height: image.naturalHeight,
      });
    };

    image.onerror = () => {
      URL.revokeObjectURL(previewUrl);
      reject(new Error("ffmpeg.wasm could not determine the image dimensions."));
    };

    image.src = previewUrl;
  });
}

export async function normalizeImageFile(file: File): Promise<NormalizedImageDocument> {
  const ffmpeg = await loadFfmpeg();
  const inputName = sanitizeName(file.name || "input-image");

  await ffmpeg.writeFile(inputName, await fetchFile(file));

  try {
    assertCommandSucceeded(
      await ffmpeg.exec(["-i", inputName, "-vf", "format=gray", "-frames:v", "1", PREVIEW_NAME]),
      "preview render",
    );

    assertCommandSucceeded(
      await ffmpeg.exec([
        "-i",
        inputName,
        "-vf",
        "format=gray",
        "-pix_fmt",
        "gray",
        "-frames:v",
        "1",
        "-f",
        "rawvideo",
        RAW_NAME,
      ]),
      "grayscale raster render",
    );

    const previewBytes = await ffmpeg.readFile(PREVIEW_NAME);
    const rawPixels = await ffmpeg.readFile(RAW_NAME);

    if (!(previewBytes instanceof Uint8Array) || !(rawPixels instanceof Uint8Array)) {
      throw new Error("ffmpeg.wasm returned an unexpected file payload.");
    }

    const { width, height } = await resolvePreviewDimensions(previewBytes);

    return {
      width,
      height,
      previewBytes,
      rawPixels,
    };
  } finally {
    await deleteFiles(ffmpeg, inputName, PREVIEW_NAME, RAW_NAME);
  }
}

export async function exportRawImage(
  rawPixels: Uint8Array,
  width: number,
  height: number,
  format: ExportFormat,
): Promise<Uint8Array> {
  const ffmpeg = await loadFfmpeg();
  const outputName = `output.${format === "jpeg" ? "jpg" : format}`;

  await ffmpeg.writeFile(RAW_NAME, Uint8Array.from(rawPixels));

  try {
    assertCommandSucceeded(
      await ffmpeg.exec([
        "-f",
        "rawvideo",
        "-pixel_format",
        "gray",
        "-video_size",
        `${width}x${height}`,
        "-i",
        RAW_NAME,
        "-frames:v",
        "1",
        outputName,
      ]),
      "image export",
    );

    const output = await ffmpeg.readFile(outputName);
    if (!(output instanceof Uint8Array)) {
      throw new Error("ffmpeg.wasm returned an unexpected export payload.");
    }

    return output;
  } finally {
    await deleteFiles(ffmpeg, RAW_NAME, outputName);
  }
}
