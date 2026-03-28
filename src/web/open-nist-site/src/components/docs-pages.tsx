import { Link, Navigate, useNavigate, useParams } from "@tanstack/react-router"
import {
  ArrowRight,
  BookOpenText,
  Boxes,
  ChevronDown,
  CircleHelp,
  FileText,
  House,
  Fingerprint,
  LibraryBig,
  ScanSearch,
  Search
} from "lucide-react"
import { isValidElement, type ReactNode, useEffect, useMemo, useRef, useState } from "react"
import Markdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { siSharp, siTypescript } from "simple-icons"

import { Badge } from "@/components/ui/badge"
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator
} from "@/components/ui/breadcrumb"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { CodeSnippet } from "@/components/ui/code-snippet"
import { Input } from "@/components/ui/input"
import { InstallCommandTabs } from "@/components/ui/install-command-tabs"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubItem,
  SidebarProvider
} from "@/components/ui/sidebar"
import {
  documentationHome,
  documentationPages,
  documentationSections,
  getDocumentationPageBySlug,
  getDocumentationPagesBySection,
  resolveDocumentationHref
} from "@/lib/docs-content"

const sectionIcons = {
  start: BookOpenText,
  dotnet: CSharpIcon,
  typescript: TypeScriptIcon,
  concepts: Boxes,
  reference: LibraryBig,
  support: CircleHelp
} as const

const conceptCards = [
  {
    slug: "ansi-nist-transactions",
    title: "ANSI/NIST transactions",
    description: "Logical records, fields, subfields, items, and opaque binary records.",
    icon: Boxes
  },
  {
    slug: "glossary",
    title: "Glossary",
    description: "Short definitions for the main biometric and interchange terms used across OpenNist.",
    icon: Fingerprint
  },
  {
    slug: "score-a-fingerprint-with-nfiq2",
    title: "NFIQ 2 guide",
    description: "Managed scoring basics, input expectations, and where quality results fit into the workflow.",
    icon: ScanSearch
  }
] as const

type DocumentationTocItem = {
  id: string
  label: string
  level: number
}

function CSharpIcon({ className }: { className?: string }) {
  return <SimpleBrandIcon path={siSharp.path} className={className} />
}

function TypeScriptIcon({ className }: { className?: string }) {
  return <SimpleBrandIcon path={siTypescript.path} className={className} />
}

function SimpleBrandIcon({ path, className }: { path: string; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={className} fill="currentColor">
      <path d={path} />
    </svg>
  )
}

export function LibraryDocumentationPage() {
  const tocItems = extractMarkdownHeadings(documentationHome.markdown)

  return (
    <DocumentationShell
      breadcrumbs={[{ label: "Home", to: "/" }, { label: "Docs" }]}
      eyebrow="Documentation"
      title={documentationHome.title}
      description={documentationHome.description}
      tocItems={tocItems}
      mobileValue="docs-home"
      onMobileValueChange={(nextValue) => {
        if (nextValue !== "docs-home") {
          return navigateToDoc(nextValue)
        }
      }}
    >
      <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
        <CardContent className="px-6 py-6 md:px-8 md:py-8">
          <DocumentationContentHeader title={documentationHome.title} description={documentationHome.description} />
          <MarkdownDocument currentSourcePath="README.md" markdown={documentationHome.markdown} />
        </CardContent>
      </Card>
    </DocumentationShell>
  )
}

export function DocumentationArticlePage() {
  const { docSlug } = useParams({ from: "/docs/$docSlug" })
  const navigate = useNavigate()
  const page = getDocumentationPageBySlug(docSlug)

  if (!page) {
    return <Navigate to="/docs" replace />
  }

  const tocItems = extractMarkdownHeadings(page.markdown)

  return (
    <DocumentationShell
      breadcrumbs={[{ label: "Home", to: "/" }, { label: "Docs", to: "/docs" }, { label: page.title }]}
      eyebrow={getSectionLabel(page.section)}
      title={page.title}
      description={page.description}
      tocItems={tocItems}
      activeSlug={page.slug}
      mobileValue={page.slug}
      onMobileValueChange={(nextValue) => {
        if (nextValue === "docs-home") {
          void navigate({ to: "/docs" })
          return
        }

        void navigateToDoc(nextValue, navigate)
      }}
    >
      <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
        <CardContent className="px-6 py-6 md:px-8 md:py-8">
          <DocumentationContentHeader title={page.title} description={page.description} />
          {page.showWasmInstallTabs ? (
            <div className="mb-8">
              <InstallCommandTabs
                title="WASM interop install"
                description="Choose the package manager you want to document for the browser-facing OpenNist package surface."
                packageName="opennist-wasm"
                note="This uses the planned browser package name so the docs UI is ready before npm publication is finalized."
              />
            </div>
          ) : null}
          <MarkdownDocument currentSourcePath={page.sourcePath} markdown={page.markdown} />
        </CardContent>
      </Card>
    </DocumentationShell>
  )
}

