const cacheName = "TotallySaneStudio-Project Unwind-0.1.4";
const contentToCache = [
    "Build/8db5deec927f2f2c2e9f0940e74c7ac6.loader.js",
    "Build/9e807047170d8b5ca800187c2aa27437.framework.js.unityweb",
    "Build/75d0d5bb349c47c86df4b12fb732af84.data.unityweb",
    "Build/8ffcc5bbdad413779dd04161be7cd428.wasm.unityweb",
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
