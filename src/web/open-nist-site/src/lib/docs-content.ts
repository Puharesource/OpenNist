import changelogMarkdown from "../../../../../CHANGELOG.md?raw"
import ansiNistTransactionsMarkdown from "../../../../../docs/concepts/ansi-nist-transactions.md?raw"
import modularPackageModelMarkdown from "../../../../../docs/concepts/modular-package-model.md?raw"
import glossaryMarkdown from "../../../../../docs/glossary.md?raw"
import decodeWsqMarkdown from "../../../../../docs/how-to/decode-wsq.md?raw"
import deployCloudflareMarkdown from "../../../../../docs/how-to/deploy-the-site-to-cloudflare.md?raw"
import inspectNistFileMarkdown from "../../../../../docs/how-to/inspect-a-nist-file.md?raw"
import scoreFingerprintMarkdown from "../../../../../docs/how-to/score-a-fingerprint-with-nfiq2.md?raw"
import useOpenNistFromDotnetMarkdown from "../../../../../docs/how-to/use-opennist-from-dotnet.md?raw"
import useOpenNistFromTypescriptMarkdown from "../../../../../docs/how-to/use-opennist-from-typescript.md?raw"
import packageOverviewMarkdown from "../../../../../docs/package-overview.md?raw"
import quickstartMarkdown from "../../../../../docs/quickstart.md?raw"
import docsIndexMarkdown from "../../../../../docs/README.md?raw"
import errorCodesMarkdown from "../../../../../docs/reference/error-codes.md?raw"
import nistRecordAndFieldReferenceMarkdown from "../../../../../docs/reference/nist-record-and-field-reference.md?raw"
import nistSubfieldAndItemReferenceMarkdown from "../../../../../docs/reference/nist-subfield-and-item-reference.md?raw"
import packageReferenceMarkdown from "../../../../../docs/reference/package-reference.md?raw"
import repositoryLayoutMarkdown from "../../../../../docs/reference/repository-layout.md?raw"
import troubleshootingMarkdown from "../../../../../docs/troubleshooting.md?raw"

export type DocumentationSectionId = "start" | "dotnet" | "typescript" | "concepts" | "reference" | "support"

export type DocumentationPage = {
  slug: string
  title: string
  description: string
  section: DocumentationSectionId
  sourcePath: string
  markdown: string
  seoTitle: string
  seoDescription: string
  showWasmInstallTabs?: boolean
}

export const documentationSections: Array<{ id: DocumentationSectionId; title: string }> = [
  { id: "start", title: "Getting started" },
  { id: "dotnet", title: ".NET guides" },
  { id: "typescript", title: "TypeScript guides" },
  { id: "concepts", title: "Concepts" },
  { id: "reference", title: "Reference" },
  { id: "support", title: "Support" }
]

export const documentationHome = {
  title: "Documentation",
  description: "Guides, concepts, and reference docs for OpenNist on .NET and WASM.",
  markdown: stripLeadingHeading(docsIndexMarkdown),
  seoTitle: "OpenNist Documentation",
  seoDescription:
    "Browse OpenNist documentation for NIST transactions, WSQ workflows, NFIQ 2 scoring, browser interop, and package reference material."
} as const

