Bundled FFmpeg build

Version: n9.0.2-23-g27b46f0fbc (2026-10-08)
Variant: Windows x64, LGPL shared
Source archive:
https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-08-13-05/ffmpeg-n9.0.2-23-g27b46f0fbc-win64-lgpl-shared-9.0.zip

FFmpeg source commit:
https://github.com/FFmpeg/FFmpeg/tree/27b46f0fbc
Build scripts:
https://github.com/BtbN/FFmpeg-Builds

The bundled tools are unmodified. The DLLs in bin are required by ffmpeg.exe
and ffprobe.exe. The upstream distribution identifies this as an LGPL build;
its license text is included as LICENSE.txt.

When updating FFmpeg, replace the files in bin and LICENSE.txt together, and
update this file with the new version, source archive, and source commit.
