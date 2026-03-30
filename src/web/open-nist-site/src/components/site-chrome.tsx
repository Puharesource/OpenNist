import { useRouterState, Link, Outlet, useNavigate } from "@tanstack/react-router"
import { AppWindow, Code2, Download, PanelLeft } from "lucide-react"
import { useEffect, useState } from "react"

import { Button } from "@/components/ui/button"
import { getFileFingerprint } from "@/lib/codecs-document"
import { footerLinks, getSiteSeo, sharedNavItems } from "@/lib/site-content"
import { routeForFileName } from "@/lib/workspace-file-intake"
import { setWorkspaceActiveFile } from "@/lib/workspace-session"

type BeforeInstallPromptEvent = Event & {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: "accepted" | "dismissed"; platform: string }>
}

type LaunchQueueWindow = Window & {
  launchQueue?: {
    setConsumer(consumer: (launchParams: { files: Array<{ getFile(): Promise<File> }> }) => void): void
  }
}

export function RootLayout() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const navigate = useNavigate()
  const onApp = pathname.startsWith("/app")

  useEffect(() => {
    const seo = getSiteSeo(pathname)
    document.title = seo.title

    const updateMeta = (selector: string, attribute: "name" | "property", value: string) => {
      let element = document.head.querySelector<HTMLMetaElement>(selector)
      if (!element) {
        element = document.createElement("meta")
        element.setAttribute(attribute, selector.match(/"(.+?)"/)?.[1] ?? "")
        document.head.append(element)
      }

      element.content = value
    }

    const updateLink = (selector: string, rel: string, href: string) => {
      let element = document.head.querySelector<HTMLLinkElement>(selector)
      if (!element) {
        element = document.createElement("link")
        element.rel = rel
        document.head.append(element)
      }

      element.href = href
    }

    updateMeta('meta[name="description"]', "name", seo.description)
    updateMeta('meta[property="og:title"]', "property", seo.title)
    updateMeta('meta[property="og:description"]', "property", seo.description)
    updateMeta('meta[property="og:url"]', "property", seo.canonicalUrl)
    updateMeta('meta[name="twitter:title"]', "name", seo.title)
    updateMeta('meta[name="twitter:description"]', "name", seo.description)
    updateLink('link[rel="canonical"]', "canonical", seo.canonicalUrl)
  }, [pathname])

  useEffect(() => {
    const launchWindow = window as LaunchQueueWindow
    if (!launchWindow.launchQueue?.setConsumer) {
      return
    }

    launchWindow.launchQueue.setConsumer(({ files }) => {
      void (async () => {
        const file = await files[0]?.getFile()
        if (!file) {
          return
        }

        setWorkspaceActiveFile(file, getFileFingerprint(file))
        await navigate({ to: routeForFileName(file.name, "codecs") })
      })()
    })
  }, [navigate])

  return (
    <div id="top" className="relative flex min-h-screen flex-col overflow-hidden">
      <a
        href="#main-content"
        className="absolute left-4 top-4 z-50 -translate-y-20 rounded-full bg-[var(--color-primary)] px-4 py-2 text-sm font-medium text-[var(--on-primary)] shadow-lg transition-transform focus:translate-y-0"
      >
        Skip to content
      </a>
      <div className="blueprint-grid pointer-events-none absolute inset-0 opacity-50" />
      <SharedHeader />
      <main id="main-content" className="relative z-10 flex-1">
        <Outlet />
      </main>
      {!onApp ? <SiteFooter /> : null}
    </div>
  )
}