export function BiometricSubjectsPage() {
  return (
    <section className="px-6 py-18 md:px-10 md:py-22">
      <div className="mx-auto flex w-full max-w-[1600px] flex-col gap-10">
        <div className="max-w-3xl space-y-4">
          <Badge className="rounded-full border-0 bg-[var(--color-primary)] px-4 py-1.5 text-[0.72rem] uppercase tracking-[0.22em] text-[var(--on-primary)]">
            Subject reference
          </Badge>
          <h1 className="font-display text-5xl font-semibold tracking-[-0.06em] text-[var(--color-primary)]">
            Biometric subjects
          </h1>
          <p className="text-lg leading-8 text-[var(--color-on-surface-variant)]">
            The subject reference now lives alongside the rest of the documentation instead of on a placeholder page.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {conceptCards.map((card) => (
            <Card
              key={card.slug}
              className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]"
            >
              <CardHeader className="space-y-4">
                <div className="flex size-12 items-center justify-center rounded-[var(--radius-lg)] bg-[var(--color-primary-container)] text-[var(--color-on-primary-container)]">
                  <card.icon className="size-5" />
                </div>
                <div>
                  <CardTitle className="font-display text-xl tracking-[-0.03em] text-[var(--color-primary)]">
                    {card.title}
                  </CardTitle>
                  <CardDescription className="mt-2 leading-7 text-[var(--color-on-surface-variant)]">
                    {card.description}
                  </CardDescription>
                </div>
              </CardHeader>
              <CardContent>
                <Button
                  asChild
                  variant="outline"
                  className="border-[color:var(--effect-ghost-border)] bg-white text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
                >
                  <Link to="/docs/$docSlug" params={{ docSlug: card.slug }}>
                    <FileText className="size-4" />
                    Open document
                  </Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>

        <div>
          <Button
            asChild
            className="rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
          >
            <Link to="/docs">
              <ArrowRight className="size-4" />
              Browse all documentation
            </Link>
          </Button>
        </div>
      </div>
    </section>
  )
}

function DocumentationShell({
  eyebrow,
  title,
  description,
  children,
  breadcrumbs,
  tocItems,
  activeSlug,
  mobileValue,
  onMobileValueChange
}: {
  eyebrow: string
  title: string
  description: string
  children: ReactNode
  breadcrumbs: Array<{ label: string; to?: string }>
  tocItems: DocumentationTocItem[]
  activeSlug?: string
  mobileValue: string
  onMobileValueChange: (value: string) => void
}) {
  const hasToc = tocItems.length > 0
  const articleScrollRef = useRef<HTMLDivElement | null>(null)
  const activeSectionId = useMemo(() => {
    if (!activeSlug) {
      return null
    }

    return documentationPages.find((page) => page.slug === activeSlug)?.section ?? null
  }, [activeSlug])
  const [searchQuery, setSearchQuery] = useState("")
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(documentationSections.map((section) => [section.id, section.id !== activeSectionId]))
  )
  const normalizedSearchQuery = searchQuery.trim().toLowerCase()

  useEffect(() => {
    if (!activeSectionId) {
      return
    }

    setCollapsedSections((current) => {
      if (!current[activeSectionId]) {
        return current
      }

      return {
        ...current,
        [activeSectionId]: false
      }
    })
  }, [activeSectionId])

  const toggleSection = (sectionId: string) => {
    setCollapsedSections((current) => ({
      ...current,
      [sectionId]: !current[sectionId]
    }))
  }

  const filteredPagesBySection = useMemo(() => {
    return new Map(
      documentationSections.map((section) => {
        const pages = getDocumentationPagesBySection(section.id).filter((page) =>
          normalizedSearchQuery ? page.title.toLowerCase().includes(normalizedSearchQuery) : true
        )

        return [section.id, pages]
      })
    )
  }, [normalizedSearchQuery])

  return (
    <section className="px-6 py-18 md:px-10 md:py-22 xl:h-[calc(100dvh-81px)] xl:overflow-hidden xl:px-0 xl:py-0">
      <div className="space-y-5 xl:hidden">
        <Badge className="rounded-full border-0 bg-[var(--color-secondary-container)] px-4 py-1.5 text-[0.72rem] uppercase tracking-[0.22em] text-[var(--color-secondary)]">
          {eyebrow}
        </Badge>
        <div className="space-y-3">
          <h1 className="font-display text-4xl font-semibold tracking-[-0.06em] text-[var(--color-primary)] md:text-5xl">
            {title}
          </h1>
          <p className="text-base leading-8 text-[var(--color-on-surface-variant)] md:text-lg">{description}</p>
        </div>

        <Select value={mobileValue} onValueChange={onMobileValueChange}>
          <SelectTrigger className="h-11 w-full rounded-[var(--radius-xl)] border-[color:var(--effect-ghost-border)] bg-white px-4">
            <SelectValue placeholder="Choose a document" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="docs-home">Documentation home</SelectItem>
            {documentationSections.map((section) => (
              <div key={section.id}>
                {getDocumentationPagesBySection(section.id).map((page) => (
                  <SelectItem key={page.slug} value={page.slug}>
                    {page.title}
                  </SelectItem>
                ))}
              </div>
            ))}
          </SelectContent>
        </Select>
      </div>

      <SidebarProvider
        className={`grid w-full gap-8 xl:h-full xl:gap-0 ${
          hasToc ? "xl:grid-cols-[18rem_minmax(0,1fr)_16rem]" : "xl:grid-cols-[18rem_minmax(0,1fr)]"
        }`}
      >
        <Sidebar className="hidden xl:flex xl:h-full xl:flex-col xl:border-r xl:border-[color:var(--effect-ghost-border)] xl:p-4">
          <SidebarContent className="pr-1">
            <div className="mb-5">
              <div className="relative">
                <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--color-on-surface-variant)]" />
                <Input
                  type="search"
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                  placeholder="Search by title"
                  aria-label="Search documentation titles"
                  className="h-10 rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white pr-3 pl-9 text-sm"
                />
              </div>
            </div>

            <SidebarGroup className="space-y-5">
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton asChild isActive={!activeSlug} variant="muted">
                      <Link to="/docs">
                        <span className="flex min-w-0 items-center gap-2">
                          <House className="size-3.5 shrink-0" />
                          <span className="min-w-0 font-display text-sm font-semibold tracking-[-0.02em]">
                            Documentation home
                          </span>
                        </span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>

            {documentationSections.map((section) => {
              const Icon = sectionIcons[section.id]
              const pages = filteredPagesBySection.get(section.id) ?? []
              const isCollapsed = normalizedSearchQuery ? false : (collapsedSections[section.id] ?? false)

              if (pages.length === 0) {
                return null
              }

              return (
                <SidebarGroup key={section.id} className="mt-5">
                  <SidebarMenu>
                    <SidebarMenuItem>
                      <SidebarMenuButton
                        type="button"
                        variant="muted"
                        className="items-center justify-between"
                        onClick={() => toggleSection(section.id)}
                        aria-expanded={!isCollapsed}
                        aria-controls={`docs-section-${section.id}`}
                      >
                        <span className="flex min-w-0 items-center gap-2">
                          <Icon className="size-3.5 shrink-0" />
                          <span className="truncate font-display text-sm font-semibold tracking-[-0.02em]">
                            {section.title}
                          </span>
                        </span>
                        <ChevronDown
                          className={`size-4 shrink-0 transition-transform ${isCollapsed ? "-rotate-90" : "rotate-0"}`}
                        />
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  </SidebarMenu>
                  {!isCollapsed ? (
                    <SidebarGroupContent id={`docs-section-${section.id}`}>
                      <SidebarMenuSub>
                        {pages.map((page) => (
                          <SidebarMenuSubItem key={page.slug}>
                            <SidebarMenuButton asChild isActive={activeSlug === page.slug}>
                              <Link to="/docs/$docSlug" params={{ docSlug: page.slug }}>
                                <span className="min-w-0 font-display text-sm font-semibold tracking-[-0.02em]">
                                  {page.title}
                                </span>
                              </Link>
                            </SidebarMenuButton>
                          </SidebarMenuSubItem>
                        ))}
                      </SidebarMenuSub>
                    </SidebarGroupContent>
                  ) : null}
                </SidebarGroup>
              )
            })}
          </SidebarContent>
        </Sidebar>

        <div ref={articleScrollRef} className="scroll-smooth xl:h-full xl:min-h-0 xl:overflow-y-auto">
          <div className="space-y-6 xl:mx-auto xl:w-full xl:max-w-[1200px] xl:px-10 xl:pt-6 xl:pb-10 2xl:px-14">
            <DocumentationBreadcrumbs items={breadcrumbs} />
            {children}
          </div>
        </div>

        {hasToc ? <DocumentationTableOfContents items={tocItems} scrollContainerRef={articleScrollRef} /> : null}
      </SidebarProvider>
    </section>
  )
}

