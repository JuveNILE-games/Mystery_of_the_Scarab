import React from 'react';
import {interpolate, spring, useCurrentFrame, useVideoConfig} from 'remotion';
import {Letters} from './logoPaths';
import {seconds, Timeline} from './timeline';

// Letters rise out of an invisible "sand line" just below the baseline.
const SandLine = Math.max(...Letters.map((l) => l.y + l.height)) + 1;
const RiseDistance = 100;

type WordmarkProps = {
    idPrefix: string;
    // Overrides every letter's brand colour (used for the shine mask).
    fill?: string;
};

export const Wordmark: React.FC<WordmarkProps> = ({idPrefix, fill}) => {
    const frame = useCurrentFrame();
    const {fps} = useVideoConfig();
    const clipId = `${idPrefix}-sand`;

    return (
        <g>
            <defs>
                <clipPath id={clipId}>
                    <rect x={0} y={-200} width={2000} height={200 + SandLine} />
                </clipPath>
            </defs>
            <g clipPath={`url(#${clipId})`}>
                {Letters.map((letter, i) => {
                    const start = seconds(Timeline.lettersStart + i * Timeline.letterStagger, fps);
                    const rise = spring({
                        frame: frame - start,
                        fps,
                        config: {damping: 13, stiffness: 170, mass: 0.7},
                    });
                    const opacity = interpolate(rise, [0, 0.5], [0, 1], {
                        extrapolateLeft: 'clamp',
                        extrapolateRight: 'clamp',
                    });
                    const cx = letter.x + letter.width / 2;
                    const by = letter.y + letter.height;
                    const squash = 1 + (1 - rise) * 0.12;
                    return (
                        <g
                            key={i}
                            opacity={opacity}
                            transform={`translate(0 ${(1 - rise) * RiseDistance}) translate(${cx} ${by}) scale(${1 / squash} ${squash}) translate(${-cx} ${-by})`}
                        >
                            <path d={letter.d} fill={fill ?? letter.color} />
                        </g>
                    );
                })}
            </g>
        </g>
    );
};
