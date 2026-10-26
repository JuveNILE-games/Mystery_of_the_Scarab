// See https://www.remotion.dev/docs/config
import {Config} from '@remotion/cli/config';

Config.setVideoImageFormat('jpeg');
Config.setJpegQuality(95);
Config.setOverwriteOutput(true);
// High quality H.264 — the splash is short, so file size is not a concern.
Config.setCrf(14);
Config.setPixelFormat('yuv420p');

// VP8 (the transparent WebM) only honours -crf under a bitrate ceiling. Remotion passes no
// -b:v, so libvpx falls back to its tiny default target and the output turns blocky (~2 Mbit/s
// at 1080p60). Give it a generous ceiling and cap the quantizer instead: measured against the
// lossless frames this lifts the worst frame from 36.5 dB to 49 dB PSNR at ~3.6 MB.
const Vp8Quality = ['-crf', '4', '-b:v', '60M', '-qmin', '0', '-qmax', '10', '-deadline', 'good', '-cpu-used', '0'];

Config.overrideFfmpegCommand(({args}) => {
    // Only the pass that actually encodes with libvpx (the final mux uses -c:v copy).
    if (!args.includes('libvpx')) return args;
    const out = [...args];
    const crf = out.indexOf('-crf');
    if (crf !== -1) out.splice(crf, 2);
    const overwrite = out.lastIndexOf('-y');
    out.splice(overwrite === -1 ? out.length - 1 : overwrite, 0, ...Vp8Quality);
    return out;
});
