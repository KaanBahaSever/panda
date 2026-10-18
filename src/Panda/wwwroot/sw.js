// Panda's panel as an app on the phone's home screen. Everything comes from the server (the panel is
// live); the service worker only makes the panel installable and passes requests on.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* the network, as usual */ });
