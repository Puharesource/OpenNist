import { Info } from "lucide-react"
import { useEffect, useId, useRef, useState, type ReactNode } from "react"

type InspectorItem = {
  label: string
  value: string
  description?: string
}

export function InspectorPanel({
  title,
  summary,
  headerActions,
  rightDocked,
  rightOverlayVisible,
  children
}: {
  title: string
  summary?: ReactNode
  headerActions?: ReactNode
  rightDocked: boolean
  rightOverlayVisible: boolean
  children: ReactNode
}) {
  return (
    <aside
      className={`custom-scrollbar h-full shrink-0 overflow-y-auto bg-white ${
        rightDocked
          ? "w-[390px]"
          : `absolute inset-y-0 right-0 z-30 w-[min(24.375rem,calc(100vw-1rem))] border-l border-[color:var(--effect-ghost-border)] shadow-[var(--effect-modal-shadow)] transition-transform duration-200 ${
              rightOverlayVisible ? "translate-x-0" : "translate-x-full pointer-events-none"
            }`
      }`}
    >
      <div className="flex h-full flex-col">
        <div className="border-b border-[color:var(--effect-ghost-border)] px-7 py-7">
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <h2 className="truncate font-display text-3xl font-semibold leading-tight tracking-[-0.05em] text-[var(--color-primary)]">
                {title}
              </h2>
              {summary ? (
                <p className="mt-3 text-sm leading-7 text-[var(--color-on-surface-variant)]">{summary}</p>
              ) : null}
            </div>
            {headerActions ? <div className="flex shrink-0 items-center gap-2">{headerActions}</div> : null}
          </div>
        </div>

        <div className="flex-1 space-y-7 px-7 py-7">{children}</div>
      </div>
    </aside>
  )
}

export function InspectorMetric({
  eyebrow,
  value,
  meta,
  description
}: {
  eyebrow: string
  value: string
  meta?: string
  description?: string
}) {
  return (
    <div className="rounded-[var(--radius-xl)] border border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] p-5">
      <div className="flex items-center gap-2">
        <p className="font-mono text-[0.62rem] uppercase tracking-[0.2em] text-[var(--color-secondary)]">{eyebrow}</p>
        {description ? <InspectorInfoButton label={eyebrow} description={description} /> : null}
      </div>
      <div className="mt-3 flex items-end gap-3">
        <span className="font-display text-6xl font-semibold leading-none tracking-[-0.06em] text-[var(--color-primary)]">
          {value}
        </span>
        {meta ? (
          <span className="pb-1 font-mono text-[0.72rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
            {meta}
          </span>
        ) : null}
      </div>
    </div>
  )
}

export function InspectorNotice({
  title,
  message,
  tone = "warning"
}: {
  title: string
  message: string
  tone?: "warning" | "error"
}) {
  const toneClasses =
    tone === "error" ? "border-red-300/60 bg-red-50 text-red-900" : "border-amber-300/60 bg-amber-50 text-amber-900"

  return (
    <div className={`rounded-[var(--radius-lg)] border px-4 py-3 ${toneClasses}`}>
      <p className="font-mono text-[0.62rem] uppercase tracking-[0.18em]">{title}</p>
      <p className="mt-2 text-sm leading-6">{message}</p>
    </div>
  )
}

