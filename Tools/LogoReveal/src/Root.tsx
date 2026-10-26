import React from 'react';
import {Composition} from 'remotion';
import {LogoReveal} from './LogoReveal';
import {Timeline} from './timeline';

const Fps = 60;
const Duration = Math.round(Timeline.end * Fps);

export const RemotionRoot: React.FC = () => (
    <>
        {/* Full-screen splash on the warm dark background. */}
        <Composition
            id="LogoReveal"
            component={LogoReveal}
            durationInFrames={Duration}
            fps={Fps}
            width={1920}
            height={1080}
            defaultProps={{transparent: false}}
        />
        {/* Same animation without background, for compositing over a custom splash in Unity. */}
        <Composition
            id="LogoRevealTransparent"
            component={LogoReveal}
            durationInFrames={Duration}
            fps={Fps}
            width={1920}
            height={1080}
            defaultProps={{transparent: true}}
        />
    </>
);
