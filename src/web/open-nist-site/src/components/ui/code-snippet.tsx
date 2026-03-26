import { startTransition, useEffect, useState } from "react"

import { cn } from "@/lib/utils"

type CodeSnippetProps = {
  code: string
  lang: "bash" | "csharp" | "json" | "ts" | "tsx"
  className?: string
}

const shikiTheme = "github-dark-default"
const highlighterPromise = import("shiki").then(({ createHighlighter }) =>
  createHighlighter({
    themes: [shikiTheme],
    langs: ["bash", "csharp", "json", "ts", "tsx"]
  })
)

export function CodeSnippet({ code, lang, className }: CodeSnippetProps) {
  const [html, setHtml] = useState<string | null>(null)

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

  if (!html) {
    return (
      <pre
        className={cn(
          "overflow-x-auto whitespace-pre rounded-[inherit] bg-[#0d1117] p-0 font-mono text-sm leading-7 text-[#c9d1d9]",
          className
        )}
      >
        <code className="block min-w-full px-6 py-6">{code}</code>
      </pre>
    )
  }

  return (
    <div
      className={cn(
        "overflow-hidden rounded-[inherit] [&_.line]:min-h-[1.75rem] [&_.shiki]:m-0 [&_.shiki]:overflow-x-auto [&_.shiki]:bg-transparent! [&_.shiki]:px-6 [&_.shiki]:py-6 [&_.shiki]:font-mono [&_.shiki]:text-sm [&_.shiki]:leading-7 [&_pre]:m-0",
        className
      )}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
