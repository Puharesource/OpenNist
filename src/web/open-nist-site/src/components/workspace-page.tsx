import { Link } from "@tanstack/react-router"
import { CloudDownload } from "lucide-react"

import { CodecsWorkspace } from "@/components/codecs-workspace"
import { NfiqWorkspace } from "@/components/nfiq-workspace"
import { NistWorkspace } from "@/components/nist-workspace"
import { Button } from "@/components/ui/button"
import {
  WorkspaceSidebarBackdrop,
  WorkspaceSidebarsProvider,
  useWorkspaceSidebars
} from "@/components/workspace-sidebars"
import { workspaceViews, type WorkspaceView, getWorkspaceViewConfig } from "@/lib/site-content"
import { ACCEPTED_FILES, useWorkspaceFileIntake, type WorkspaceFileIntake } from "@/lib/workspace-file-intake"
import { useWorkspaceSession } from "@/lib/workspace-session"

export function WorkspacePage({ view }: { view: WorkspaceView }) {
  const currentView = getWorkspaceViewConfig(view)
  const { activeFile } = useWorkspaceSession()
  const intake = useWorkspaceFileIntake(view)

  return (
    <WorkspaceSidebarsProvider>
      <section className="flex h-[calc(100dvh-73px)] flex-col overflow-hidden md:h-[calc(100dvh-81px)]">
        <div className="relative flex h-full w-full flex-1 overflow-hidden">
          <WorkspaceSidebarBackdrop />
          <WorkspaceSidebar currentView={view} intake={intake} />
          {view === "nist" ? (
            <NistWorkspace currentLabel={currentView.label} intake={intake} incomingFile={activeFile} />
          ) : view === "codecs" ? (
            <CodecsWorkspace currentLabel={currentView.label} intake={intake} incomingFile={activeFile} />
          ) : (
            <NfiqWorkspace currentLabel={currentView.label} intake={intake} incomingFile={activeFile} />
          )}
        </div>
      </section>
    </WorkspaceSidebarsProvider>
  )
}

function WorkspaceSidebar({ currentView, intake }: { currentView: WorkspaceView; intake: WorkspaceFileIntake }) {
  const { leftDocked, leftInlineVisible, leftOverlayVisible } = useWorkspaceSidebars()

  return (
    <aside
      className={`h-full shrink-0 border-r border-[color:var(--effect-ghost-border)] bg-[color:color-mix(in_srgb,var(--color-surface)_90%,white_10%)] p-4 ${
        leftDocked
          ? leftInlineVisible
            ? "flex w-72 flex-col"
            : "hidden"
          : `absolute inset-y-0 left-0 z-30 flex w-[min(18rem,calc(100vw-1rem))] flex-col shadow-[var(--effect-modal-shadow)] transition-transform duration-200 ${
              leftOverlayVisible ? "translate-x-0" : "-translate-x-full pointer-events-none"
            }`
      }`}
    >
      <nav aria-label="Workspace views" className="custom-scrollbar min-h-0 flex-1 space-y-1 overflow-y-auto pr-1">
        {workspaceViews.map((view) => {
          const isActive = view.id === currentView
          return (
            <Link
              key={view.id}
              to={`/app/${view.id}`}
              className={`flex w-full items-center gap-3 rounded-[var(--radius-lg)] px-3 py-3 text-left transition-colors ${
                isActive
                  ? "bg-[var(--color-primary-fixed)]/30 text-[var(--color-primary)]"
                  : "text-[var(--color-on-surface)]/78 hover:bg-[var(--color-surface-container-low)]"
              }`}
            >
              <div className="flex size-9 items-center justify-center rounded-[var(--radius-md)]">
                <view.icon className="size-5" />
              </div>
              <div className="min-w-0">
                <p className="font-display text-sm font-semibold tracking-[-0.02em]">{view.label}</p>
                <p className="truncate font-mono text-[0.62rem] uppercase tracking-[0.18em] opacity-70">
                  {view.eyebrow}
                </p>
              </div>
            </Link>
          )
        })}
      </nav>

      <div
        className={`mt-4 rounded-[var(--radius-xl)] bg-[var(--color-primary)] p-4 text-[var(--on-primary)] transition-colors ${
          intake.isDragActive ? "bg-[color:color-mix(in_srgb,var(--color-primary)_86%,white_14%)]" : ""
        }`}
        onDragEnter={intake.activateDrag}
        onDragOver={(event) => {
          event.preventDefault()
          intake.activateDrag()
        }}
        onDragLeave={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
            intake.deactivateDrag()
          }
        }}
        onDrop={intake.handleDrop}
      >
        <input
          ref={intake.fileInputRef}
          type="file"
          className="sr-only"
          accept={ACCEPTED_FILES}
          onChange={intake.handleInputChange}
        />
        <div className="flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-[var(--radius-md)] bg-white/10">
            <CloudDownload className="size-4" />
          </div>
          <div>
            <p className="font-display text-sm font-semibold tracking-[-0.02em]">Open files</p>
            <p className="font-mono text-[0.62rem] uppercase tracking-[0.18em] text-[var(--color-primary-fixed)]">
              NIST, WSQ, JP2, images
            </p>
          </div>
        </div>
        <Button
          type="button"
          className="mt-4 w-full rounded-[var(--radius-lg)] bg-white/12 text-[var(--on-primary)] hover:bg-white/18"
          onClick={intake.openPicker}
        >
          <CloudDownload className="size-4" />
          Open File
        </Button>
      </div>
    </aside>
  )
}
