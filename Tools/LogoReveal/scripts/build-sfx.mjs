// Synthesises the logo reveal soundtrack (public/logo-reveal-sfx.wav), 48 kHz 16-bit stereo.
//
// Every sound is generated in code and timed from src/timeline.ts (and the same magnetic
// attraction curve as the visuals, src/magnet.ts), so the audio stays in sync whenever the
// animation timing changes. Deterministic: same input, same file.
//
// Usage: npm run build:sfx

import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {Timeline} from '../src/timeline.ts';
import {magneticGap} from '../src/magnet.ts';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outPath = path.join(root, 'public', 'logo-reveal-sfx.wav');

const SampleRate = 48000;
const Length = Math.round(Timeline.end * SampleRate);
const TwoPi = Math.PI * 2;

// Dry mix and reverb send, per channel.
const dryL = new Float32Array(Length);
const dryR = new Float32Array(Length);
const sendL = new Float32Array(Length);
const sendR = new Float32Array(Length);

// --- Helpers --------------------------------------------------------------------------------
const mulberry32 = (seed) => () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};
const rand = mulberry32(0x5ca7ab);
const noise = () => rand() * 2 - 1;
const clamp01 = (v) => Math.min(1, Math.max(0, v));
const midi = (n) => 440 * Math.pow(2, (n - 69) / 12);

// Equal-power pan, -1 (left) .. 1 (right).
const write = (i, value, pan = 0, send = 0) => {
    if (i < 0 || i >= Length) return;
    const a = ((clamp01((pan + 1) / 2)) * Math.PI) / 2;
    const l = value * Math.cos(a) * Math.SQRT2;
    const r = value * Math.sin(a) * Math.SQRT2;
    dryL[i] += l;
    dryR[i] += r;
    sendL[i] += l * send;
    sendR[i] += r * send;
};
const sampleAt = (seconds) => Math.round(seconds * SampleRate);

// Chamberlin state-variable filter; returns {low, band, high} per call.
const svf = () => {
    let low = 0;
    let band = 0;
    return (x, cutoff, q = 0.7) => {
        const f = 2 * Math.sin((Math.PI * Math.min(cutoff, SampleRate / 6)) / SampleRate);
        low += f * band;
        const high = x - low - band / q;
        band += f * high;
        return {low, band, high};
    };
};

// Short filtered noise tick (insect footsteps, sand, sparks).
const tick = (t0, {freq = 3000, q = 2, duration = 0.012, amp = 0.15, pan = 0, send = 0.1}) => {
    const filter = svf();
    const n = sampleAt(duration);
    const start = sampleAt(t0);
    for (let i = 0; i < n; i++) {
        const env = Math.exp((-6 * i) / n);
        write(start + i, filter(noise(), freq, q).band * env * amp, pan, send);
    }
};

// Plucked string / mallet: harmonics that decay faster the higher they are.
const pluck = (t0, freq, {amp = 0.1, pan = 0, send = 0.3, duration = 1.4, brightness = 1}) => {
    const start = sampleAt(t0);
    const n = sampleAt(duration);
    for (let i = 0; i < n; i++) {
        const t = i / SampleRate;
        let v = 0;
        for (let k = 1; k <= 8; k++) {
            if (freq * k > 12000) break;
            v += (Math.sin(TwoPi * freq * k * t) / Math.pow(k, 1.4 / brightness)) * Math.exp(-t * (2.2 + k * 2.4));
        }
        const attack = Math.min(1, t / 0.002);
        write(start + i, v * attack * amp, pan, send);
    }
};

