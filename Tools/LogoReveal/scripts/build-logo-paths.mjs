// Builds src/logoPaths.ts — the vector shapes of the JuveNILE Games logo — from
// assets/juvenile-logo-source.png.
//
// - Wordmark: real glyph outlines from Quicksand Medium (the logo font). The logo's letters are
//   slightly condensed compared to stock Quicksand, so the glyphs share one x/y scale measured
//   from the source image and each one is centred on its letter's position in the image. That
//   keeps the original spacing and proportions exactly while staying perfectly crisp.
// - Beetle icon: not a font glyph, so it is traced with potrace. The crop is upscaled before
//   tracing so potrace can use the anti-aliased edges instead of a hard 1px staircase.
//
// Usage: npm run build:paths

import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {PNG} from 'pngjs';
import Jimp from 'jimp';
import potrace from 'potrace';
import opentype from 'opentype.js';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourcePath = path.join(root, 'assets', 'juvenile-logo-source.png');
const fontPath = path.join(root, 'node_modules', '@fontsource', 'quicksand', 'files', 'quicksand-latin-500-normal.woff');
const outPath = path.join(root, 'src', 'logoPaths.ts');

const Upscale = 8;
const Word = 'JUVENILEGAMES';
const AlphaSolid = 128;

const png = PNG.sync.read(fs.readFileSync(sourcePath));
const {width, height, data} = png;
const alphaAt = (x, y) => data[(y * width + x) * 4 + 3];
const rgbAt = (x, y) => {
    const i = (y * width + x) * 4;
    return [data[i], data[i + 1], data[i + 2]];
};
const toHex = ([r, g, b]) => '#' + [r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('').toUpperCase();
const round = (v) => Math.round(v * 100) / 100;
const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.floor(sorted.length / 2)];
};

// --- 1. Split the image into column segments: beetle icon + one per letter ---------------
const segments = [];
let start = -1;
for (let x = 0; x <= width; x++) {
    let filled = false;
    if (x < width) {
        for (let y = 0; y < height && !filled; y++) filled = alphaAt(x, y) > 20;
    }
    if (filled && start < 0) start = x;
    if (!filled && start >= 0) {
        segments.push([start, x]);
        start = -1;
    }
}
if (segments.length !== Word.length + 1) {
    throw new Error(`Expected ${Word.length + 1} segments (icon + letters), found ${segments.length}`);
}

// Sub-pixel bounding box: anti-aliased edge pixels count by their coverage.
const bbox = (x0, x1) => {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (let y = 0; y < height; y++) {
        for (let x = x0; x < x1; x++) {
            const a = alphaAt(x, y) / 255;
            if (a <= 0) continue;
            minX = Math.min(minX, x + 1 - a);
            maxX = Math.max(maxX, x + a);
            minY = Math.min(minY, y + 1 - a);
            maxY = Math.max(maxY, y + a);
        }
    }
    return {x: minX, y: minY, width: maxX - minX, height: maxY - minY};
};

const dominantColor = (x0, x1) => {
    const counts = new Map();
    for (let y = 0; y < height; y++) {
        for (let x = x0; x < x1; x++) {
            if (alphaAt(x, y) < 250) continue;
            const key = toHex(rgbAt(x, y));
            counts.set(key, (counts.get(key) ?? 0) + 1);
        }
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1])[0][0];
};

// --- 2. Trace the beetle icon -------------------------------------------------------------
const traceIcon = async (x0, x1) => {
    const w = x1 - x0;
    const img = new Jimp(w, height, 0xffffffff);
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < w; x++) {
            const v = 255 - alphaAt(x0 + x, y);
            img.setPixelColor(Jimp.rgbaToInt(v, v, v, 255), x, y);
        }
    }
    // Pad so shapes touching the crop edge still close cleanly, then upscale.
    const padded = new Jimp(w + 4, height + 4, 0xffffffff).composite(img, 2, 2);
    padded.resize((w + 4) * Upscale, (height + 4) * Upscale, Jimp.RESIZE_BICUBIC);

    const tracer = new potrace.Potrace({threshold: 128, turdSize: 20, optTolerance: 0.4, alphaMax: 1});
    await new Promise((resolve, reject) => tracer.loadImage(padded, (err) => (err ? reject(err) : resolve())));
    const d = /d="([^"]+)"/.exec(tracer.getPathTag('#000'))[1];
    // Back to source pixel space. potrace emits absolute "x y" pairs separated by commas, so
    // every space-separated pair is one point.
    return d.replace(
        /(-?\d+(?:\.\d+)?) (-?\d+(?:\.\d+)?)/g,
        (_m, px, py) => `${round(px / Upscale - 2 + x0)} ${round(py / Upscale - 2)}`,
    );
};

const [iconStart, iconEnd] = segments[0];
const icon = {...bbox(iconStart, iconEnd), color: dominantColor(iconStart, iconEnd), d: await traceIcon(iconStart, iconEnd)};

