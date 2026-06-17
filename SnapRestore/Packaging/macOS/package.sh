#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPO_DIR="$(cd "$PROJECT_DIR/.." && pwd)"

APP_NAME="SnapRestore"
EXECUTABLE_NAME="SnapRestore"
RUNTIME="${1:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
VERSION="${VERSION:-1.0.0}"

PUBLISH_DIR="$PROJECT_DIR/bin/$CONFIGURATION/net10.0/$RUNTIME/publish"
ARTIFACTS_DIR="$REPO_DIR/artifacts/macos/$RUNTIME"
APP_BUNDLE="$ARTIFACTS_DIR/$APP_NAME.app"
CONTENTS_DIR="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
DMG_PATH="$ARTIFACTS_DIR/$APP_NAME-$VERSION-$RUNTIME.dmg"
DMG_STAGING_DIR="$ARTIFACTS_DIR/dmg"

dotnet publish "$PROJECT_DIR/SnapRestore.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

cp -R "$PUBLISH_DIR"/. "$MACOS_DIR"
cp "$SCRIPT_DIR/Info.plist" "$CONTENTS_DIR/Info.plist"
cp "$PROJECT_DIR/Assets/Icons/snaprestore.icns" "$RESOURCES_DIR/snaprestore.icns"

if [ -d "$MACOS_DIR/Tools" ]; then
  find "$MACOS_DIR/Tools" -mindepth 1 -maxdepth 1 -type d ! -name "$RUNTIME" -exec rm -rf {} +
fi

chmod +x "$MACOS_DIR/$EXECUTABLE_NAME"
find "$MACOS_DIR/Tools" -type f \( -name "ffmpeg" -o -name "exiftool" \) -exec chmod +x {} \;

/usr/bin/codesign --force --deep --sign - "$APP_BUNDLE"

rm -rf "$DMG_STAGING_DIR"
mkdir -p "$DMG_STAGING_DIR"
cp -R "$APP_BUNDLE" "$DMG_STAGING_DIR/"
ln -s /Applications "$DMG_STAGING_DIR/Applications"

rm -f "$DMG_PATH"
hdiutil create \
  -volname "$APP_NAME" \
  -srcfolder "$DMG_STAGING_DIR" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "Created app: $APP_BUNDLE"
echo "Created installer: $DMG_PATH"
