import type { LucideIcon } from "lucide-react"
import { Boxes, Cpu, FolderTree, Gauge, Globe, Images, ScanSearch, ShieldCheck, Workflow } from "lucide-react"

export type WorkspaceView = "codecs" | "nist" | "nfiq"

export const sharedNavItems = [
  { label: "Home", to: "/" },
  { label: "Documentation", to: "/docs" },
  { label: "Biometric Subjects", to: "/subjects" }
] as const

export const landingStats = [
  { value: "Managed", label: "Pure .NET implementation" },
  { value: "WASM", label: "Browser-safe runtime target" },
  { value: "Apache-2.0", label: "Open source licensing" }
] as const

export const landingCapabilities = [
  {
    title: "WSQ and JPEG2000 encoding",
    description:
      "Convert fingerprint rasters into standards-aligned WSQ and JPEG2000 payloads with controllable bitrate and metadata handling.",
    icon: Workflow
  },
  {
    title: "NIST record workflows",
    description:
      "Decode, inspect, and eventually edit record sets for modern biometric interchange without dropping into unmanaged code.",
    icon: Boxes
  },
  {
    title: "Quality scoring",
    description:
      "Run the managed NFIQ 2 pipeline directly in .NET and WebAssembly without shelling out to external binaries.",
    icon: ScanSearch
  }
] as const

export const landingFeatures = [
  {
    title: "Native Performance",
    description:
      "Core biometric formats are implemented in C# for high-throughput services, back-office processing, and browser-hosted runtimes.",
    icon: Cpu,
    tone: "bg-[var(--color-primary-container)] text-[var(--color-on-primary-container)]"
  },
  {
    title: "WebAssembly Ready",
    description:
      "The same stack can run in the browser, including managed NFIQ, WSQ encode/decode, and the OpenNist runtime bridge.",
    icon: Globe,
    tone: "bg-[var(--color-secondary-container)] text-[var(--color-secondary)]"
  },
  {
    title: "Standards Focused",
    description:
      "OpenNist is built around operational biometric formats, not generic imaging demos, so the API stays close to real interchange workflows.",
    icon: ShieldCheck,
    tone: "bg-[var(--color-tertiary-fixed)] text-[var(--color-primary)]"
  }
] as const

export const runtimePoints = [
  "NIST package parsing and record access",
  "WSQ compression and expansion",
  "JPEG2000 support across the .NET surface",
  "Managed NFIQ 2 scoring and reporting"
] as const

export const workspaceViews: Array<{
  id: WorkspaceView
  label: string
  eyebrow: string
  icon: LucideIcon
}> = [
  { id: "nist", label: "NIST Structure", eyebrow: "Record inspection", icon: FolderTree },
  { id: "nfiq", label: "NFIQ 2 Review", eyebrow: "Quality scoring", icon: Gauge },
  { id: "codecs", label: "Image Codecs", eyebrow: "WSQ and JPEG2000", icon: Images }
] as const

export const footerLinks = [
  { label: "Documentation", href: "https://github.com/OpenNist/OpenNist" },
  { label: "GitHub", href: "https://github.com/OpenNist/OpenNist" },
  { label: "NIST Standards", href: "https://www.nist.gov/itl/iad/image-group" }
] as const

const siteBaseUrl = "https://opennist.tarkan.dev"

export type SiteSeo = {
  title: string
  description: string
  canonicalUrl: string
}

export function getSiteSeo(pathname: string): SiteSeo {
  const normalizedPath = pathname === "/" ? "/" : pathname.replace(/\/+$/, "")

  switch (normalizedPath) {
    case "/docs":
      return {
        title: "OpenNist Documentation for .NET, WSQ, NIST, JPEG2000 and NFIQ 2",
        description:
          "Read OpenNist documentation for biometric image conversion, NIST record parsing, WSQ and JPEG2000 workflows, and managed NFIQ 2 scoring.",
        canonicalUrl: `${siteBaseUrl}/docs`
      }
    case "/subjects":
      return {
        title: "Biometric Subjects and Interchange Formats in OpenNist",
        description:
          "Explore the biometric subjects and interchange formats supported by OpenNist, including fingerprint-focused NIST, WSQ, JPEG2000, and NFIQ 2 workflows.",
        canonicalUrl: `${siteBaseUrl}/subjects`
      }
    case "/app/nist":
      return {
        title: "OpenNist NIST Explorer",
        description:
          "Inspect ANSI NIST, AN2, EFT and related transaction files in the browser with collapsible records, field metadata, and embedded image preview support.",
        canonicalUrl: `${siteBaseUrl}/app/nist`
      }
    case "/app/nfiq":
      return {
        title: "OpenNist NFIQ 2 Review",
        description:
          "Score fingerprint images in the browser with the OpenNist managed NFIQ 2 workflow, supporting WSQ input, image normalization, and detailed quality measures.",
        canonicalUrl: `${siteBaseUrl}/app/nfiq`
      }
    case "/app":
      return {
        title: "OpenNist NIST Explorer",
        description:
          "Inspect ANSI NIST, AN2, EFT and related transaction files in the browser with collapsible records, field metadata, and embedded image preview support.",
        canonicalUrl: `${siteBaseUrl}/app/nist`
      }
    case "/app/codecs":
      return {
        title: "OpenNist Image Codecs Workspace",
        description:
          "Convert fingerprint images between WSQ, JPEG2000, and raster formats in the browser with OpenNist WebAssembly codecs and native save support.",
        canonicalUrl: `${siteBaseUrl}/app/codecs`
      }
    case "/":
    default:
      return {
        title: "OpenNist: .NET and WebAssembly Toolkit for NIST, WSQ, JPEG2000 and NFIQ 2",
        description:
          "OpenNist is an open source biometric toolkit for .NET and WebAssembly with managed NIST parsing, WSQ and JPEG2000 codecs, and NFIQ 2 scoring.",
        canonicalUrl: normalizedPath === "/" ? siteBaseUrl : `${siteBaseUrl}${normalizedPath}`
      }
  }
}