// --- 1. Magnetic charge: hum + rising noise + sparks, following the attraction curve --------
{
    const start = sampleAt(Timeline.halvesStart);
    const end = sampleAt(Timeline.snap);
    const span = Timeline.snap - Timeline.halvesStart;
    let phaseL = 0;
    let phaseR = 0;
    let tremPhase = 0;
    const riserL = svf();
    const riserR = svf();
    for (let i = start; i < end; i++) {
        const t = i / SampleRate - Timeline.halvesStart;
        const pull = 1 - magneticGap(t / span);
        const fadeIn = clamp01(t / 0.25);
        const fadeOut = clamp01((end - i) / (0.004 * SampleRate));

        // Hum: buzzy low tone rising in pitch, tremolo speeding up like the visual tremble.
        const f0 = 45 + 80 * pull * pull;
        phaseL += (TwoPi * f0) / SampleRate;
        phaseR += (TwoPi * (f0 + 0.8)) / SampleRate;
        tremPhase += (TwoPi * (6 + 44 * pull * pull)) / SampleRate;
        let humL = 0;
        let humR = 0;
        for (let k = 1; k <= 7; k++) {
            humL += Math.sin(k * phaseL) / Math.pow(k, 1.2);
            humR += Math.sin(k * phaseR) / Math.pow(k, 1.2);
        }
        const trem = 0.65 + 0.35 * Math.sin(tremPhase);
        const humAmp = (0.1 + 0.26 * pull * pull) * trem * fadeIn * fadeOut;

        // Riser: band-passed noise sweeping up as the halves accelerate.
        const cutoff = 250 * Math.pow(5500 / 250, pull);
        const riserAmp = (0.02 + 0.35 * pull * pull * pull) * fadeIn * fadeOut;

        dryL[i] += humL * humAmp * 0.5 + riserL(noise(), cutoff, 1.8).band * riserAmp;
        dryR[i] += humR * humAmp * 0.5 + riserR(noise(), cutoff, 1.8).band * riserAmp;
    }

    // Sparks crackling across the gap, denser as the pull grows.
    for (let t = 0; t < span; ) {
        const pull = 1 - magneticGap(t / span);
        t += 1 / (12 + 260 * pull * pull) * (0.4 + rand() * 1.2);
        if (t >= span) break;
        tick(Timeline.halvesStart + t, {
            freq: 2500 + rand() * 6000,
            q: 1.2,
            duration: 0.004 + rand() * 0.008,
            amp: (0.05 + 0.2 * pull) * (0.5 + rand() * 0.5),
            pan: (rand() * 2 - 1) * 0.7,
            send: 0.15,
        });
    }
}

// --- 2. Snap: low boom, transient click and a metallic clank ---------------------------------
{
    const start = sampleAt(Timeline.snap);
    let phase = 0;
    for (let i = 0; i < sampleAt(1.2); i++) {
        const t = i / SampleRate;
        phase += (TwoPi * (34 + 50 * Math.exp(-t * 16))) / SampleRate;
        const boom = Math.sin(phase) * Math.exp(-t * 4.5) * 0.85;
        const click = noise() * Math.exp(-t * 450) * 0.55;
        let clank = 0;
        for (const [f, a, d] of [
            [392, 0.3, 9],
            [1046, 0.22, 13],
            [1718, 0.16, 17],
            [2637, 0.11, 24],
            [3905, 0.07, 32],
        ]) {
            clank += Math.sin(TwoPi * f * t) * a * Math.exp(-t * d);
        }
        write(start + i, boom + click, 0, 0.15);
        write(start + i, clank * 0.8, 0, 0.3);
    }
}

// --- 3. Controller powers on: a blip as the d-pad and each button punch open ----------------
{
    const blips = [
        {notes: [76, 83], pan: -0.35}, // d-pad (left half)
        {notes: [83, 88], pan: 0.25}, // button A
        {notes: [88, 95], pan: 0.4}, // button B
    ];
    blips.forEach(({notes, pan}, index) => {
        const t0 = Timeline.snap + Timeline.holesDelay + index * Timeline.holeStagger;
        const start = sampleAt(t0);
        let phase = 0;
        const n = sampleAt(0.14);
        for (let i = 0; i < n; i++) {
            const t = i / SampleRate;
            const f = midi(t < 0.035 ? notes[0] : notes[1]);
            phase += (TwoPi * f) / SampleRate;
            // Soft square wave (odd harmonics) for a retro console feel.
            let v = 0;
            for (let k = 1; k <= 9; k += 2) v += Math.sin(k * phase) / k;
            const env = Math.min(1, t / 0.002) * Math.exp(-t * 18);
            write(start + i, v * env * 0.13, pan, 0.35);
        }
    });
}

