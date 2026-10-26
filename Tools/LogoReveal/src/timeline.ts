// All beats of the reveal, in seconds. Converted to frames with the composition's fps so the
// animation keeps its timing if it is rendered at 30, 50 or 60 fps.
export const Timeline = {
    // Two plain scarab shell halves attract like magnets: they hover, then accelerate ever
    // harder until they snap together (see magnet.ts).
    halvesStart: 0.05,
    snap: 0.72,
    // After the snap the d-pad, then each button, punch open: the scarab becomes a controller.
    holesDelay: 0.04,
    holeStagger: 0.07,
    // Legs spring out from the body one pair at a time, then scuttle.
    legsStart: 0.85,
    legStagger: 0.05,
    scuttleStart: 1.15,
    scuttleDuration: 0.75,
    // The beetle moves from the centre of the screen to its place in the lockup.
    lockupStart: 1.27,
    // Letters rise out of the sand, left to right.
    lettersStart: 1.45,
    letterStagger: 0.04,
    // A gold glint sweeps across the finished logo.
    shineStart: 2.37,
    shineDuration: 0.75,
    // Hold, then fade out.
    fadeOutStart: 3.5,
    end: 3.95,
} as const;

export const seconds = (s: number, fps: number) => Math.round(s * fps);
