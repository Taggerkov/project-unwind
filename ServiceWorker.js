const cacheName = "TotallySaneStudio-Holy Order-0.1.3";
const contentToCache = [
    "Build/ec3f22810f4b3b0b7c617aa63af1f4e2.loader.js",
    "Build/c7fa1762f4aecbf2ed37386181df8746.framework.js",
    "Build/c9db56d594ada7434c73e0a82e3ab9a3.data",
    "Build/96d99bd73318314c7ec143f666d3222c.wasm",
    "TemplateData/style.css"

];

self.addEventListener('install', function (e) {
    console.log('[Service Worker] Install');
    
    e.waitUntil((async function () {
      const cache = await caches.open(cacheName);
      console.log('[Service Worker] Caching all: app shell and content');
      await cache.addAll(contentToCache);
    })());
});

self.addEventListener('fetch', function (e) {
    e.respondWith((async function () {
      let response = await caches.match(e.request);
      console.log(`[Service Worker] Fetching resource: ${e.request.url}`);
      if (response) { return response; }

      response = await fetch(e.request);
      const cache = await caches.open(cacheName);
      console.log(`[Service Worker] Caching new resource: ${e.request.url}`);
      cache.put(e.request, response.clone());
      return response;
    })());
});
