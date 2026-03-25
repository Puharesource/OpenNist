import { Link } from "@tanstack/react-router";
import {
  BadgeInfo,
  CloudDownload,
  Code2,
  Download,
  FolderTree,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { CodecsWorkspace } from "@/components/codecs-workspace";
import { NfiqWorkspace } from "@/components/nfiq-workspace";
import { InspectorMetric, InspectorPanel, InspectorSection } from "@/components/workspace-inspector";
import {
  WorkspaceSidebarBackdrop,
  WorkspaceSidebarToggleGroup,
  WorkspaceSidebarsProvider,
  useWorkspaceSidebars,
} from "@/components/workspace-sidebars";
import { workspaceViews, type WorkspaceView, getWorkspaceViewConfig, nistInspectorContent, nistTreeGroups } from "@/lib/site-content";
import { ACCEPTED_FILES, useWorkspaceFileIntake, type WorkspaceFileIntake } from "@/lib/workspace-file-intake";
import { useWorkspaceSession } from "@/lib/workspace-session";

export function WorkspacePage({ view }: { view: WorkspaceView }) {
  const currentView = getWorkspaceViewConfig(view);
  const { activeFile } = useWorkspaceSession();
  const intake = useWorkspaceFileIntake(view);

  return (
    <WorkspaceSidebarsProvider>
      <section className="flex h-[calc(100vh-81px)] flex-col overflow-hidden">
        <div className="mx-auto relative flex h-full w-full max-w-[1600px] flex-1 overflow-hidden">
          <WorkspaceSidebarBackdrop />
          <WorkspaceSidebar currentView={view} intake={intake} />
          {view === "nist" ? (
            <NistWorkspace />
          ) : view === "codecs" ? (
            <CodecsWorkspace
              currentLabel={currentView.label}
              intake={intake}
              incomingFile={activeFile}
            />
          ) : (
            <NfiqWorkspace
              currentLabel={currentView.label}
              intake={intake}
              incomingFile={activeFile}
            />
          )}
        </div>
      </section>
    </WorkspaceSidebarsProvider>
  );
}

function WorkspaceSidebar({
  currentView,
  intake,
}: {
  currentView: WorkspaceView;
  intake: WorkspaceFileIntake;
}) {
  const { leftDocked, leftInlineVisible, leftOverlayVisible } = useWorkspaceSidebars();

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
      <nav className="custom-scrollbar min-h-0 flex-1 space-y-1 overflow-y-auto pr-1">
        {workspaceViews.map((view) => {
          const isActive = view.id === currentView;
          return (
            <Link
              key={view.id}
              to={`/app/${view.id}`}
              className={`flex w-full items-center gap-3 rounded-[var(--radius-lg)] px-3 py-3 text-left transition-colors ${
                isActive
                  ? "bg-[var(--color-primary-fixed)]/30 text-[var(--color-primary)]"
                  : "text-[var(--color-on-surface-variant)] hover:bg-[var(--color-surface-container-low)]"
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
          );
        })}
      </nav>

      <div
        className={`mt-4 rounded-[var(--radius-xl)] bg-[var(--color-primary)] p-4 text-[var(--on-primary)] transition-colors ${
          intake.isDragActive ? "bg-[color:color-mix(in_srgb,var(--color-primary)_86%,white_14%)]" : ""
        }`}
        onDragEnter={intake.activateDrag}
        onDragOver={(event) => {
          event.preventDefault();
          intake.activateDrag();
        }}
        onDragLeave={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
            intake.deactivateDrag();
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
  );
}

function NistWorkspace() {
  const { rightInlineVisible, rightDocked, rightOverlayVisible } = useWorkspaceSidebars();
  const showRightSidebar = rightInlineVisible || rightOverlayVisible;

  return (
    <>
      <div
        className={`flex min-w-0 flex-1 flex-col overflow-hidden ${
          rightInlineVisible ? "border-r border-[color:var(--effect-ghost-border)]" : ""
        }`}
      >
        <div className="border-b border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] px-6 py-5">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="font-display text-3xl font-semibold tracking-[-0.05em] text-[var(--color-primary)]">
                NIST Structure
              </h1>
              <p className="mt-1 max-w-xl text-xs leading-5 text-[var(--color-on-surface-variant)]">
                Static placeholder for the future NIST tree and field inspector.
              </p>
            </div>
            <WorkspaceSidebarToggleGroup />
          </div>
        </div>

        <div className="flex-1 overflow-hidden">
          <div className="h-full overflow-auto px-6 py-6">
            <div className="surface-module flex min-h-full flex-col overflow-hidden rounded-[var(--radius-xl)] border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
              <div className="custom-scrollbar flex-1 overflow-auto p-4">
                <div className="rounded-[var(--radius-lg)] bg-[var(--color-primary-fixed)]/30 px-4 py-3">
                  <div className="flex items-center gap-3">
                    <FolderTree className="size-4 text-[var(--color-primary)]" />
                    <span className="font-mono text-sm font-semibold text-[var(--color-primary)]">NIST_FILE_ALPHA_V2.eft</span>
                  </div>
                </div>

                <div className="mt-4 space-y-5">
                  {nistTreeGroups.map((group) => (
                    <TreeGroup key={group.title} title={group.title} count={group.count} rows={group.rows} />
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {showRightSidebar ? (
        <WorkspaceInspector
          {...nistInspectorContent}
          rightDocked={rightDocked}
          rightOverlayVisible={rightOverlayVisible}
        />
      ) : null}
    </>
  );
}

function WorkspaceInspector({
  title,
  summary,
  sections,
  accentValue,
  accentMeta,
  accentDescription,
  rightDocked,
  rightOverlayVisible,
}: {
  title: string;
  summary: string;
  sections: ReadonlyArray<{
    title: string;
    description?: string;
    items: ReadonlyArray<{ label: string; value: string; description?: string }>;
  }>;
  accentValue: string;
  accentMeta: string;
  accentDescription?: string;
  rightDocked: boolean;
  rightOverlayVisible: boolean;
}) {
  return (
    <InspectorPanel
      title={title}
      summary={summary}
      rightDocked={rightDocked}
      rightOverlayVisible={rightOverlayVisible}
    >
      <InspectorMetric
        eyebrow="Selected field value"
        value={accentValue}
        meta={accentMeta}
        description={accentDescription}
      />

      {sections.map((section) => (
        <InspectorSection
          key={section.title}
          title={section.title}
          description={section.description}
          items={[...section.items]}
        />
      ))}

      <div className="grid grid-cols-2 gap-3 pt-2">
        <Button
          type="button"
          variant="outline"
          className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
        >
          <Code2 className="size-4" />
          Copy Hex
        </Button>
        <Button
          type="button"
          variant="outline"
          className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
        >
          <Download className="size-4" />
          Export JSON
        </Button>
      </div>
    </InspectorPanel>
  );
}

function TreeGroup({
  title,
  count,
  rows,
}: {
  title: string;
  count: string;
  rows: Array<{ label: string; active?: boolean }>;
}) {
  return (
    <div className="ml-4 border-l-2 border-[color:var(--effect-ghost-border)]">
      <div className="flex items-center gap-3 rounded-[var(--radius-lg)] px-3 py-2.5 hover:bg-[var(--color-surface-container-low)]">
        <FolderTree className="size-4 text-[var(--color-primary)]" />
        <span className="text-sm font-medium text-[var(--color-on-surface)]">{title}</span>
        <span className="ml-auto rounded-full bg-[var(--color-surface-container-high)] px-2 py-0.5 text-[0.62rem] uppercase tracking-[0.14em] text-[var(--color-on-surface-variant)]">
          {count}
        </span>
      </div>
      <div className="ml-6 mt-1 space-y-1">
        {rows.map((row) => (
          <div
            key={row.label}
            className={`flex items-center gap-3 rounded-[var(--radius-lg)] px-3 py-2 ${
              row.active
                ? "border-l-2 border-[var(--color-secondary)] bg-[var(--color-secondary)]/6"
                : "hover:bg-[var(--color-surface-container-low)]"
            }`}
          >
            <BadgeInfo className={`size-4 ${row.active ? "text-[var(--color-secondary)]" : "text-[var(--color-outline)]"}`} />
            <span className="font-mono text-xs text-[var(--color-on-surface)]">{row.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