export const documentationPages: DocumentationPage[] = [
  {
    slug: "quickstart",
    title: "Quickstart",
    description: "Choose the right OpenNist surface and make a first successful .NET or TypeScript call.",
    section: "start",
    sourcePath: "quickstart.md",
    markdown: stripLeadingHeading(quickstartMarkdown),
    seoTitle: "OpenNist Quickstart",
    seoDescription:
      "Choose between the .NET and browser surfaces in OpenNist, then make a first successful NIST, WSQ, or NFIQ call."
  },
  {
    slug: "changelog",
    title: "Changelog",
    description: "Release history for the public OpenNist packages and docs.",
    section: "support",
    sourcePath: "CHANGELOG.md",
    markdown: stripLeadingHeading(changelogMarkdown),
    seoTitle: "OpenNist Changelog",
    seoDescription: "Read release notes and change history for OpenNist packages, docs, and browser tooling."
  },
  {
    slug: "troubleshooting",
    title: "Troubleshooting",
    description: "Common browser, runtime, and file-format issues when working with OpenNist.",
    section: "support",
    sourcePath: "troubleshooting.md",
    markdown: stripLeadingHeading(troubleshootingMarkdown),
    seoTitle: "OpenNist Troubleshooting",
    seoDescription:
      "Troubleshoot OpenNist browser asset sync issues, NFIQ input validation, NIST decode failures, and stale worker/runtime problems."
  },
  {
    slug: "glossary",
    title: "Glossary",
    description: "Short definitions for biometric and NIST-specific terms used throughout the project.",
    section: "support",
    sourcePath: "glossary.md",
    markdown: stripLeadingHeading(glossaryMarkdown),
    seoTitle: "OpenNist Glossary",
    seoDescription:
      "Read concise definitions for ANSI/NIST, WSQ, NFIQ 2, logical records, subfields, PPI, and related biometric terminology."
  },
  {
    slug: "modular-package-model",
    title: "Modular package model",
    description: "Why OpenNist is split into focused packages instead of one large assembly.",
    section: "concepts",
    sourcePath: "concepts/modular-package-model.md",
    markdown: stripLeadingHeading(modularPackageModelMarkdown),
    seoTitle: "OpenNist Modular Package Model",
    seoDescription:
      "Understand the modular package structure behind OpenNist, including OpenNist.Primitives, OpenNist.Nist, OpenNist.Wsq, OpenNist.Nfiq, and OpenNist.Wasm."
  },
  {
    slug: "ansi-nist-transactions",
    title: "ANSI/NIST transactions and logical records",
    description: "How transaction files, logical records, fields, subfields, and binary payloads fit together.",
    section: "concepts",
    sourcePath: "concepts/ansi-nist-transactions.md",
    markdown: stripLeadingHeading(ansiNistTransactionsMarkdown),
    seoTitle: "OpenNist ANSI/NIST Transaction Concepts",
    seoDescription:
      "Learn how OpenNist models ANSI/NIST-style transactions, logical records, fields, subfields, items, and opaque binary records."
  },
  {
    slug: "use-opennist-from-dotnet",
    title: "Getting Started",
    description: "Consume the NIST, WSQ, and NFIQ packages directly from your .NET application.",
    section: "dotnet",
    sourcePath: "how-to/use-opennist-from-dotnet.md",
    markdown: stripLeadingHeading(useOpenNistFromDotnetMarkdown),
    seoTitle: "Use OpenNist from .NET",
    seoDescription:
      "Consume OpenNist.Nist, OpenNist.Wsq, and OpenNist.Nfiq directly from a .NET application, with OpenNist.Primitives providing the shared failure model under those packages."
  },
  {
    slug: "use-opennist-from-typescript",
    title: "Getting Started",
    description: "Call OpenNist.Wasm from TypeScript directly or behind a worker.",
    section: "typescript",
    sourcePath: "how-to/use-opennist-from-typescript.md",
    markdown: stripLeadingHeading(useOpenNistFromTypescriptMarkdown),
    seoTitle: "Use OpenNist from TypeScript",
    seoDescription:
      "Consume OpenNist.Wasm from TypeScript in the browser, either directly on the runtime or through a dedicated worker.",
    showWasmInstallTabs: true
  },
  {
    slug: "deploy-the-site-to-cloudflare",
    title: "Deploy the site to Cloudflare",
    description: "Deploy the website and app to Cloudflare with static assets and external FFmpeg runtime assets.",
    section: "support",
    sourcePath: "how-to/deploy-the-site-to-cloudflare.md",
    markdown: stripLeadingHeading(deployCloudflareMarkdown),
    seoTitle: "Deploy OpenNist to Cloudflare",
    seoDescription:
      "Deploy the OpenNist website and app to Cloudflare with a Worker-backed static asset deployment, configure the custom domain, and externalize FFmpeg runtime assets."
  },
  {
    slug: "decode-wsq",
    title: "Decode a WSQ file",
    description: "Inspect WSQ metadata and decode a WSQ stream into raw grayscale bytes.",
    section: "dotnet",
    sourcePath: "how-to/decode-wsq.md",
    markdown: stripLeadingHeading(decodeWsqMarkdown),
    seoTitle: "Decode a WSQ File with OpenNist",
    seoDescription: "Use OpenNist.Wsq to inspect WSQ metadata and decode a WSQ image into raw 8-bit grayscale pixels."
  },
  {
    slug: "inspect-a-nist-file",
    title: "Inspect a NIST file",
    description: "Decode a transaction, walk records and fields, and re-encode it afterward.",
    section: "dotnet",
    sourcePath: "how-to/inspect-a-nist-file.md",
    markdown: stripLeadingHeading(inspectNistFileMarkdown),
    seoTitle: "Inspect a NIST File with OpenNist",
    seoDescription:
      "Use OpenNist.Nist to decode ANSI/NIST-style transaction files, inspect records and fields, and re-encode the file model."
  },
  {
    slug: "score-a-fingerprint-with-nfiq2",
    title: "Score a fingerprint with NFIQ 2",
    description: "Run NFIQ 2 scoring on supported grayscale fingerprint images.",
    section: "dotnet",
    sourcePath: "how-to/score-a-fingerprint-with-nfiq2.md",
    markdown: stripLeadingHeading(scoreFingerprintMarkdown),
    seoTitle: "Score a Fingerprint with NFIQ 2 in OpenNist",
    seoDescription: "Use OpenNist.Nfiq to score 500 PPI 8-bit grayscale fingerprint images and retrieve NFIQ 2 results."
  },
  {
    slug: "package-reference",
    title: "Package reference",
    description: "Responsibilities and key types for each OpenNist package.",
    section: "reference",
    sourcePath: "reference/package-reference.md",
    markdown: stripLeadingHeading(packageReferenceMarkdown),
    seoTitle: "OpenNist Package Reference",
    seoDescription:
      "Reference the OpenNist package set, including OpenNist.Primitives, OpenNist.Nist, OpenNist.Wsq, OpenNist.Nfiq, and OpenNist.Wasm."
  },
  {
    slug: "error-codes",
    title: "Error codes",
    description: "Stable NFIQ 2 error codes, grouped validation failures, and public failure guidance.",
    section: "reference",
    sourcePath: "reference/error-codes.md",
    markdown: stripLeadingHeading(errorCodesMarkdown),
    seoTitle: "OpenNist Error Codes",
    seoDescription:
      "Reference OpenNist error codes, grouped validation failures, retry guidance, and the strict versus non-throwing failure model."
  },
  {
    slug: "nist-record-and-field-reference",
    title: "NIST record and field reference",
    description: "Browse the built-in OpenNist catalog for labeled record types and fields.",
    section: "reference",
    sourcePath: "reference/nist-record-and-field-reference.md",
    markdown: stripLeadingHeading(nistRecordAndFieldReferenceMarkdown),
    seoTitle: "OpenNist NIST Record and Field Reference",
    seoDescription:
      "Reference the built-in OpenNist catalog for ANSI/NIST record types, labeled fields, binary payload fields, and browser-exposed metadata."
  },
  {
    slug: "nist-subfield-and-item-reference",
    title: "NIST subfield and item reference",
    description: "Understand how OpenNist parses field values into subfields and items.",
    section: "reference",
    sourcePath: "reference/nist-subfield-and-item-reference.md",
    markdown: stripLeadingHeading(nistSubfieldAndItemReferenceMarkdown),
    seoTitle: "OpenNist NIST Subfield and Item Reference",
    seoDescription:
      "Learn how OpenNist parses ANSI/NIST field values into subfields and items, including CNT and MIN repeated-group behavior."
  },
  {
    slug: "repository-layout",
    title: "Repository layout",
    description: "Where to find the libraries, browser interop, tests, benchmarks, and docs.",
    section: "reference",
    sourcePath: "reference/repository-layout.md",
    markdown: stripLeadingHeading(repositoryLayoutMarkdown),
    seoTitle: "OpenNist Repository Layout",
    seoDescription:
      "Reference the OpenNist repository layout across .NET libraries, WebAssembly interop, the web app, tests, benchmarks, and docs."
  },
  {
    slug: "package-overview",
    title: "Package overview",
    description: "Short entry point that links to the current OpenNist package documentation.",
    section: "reference",
    sourcePath: "package-overview.md",
    markdown: stripLeadingHeading(packageOverviewMarkdown),
    seoTitle: "OpenNist Package Overview",
    seoDescription:
      "Start from the OpenNist package overview, then jump to the current package reference and modular package model."
  }
]

