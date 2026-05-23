const cacheName = "TotallySaneStudio-Project Unwind-0.1.3";
const contentToCache = [
    "Build/409660bdad0ddc344db199489dd852cf.loader.js",
    "Build/9e807047170d8b5ca800187c2aa27437.framework.js.unityweb",
    "Build/0b3eaea2fb807aa7d5587ff75dc5ab4c.data.unityweb",
    "Build/069312f9c45cbd072a9ee1baac99fc9a.wasm.unityweb",
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
