import { createRootRoute, createRoute, createRouter, Navigate } from "@tanstack/react-router"

import { BiometricSubjectsPage, LibraryDocumentationPage } from "@/components/docs-pages"
import { LandingPage } from "@/components/landing-page"
import { RootLayout } from "@/components/site-chrome"
import { WorkspacePage } from "@/components/workspace-page"

const rootRoute = createRootRoute({
  component: RootLayout
})

const landingRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: LandingPage
})

const documentationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "docs",
  component: LibraryDocumentationPage
})

const subjectsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "subjects",
  component: BiometricSubjectsPage
})

const appRedirectRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "app",
  component: AppRedirect
})

const codecsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "app/codecs",
  component: () => <WorkspacePage view="codecs" />
})

const nistRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "app/nist",
  component: () => <WorkspacePage view="nist" />
})

const nfiqRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "app/nfiq",
  component: () => <WorkspacePage view="nfiq" />
})

const routeTree = rootRoute.addChildren({
  landingRoute,
  documentationRoute,
  subjectsRoute,
  appRedirectRoute,
  codecsRoute,
  nistRoute,
  nfiqRoute
})

export const router = createRouter({
  routeTree,
  defaultPreload: "intent",
  scrollRestoration: true
})

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router
  }
}

function AppRedirect() {
  return <Navigate to="/app/nist" replace />
}