function SharedHeader() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const onApp = pathname.startsWith("/app")
  const [installPrompt, setInstallPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [isStandalone, setIsStandalone] = useState(false)

  useEffect(() => {
    setIsStandalone(window.matchMedia("(display-mode: standalone)").matches)

    const handleBeforeInstallPrompt = (event: Event) => {
      event.preventDefault()
      setInstallPrompt(event as BeforeInstallPromptEvent)
    }

    const handleAppInstalled = () => {
      setInstallPrompt(null)
      setIsStandalone(true)
    }

    window.addEventListener("beforeinstallprompt", handleBeforeInstallPrompt)
    window.addEventListener("appinstalled", handleAppInstalled)

    return () => {
      window.removeEventListener("beforeinstallprompt", handleBeforeInstallPrompt)
      window.removeEventListener("appinstalled", handleAppInstalled)
    }
  }, [])

  const canInstall = Boolean(installPrompt) && !isStandalone

  return (
    <header className="sticky top-0 z-40 border-b border-[color:var(--effect-ghost-border)] bg-[color:color-mix(in_srgb,var(--color-surface)_82%,white_18%)] backdrop-blur-xl">
      <div className="mx-auto grid w-full max-w-[1600px] grid-cols-[minmax(0,1fr)_auto] items-center gap-4 px-4 py-3 sm:px-6 md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] md:px-10 md:py-4">
        <div className="flex min-w-0 items-center">
          <Link to="/" className="flex items-center gap-3" aria-label="OpenNist home">
            <img src="/logo.svg" alt="OpenNist logo" className="size-10" />
            <p className="font-display text-lg font-semibold tracking-[-0.04em] text-foreground">OpenNist</p>
          </Link>
        </div>

        <nav aria-label="Primary" className="hidden items-center justify-center gap-6 md:flex">
          {sharedNavItems.map((item) => (
            <Link
              key={item.label}
              to={item.to}
              aria-current={pathname === item.to ? "page" : undefined}
              className={`font-display text-sm font-medium tracking-[-0.02em] transition-colors hover:text-foreground ${
                pathname === item.to ? "text-[var(--color-primary)]" : "text-[var(--color-on-surface)]/78"
              }`}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="flex items-center justify-end gap-2 sm:gap-3">
          {canInstall ? (
            <Button
              type="button"
              variant="outline"
              className="hidden rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-lowest)] text-[var(--color-primary)] hover:bg-white sm:inline-flex"
              onClick={() => {
                if (!installPrompt) {
                  return
                }

                void installPrompt.prompt().then(async () => {
                  await installPrompt.userChoice
                  setInstallPrompt(null)
                })
              }}
            >
              <Download className="size-4" />
              Install App
            </Button>
          ) : null}
          <Button
            asChild
            variant="outline"
            className="hidden rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)] sm:inline-flex"
          >
            <a href="https://github.com/Puharesource/OpenNist" target="_blank" rel="noreferrer">
              <Code2 className="size-4" />
              GitHub
            </a>
          </Button>
          <Button
            asChild
            className="rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
          >
            <Link to={onApp ? "/" : "/app/nist"}>
              {onApp ? <PanelLeft className="size-4" /> : <AppWindow className="size-4" />}
              {onApp ? "Back to Landing" : "Open App"}
            </Link>
          </Button>
        </div>
      </div>

      {!onApp ? (
        <nav
          aria-label="Primary"
          className="custom-scrollbar flex items-center gap-2 overflow-x-auto px-4 pb-3 md:hidden"
        >
          {sharedNavItems.map((item) => (
            <Link
              key={item.label}
              to={item.to}
              aria-current={pathname === item.to ? "page" : undefined}
              className={`whitespace-nowrap rounded-full border px-3 py-1.5 text-sm transition-colors ${
                pathname === item.to
                  ? "border-[var(--color-primary)] bg-[var(--color-primary-fixed)]/35 text-[var(--color-primary)]"
                  : "border-[color:var(--effect-ghost-border)] bg-white text-[var(--color-on-surface)]/78"
              }`}
            >
              {item.label}
            </Link>
          ))}
          {canInstall ? (
            <Button
              type="button"
              variant="outline"
              className="h-9 shrink-0 rounded-full border-[color:var(--effect-ghost-border)] bg-white px-3 text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
              onClick={() => {
                if (!installPrompt) {
                  return
                }

                void installPrompt.prompt().then(async () => {
                  await installPrompt.userChoice
                  setInstallPrompt(null)
                })
              }}
            >
              <Download className="size-4" />
              Install
            </Button>
          ) : null}
        </nav>
      ) : null}
    </header>
  )
}

function SiteFooter() {
  return (
    <footer className="relative z-10 border-t border-[color:var(--effect-ghost-border)] bg-white px-6 py-8 opacity-100 md:px-10">
      <div className="mx-auto flex w-full max-w-[1600px] flex-col gap-6 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <p className="font-mono text-xs uppercase tracking-[0.22em] text-[var(--color-on-surface-variant)]">
            OpenNist Project
          </p>
          <p className="text-sm text-[var(--color-on-surface)]/76">
            Biometric formats and quality tooling for .NET and WASM.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
          {footerLinks.map((link) => (
            <a
              key={link.label}
              href={link.href}
              target="_blank"
              rel="noreferrer"
              className="text-sm text-[var(--color-on-surface)]/76 underline-offset-4 transition-colors hover:text-[var(--color-secondary)] hover:underline"
            >
              {link.label}
            </a>
          ))}
        </div>
      </div>
    </footer>
  )
}