function DocumentationBreadcrumbs({ items }: { items: Array<{ label: string; to?: string }> }) {
  return (
    <Breadcrumb>
      <BreadcrumbList>
        {items.flatMap((item) => {
          const breadcrumbKey = `${item.to ?? "current"}-${item.label}`
          const isLast = item === items[items.length - 1]
          const nodes = [
            <BreadcrumbItem key={breadcrumbKey}>
              {isLast || !item.to ? (
                <BreadcrumbPage>{item.label}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink asChild>
                  <Link to={item.to}>{item.label}</Link>
                </BreadcrumbLink>
              )}
            </BreadcrumbItem>
          ]

          if (!isLast) {
            nodes.push(<BreadcrumbSeparator key={`separator-${breadcrumbKey}`} />)
          }

          return nodes
        })}
      </BreadcrumbList>
    </Breadcrumb>
  )
}

function DocumentationContentHeader({ title, description }: { title: string; description: string }) {
  return (
    <div className="mb-8 border-b border-[color:var(--effect-ghost-border)] pb-8">
      <div className="space-y-3">
        <h1 className="font-display text-4xl font-semibold tracking-[-0.06em] text-[var(--color-primary)] md:text-5xl">
          {title}
        </h1>
        <p className="max-w-3xl text-base leading-8 text-[var(--color-on-surface-variant)] md:text-lg">{description}</p>
      </div>
    </div>
  )
}

