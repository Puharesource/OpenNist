import { Check, Copy, FileCode2 } from "lucide-react"
import { type ReactNode, startTransition, useEffect, useMemo, useState } from "react"

import { cn } from "@/lib/utils"

type CodeSnippetProps = {
  code: string
  lang: "bash" | "csharp" | "json" | "text" | "ts" | "tsx"
  className?: string
  filename?: string
  headerStart?: ReactNode
  languageLabel?: string
}

const shikiTheme = "github-dark-default"
const codeFontFamily = '"JetBrains Mono Variable", "JetBrains Mono", ui-monospace, monospace'

const languageLabels: Record<CodeSnippetProps["lang"], string> = {
  bash: "sh",
  csharp: "C#",
  json: "JSON",
  text: "Text",
  ts: "TypeScript",
  tsx: "TSX"
}

const highlighterPromise = import("shiki").then(({ createHighlighter }) =>
  createHighlighter({
    themes: [shikiTheme],
    langs: ["bash", "csharp", "json", "text", "ts", "tsx"]
  })
)

export function CodeSnippet({ code, lang, className, filename, headerStart, languageLabel }: CodeSnippetProps) {
  const [html, setHtml] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const resolvedLanguageLabel = useMemo(() => languageLabel ?? languageLabels[lang], [lang, languageLabel])

  useEffect(() => {
    let cancelled = false

    void highlighterPromise
      .then((highlighter) => highlighter.codeToHtml(code, { lang, theme: shikiTheme }))
      .then((nextHtml) => {
        if (cancelled) {
          return
        }

        startTransition(() => {
          setHtml(nextHtml)
        })
      })

    return () => {
      cancelled = true
    }
  }, [code, lang])

  const showHeader = Boolean(filename || headerStart)

  const copyCode = () => {
    void navigator.clipboard.writeText(code).then(() => {
      setCopied(true)
      window.setTimeout(() => {
        setCopied(false)
      }, 1800)
    })
  }

  return (
    <div
      className={cn(
        "group/code-snippet relative overflow-hidden rounded-[var(--radius-xl)] bg-[#0d1117] ring-1 ring-white/8",
        className
      )}
    >
      {showHeader ? (
        <div
          className="relative flex items-center justify-between gap-3 border-b border-white/8 bg-[#0a0f16] px-4 py-2.5"
          style={{ fontFamily: codeFontFamily }}
        >
          <div className="flex min-w-0 flex-1 items-center gap-2 overflow-x-auto">
            {headerStart}
            {filename ? (
              <div className="inline-flex min-w-0 items-center gap-2 text-[0.72rem] text-white/75">
                <FileCode2 className="size-3.5 shrink-0" />
                <span className="truncate">{filename}</span>
              </div>
            ) : null}
          </div>

          <button
            type="button"
            className="absolute inset-y-0 right-4 z-10 inline-flex min-w-[5rem] items-center justify-end px-0 text-[0.68rem] text-white/55 transition-colors hover:text-white"
            onClick={copyCode}
            aria-label={`Copy ${resolvedLanguageLabel} snippet`}
          >
            {copied ? (
              <span className="inline-flex items-center gap-1.5 text-white/90">
                <Check className="size-3.5" />
                Copied
              </span>
            ) : (
              <>
                <span className="transition-opacity group-hover/code-snippet:opacity-0">{resolvedLanguageLabel}</span>
                <span className="pointer-events-none absolute inset-0 hidden items-center justify-end gap-1.5 opacity-0 transition-opacity group-hover/code-snippet:flex group-hover/code-snippet:opacity-100">
                  <Copy className="size-3.5" />
                  Copy
                </span>
              </>
            )}
          </button>
        </div>
      ) : null}

      {!showHeader ? (
        <button
          type="button"
          className="absolute top-3 right-4 z-10 inline-flex h-7 min-w-[5rem] items-center justify-end px-0 text-[0.68rem] text-white/55 transition-colors hover:text-white"
          onClick={copyCode}
          aria-label={`Copy ${resolvedLanguageLabel} snippet`}
          style={{ fontFamily: codeFontFamily }}
        >
          {copied ? (
            <span className="inline-flex items-center gap-1.5 text-white/90">
              <Check className="size-3.5" />
              Copied
            </span>
          ) : (
            <>
              <span className="transition-opacity group-hover/code-snippet:opacity-0">{resolvedLanguageLabel}</span>
              <span className="pointer-events-none absolute inset-0 hidden items-center justify-end gap-1.5 opacity-0 transition-opacity group-hover/code-snippet:flex group-hover/code-snippet:opacity-100">
                <Copy className="size-3.5" />
                Copy
              </span>
            </>
          )}
        </button>
      ) : null}

      {!html ? (
        <pre
          className={cn(
            "overflow-x-auto whitespace-pre p-0 text-sm leading-7 text-[#c9d1d9]",
            showHeader ? "pt-4" : "pt-0"
          )}
          style={{ fontFamily: codeFontFamily }}
        >
          <code className="block min-w-full px-6 py-6">{code}</code>
        </pre>
      ) : (
        <div
          className="[&_.line]:min-h-[1.75rem] [&_.shiki]:m-0 [&_.shiki]:overflow-x-auto [&_.shiki]:bg-transparent! [&_.shiki]:px-6 [&_.shiki]:pt-6 [&_.shiki]:pb-6 [&_.shiki]:text-sm [&_.shiki]:leading-7 [&_pre]:m-0"
          style={{ fontFamily: codeFontFamily }}
          dangerouslySetInnerHTML={{ __html: html }}
        />
      )}
    </div>
  )
}
