#!/usr/bin/env bash
# Assembles a macOS application bundle around the published binary.
#
#   make-bundle.sh <publish dir> <version> <output .app>
#
# Signing and notarization are the caller's job and only happen when the credentials exist, so a
# fork without an Apple Developer ID still produces a working, if unsigned, bundle.
set -euo pipefail

source_dir=${1:?publish directory is required}
version=${2:?version is required}
bundle=${3:?output bundle path is required}

executable=CgfCameraControl
identifier=io.github.sensslen.CgfCameraControl

rm -rf "$bundle"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"

# Symbols are larger than the application itself and of no use to an operator.
find "$source_dir" -maxdepth 1 -type f ! -name '*.pdb' ! -name '*.dSYM' \
    -exec cp {} "$bundle/Contents/MacOS/" \;
chmod +x "$bundle/Contents/MacOS/$executable"

cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Camera Control</string>
  <key>CFBundleDisplayName</key><string>Camera Control</string>
  <key>CFBundleIdentifier</key><string>$identifier</string>
  <key>CFBundleExecutable</key><string>$executable</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <!-- Nothing about this application belongs in a light or dark variant only. -->
  <key>NSRequiresAquaSystemAppearance</key><false/>
</dict>
</plist>
PLIST

echo "built $bundle for $version"
