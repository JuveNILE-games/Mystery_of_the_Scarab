import React from 'react';
import {AbsoluteFill, interpolate, random, useCurrentFrame, useVideoConfig} from 'remotion';
import {BackgroundInner, BackgroundOuter, Gold} from './theme';
import {Timeline} from './timeline';

const DustCount = 48;

// Warm dark vignette, a soft glow that blooms when the beetle snaps together, and slow
// drifting sand dust. Deterministic (remotion's seeded random) so every render is identical.
export const Background: React.FC<{transparent: boolean}> = ({transparent}) => {
    const frame = useCurrentFrame();
    const {fps, width, height} = useVideoConfig();
    const t = frame / fps;

    const sinceSnap = t - Timeline.snap;
    const bloom = sinceSnap >= 0 ? 0.35 + 0.65 * Math.exp(-sinceSnap * 3) : interpolate(t, [0, Timeline.snap], [0, 0.2]);
    const dustIn = interpolate(t, [Timeline.snap, Timeline.snap + 0.6], [0, 1], {
        extrapolateLeft: 'clamp',
        extrapolateRight: 'clamp',
    });

    return (
        <AbsoluteFill>
            {!transparent && (
                <AbsoluteFill
                    style={{background: `radial-gradient(ellipse at 50% 50%, ${BackgroundInner} 0%, ${BackgroundOuter} 70%)`}}
                />
            )}
            <AbsoluteFill
                style={{
                    background: `radial-gradient(ellipse 40% 30% at 50% 50%, ${Gold}33 0%, transparent 100%)`,
                    opacity: bloom,
                }}
            />
            <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} style={{position: 'absolute'}}>
                {Array.from({length: DustCount}, (_, i) => {
                    const x0 = random(`dust-x-${i}`) * width;
                    const y0 = random(`dust-y-${i}`) * height;
                    const speed = 12 + random(`dust-speed-${i}`) * 30;
                    const size = 1 + random(`dust-size-${i}`) * 2.6;
                    const sway = Math.sin(t * (0.6 + random(`dust-sway-${i}`)) + i) * 14;
                    const twinkle = 0.45 + 0.55 * Math.sin(t * (2 + random(`dust-tw-${i}`) * 3) + i * 1.7);
                    const y = (((y0 - t * speed) % height) + height) % height;
                    return (
                        <circle
                            key={i}
                            cx={x0 + sway}
                            cy={y}
                            r={size * (height / 1080)}
                            fill={Gold}
                            opacity={dustIn * twinkle * 0.35}
                        />
                    );
                })}
            </svg>
        </AbsoluteFill>
    );
};
