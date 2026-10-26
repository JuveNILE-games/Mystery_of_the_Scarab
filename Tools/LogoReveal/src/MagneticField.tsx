import React from 'react';
import {random} from 'remotion';
import {Gold, Highlight} from './theme';

type MagneticFieldProps = {
    // Seconds since the halves appeared (drives the flow of the field lines).
    time: number;
    // Maps a point on the left half's inner edge to where it currently is on screen (the right
    // half is its mirror image).
    placeLeft: (x: number, y: number) => [number, number];
    placeRight: (x: number, y: number) => [number, number];
    // How far each half still is from its resting place, in icon pixels.
    offset: number;
    // 0 = halves far apart, 1 = touching.
    pull: number;
    centerX: number;
    centerY: number;
};

// The gap between the two half-shells as seen at the seam (icon pixels).
const SeamLeft = 57;
const SeamRight = 64;
const LineYs = [26, 40, 54, 68, 82, 96];
const MoteCount = 14;

// Glowing field lines arcing across the gap and energy motes being sucked into the seam. Both
// grow stronger as the halves close in, so the attraction reads as a force, not just a tween.
export const MagneticField: React.FC<MagneticFieldProps> = ({time, placeLeft, placeRight, offset, pull, centerX, centerY}) => {
    if (offset < 0.5) return null;
    const strength = Math.min(1, 0.15 + pull * pull * 1.1);

    return (
        <g>
            {LineYs.map((y, i) => {
                // Each line runs between matching points on the two inner edges; outer lines bow
                // away from the centre, like field lines between two poles.
                const [xl, yl] = placeLeft(SeamLeft, y);
                const [xr, yr] = placeRight(SeamRight, y);
                const span = xr - xl;
                const bow = ((y - centerY) / 40) * span * 0.35;
                const flicker = 0.75 + 0.25 * Math.sin(time * 31 + i * 2.1);
                return (
                    <path
                        key={i}
                        d={`M ${xl} ${yl} C ${xl + span * 0.3} ${yl + bow}, ${xr - span * 0.3} ${yr + bow}, ${xr} ${yr}`}
                        fill="none"
                        stroke={i % 2 === 0 ? Gold : Highlight}
                        strokeWidth={0.8 + strength * 0.9}
                        strokeLinecap="round"
                        strokeDasharray="2 5"
                        // Alternate lines flow in opposite directions.
                        strokeDashoffset={(i % 2 === 0 ? -1 : 1) * time * 40}
                        opacity={strength * flicker * 0.85}
                    />
                );
            })}
            {Array.from({length: MoteCount}, (_, i) => {
                // Each mote spirals in from a ring around the beetle on a staggered loop.
                const period = 0.55 + random(`mote-period-${i}`) * 0.35;
                const phase = ((time + random(`mote-phase-${i}`) * period) % period) / period;
                const angle = random(`mote-angle-${i}`) * Math.PI * 2 + phase * 1.6;
                const radius = (1 - phase) * (55 + random(`mote-radius-${i}`) * 45);
                const x = centerX + Math.cos(angle) * radius;
                const y = centerY + Math.sin(angle) * radius * 1.15;
                return (
                    <circle
                        key={i}
                        cx={x}
                        cy={y}
                        r={0.7 + phase * 1.3}
                        fill={Highlight}
                        opacity={strength * Math.sin(phase * Math.PI) * 0.9}
                    />
                );
            })}
        </g>
    );
};