// --- 4. Legs: one click per leg as it springs out, then a skittering scuttle ----------------
{
    for (let order = 0; order < 3; order++) {
        const t0 = Timeline.legsStart + order * Timeline.legStagger;
        tick(t0, {freq: 3200 - order * 300, duration: 0.014, amp: 0.22, pan: -0.55, send: 0.15});
        tick(t0 + 0.006, {freq: 3400 - order * 300, duration: 0.014, amp: 0.22, pan: 0.55, send: 0.15});
    }
    const steps = Math.floor(Timeline.scuttleDuration * 10);
    for (let s = 0; s < steps; s++) {
        const u = (s + 0.5) / steps;
        const t0 = Timeline.scuttleStart + u * Timeline.scuttleDuration + (rand() - 0.5) * 0.01;
        tick(t0, {
            freq: 2600 + rand() * 1600,
            duration: 0.01,
            amp: 0.13 * Math.sin(u * Math.PI),
            pan: s % 2 === 0 ? -0.5 : 0.5,
            send: 0.1,
        });
    }
}

// --- 5. Lockup glide: a soft whoosh moving left with the beetle ------------------------------
{
    const start = sampleAt(Timeline.lockupStart);
    const n = sampleAt(0.6);
    const filter = svf();
    for (let i = 0; i < n; i++) {
        const u = i / n;
        const env = Math.pow(Math.sin(u * Math.PI), 1.5) * 0.14;
        write(start + i, filter(noise(), 1800 * Math.pow(500 / 1800, u), 1.2).band * env, -0.4 * u, 0.2);
    }
}

// --- 6. Letters: an ascending harp run (D Phrygian dominant) + a grain of sand each ----------
{
    // D Phrygian dominant, from F#4 up to D6: one note per letter.
    const notes = [66, 67, 69, 70, 72, 74, 75, 78, 79, 81, 82, 84, 86];
    notes.forEach((note, i) => {
        const t0 = Timeline.lettersStart + i * Timeline.letterStagger;
        const pan = -0.6 + (1.2 * i) / (notes.length - 1);
        pluck(t0, midi(note), {amp: 0.075, pan, send: 0.45, duration: 1.3});
        tick(t0, {freq: 5500, q: 0.9, duration: 0.06, amp: 0.035, pan, send: 0.05});
    });
}

// --- 7. Warm pad: the "world" settles on a D major chord after the snap ---------------------
{
    const start = sampleAt(Timeline.snap + 0.1);
    const voices = [38, 45, 50, 54, 57, 62];
    const attackEnd = Timeline.lettersStart + 0.6;
    for (let i = start; i < Length; i++) {
        const t = i / SampleRate;
        const env = clamp01((t - (Timeline.snap + 0.1)) / (attackEnd - Timeline.snap - 0.1));
        voices.forEach((note, v) => {
            const f = midi(note);
            let s = 0;
            // Detuned copies add warmth, but on the bass notes they beat audibly, so keep those pure.
            for (const detune of note < 50 ? [0] : [-0.11, 0, 0.13]) {
                const fd = f * Math.pow(2, detune / 12) * (1 + 0.002 * Math.sin(t * 0.7 + v));
                for (let k = 1; k <= 6; k++) {
                    if (fd * k > 2500) break;
                    s += Math.sin(TwoPi * fd * k * t + v + detune * 7) / Math.pow(k, 1.7);
                }
            }
            write(i, s * env * env * (note < 50 ? 0.024 : 0.012), v % 2 === 0 ? -0.35 : 0.35, 0.5);
        });
    }
}

