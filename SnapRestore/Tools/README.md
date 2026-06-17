Bundled external tools
======================

Release binaries are bundled in runtime-specific folders:

- `Tools/osx-arm64/ffmpeg`
- `Tools/osx-arm64/exiftool`
- `Tools/osx-x64/ffmpeg`
- `Tools/osx-x64/exiftool`
- `Tools/win-x64/ffmpeg.exe`
- `Tools/win-x64/exiftool.exe`

Anything under `Tools/` is copied to build and publish output.

At runtime the app checks the matching folder first, then falls back to `ffmpeg` and `exiftool` on PATH.

Current bundled versions:

- FFmpeg: 6.0 static binaries from `ffmpeg-static`
- ExifTool: 13.59 from the official ExifTool distribution

The Windows ExifTool package requires the sibling `exiftool_files` folder to remain next to `exiftool.exe`.
