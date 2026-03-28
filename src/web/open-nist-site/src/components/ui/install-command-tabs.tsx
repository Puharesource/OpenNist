import { useMemo, useState } from "react"
import { siBun, siDeno, siNpm, siPnpm, siYarn, type SimpleIcon } from "simple-icons"

import { CodeSnippet } from "@/components/ui/code-snippet"
import { cn } from "@/lib/utils"

const packageManagers = [
  { id: "npm", label: "npm", commandPrefix: "npm install", icon: siNpm },
  { id: "bun", label: "bun", commandPrefix: "bun add", icon: siBun },
  { id: "pnpm", label: "pnpm", commandPrefix: "pnpm add", icon: siPnpm },
  { id: "yarn", label: "yarn", commandPrefix: "yarn add", icon: siYarn },
  { id: "deno", label: "deno", commandPrefix: "deno add", icon: siDeno }
] as const

type InstallCommandTabsProps = {
  title: string
  description: string
  packageName: string
  note?: string
}

export function InstallCommandTabs({ title, description, packageName, note }: InstallCommandTabsProps) {
  const [value, setValue] = useState<(typeof packageManagers)[number]["id"]>("npm")
  const selected = packageManagers.find((manager) => manager.id === value) ?? packageManagers[0]
  const command = useMemo(() => `${selected.commandPrefix} ${packageName}`, [packageName, selected.commandPrefix])

  return (
    <section className="space-y-4">
      <div className="space-y-2">
        <h2 className="font-display text-2xl tracking-[-0.03em] text-[var(--color-primary)]">{title}</h2>
        <p className="max-w-2xl leading-7 text-[var(--color-on-surface-variant)]">{description}</p>
        {note ? <p className="text-sm leading-6 text-[var(--color-on-surface-variant)]">{note}</p> : null}
      </div>

      <CodeSnippet
        code={command}
        lang="bash"
        languageLabel="sh"
        headerStart={
          <div className="flex flex-wrap items-center gap-2">
            {packageManagers.map((manager) => (
              <button
                key={manager.id}
                type="button"
                onClick={() => setValue(manager.id)}
                className={cn(
                  "inline-flex items-center gap-2 rounded-md border px-2.5 py-1.5 text-[0.72rem] transition-colors",
                  manager.id === value
                    ? "border-white/18 bg-white/12 text-white"
                    : "border-white/10 bg-white/5 text-white/70 hover:border-white/18 hover:bg-white/10 hover:text-white"
                )}
              >
                <SimpleBrandIcon icon={manager.icon} className="size-3.5 shrink-0" />
                <span>{manager.label}</span>
              </button>
            ))}
          </div>
        }
      />
    </section>
  )
}

function SimpleBrandIcon({ icon, className }: { icon: SimpleIcon; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={className} fill="currentColor">
      <path d={icon.path} />
    </svg>
  )
}
