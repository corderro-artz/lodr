// In development, always fetch from the network and do not enable offline support.
// skipWaiting + clients.claim forces this dev SW to immediately replace any
// previously installed published SW (which caches WASM/assets and serves stale data).
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });
