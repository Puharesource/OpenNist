import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { PanelLeft, PanelLeftClose, PanelRight, PanelRightClose } from "lucide-react";

import { Button } from "@/components/ui/button";

type WorkspaceSidebarsContextValue = {
  leftDocked: boolean;
  rightDocked: boolean;
  leftInlineVisible: boolean;
  rightInlineVisible: boolean;
  leftOverlayVisible: boolean;
  rightOverlayVisible: boolean;
  toggleLeftSidebar(): void;
  toggleRightSidebar(): void;
  closeOverlaySidebars(): void;
  closeRightSidebar(): void;
};

const WorkspaceSidebarsContext = createContext<WorkspaceSidebarsContextValue | null>(null);

function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() =>
    typeof window !== "undefined" ? window.matchMedia(query).matches : false,
  );

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const mediaQuery = window.matchMedia(query);
    const onChange = (event: MediaQueryListEvent) => {
      setMatches(event.matches);
    };

    setMatches(mediaQuery.matches);
    mediaQuery.addEventListener("change", onChange);
    return () => {
      mediaQuery.removeEventListener("change", onChange);
    };
  }, [query]);

  return matches;
}

export function WorkspaceSidebarsProvider({ children }: { children: ReactNode }) {
  const leftDocked = useMediaQuery("(min-width: 1024px)");
  const rightDocked = useMediaQuery("(min-width: 1280px)");
  const [leftDesktopOpen, setLeftDesktopOpen] = useState(true);
  const [rightDesktopOpen, setRightDesktopOpen] = useState(true);
  const [leftOverlayOpen, setLeftOverlayOpen] = useState(false);
  const [rightOverlayOpen, setRightOverlayOpen] = useState(false);

  useEffect(() => {
    if (leftDocked) {
      setLeftOverlayOpen(false);
    }
  }, [leftDocked]);

  useEffect(() => {
    if (rightDocked) {
      setRightOverlayOpen(false);
    }
  }, [rightDocked]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") {
        return;
      }

      setLeftOverlayOpen(false);
      setRightOverlayOpen(false);
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  const value = useMemo<WorkspaceSidebarsContextValue>(() => {
    const leftInlineVisible = leftDocked && leftDesktopOpen;
    const rightInlineVisible = rightDocked && rightDesktopOpen;
    const leftOverlayVisible = !leftDocked && leftOverlayOpen;
    const rightOverlayVisible = !rightDocked && rightOverlayOpen;

    return {
      leftDocked,
      rightDocked,
      leftInlineVisible,
      rightInlineVisible,
      leftOverlayVisible,
      rightOverlayVisible,
      toggleLeftSidebar() {
        if (leftDocked) {
          setLeftDesktopOpen((current) => !current);
          return;
        }

        setLeftOverlayOpen((current) => {
          const next = !current;
          if (next) {
            setRightOverlayOpen(false);
          }

          return next;
        });
      },
      toggleRightSidebar() {
        if (rightDocked) {
          setRightDesktopOpen((current) => !current);
          return;
        }

        setRightOverlayOpen((current) => {
          const next = !current;
          if (next) {
            setLeftOverlayOpen(false);
          }

          return next;
        });
      },
      closeOverlaySidebars() {
        setLeftOverlayOpen(false);
        setRightOverlayOpen(false);
      },
      closeRightSidebar() {
        if (rightDocked) {
          setRightDesktopOpen(false);
          return;
        }

        setRightOverlayOpen(false);
      },
    };
  }, [leftDesktopOpen, leftDocked, leftOverlayOpen, rightDesktopOpen, rightDocked, rightOverlayOpen]);

  return <WorkspaceSidebarsContext.Provider value={value}>{children}</WorkspaceSidebarsContext.Provider>;
}

export function useWorkspaceSidebars(): WorkspaceSidebarsContextValue {
  const context = useContext(WorkspaceSidebarsContext);
  if (!context) {
    throw new Error("useWorkspaceSidebars must be used within a WorkspaceSidebarsProvider.");
  }

  return context;
}

export function WorkspaceSidebarBackdrop() {
  const { leftOverlayVisible, rightOverlayVisible, closeOverlaySidebars } = useWorkspaceSidebars();
  const isVisible = leftOverlayVisible || rightOverlayVisible;

  return (
    <button
      type="button"
      aria-label="Close sidebars"
      className={`absolute inset-0 z-20 bg-[color:color-mix(in_srgb,var(--color-surface)_50%,black_50%)]/35 backdrop-blur-[2px] transition-opacity ${
        isVisible ? "opacity-100" : "pointer-events-none opacity-0"
      }`}
      onClick={closeOverlaySidebars}
    />
  );
}

export function WorkspaceSidebarToggleGroup({
  children,
  showRightToggle = true,
}: {
  children?: ReactNode;
  showRightToggle?: boolean;
}) {
  const {
    leftInlineVisible,
    leftOverlayVisible,
    rightInlineVisible,
    rightOverlayVisible,
    toggleLeftSidebar,
    toggleRightSidebar,
  } = useWorkspaceSidebars();

  const isLeftOpen = leftInlineVisible || leftOverlayVisible;
  const isRightOpen = rightInlineVisible || rightOverlayVisible;

  return (
    <div className="flex items-center gap-2">
      <Button
        type="button"
        variant="outline"
        size="icon-sm"
        className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/75 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] backdrop-blur-sm hover:bg-[var(--color-surface-container-low)]"
        onClick={toggleLeftSidebar}
        aria-label={isLeftOpen ? "Collapse navigation sidebar" : "Expand navigation sidebar"}
        title={isLeftOpen ? "Collapse navigation" : "Expand navigation"}
      >
        {isLeftOpen ? <PanelLeftClose className="size-4" /> : <PanelLeft className="size-4" />}
      </Button>

      {children}

      {showRightToggle ? (
        <Button
          type="button"
          variant="outline"
          size="icon-sm"
          className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-white/75 text-[var(--color-primary)] shadow-[var(--effect-subtle-shadow)] backdrop-blur-sm hover:bg-[var(--color-surface-container-low)]"
          onClick={toggleRightSidebar}
          aria-label={isRightOpen ? "Collapse details sidebar" : "Expand details sidebar"}
          title={isRightOpen ? "Collapse details" : "Expand details"}
        >
          {isRightOpen ? <PanelRightClose className="size-4" /> : <PanelRight className="size-4" />}
        </Button>
      ) : null}
    </div>
  );
}
