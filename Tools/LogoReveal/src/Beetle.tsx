import React from 'react';
import {Easing, interpolate, spring, useCurrentFrame, useVideoConfig} from 'remotion';
import {Icon, IconHoles, IconOutline} from './logoPaths';
import {MagneticField} from './MagneticField';
import {magneticGap} from './magnet';
import {Gold, Highlight} from './theme';
import {seconds, Timeline} from './timeline';

// The traced beetle is a single connected shape (the legs touch the body), so it is split into
// animatable layers with clip paths and masks instead of separate paths. All coordinates are in
// source-image pixels; the icon is mirror-symmetric around x = 60.5.
const IconPath = Icon.d;
const IconWidth = 121;
const MidX = IconWidth / 2;
export const BeetleCenter = {x: MidX, y: 60};

// Once the legs grow, the body clip sits just outside the body, so the leg layers contain only
// leg (anything else would be dragged along when a leg springs or scuttles) and the body carries
// short leg stubs that the legs grow out of. The leg cutout is 1px smaller so the two overlap
// without an anti-aliasing seam. Before that the clip hugs the body (ShellClip) so the plain
// shell halves have no stubs.
const BodyClip = {rx: 40, ry: 53};
const ShellClip = {rx: 37, ry: 50.5};
const LegCutout = {rx: 39, ry: 52};
const LightClip = {rx: 32, ry: 45};
const HeadRect = {x0: 39, x1: 82, y0: -2, y1: 24};
const HalfSlide = 46;
// The halves start tilted open like a beetle's wing cases and pull straight as they align.
const HalfHinge = {x: MidX, y: 6};
const HalfSwing = 22;

type Rect = {x0: number; x1: number; y0: number; y1: number};
type Leg = {id: string; regions: Rect[]; root: [number, number]; order: number; phase: number};

// Left-hand legs; the right-hand ones are mirrored.
const LeftLegs: Leg[] = [
    {
        id: 'upper',
        // Stops short of the shoulders above y = 40 so they never swing with the leg.
        regions: [
            {x0: -4, x1: 30, y0: -4, y1: 40},
            {x0: -4, x1: MidX, y0: 40, y1: 59},
        ],
        root: [25, 52],
        order: 0,
        phase: 0,
    },
    {
        id: 'lower',
        regions: [
            {x0: -4, x1: MidX, y0: 59, y1: 80},
            {x0: -4, x1: 15, y0: 80, y1: 124},
        ],
        root: [25, 67],
        order: 1,
        phase: Math.PI,
    },
    {id: 'bottom', regions: [{x0: 15, x1: MidX, y0: 80, y1: 124}], root: [28, 92], order: 2, phase: 0},
];

const mirrorRect = (r: Rect): Rect => ({x0: IconWidth - r.x1, x1: IconWidth - r.x0, y0: r.y0, y1: r.y1});
const RightLegs: Leg[] = LeftLegs.map((leg) => ({
    id: leg.id,
    regions: leg.regions.map(mirrorRect),
    root: [IconWidth - leg.root[0], leg.root[1]],
    order: leg.order,
    // Tripod gait: each leg moves opposite to its mirror twin.
    phase: leg.phase + Math.PI,
}));

const RectEl: React.FC<{r: Rect; fill?: string}> = ({r, fill}) => (
    <rect x={r.x0} y={r.y0} width={r.x1 - r.x0} height={r.y1 - r.y0} fill={fill} />
);

type BeetleProps = {
    // Unique per instance: the beetle is also drawn inside the shine mask.
    idPrefix: string;
    fill?: string;
    // Mask copies skip the purely decorative layers (flash, pulse ring).
    shapeOnly?: boolean;
};