// Split the icon into its outline and its holes (d-pad + buttons) so the controller details
// can be punched open separately. Holes are ordered largest first (the d-pad).
const subpathBox = (d) => {
    const n = d.match(/-?\d+(?:\.\d+)?/g).map(Number);
    const xs = n.filter((_, i) => i % 2 === 0);
    const ys = n.filter((_, i) => i % 2 === 1);
    return {x1: Math.min(...xs), x2: Math.max(...xs), y1: Math.min(...ys), y2: Math.max(...ys)};
};
const iconSubpaths = icon.d
    .split(/(?=M)/)
    .map((d) => d.trim())
    .filter(Boolean)
    .map((d) => ({d, box: subpathBox(d)}))
    .sort((a, b) => (b.box.x2 - b.box.x1) * (b.box.y2 - b.box.y1) - (a.box.x2 - a.box.x1) * (a.box.y2 - a.box.y1));
const iconOutline = iconSubpaths[0].d;
const iconHoles = iconSubpaths.slice(1).map(({d, box}) => ({
    d,
    cx: round((box.x1 + box.x2) / 2),
    cy: round((box.y1 + box.y2) / 2),
}));

// --- 3. Fit Quicksand glyphs onto the letter positions -------------------------------------
const fontBuffer = fs.readFileSync(fontPath);
const font = opentype.parse(fontBuffer.buffer.slice(fontBuffer.byteOffset, fontBuffer.byteOffset + fontBuffer.byteLength));

const measured = Word.split('').map((char, i) => {
    const [x0, x1] = segments[i + 1];
    const glyph = font.charToGlyph(char);
    const glyphPath = glyph.getPath(0, 0, font.unitsPerEm); // y-down, font units
    return {char, color: dominantColor(x0, x1), box: bbox(x0, x1), glyphPath, glyphBox: glyphPath.getBoundingBox()};
});

// One shared scale per axis, so the letters stay consistent with each other.
const scaleX = median(measured.map((m) => m.box.width / (m.glyphBox.x2 - m.glyphBox.x1)));
const scaleY = median(measured.map((m) => m.box.height / (m.glyphBox.y2 - m.glyphBox.y1)));

const letters = measured.map(({char, color, box, glyphPath, glyphBox}) => {
    const gcx = (glyphBox.x1 + glyphBox.x2) / 2;
    const gcy = (glyphBox.y1 + glyphBox.y2) / 2;
    const cx = box.x + box.width / 2;
    const cy = box.y + box.height / 2;
    const tx = (x) => round((x - gcx) * scaleX + cx);
    const ty = (y) => round((y - gcy) * scaleY + cy);
    const d = glyphPath.commands
        .map((c) => {
            switch (c.type) {
                case 'M':
                case 'L':
                    return `${c.type} ${tx(c.x)} ${ty(c.y)}`;
                case 'Q':
                    return `Q ${tx(c.x1)} ${ty(c.y1)} ${tx(c.x)} ${ty(c.y)}`;
                case 'C':
                    return `C ${tx(c.x1)} ${ty(c.y1)} ${tx(c.x2)} ${ty(c.y2)} ${tx(c.x)} ${ty(c.y)}`;
                case 'Z':
                    return 'Z';
                default:
                    throw new Error(`Unexpected path command ${c.type}`);
            }
        })
        .join(' ');
    const w = (glyphBox.x2 - glyphBox.x1) * scaleX;
    const h = (glyphBox.y2 - glyphBox.y1) * scaleY;
    return {char, color, x: round(cx - w / 2), y: round(cy - h / 2), width: round(w), height: round(h), d};
});

// --- 4. Write output ----------------------------------------------------------------------
const roundBox = ({x, y, width: w, height: h}) => ({x: round(x), y: round(y), width: round(w), height: round(h)});
const out = `// AUTO-GENERATED by scripts/build-logo-paths.mjs — do not edit.
// Coordinates are in source-image pixels (${width}x${height}) of assets/juvenile-logo-source.png.
// The wordmark is Quicksand Medium (scaled ${round(scaleX * 1000) / 1000} x ${round(scaleY * 1000) / 1000} from font units);
// the icon is traced and must be drawn with fill-rule="evenodd".

export const LogoSize = {width: ${width}, height: ${height}} as const;

export type LogoShape = {
    x: number;
    y: number;
    width: number;
    height: number;
    color: string;
    d: string;
};

export type LogoLetter = LogoShape & {char: string};

export const Icon: LogoShape = ${JSON.stringify({...roundBox(icon), color: icon.color, d: icon.d}, null, 4)};

// Icon.d split into the outer outline and the controller holes (largest first: d-pad, buttons).
export const IconOutline = ${JSON.stringify(iconOutline)};

export const IconHoles: {d: string; cx: number; cy: number}[] = ${JSON.stringify(iconHoles, null, 4)};

export const Letters: LogoLetter[] = ${JSON.stringify(letters, null, 4)};
`;
fs.writeFileSync(outPath, out);
console.log(`Wrote icon + ${letters.length} Quicksand letters to ${path.relative(root, outPath)} (scale ${scaleX.toFixed(4)} x ${scaleY.toFixed(4)})`);
