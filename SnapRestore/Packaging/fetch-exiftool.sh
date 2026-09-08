#!/usr/bin/env bash
set -euo pipefail

VERSION="13.59"
SHA256="668ea3acececb7235fbd0f4900e72d5f12c9b07e5c778fd36cb1e9b5828fd65a"
URL="https://sourceforge.net/projects/exiftool/files/Image-ExifTool-${VERSION}.tar.gz/download"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RUNTIME="${1:-osx-arm64}"

case "$RUNTIME" in
  osx-arm64|osx-x64) ;;
  *)
    echo "Unsupported ExifTool runtime: $RUNTIME" >&2
    exit 1
    ;;
esac

TARGET_DIR="$PROJECT_DIR/Tools/$RUNTIME"
TEMP_DIR="$(mktemp -d)"
ARCHIVE="$TEMP_DIR/Image-ExifTool-${VERSION}.tar.gz"
trap 'rm -rf "$TEMP_DIR"' EXIT

curl --fail --location --retry 3 "$URL" --output "$ARCHIVE"
printf '%s  %s\n' "$SHA256" "$ARCHIVE" | shasum -a 256 --check --status
tar -xzf "$ARCHIVE" -C "$TEMP_DIR"

SOURCE_DIR="$TEMP_DIR/Image-ExifTool-${VERSION}"
rm -rf "$TARGET_DIR/exiftool" "$TARGET_DIR/lib" "$TARGET_DIR/ExifTool-README.txt"
mkdir -p "$TARGET_DIR"
cp "$SOURCE_DIR/exiftool" "$TARGET_DIR/exiftool"
cp -R "$SOURCE_DIR/lib" "$TARGET_DIR/lib"
cp "$SOURCE_DIR/README" "$TARGET_DIR/ExifTool-README.txt"
chmod +x "$TARGET_DIR/exiftool"

"$TARGET_DIR/exiftool" -ver
