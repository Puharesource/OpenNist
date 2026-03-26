const CACHE_NAME = "opennist-shell-v1"
const SHELL_ASSETS = ["/", "/index.html", "/manifest.webmanifest", "/logo.svg"]

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS)))
  self.skipWaiting()
})

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))))
  )
  self.clients.claim()
})

self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") {
    return
  }

  if (event.request.mode === "navigate") {
    event.respondWith(
      fetch(event.request).catch(async () => {
        const cache = await caches.open(CACHE_NAME)
        return (await cache.match("/index.html")) || Response.error()
      })
    )
    return
  }

  const requestUrl = new URL(event.request.url)
  if (requestUrl.origin !== self.location.origin) {
    return
  }

  event.respondWith(
    caches.match(event.request).then(
      (cachedResponse) =>
        cachedResponse ||
        fetch(event.request).then((networkResponse) => {
          if (networkResponse.ok && requestUrl.pathname.startsWith("/assets/")) {
            const responseClone = networkResponse.clone()
            void caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseClone))
          }

          return networkResponse
        })
    )
  )
})