// --- 8. Glint: sparkling bells sweeping left to right with the shine ------------------------
{
    const bellNotes = [86, 90, 93, 98, 91, 95, 100, 102];
    const count = 16;
    for (let b = 0; b < count; b++) {
        const u = (b + rand() * 0.6) / count;
        const eased = u < 0.5 ? 2 * u * u : 1 - Math.pow(-2 * u + 2, 2) / 2;
        const t0 = Timeline.shineStart + u * Timeline.shineDuration;
        const f = midi(bellNotes[Math.floor(rand() * bellNotes.length)]);
        const start = sampleAt(t0);
        const n = sampleAt(0.9);
        const amp = 0.045 * Math.sin(u * Math.PI) + 0.01;
        for (let i = 0; i < n; i++) {
            const t = i / SampleRate;
            const v = (Math.sin(TwoPi * f * t) + 0.35 * Math.sin(TwoPi * f * 2.76 * t) * Math.exp(-t * 9)) * Math.exp(-t * 5.5);
            write(start + i, v * Math.min(1, t / 0.001) * amp, -0.7 + 1.4 * eased, 0.6);
        }
    }
}

// --- Reverb (Freeverb-style: parallel combs into series all-passes) --------------------------
const reverb = (input, spread) => {
    const scale = SampleRate / 44100;
    const combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map((d) => ({
        buf: new Float32Array(Math.round((d + spread) * scale)),
        idx: 0,
        store: 0,
    }));
    const allpasses = [556, 441, 341, 225].map((d) => ({buf: new Float32Array(Math.round((d + spread) * scale)), idx: 0}));
    const feedback = 0.8;
    const damp = 0.5;
    const out = new Float32Array(input.length);
    for (let i = 0; i < input.length; i++) {
        const x = input[i] * 0.015;
        let y = 0;
        for (const c of combs) {
            const o = c.buf[c.idx];
            c.store = o * (1 - damp) + c.store * damp;
            c.buf[c.idx] = x + c.store * feedback;
            c.idx = (c.idx + 1) % c.buf.length;
            y += o;
        }
        for (const a of allpasses) {
            const o = a.buf[a.idx];
            a.buf[a.idx] = y + o * 0.5;
            a.idx = (a.idx + 1) % a.buf.length;
            y = o - y;
        }
        out[i] = y;
    }
    return out;
};
const wetL = reverb(sendL, 0);
const wetR = reverb(sendR, 23);

// --- Master: mix, follow the visual fade-out, soft-clip, normalise to -1 dBFS ---------------
const outL = new Float32Array(Length);
const outR = new Float32Array(Length);
let peak = 0;
for (let i = 0; i < Length; i++) {
    const t = i / SampleRate;
    const u = clamp01((t - Timeline.fadeOutStart) / (Timeline.end - Timeline.fadeOutStart));
    const fade = 1 - u * u;
    outL[i] = Math.tanh((dryL[i] + wetL[i] * 1.5) * 1.2) * fade;
    outR[i] = Math.tanh((dryR[i] + wetR[i] * 1.5) * 1.2) * fade;
    peak = Math.max(peak, Math.abs(outL[i]), Math.abs(outR[i]));
}
const gain = Math.pow(10, -1 / 20) / peak;

const bytesPerSample = 2;
const dataSize = Length * 2 * bytesPerSample;
const wav = Buffer.alloc(44 + dataSize);
wav.write('RIFF', 0);
wav.writeUInt32LE(36 + dataSize, 4);
wav.write('WAVE', 8);
wav.write('fmt ', 12);
wav.writeUInt32LE(16, 16);
wav.writeUInt16LE(1, 20); // PCM
wav.writeUInt16LE(2, 22); // stereo
wav.writeUInt32LE(SampleRate, 24);
wav.writeUInt32LE(SampleRate * 2 * bytesPerSample, 28);
wav.writeUInt16LE(2 * bytesPerSample, 32);
wav.writeUInt16LE(16, 34);
wav.write('data', 36);
wav.writeUInt32LE(dataSize, 40);
for (let i = 0; i < Length; i++) {
    wav.writeInt16LE(Math.round(Math.max(-1, Math.min(1, outL[i] * gain)) * 32767), 44 + i * 4);
    wav.writeInt16LE(Math.round(Math.max(-1, Math.min(1, outR[i] * gain)) * 32767), 46 + i * 4);
}
fs.mkdirSync(path.dirname(outPath), {recursive: true});
fs.writeFileSync(outPath, wav);
console.log(`Wrote ${path.relative(root, outPath)} (${Timeline.end}s, peak normalised from ${peak.toFixed(3)})`);
