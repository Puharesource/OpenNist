import { useRouterState, Link, Outlet } from "@tanstack/react-router";
import { AppWindow, Code2, PanelLeft } from "lucide-react";

import { Button } from "@/components/ui/button";
import openNistLogo from "@/assets/opennist-logo.svg";
import { footerLinks, sharedNavItems } from "@/lib/site-content";

export function RootLayout() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const onApp = pathname.startsWith("/app");

  return (
    <div id="top" className="relative min-h-screen overflow-hidden">
      <div className="blueprint-grid pointer-events-none absolute inset-0 opacity-50" />
      <SharedHeader />
      <main className="relative z-10">
        <Outlet />
      </main>
      {!onApp ? <SiteFooter /> : null}
    </div>
  );
}

function SharedHeader() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const onApp = pathname.startsWith("/app");

  return (
    <header className="sticky top-0 z-40 border-b border-[color:var(--effect-ghost-border)] bg-[color:color-mix(in_srgb,var(--color-surface)_82%,white_18%)] backdrop-blur-xl">
      <div className="mx-auto grid w-full max-w-[1600px] grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-4 px-6 py-4 md:px-10">
        <div className="flex min-w-0 items-center">
          <Link to="/" className="flex items-center gap-3">
            <img src={openNistLogo} alt="OpenNist logo" className="size-10" />
            <p className="font-display text-lg font-semibold tracking-[-0.04em] text-foreground">OpenNist</p>
          </Link>
        </div>

        <nav className="hidden items-center justify-center gap-6 md:flex">
          {sharedNavItems.map((item) => (
            <Link
              key={item.label}
              to={item.to}
              className={`font-display text-sm font-medium tracking-[-0.02em] transition-colors hover:text-foreground ${
                pathname === item.to
                  ? "text-[var(--color-primary)]"
                  : "text-[var(--color-on-surface-variant)]"
              }`}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="flex items-center justify-end gap-3">
          <Button
            asChild
            variant="outline"
            className="hidden rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)] sm:inline-flex"
          >
            <a href="https://github.com/OpenNist/OpenNist" target="_blank" rel="noreferrer">
              <Code2 className="size-4" />
              GitHub
            </a>
          </Button>
          <Button
            asChild
            className="rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
          >
            <Link to={onApp ? "/" : "/app/codecs"}>
              {onApp ? <PanelLeft className="size-4" /> : <AppWindow className="size-4" />}
              {onApp ? "Back to Landing" : "Open App"}
            </Link>
          </Button>
        </div>
      </div>
    </header>
  );
}

function SiteFooter() {
  return (
    <footer className="border-t border-[color:var(--effect-ghost-border)] bg-[color:color-mix(in_srgb,var(--color-surface)_88%,white_12%)] px-6 py-8 md:px-10">
      <div className="mx-auto flex w-full max-w-[1600px] flex-col gap-6 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <p className="font-mono text-xs uppercase tracking-[0.22em] text-[var(--color-on-surface-variant)]">
            OpenNist Project
          </p>
          <p className="text-sm text-[var(--color-on-surface-variant)]">
            Managed biometric formats and quality tooling for .NET and WebAssembly.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
          {footerLinks.map((link) => (
            <a
              key={link.label}
              href={link.href}
              target="_blank"
              rel="noreferrer"
              className="text-sm text-[var(--color-on-surface-variant)] underline-offset-4 transition-colors hover:text-[var(--color-secondary)] hover:underline"
            >
              {link.label}
            </a>
          ))}
        </div>
      </div>
    </footer>
  );
}