export const Beetle: React.FC<BeetleProps> = ({idPrefix, fill = Gold, shapeOnly = false}) => {
    const frame = useCurrentFrame();
    const {fps} = useVideoConfig();
    const t = frame / fps;
    const id = (name: string) => `${idPrefix}-${name}`;

    // --- Halves: magnetic attraction. They hover, then accelerate ever harder into the snap ---
    const sinceHalves = t - Timeline.halvesStart;
    const gap = magneticGap(sinceHalves / (Timeline.snap - Timeline.halvesStart));
    const pull = 1 - gap;
    const halfOffset = gap * HalfSlide;
    const halfSwing = gap * HalfSwing;
    // Hovering float that fades out as the pull takes over, and a tremble that builds with it.
    const float = Math.sin(sinceHalves * 7) * 1.6 * gap;
    const tremble = gap > 0 ? Math.sin(sinceHalves * 95) * pull * pull * 1.2 : 0;
    const halfTransform = (side: 1 | -1) =>
        `translate(${side * (halfOffset + tremble)} ${float * side}) rotate(${-side * halfSwing} ${HalfHinge.x} ${HalfHinge.y})`;
    // The same transform applied to a point, so the field lines can attach to the halves.
    const placeOnHalf = (side: 1 | -1) => (x: number, y: number): [number, number] => {
        const a = (-side * halfSwing * Math.PI) / 180;
        const dx = x - HalfHinge.x;
        const dy = y - HalfHinge.y;
        return [
            HalfHinge.x + dx * Math.cos(a) - dy * Math.sin(a) + side * (halfOffset + tremble),
            HalfHinge.y + dx * Math.sin(a) + dy * Math.cos(a) + float * side,
        ];
    };
    const halfOpacity = interpolate(t, [Timeline.halvesStart, Timeline.halvesStart + 0.25], [0, 1], {
        extrapolateLeft: 'clamp',
        extrapolateRight: 'clamp',
    });

    // --- Snap impact: a quick squash-and-stretch punch ---
    const sinceSnap = t - Timeline.snap;
    const punch = sinceSnap >= 0 ? Math.sin(sinceSnap * 28) * Math.exp(-sinceSnap * 9) : 0;
    const scaleX = 1 + punch * 0.09;
    const scaleY = 1 - punch * 0.06;

    // --- Legs ---
    const legTransform = (leg: Leg) => {
        const start = Timeline.legsStart + leg.order * Timeline.legStagger;
        const grow = spring({
            frame: frame - seconds(start, fps),
            fps,
            config: {damping: 10, stiffness: 240, mass: 0.6},
        });
        const sinceScuttle = t - Timeline.scuttleStart;
        const scuttle =
            sinceScuttle >= 0 && sinceScuttle <= Timeline.scuttleDuration
                ? Math.sin(sinceScuttle * Math.PI * 2 * 5 + leg.phase) *
                  Math.sin((sinceScuttle / Timeline.scuttleDuration) * Math.PI) *
                  6
                : 0;
        const [rx, ry] = leg.root;
        return `translate(${rx} ${ry}) rotate(${scuttle}) scale(${Math.max(0, grow)}) translate(${-rx} ${-ry})`;
    };

    const renderLegs = (side: 'l' | 'r', legs: Leg[]) =>
        legs.map((leg) => (
            <g key={`${side}-${leg.id}`} transform={legTransform(leg)}>
                <g mask={`url(#${id(`leg-${side}-${leg.id}`)})`}>
                    <path d={IconPath} fill={fill} fillRule="evenodd" />
                </g>
            </g>
        ));

    // --- Controller details: the halves arrive as plain shells; once they lock together the
    // d-pad and then each button punch open, turning the scarab into a controller. ---
    const holeScale = (index: number) =>
        spring({
            frame: frame - seconds(Timeline.snap + Timeline.holesDelay + index * Timeline.holeStagger, fps),
            fps,
            config: {damping: 11, stiffness: 260, mass: 0.5},
        });

    // Grow the body clip out to the leg stubs as the first legs appear.
    const stubs = Math.min(
        1,
        3 * spring({frame: frame - seconds(Timeline.legsStart, fps), fps, config: {damping: 10, stiffness: 240, mass: 0.6}}),
    );
    const bodyRx = interpolate(stubs, [0, 1], [ShellClip.rx, BodyClip.rx]);
    const bodyRy = interpolate(stubs, [0, 1], [ShellClip.ry, BodyClip.ry]);

    // --- Magnetic glow around the halves: builds with the pull, flares on contact, settles ---
    const glow = sinceSnap < 0 ? pull * pull : Math.exp(-sinceSnap * 4);

    // --- Light flashing through the seam and the freshly opened holes ---
    const flash = sinceSnap >= 0 ? Math.exp(-sinceSnap * 3.5) : 0;
    const ring = interpolate(sinceSnap, [0, 0.55], [0, 1], {
        easing: Easing.out(Easing.cubic),
        extrapolateLeft: 'clamp',
        extrapolateRight: 'clamp',
    });

    return (
        <g
            transform={`translate(${BeetleCenter.x} ${BeetleCenter.y}) scale(${scaleX} ${scaleY}) translate(${-BeetleCenter.x} ${-BeetleCenter.y})`}
        >
            <defs>
                <clipPath id={id('body')}>
                    <ellipse cx={BeetleCenter.x} cy={BeetleCenter.y} rx={bodyRx} ry={bodyRy} />
                    <RectEl r={HeadRect} />
                </clipPath>
                {/* 0.5px overlap at the centre so the closed head dome shows no seam. */}
                <clipPath id={id('half-l')}>
                    <RectEl r={{x0: -10, x1: MidX + 0.5, y0: -10, y1: 130}} />
                </clipPath>
                <clipPath id={id('half-r')}>
                    <RectEl r={{x0: MidX - 0.5, x1: IconWidth + 10, y0: -10, y1: 130}} />
                </clipPath>
                <clipPath id={id('light')}>
                    <ellipse cx={BeetleCenter.x} cy={BeetleCenter.y} rx={LightClip.rx} ry={LightClip.ry} />
                    <RectEl r={{x0: 46, x1: 75, y0: 4, y1: 22}} />
                </clipPath>
                <filter id={id('glow')} x="-60%" y="-60%" width="220%" height="220%">
                    <feGaussianBlur in="SourceAlpha" stdDeviation={2 + glow * 5} result="blur" />
                    <feFlood floodColor={Highlight} floodOpacity={glow * 0.9} />
                    <feComposite in2="blur" operator="in" result="halo" />
                    <feMerge>
                        <feMergeNode in="halo" />
                        <feMergeNode in="SourceGraphic" />
                    </feMerge>
                </filter>
                <mask id={id('holes')} maskUnits="userSpaceOnUse" x={-20} y={-20} width={IconWidth + 40} height={160}>
                    <rect x={-20} y={-20} width={IconWidth + 40} height={160} fill="white" />
                    {IconHoles.map((hole, i) => (
                        <path
                            key={i}
                            d={hole.d}
                            fill="black"
                            transform={`translate(${hole.cx} ${hole.cy}) scale(${Math.max(0, holeScale(i))}) translate(${-hole.cx} ${-hole.cy})`}
                        />
                    ))}
                </mask>
                {(
                    [
                        ['l', LeftLegs],
                        ['r', RightLegs],
                    ] as const
                ).flatMap(([side, legs]) =>
                    legs.map((leg) => (
                        <mask
                            key={`${side}-${leg.id}`}
                            id={id(`leg-${side}-${leg.id}`)}
                            maskUnits="userSpaceOnUse"
                            x={-20}
                            y={-20}
                            width={IconWidth + 40}
                            height={160}
                        >
                            {leg.regions.map((r, i) => (
                                <RectEl key={i} r={r} fill="white" />
                            ))}
                            <ellipse
                                cx={BeetleCenter.x}
                                cy={BeetleCenter.y}
                                rx={LegCutout.rx}
                                ry={LegCutout.ry}
                                fill="black"
                            />
                            <RectEl r={HeadRect} fill="black" />
                        </mask>
                    )),
                )}
            </defs>

            {!shapeOnly && sinceHalves > 0 && (
                <MagneticField
                    time={sinceHalves}
                    placeLeft={placeOnHalf(-1)}
                    placeRight={placeOnHalf(1)}
                    offset={halfOffset}
                    pull={pull}
                    centerX={BeetleCenter.x}
                    centerY={BeetleCenter.y}
                />
            )}

            {!shapeOnly && ring > 0 && ring < 1 && (
                <ellipse
                    cx={BeetleCenter.x}
                    cy={BeetleCenter.y}
                    rx={40 + ring * 90}
                    ry={50 + ring * 90}
                    fill="none"
                    stroke={Gold}
                    strokeWidth={6 * (1 - ring) + 0.5}
                    opacity={(1 - ring) * 0.8}
                />
            )}

            {!shapeOnly && flash > 0.01 && (
                <g clipPath={`url(#${id('light')})`} opacity={flash}>
                    <rect x={0} y={0} width={IconWidth} height={120} fill={Highlight} />
                </g>
            )}

            {renderLegs('l', LeftLegs)}
            {renderLegs('r', RightLegs)}

            <g opacity={halfOpacity} filter={!shapeOnly && glow > 0.01 ? `url(#${id('glow')})` : undefined}>
                <g transform={halfTransform(-1)} clipPath={`url(#${id('half-l')})`}>
                    <g clipPath={`url(#${id('body')})`} mask={`url(#${id('holes')})`}>
                        <path d={IconOutline} fill={fill} />
                    </g>
                </g>
                <g transform={halfTransform(1)} clipPath={`url(#${id('half-r')})`}>
                    <g clipPath={`url(#${id('body')})`} mask={`url(#${id('holes')})`}>
                        <path d={IconOutline} fill={fill} />
                    </g>
                </g>
            </g>
        </g>
    );
};
