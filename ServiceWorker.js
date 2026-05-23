const cacheName = "TotallySaneStudio-Project Unwind-0.1.4";
const contentToCache = [
    "Build/78d073282581829b8f221710ac41cf40.loader.js",
    "Build/9e807047170d8b5ca800187c2aa27437.framework.js.unityweb",
    "Build/6dd9a6db41804ce240cc54ba95d09b28.data.unityweb",
    "Build/30c24dda1c3f534bea851726460da11c.wasm.unityweb",
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
