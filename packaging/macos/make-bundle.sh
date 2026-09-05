#!/usr/bin/env bash
# Assembles a macOS application bundle around the published binary.
#
#   make-bundle.sh <publish dir> <version> <output .app>
#
# The bundle is ad-hoc signed here. Signing with a Developer ID and notarizing are the caller's job
# and only happen when the credentials exist, so a fork without one still produces a bundle that can
# be opened.
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

cp "$(dirname "$0")/../icon/AppIcon.icns" "$bundle/Contents/Resources/AppIcon.icns"

cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Camera Control</string>
  <key>CFBundleDisplayName</key><string>Camera Control</string>
  <key>CFBundleIdentifier</key><string>$identifier</string>
  <key>CFBundleExecutable</key><string>$executable</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
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

# An arm64 Mach-O with no signature at all is refused by the kernel, and Gatekeeper calls a
# quarantined bundle without one damaged rather than unidentified, which reads as a broken download
# and offers the reader nothing but "eject". An ad-hoc signature is not a Developer ID and macOS
# still asks before opening it, but the question then has an "Open Anyway" behind it. A real identity
# replaces this signature when one is configured.
#
# Nested code is signed first: a bundle signature covers what is inside it, so anything signed
# afterwards invalidates it.
find "$bundle/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) \
    -exec codesign --force --sign - {} \;
codesign --force --sign - "$bundle"
codesign --verify --strict --verbose=2 "$bundle"

echo "built $bundle for $version"