const documentationPagesBySlug = new Map(documentationPages.map((page) => [page.slug, page]))
const documentationPagesBySourcePath = new Map(documentationPages.map((page) => [page.sourcePath, page]))

export function getDocumentationPageBySlug(slug: string) {
  return documentationPagesBySlug.get(slug) ?? null
}

export function getDocumentationPageBySourcePath(sourcePath: string) {
  return documentationPagesBySourcePath.get(sourcePath) ?? null
}

export function getDocumentationPagesBySection(section: DocumentationSectionId) {
  return documentationPages.filter((page) => page.section === section)
}

export function getDocumentationSeo(pathname: string) {
  if (pathname === "/docs") {
    return {
      title: documentationHome.seoTitle,
      description: documentationHome.seoDescription,
      canonicalUrl: "https://opennist.tarkan.dev/docs"
    }
  }

  const match = /^\/docs\/([^/]+)$/.exec(pathname)
  if (!match) {
    return null
  }

  const page = getDocumentationPageBySlug(match[1])
  if (!page) {
    return null
  }

  return {
    title: page.seoTitle,
    description: page.seoDescription,
    canonicalUrl: `https://opennist.tarkan.dev/docs/${page.slug}`
  }
}

export function resolveDocumentationHref(currentSourcePath: string, href: string) {
  if (href.startsWith("#")) {
    return href
  }

  if (/^(https?:|mailto:|tel:)/.test(href)) {
    return href
  }

  const resolvedUrl = new URL(href, `https://opennist.local/docs/${currentSourcePath}`)
  const targetSourcePath = decodeURIComponent(resolvedUrl.pathname.replace(/^\/docs\//, ""))

  if (targetSourcePath === "README.md") {
    return resolvedUrl.hash ? `/docs${resolvedUrl.hash}` : "/docs"
  }

  const page = getDocumentationPageBySourcePath(targetSourcePath)
  if (!page) {
    return null
  }

  return resolvedUrl.hash ? `/docs/${page.slug}${resolvedUrl.hash}` : `/docs/${page.slug}`
}

function stripLeadingHeading(markdown: string) {
  return markdown.replace(/^#\s.+?\n+/, "").trim()
}
