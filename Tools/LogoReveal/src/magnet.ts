// Two bodies released at rest under an inverse-square attraction (like two magnets) close the gap
// along a cycloid: they hover almost still at first, then accelerate harder and harder until they
// slam together. Parametric solution of the radial free-fall:
//     gap(η) = (1 + cos η) / 2,   time(η) = (η + sin η) / π,   η ∈ [0, π]
// Given normalised time s ∈ [0, 1], solve η + sin η = π·s with Newton's method.
export const magneticGap = (s: number): number => {
    if (s <= 0) return 1;
    if (s >= 1) return 0;
    const target = Math.PI * s;
    let eta = target / 2;
    for (let i = 0; i < 12; i++) {
        const f = eta + Math.sin(eta) - target;
        const df = 1 + Math.cos(eta);
        if (df < 1e-6) break;
        eta = Math.min(Math.PI, Math.max(0, eta - f / df));
    }
    return (1 + Math.cos(eta)) / 2;
};