function MarkdownDocument({ markdown, currentSourcePath }: { markdown: string; currentSourcePath: string }) {
  const headingIdFor = createHeadingIdFactory()

  return (
    <div className="space-y-5 text-[var(--color-on-surface)]">
      <Markdown
        remarkPlugins={[remarkGfm]}
        components={{
          h1: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h2
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 font-display text-3xl font-semibold tracking-[-0.05em] text-[var(--color-primary)]"
              >
                {children}
              </h2>
            )
          },
          h2: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h2
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 pt-2 font-display text-3xl font-semibold tracking-[-0.05em] text-[var(--color-primary)]"
              >
                {children}
              </h2>
            )
          },
          h3: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h3
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 pt-2 font-display text-2xl font-semibold tracking-[-0.04em] text-[var(--color-primary)]"
              >
                {children}
              </h3>
            )
          },
          h4: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h4
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 pt-2 text-xl font-semibold tracking-[-0.03em] text-[var(--color-primary)]"
              >
                {children}
              </h4>
            )
          },
          h5: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h5
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 pt-2 text-lg font-semibold text-[var(--color-primary)]"
              >
                {children}
              </h5>
            )
          },
          h6: ({ children }) => {
            const id = headingIdFor(extractNodeText(children))

            return (
              <h6
                id={id}
                data-doc-heading="true"
                className="scroll-mt-28 pt-2 text-base font-semibold uppercase tracking-[0.12em] text-[var(--color-primary)]"
              >
                {children}
              </h6>
            )
          },
          p: ({ children }) => <p className="text-base leading-8 text-[var(--color-on-surface)]">{children}</p>,
          ul: ({ children }) => (
            <ul className="list-disc space-y-3 pl-5 text-base leading-8 marker:text-[var(--color-primary)]">
              {children}
            </ul>
          ),
          ol: ({ children }) => (
            <ol className="list-decimal space-y-3 pl-5 text-base leading-8 marker:text-[var(--color-primary)]">
              {children}
            </ol>
          ),
          li: ({ children }) => <li className="pl-1 text-[var(--color-on-surface)]">{children}</li>,
          blockquote: ({ children }) => (
            <blockquote className="rounded-[var(--radius-xl)] border-l-4 border-[var(--color-primary)] bg-[var(--color-surface-container-low)] px-5 py-4 text-[var(--color-on-surface)]/90">
              {children}
            </blockquote>
          ),
          table: ({ children }) => (
            <div className="overflow-x-auto">
              <table className="min-w-full border-collapse text-left text-sm">{children}</table>
            </div>
          ),
          thead: ({ children }) => (
            <thead className="border-b border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)]">
              {children}
            </thead>
          ),
          tbody: ({ children }) => <tbody>{children}</tbody>,
          tr: ({ children }) => <tr className="border-b border-[color:var(--effect-ghost-border)]/60">{children}</tr>,
          th: ({ children }) => <th className="px-3 py-3 font-semibold text-[var(--color-primary)]">{children}</th>,
          td: ({ children }) => <td className="px-3 py-3 align-top text-[var(--color-on-surface)]">{children}</td>,
          hr: () => <hr className="border-0 border-t border-[color:var(--effect-ghost-border)]" />,
          img: ({ src, alt }) => (
            // eslint-disable-next-line jsx-a11y/alt-text
            <img
              src={src ?? undefined}
              alt={alt ?? ""}
              className="rounded-[var(--radius-xl)] ring-1 ring-[color:var(--effect-ghost-border)]"
            />
          ),
          pre: ({ children }) => {
            if (isValidElement<{ className?: string; children?: ReactNode; node?: unknown }>(children)) {
              const className = children.props.className
              const code = String(children.props.children ?? "").replace(/\n$/, "")
              const language = className?.replace(/^language-/, "") ?? "text"
              const snippetLanguage = isSupportedSnippetLanguage(language) ? language : "text"
              const filename = extractSnippetFilename(
                "node" in children.props &&
                  children.props.node &&
                  typeof children.props.node === "object" &&
                  "meta" in children.props.node
                  ? String(children.props.node.meta ?? "")
                  : "node" in children.props &&
                      children.props.node &&
                      typeof children.props.node === "object" &&
                      "data" in children.props.node &&
                      children.props.node.data &&
                      typeof children.props.node.data === "object" &&
                      "meta" in children.props.node.data
                    ? String(children.props.node.data.meta ?? "")
                    : ""
              )

              return <CodeSnippet code={code} lang={snippetLanguage} filename={filename ?? undefined} />
            }

            return <pre>{children}</pre>
          },
          code: ({ children }) => (
            <code className="rounded-md bg-[var(--color-surface-container-low)] px-1.5 py-0.5 font-mono text-[0.92em] text-[var(--color-primary)]">
              {children}
            </code>
          ),
          a: ({ href, children }) => {
            if (!href) {
              return <span>{children}</span>
            }

            const internalHref = resolveDocumentationHref(currentSourcePath, href)
            if (internalHref === "/docs") {
              return (
                <Link
                  to="/docs"
                  className="font-medium text-[var(--color-primary)] underline underline-offset-4 transition-opacity hover:opacity-80"
                >
                  {children}
                </Link>
              )
            }

            if (internalHref?.startsWith("/docs/")) {
              if (internalHref.includes("#")) {
                return (
                  <a
                    href={internalHref}
                    className="font-medium text-[var(--color-primary)] underline underline-offset-4 transition-opacity hover:opacity-80"
                  >
                    {children}
                  </a>
                )
              }

              const docSlug = internalHref.replace(/^\/docs\//, "").split("#")[0]

              return (
                <Link
                  to="/docs/$docSlug"
                  params={{ docSlug }}
                  className="font-medium text-[var(--color-primary)] underline underline-offset-4 transition-opacity hover:opacity-80"
                >
                  {children}
                </Link>
              )
            }

            if (internalHref?.startsWith("#")) {
              return (
                <a href={internalHref} className="font-medium text-[var(--color-primary)] underline underline-offset-4">
                  {children}
                </a>
              )
            }

            return (
              <a
                href={href}
                target="_blank"
                rel="noreferrer"
                className="font-medium text-[var(--color-primary)] underline underline-offset-4 transition-opacity hover:opacity-80"
              >
                {children}
              </a>
            )
          }
        }}
      >
        {markdown}
      </Markdown>
    </div>
  )
}

