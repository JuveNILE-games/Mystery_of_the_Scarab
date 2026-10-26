// See https://www.remotion.dev/docs/config
import {Config} from '@remotion/cli/config';

Config.setVideoImageFormat('jpeg');
Config.setJpegQuality(95);
Config.setOverwriteOutput(true);
// High quality H.264 — the splash is short, so file size is not a concern.
Config.setCrf(14);
Config.setPixelFormat('yuv420p');
