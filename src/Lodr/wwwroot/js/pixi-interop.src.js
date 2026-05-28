import { Application, Graphics } from 'pixi.js';

let _app = null;
let _stage = null;

export async function init(canvasId, width, height) {
    if (_app) {
        _app.destroy();
        _app = null;
        _stage = null;
    }

    _app = new Application();
    await _app.init({
        canvas: document.getElementById(canvasId),
        width,
        height,
        background: 0x111827,
        antialias: true,
        resolution: window.devicePixelRatio || 1,
        autoDensity: true,
        preference: 'webgl',
    });
    _stage = _app.stage;
}

export function render(trailerLength, trailerWidth, positions, palletLength, palletWidth) {
    if (!_app || !_stage) return;
    _stage.removeChildren();

    const PAD = 24;
    const availW = _app.renderer.width  - PAD * 2;
    const availH = _app.renderer.height - PAD * 2;
    const scaleX = availW / trailerLength;
    const scaleY = availH / trailerWidth;
    const scale  = Math.min(scaleX, scaleY);

    const offsetX = PAD + (availW - trailerLength * scale) / 2;
    const offsetY = PAD + (availH - trailerWidth  * scale) / 2;

    // Trailer fill + outline — v8 Graphics API: chain shape → fill/stroke
    const trailer = new Graphics();
    trailer
        .rect(offsetX, offsetY, trailerLength * scale, trailerWidth * scale)
        .fill({ color: 0x1f2937 })
        .rect(offsetX, offsetY, trailerLength * scale, trailerWidth * scale)
        .stroke({ color: 0x374151, width: 2 });
    _stage.addChild(trailer);

    // Pallets
    positions.forEach(pos => {
        const pw = pos.rotated ? palletWidth  : palletLength;
        const ph = pos.rotated ? palletLength : palletWidth;
        const g = new Graphics();
        g
            .rect(
                offsetX + pos.x * scale + 1,
                offsetY + pos.y * scale + 1,
                pw * scale - 2,
                ph * scale - 2
            )
            .fill({ color: 0x22c55e, alpha: 0.8 })
            .rect(
                offsetX + pos.x * scale + 1,
                offsetY + pos.y * scale + 1,
                pw * scale - 2,
                ph * scale - 2
            )
            .stroke({ color: 0x15803d, width: 1 });
        _stage.addChild(g);
    });
}

export function clear() {
    if (_stage) _stage.removeChildren();
}

export function destroy() {
    if (_app) {
        _app.destroy();
        _app = null;
        _stage = null;
    }
}