export const codeSampleLines = [
  "using OpenNist;",
  "",
  'byte[] bytes = await File.ReadAllBytesAsync("sample.nist");',
  "var file = NistDecoder.Decode(bytes);",
  "var image = file.Records[0].AsFingerprintImage();",
  "var score = Nfiq2Algorithm.Default.Assess(image);",
  'Console.WriteLine($"NFIQ: {score.Score}");'
] as const

export type WorkspaceContent = {
  title: string
  description: string
  previewLabel: string
  previewDescription: string
  inspectorTitle: string
  inspectorBadge: string
  inspectorEyebrow: string
  inspectorSummary: string
  accentValue: string
  accentMeta: string
  inspectorSections: Array<{ title: string; items: Array<{ label: string; value: string }> }>
}

export function getWorkspaceViewConfig(view: WorkspaceView) {
  return workspaceViews.find((candidate) => candidate.id === view) ?? workspaceViews[0]
}

export function getImageWorkspaceContent(view: Exclude<WorkspaceView, "nist">): WorkspaceContent {
  switch (view) {
    case "nfiq":
      return {
        title: "NFIQ 2 quality review surface",
        description: "Review a fingerprint image and its NFIQ 2 result.",
        previewLabel: "NFIQ inspection canvas",
        previewDescription: "Fingerprint preview with score details and quality measures in the inspector.",
        inspectorTitle: "NFIQ 2 Score Contents",
        inspectorBadge: "Score",
        inspectorEyebrow: "NFIQ 2",
        inspectorSummary: "NFIQ 2 score, quality band, and the core measures used to interpret image quality.",
        accentValue: "2",
        accentMeta: "NFIQ 2 score",
        inspectorSections: [
          {
            title: "Quality summary",
            items: [
              { label: "Quality band", value: "Excellent" },
              { label: "Classification", value: "High-value acquisition candidate" }
            ]
          },
          {
            title: "Measure detail",
            items: [
              { label: "Mu", value: "Awaiting file input" },
              { label: "Sigma", value: "Awaiting file input" },
              { label: "Block metrics", value: "Awaiting file input" }
            ]
          }
        ]
      }
    case "codecs":
    default:
      return {
        title: "Image codec conversion workspace",
        description: "Convert between WSQ, JPEG2000, and image formats.",
        previewLabel: "WSQ and JPEG2000 preview surface",
        previewDescription: "Image preview with file and codec details in the inspector.",
        inspectorTitle: "Image Contents",
        inspectorBadge: "Inspector",
        inspectorEyebrow: "Codecs",
        inspectorSummary: "Image size, format details, and the available WSQ and JPEG2000 conversions.",
        accentValue: "512×512",
        accentMeta: "8-bit grayscale image",
        inspectorSections: [
          {
            title: "File metadata",
            items: [
              { label: "Formats", value: "WSQ, JP2, PNG, TIFF" },
              { label: "Resolution", value: "500 ppi target" },
              { label: "Source class", value: "Fingerprint image" }
            ]
          },
          {
            title: "Planned actions",
            items: [
              { label: "Decode", value: "Display WSQ and JP2 on the main canvas" },
              { label: "Transcode", value: "Move between WSQ, JP2, and image formats" },
              { label: "Export", value: "Raster and codec download targets" }
            ]
          }
        ]
      }
  }
}

export const nistInspectorContent = {
  title: "1.001: Logical Record Length",
  summary: "Example field details for a future NIST inspector, shown in the same sidebar style as the other app pages.",
  accentValue: "1243",
  accentMeta: "Encoding: ASCII",
  accentDescription:
    "This is the current field value. In a real NIST editor, this would update when you select a different field in the record tree.",
  sections: [
    {
      title: "Field metadata",
      description: "Basic rule information taken from the record specification.",
      items: [
        {
          label: "Status",
          value: "Mandatory",
          description: "Whether the field is required by the standard or can be omitted."
        },
        {
          label: "Occurrences",
          value: "1-1",
          description: "How many times this field is allowed to appear inside the record."
        }
      ]
    },
    {
      title: "Specification note",
      description: "Plain-language context for what this field actually means.",
      items: [
        {
          label: "Description",
          value: "The record length includes the tag, value, field separator, and record separator characters."
        }
      ]
    }
  ]
} as const

export type TreeGroupRow = { label: string; active?: boolean }

export const nistTreeGroups: Array<{ title: string; count: string; rows: TreeGroupRow[] }> = [
  {
    title: "Type-1 Transaction Information",
    count: "9 fields",
    rows: [{ label: "1.001: LEN", active: true }, { label: "1.002: VER" }, { label: "1.003: CNT" }]
  },
  {
    title: "Type-4 Grayscale Image",
    count: "14 fields",
    rows: [{ label: "4.001: LEN" }, { label: "4.004: IMP" }, { label: "4.009: DATA" }]
  },
  {
    title: "Type-14 Variable-Resolution Image",
    count: "18 fields",
    rows: [{ label: "14.001: LEN" }, { label: "14.011: CST" }, { label: "14.999: DATA" }]
  }
] as const
