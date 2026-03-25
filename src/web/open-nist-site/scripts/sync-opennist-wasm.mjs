import { cp, mkdtemp, mkdir, rm, stat } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { tmpdir } from "node:os";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const projectRoot = resolve(__dirname, "..");
const repoRoot = resolve(projectRoot, "../../..");
const configuration = (process.argv[2] ?? "Debug").toLowerCase() === "release" ? "Release" : "Debug";
const interopProject = resolve(repoRoot, "src/dotnet/interop/OpenNist.Wasm/OpenNist.Wasm.csproj");
const outputDirectory = resolve(projectRoot, "public/opennist-wasm");
const contentDirectory = resolve(projectRoot, "public/_content");
const appFrameworkDirectory = resolve(projectRoot, "public/app/_framework");
const appContentDirectory = resolve(projectRoot, "public/app/_content");
const publishDirectory = await mkdtemp(resolve(tmpdir(), "opennist-wasm-publish-"));

async function directoryExists(path) {
  try {
    const metadata = await stat(path);
    return metadata.isDirectory();
  } catch {
    return false;
  }
}

await mkdir(outputDirectory, { recursive: true });

const result = spawnSync(
  "dotnet",
  [
    "publish",
    interopProject,
    "-c",
    configuration,
    "-o",
    publishDirectory,
    "/p:DeleteExistingFiles=true",
  ],
  {
    cwd: repoRoot,
    stdio: "inherit",
  },
);

if (result.status !== 0) {
  await rm(publishDirectory, { recursive: true, force: true });
  process.exit(result.status ?? 1);
}

await rm(outputDirectory, { recursive: true, force: true });
await mkdir(outputDirectory, { recursive: true });
await cp(resolve(publishDirectory, "wwwroot"), outputDirectory, { recursive: true });

const publishedFrameworkDirectory = resolve(publishDirectory, "wwwroot/_framework");
await rm(appFrameworkDirectory, { recursive: true, force: true });

if (await directoryExists(publishedFrameworkDirectory)) {
  await mkdir(appFrameworkDirectory, { recursive: true });
  await cp(publishedFrameworkDirectory, appFrameworkDirectory, { recursive: true });
}

const publishedContentDirectory = resolve(publishDirectory, "wwwroot/_content");
await rm(contentDirectory, { recursive: true, force: true });
await rm(appContentDirectory, { recursive: true, force: true });

if (await directoryExists(publishedContentDirectory)) {
  await mkdir(contentDirectory, { recursive: true });
  await cp(publishedContentDirectory, contentDirectory, { recursive: true });

  await mkdir(appContentDirectory, { recursive: true });
  await cp(publishedContentDirectory, appContentDirectory, { recursive: true });
}

await rm(publishDirectory, { recursive: true, force: true });