function DocumentationTableOfContents({
  items,
  scrollContainerRef
}: {
  items: DocumentationTocItem[]
  scrollContainerRef: React.RefObject<HTMLDivElement | null>
}) {
  const [resolvedItems, setResolvedItems] = useState(items)
  const [activeId, setActiveId] = useState(items[0]?.id ?? "")

  useEffect(() => {
    const scrollContainer = scrollContainerRef.current
    if (!scrollContainer || items.length === 0) {
      return
    }

    const headings = Array.from(scrollContainer.querySelectorAll<HTMLElement>("[data-doc-heading='true'][id]"))
      .map((element) => ({
        item: {
          id: element.id,
          label: normalizeHeadingLabel(element.textContent ?? ""),
          level: Number.parseInt(element.tagName.replace(/[^0-9]/g, ""), 10) || 2
        },
        element
      }))
      .filter((entry) => entry.item.label)

    if (headings.length === 0) {
      setResolvedItems(items)
      return
    }

    const nextItems = headings.map((heading) => heading.item)
    setResolvedItems(nextItems)
    setActiveId((current) => current || nextItems[0]?.id || "")

    let animationFrameId = 0

    const syncActiveHeading = () => {
      const activationOffset = 120
      const currentThreshold = scrollContainer.scrollTop + activationOffset
      let nextActiveId = headings[0].item.id

      for (const heading of headings) {
        const relativeTop = getElementTopWithinScrollContainer(heading.element, scrollContainer)
        if (relativeTop <= currentThreshold) {
          nextActiveId = heading.item.id
          continue
        }

        break
      }

      setActiveId((current) => (current === nextActiveId ? current : nextActiveId))
    }

    const onScroll = () => {
      cancelAnimationFrame(animationFrameId)
      animationFrameId = window.requestAnimationFrame(syncActiveHeading)
    }

    syncActiveHeading()
    scrollContainer.addEventListener("scroll", onScroll, { passive: true })
    window.addEventListener("resize", onScroll)

    return () => {
      cancelAnimationFrame(animationFrameId)
      scrollContainer.removeEventListener("scroll", onScroll)
      window.removeEventListener("resize", onScroll)
    }
  }, [items, scrollContainerRef])

  const scrollToHeading = (headingId: string) => {
    const scrollContainer = scrollContainerRef.current
    if (!scrollContainer) {
      return
    }

    const heading = findHeadingWithinContainer(scrollContainer, headingId)
    if (!(heading instanceof HTMLElement)) {
      return
    }

    const top = Math.max(0, getElementTopWithinScrollContainer(heading, scrollContainer) - 24)

    scrollContainer.scrollTo({
      top,
      behavior: "smooth"
    })
    setActiveId(headingId)
    window.history.replaceState(null, "", `${window.location.pathname}${window.location.search}#${headingId}`)
  }

  return (
    <aside className="hidden xl:flex xl:h-full xl:flex-col xl:overflow-hidden xl:border-l xl:border-[color:var(--effect-ghost-border)] xl:bg-[color:color-mix(in_srgb,var(--color-surface)_92%,white_8%)] xl:p-4">
      <div className="min-h-0 flex-1">
        <div className="custom-scrollbar h-full overflow-y-auto pr-1">
          <div className="mb-4 px-3">
            <p className="font-display text-base font-semibold tracking-[-0.02em] text-[var(--color-primary)]">
              In this article
            </p>
          </div>

          <nav aria-label="In this article" className="space-y-1">
            {resolvedItems.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => {
                  scrollToHeading(item.id)
                }}
                className={`block w-full rounded-[var(--radius-lg)] px-3 py-2 text-left text-sm leading-6 transition-colors hover:bg-[var(--color-surface-container-low)] hover:text-[var(--color-primary)] ${
                  activeId === item.id
                    ? "bg-[var(--color-primary-fixed)]/35 text-[var(--color-primary)]"
                    : item.level <= 2
                      ? "font-medium text-[var(--color-on-surface)]"
                      : "text-[var(--color-on-surface-variant)]"
                }`}
                style={{ paddingLeft: `${0.75 + Math.max(item.level - 1, 0) * 0.75}rem` }}
              >
                {item.label}
              </button>
            ))}
          </nav>
        </div>
      </div>
    </aside>
  )
}

