Bundled external tools
======================

FFmpeg release binaries are stored in runtime-specific folders:

- `Tools/osx-arm64/ffmpeg`
- `Tools/osx-x64/ffmpeg`
- `Tools/win-x64/ffmpeg.exe`

ExifTool is deliberately not committed. Packaging scripts download the official
pinned 13.59 archive, verify its SHA-256 checksum, and stage the executable and
required support files in the matching `Tools/<runtime-id>/` folder. Anything
under `Tools/` is then copied to build and publish output.

At runtime the app checks the matching folder first, then falls back to `ffmpeg` and `exiftool` on PATH.

Current bundled versions:

- FFmpeg: 9.0.1
- ExifTool: 13.59, fetched from the official SourceForge downloads published at
  https://exiftool.org/

The macOS FFmpeg executables are built from the official 9.0.1 source release:
https://ffmpeg.org/releases/ffmpeg-9.0.1.tar.xz

They are LGPL 2.1-or-later builds with FFmpeg's native MPEG-4 software encoder
and Apple's VideoToolbox support. Their
configure flags are recorded by `ffmpeg -version`; no GPL or nonfree components
are enabled. The x64 build additionally disables x86 assembly because NASM is
not part of the standard macOS command-line tools.

The Windows executable is the 9.0.1 essentials build from Gyan.dev:
https://www.gyan.dev/ffmpeg/builds/

It is a GPLv3 build and includes `libx264`. The upstream licence and build README
are stored beside the executable. SnapRestore itself is distributed separately
from FFmpeg and invokes it as an external process.

The Windows ExifTool package requires the sibling `exiftool_files` folder to
remain next to `exiftool.exe`. The macOS Perl distribution requires its sibling
`lib` folder. Both are present in finished releases but ignored by Git.

Fetch ExifTool for local packaging with:

```sh
SnapRestore/Packaging/fetch-exiftool.sh osx-arm64
SnapRestore/Packaging/fetch-exiftool.sh osx-x64
```

On Windows, run `SnapRestore/Packaging/fetch-exiftool.ps1` in PowerShell.

Verification
------------

Run `shasum -a 256 -c Tools/checksums.sha256` from the `SnapRestore` project
directory after replacing FFmpeg. ExifTool archive checksums are pinned in the
fetch scripts and verified before extraction.

Redistribution notice
---------------------

ExifTool is distributed under the same terms as Perl. Preserve its upstream
documentation and licence when packaging SnapRestore:
https://exiftool.org/

FFmpeg licence files are stored beside each bundled executable. The release
workflow rejects any replacement that reports `--enable-nonfree` and verifies
that each platform's required video encoder is present. When replacing FFmpeg,
update these notices and checksums and review the new configuration. See
https://ffmpeg.org/legal.html.
