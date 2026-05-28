const DB_NAME = 'lodr-db';
const DB_VERSION = 1;
const STORES = ['trailer-presets', 'pallet-presets', 'last-used'];

let _dbPromise = null;

function openDB() {
    if (_dbPromise) return _dbPromise;
    _dbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            STORES.forEach(name => {
                if (!db.objectStoreNames.contains(name))
                    db.createObjectStore(name, { keyPath: 'id' });
            });
        };
        req.onsuccess = e => resolve(e.target.result);
        req.onerror = e => { _dbPromise = null; reject(e.target.error); };
    });
    return _dbPromise;
}

function tx(storeName, mode, fn) {
    return openDB().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(storeName, mode);
        const store = t.objectStore(storeName);
        const req = fn(store);
        req.onsuccess = () => resolve(req.result ?? null);
        req.onerror = () => reject(req.error);
    }));
}

export function saveItem(storeName, item) {
    return tx(storeName, 'readwrite', store => store.put(item));
}

export function getItem(storeName, id) {
    return tx(storeName, 'readonly', store => store.get(id));
}

export function getAllItems(storeName) {
    return openDB().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(storeName, 'readonly');
        const req = t.objectStore(storeName).getAll();
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    }));
}

export function deleteItem(storeName, id) {
    return tx(storeName, 'readwrite', store => store.delete(id));
}
