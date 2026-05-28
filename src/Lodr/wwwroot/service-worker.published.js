self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff/, /\.png$/, /\.ico$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

async function onInstall(event) {
    self.skipWaiting();
    const assetsRequests = self.assetsManifest.assets
        .filter(x => offlineAssetsInclude.some(p => p.test(x.url)))
        .filter(x => !offlineAssetsExclude.some(p => p.test(x.url)))
        .map(x => new Request(x.url, { integrity: x.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(c => c.addAll(assetsRequests));
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    if (event.request.method !== 'GET') return;
    const cached = await caches.match(event.request, { ignoreVary: true });
    return cached ?? fetch(event.request);
}
