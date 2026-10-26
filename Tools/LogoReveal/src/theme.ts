import {Icon, Letters} from './logoPaths';

// Brand colours are sampled from the source logo by scripts/trace-logo.mjs.
export const Gold = Icon.color;
export const Sand = Letters.find((l) => l.char === 'N')!.color;

export const BackgroundInner = '#21180C';
export const BackgroundOuter = '#070604';
export const Highlight = '#FFF4D6';
