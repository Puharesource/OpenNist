import { cva, type VariantProps } from "class-variance-authority"
import { Slot } from "radix-ui"
import * as React from "react"

import { cn } from "@/lib/utils"

const SidebarContext = React.createContext<{ open: boolean }>({ open: true })

function SidebarProvider({
  className,
  defaultOpen = true,
  children,
  ...props
}: React.ComponentProps<"div"> & {
  defaultOpen?: boolean
}) {
  const value = React.useMemo(() => ({ open: defaultOpen }), [defaultOpen])

  return (
    <SidebarContext.Provider value={value}>
      <div data-slot="sidebar-provider" className={cn("min-h-0 w-full", className)} {...props}>
        {children}
      </div>
    </SidebarContext.Provider>
  )
}

function Sidebar({ className, children, ...props }: React.ComponentProps<"aside">) {
  return (
    <aside
      data-slot="sidebar"
      className={cn(
        "bg-[color:color-mix(in_srgb,var(--color-surface)_90%,white_10%)] text-[var(--color-on-surface)]",
        className
      )}
      {...props}
    >
      {children}
    </aside>
  )
}

function SidebarHeader({ className, ...props }: React.ComponentProps<"div">) {
  return <div data-slot="sidebar-header" className={cn("flex flex-col gap-2", className)} {...props} />
}

function SidebarContent({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sidebar-content"
      className={cn("custom-scrollbar min-h-0 flex-1 overflow-y-auto", className)}
      {...props}
    />
  )
}

function SidebarGroup({ className, ...props }: React.ComponentProps<"section">) {
  return <section data-slot="sidebar-group" className={cn("space-y-2", className)} {...props} />
}

function SidebarGroupLabel({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sidebar-group-label"
      className={cn(
        "flex items-center gap-2 px-3 text-xs font-medium tracking-[0.18em] text-[var(--color-on-surface-variant)] uppercase",
        className
      )}
      {...props}
    />
  )
}

function SidebarGroupContent({ className, ...props }: React.ComponentProps<"div">) {
  return <div data-slot="sidebar-group-content" className={cn("space-y-1", className)} {...props} />
}

function SidebarMenu({ className, ...props }: React.ComponentProps<"ul">) {
  return <ul data-slot="sidebar-menu" className={cn("space-y-1", className)} {...props} />
}

function SidebarMenuItem({ className, ...props }: React.ComponentProps<"li">) {
  return <li data-slot="sidebar-menu-item" className={cn("list-none", className)} {...props} />
}

function SidebarMenuSub({ className, ...props }: React.ComponentProps<"ul">) {
  return (
    <ul
      data-slot="sidebar-menu-sub"
      className={cn("mt-1 ml-4 space-y-1 border-l border-[color:var(--effect-ghost-border)] pl-3", className)}
      {...props}
    />
  )
}

function SidebarMenuSubItem({ className, ...props }: React.ComponentProps<"li">) {
  return <li data-slot="sidebar-menu-sub-item" className={cn("list-none", className)} {...props} />
}

const sidebarMenuButtonVariants = cva(
  "flex w-full items-start rounded-[var(--radius-lg)] px-3 py-3 text-left text-sm transition-colors outline-none",
  {
    variants: {
      variant: {
        default:
          "text-[var(--color-on-surface)] hover:bg-[var(--color-surface-container-low)] hover:text-[var(--color-primary)]",
        active: "bg-[var(--color-primary-fixed)]/45 text-[var(--color-primary)]",
        muted:
          "text-[var(--color-on-surface)]/78 hover:bg-[var(--color-surface-container-low)] hover:text-[var(--color-primary)]"
      }
    },
    defaultVariants: {
      variant: "default"
    }
  }
)

function SidebarMenuButton({
  className,
  asChild = false,
  isActive = false,
  variant,
  ...props
}: React.ComponentProps<"button"> &
  VariantProps<typeof sidebarMenuButtonVariants> & {
    asChild?: boolean
    isActive?: boolean
  }) {
  const Comp = asChild ? Slot.Root : "button"

  return (
    <Comp
      data-slot="sidebar-menu-button"
      data-active={isActive}
      className={cn(sidebarMenuButtonVariants({ variant: isActive ? "active" : variant, className }))}
      {...props}
    />
  )
}

export {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubItem,
  SidebarProvider
}