function getSectionLabel(section: (typeof documentationSections)[number]["id"]) {
  return documentationSections.find((candidate) => candidate.id === section)?.title ?? "Documentation"
}

function extractSnippetFilename(meta: string) {
  if (!meta) {
    return null
  }

  const filenameMatch =
    /(?:^|\s)(?:filename|title)=(?:"([^"]+)"|'([^']+)'|([^\s]+))/.exec(meta) ??
    /(?:^|\s)(?:filename|title):(?:"([^"]+)"|'([^']+)'|([^\s]+))/.exec(meta)

  return filenameMatch?.[1] ?? filenameMatch?.[2] ?? filenameMatch?.[3] ?? null
}

function isSupportedSnippetLanguage(language: string): language is "bash" | "csharp" | "json" | "text" | "ts" | "tsx" {
  return ["bash", "csharp", "json", "text", "ts", "tsx"].includes(language)
}

function navigateToDoc(docSlug: string, navigate?: ReturnType<typeof useNavigate>) {
  if (navigate) {
    void navigate({ to: "/docs/$docSlug", params: { docSlug } })
    return
  }

  window.location.assign(`/docs/${docSlug}`)
}

function extractMarkdownHeadings(markdown: string): DocumentationTocItem[] {
  const lines = markdown.split(/\r?\n/)
  const headingIdFor = createHeadingIdFactory()
  const items: DocumentationTocItem[] = []
  let inFence = false

  for (const line of lines) {
    if (/^\s*(```|~~~)/.test(line)) {
      inFence = !inFence
      continue
    }

    if (inFence) {
      continue
    }

    const match = line.match(/^(#{1,6})\s+(.+?)\s*#*\s*$/)
    if (!match) {
      continue
    }

    const level = match[1].length
    const label = normalizeHeadingLabel(match[2])
    if (!label) {
      continue
    }

    items.push({
      id: headingIdFor(label),
      label,
      level
    })
  }

  return items
}

function createHeadingIdFactory() {
  const seen = new Map<string, number>()

  return (value: string) => {
    const base = slugifyHeading(value)
    const count = seen.get(base) ?? 0
    seen.set(base, count + 1)
    return count === 0 ? base : `${base}-${count + 1}`
  }
}

function normalizeHeadingLabel(value: string) {
  return value
    .replace(/!\[([^\]]*)\]\([^)]+\)/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
    .replace(/[`*_~]/g, "")
    .trim()
}

function slugifyHeading(value: string) {
  const slug = normalizeHeadingLabel(value)
    .toLowerCase()
    .replace(/&/g, " and ")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")

  return slug || "section"
}

function getElementTopWithinScrollContainer(element: HTMLElement, scrollContainer: HTMLDivElement) {
  const elementRect = element.getBoundingClientRect()
  const containerRect = scrollContainer.getBoundingClientRect()
  return elementRect.top - containerRect.top + scrollContainer.scrollTop
}

function findHeadingWithinContainer(scrollContainer: HTMLDivElement, headingId: string) {
  const element = document.getElementById(headingId)
  if (!(element instanceof HTMLElement)) {
    return null
  }

  return scrollContainer.contains(element) && element.dataset.docHeading === "true" ? element : null
}

function extractNodeText(node: ReactNode): string {
  if (typeof node === "string" || typeof node === "number") {
    return String(node)
  }

  if (Array.isArray(node)) {
    return node.map(extractNodeText).join("")
  }

  if (isValidElement<{ children?: ReactNode }>(node)) {
    return extractNodeText(node.props.children)
  }

  return ""
}