export function InspectorSection({
  title,
  description,
  items
}: {
  title: string
  description?: string
  items: InspectorItem[]
}) {
  return (
    <div className="space-y-3">
      <div className="space-y-2 border-b border-[color:var(--effect-ghost-border)] pb-2">
        <p className="font-mono text-[0.66rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">
          {title}
        </p>
        {description ? <p className="text-sm leading-6 text-[var(--color-on-surface-variant)]">{description}</p> : null}
      </div>
      <div className="space-y-3">
        {items.map((item) => (
          <div
            key={item.label}
            className="rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white p-4 shadow-[var(--effect-subtle-shadow)]"
          >
            <div className="flex items-start justify-between gap-4">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-[var(--color-on-surface)]">{item.label}</p>
                  {item.description ? <InspectorInfoButton label={item.label} description={item.description} /> : null}
                </div>
              </div>
              <p className="max-w-[48%] text-right font-mono text-xs uppercase tracking-[0.16em] text-[var(--color-secondary)]">
                {item.value}
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export function InspectorInfoButton({ label, description }: { label: string; description: string }) {
  const [open, setOpen] = useState(false)
  const [pinned, setPinned] = useState(false)
  const wrapperRef = useRef<HTMLSpanElement | null>(null)
  const panelRef = useRef<HTMLDivElement | null>(null)
  const closeTimeoutRef = useRef<number | null>(null)
  const panelId = useId()

  useEffect(() => {
    if (!open) {
      return
    }

    const positionPanel = () => {
      const wrapper = wrapperRef.current
      const panel = panelRef.current

      if (!wrapper || !panel) {
        return
      }

      const gutter = 16
      const preferredWidth = 288
      const availableWidth = Math.max(220, window.innerWidth - gutter * 2)
      const width = Math.min(preferredWidth, availableWidth)
      const wrapperRect = wrapper.getBoundingClientRect()

      panel.style.position = "fixed"
      panel.style.width = `${width}px`

      const panelHeight = panel.offsetHeight
      const centeredLeft = wrapperRect.left + wrapperRect.width / 2 - width / 2
      const left = Math.min(Math.max(gutter, centeredLeft), Math.max(gutter, window.innerWidth - width - gutter))

      const belowTop = wrapperRect.bottom + 8
      const aboveTop = wrapperRect.top - panelHeight - 8
      const top =
        belowTop + panelHeight <= window.innerHeight - gutter || aboveTop < gutter
          ? Math.min(belowTop, Math.max(gutter, window.innerHeight - panelHeight - gutter))
          : aboveTop

      panel.style.left = `${left}px`
      panel.style.top = `${top}px`
    }

    const rafId = window.requestAnimationFrame(positionPanel)
    window.addEventListener("resize", positionPanel)
    window.addEventListener("scroll", positionPanel, true)

    return () => {
      window.cancelAnimationFrame(rafId)
      window.removeEventListener("resize", positionPanel)
      window.removeEventListener("scroll", positionPanel, true)
    }
  }, [open])

  useEffect(() => {
    if (!open) {
      return
    }

    const onPointerDown = (event: PointerEvent) => {
      if (!wrapperRef.current?.contains(event.target as Node)) {
        setOpen(false)
        setPinned(false)
      }
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false)
        setPinned(false)
      }
    }

    window.addEventListener("pointerdown", onPointerDown)
    window.addEventListener("keydown", onKeyDown)
    return () => {
      window.removeEventListener("pointerdown", onPointerDown)
      window.removeEventListener("keydown", onKeyDown)
    }
  }, [open])

  useEffect(() => {
    return () => {
      if (closeTimeoutRef.current !== null) {
        window.clearTimeout(closeTimeoutRef.current)
      }
    }
  }, [])

  const cancelClose = () => {
    if (closeTimeoutRef.current !== null) {
      window.clearTimeout(closeTimeoutRef.current)
      closeTimeoutRef.current = null
    }
  }

  const scheduleClose = () => {
    cancelClose()

    if (pinned) {
      return
    }

    closeTimeoutRef.current = window.setTimeout(() => {
      setOpen(false)
    }, 120)
  }

  return (
    <span
      ref={wrapperRef}
      className="relative inline-flex"
      onMouseEnter={() => {
        cancelClose()
        if (!pinned) {
          setOpen(true)
        }
      }}
      onMouseLeave={scheduleClose}
    >
      <button
        type="button"
        aria-label={`More information about ${label}`}
        aria-expanded={open}
        aria-controls={panelId}
        className="inline-flex size-5 items-center justify-center rounded-full border border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] text-[var(--color-secondary)] transition-colors hover:bg-[var(--color-primary-fixed)]/25 hover:text-[var(--color-primary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary)]/30"
        onClick={() => {
          cancelClose()
          setPinned((current) => {
            const next = !current
            setOpen(next || !open)
            return next
          })
        }}
        onFocus={() => {
          cancelClose()
          setOpen(true)
        }}
        onBlur={() => {
          if (!pinned) {
            scheduleClose()
          }
        }}
      >
        <Info className="size-3" />
      </button>

      <div
        ref={panelRef}
        id={panelId}
        role="tooltip"
        aria-hidden={!open}
        className={`fixed left-0 top-0 z-20 whitespace-pre-line rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-white p-3 text-sm leading-6 text-[var(--color-on-surface)] shadow-[var(--effect-modal-shadow)] transition-opacity ${
          open ? "pointer-events-auto opacity-100" : "pointer-events-none opacity-0"
        }`}
        onMouseEnter={cancelClose}
        onMouseLeave={scheduleClose}
      >
        {description}
      </div>
    </span>
  )
}
