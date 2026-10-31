#!/usr/bin/env python3
"""Validate icon assets, application assignments and Windows executable resources."""

import argparse
import configparser
import ctypes
import os
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
import zlib


ROOT = Path(__file__).resolve().parents[2]
ICON_SIZES = {16, 24, 32, 48, 64, 128, 256}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def png_size(data):
    require(data[:8] == b"\x89PNG\r\n\x1a\n", "Invalid PNG signature")
    offset = 8
    dimensions = None
    while offset < len(data):
        length, kind = struct.unpack_from(">I4s", data, offset)
        end = offset + 12 + length
        require(end <= len(data), "Truncated PNG chunk")
        checksum = struct.unpack_from(">I", data, end - 4)[0]
        require(zlib.crc32(data[offset + 4:end - 4]) == checksum, "Invalid PNG checksum")
        if kind == b"IHDR":
            dimensions = struct.unpack_from(">II", data, offset + 8)
        offset = end
        if kind == b"IEND":
            require(offset == len(data), "Trailing PNG data")
            return dimensions
    raise ValueError("PNG has no end chunk")


def ico_frames(path):
    data = path.read_bytes()
    require(struct.unpack_from("<HH", data) == (0, 1), f"Invalid ICO header: {path}")
    count = struct.unpack_from("<H", data, 4)[0]
    frames = {}
    for index in range(count):
        width, height, _, reserved, planes, bits, length, offset = struct.unpack_from(
            "<BBBBHHII", data, 6 + index * 16)
        dimensions = (width or 256, height or 256)
        require(reserved == 0 and planes in (0, 1) and bits == 32, f"Invalid ICO frame: {path}")
        require(offset >= 6 + count * 16 and offset + length <= len(data), f"Invalid ICO range: {path}")
        frame = data[offset:offset + length]
        require(png_size(frame) == dimensions, f"ICO dimensions disagree: {path}")
        require(dimensions not in frames, f"Duplicate ICO size: {path}")
        frames[dimensions] = frame
    require(set(frames) == {(size, size) for size in ICON_SIZES}, f"Missing ICO sizes: {path}")
    return frames


def verify_assets():
    icons = ROOT / "packaging" / "icon"
    app = ROOT / "src" / "Cgf.CameraControl.App"
    frames = ico_frames(app / "Assets" / "appicon.ico")
    require(ico_frames(icons / "installer.ico") == frames, "Installer and application icons differ")
    for size in [16, 24, 32, 48, 64, 128, 256, 512, 1024]:
        data = (icons / f"icon-{size}.png").read_bytes()
        require(png_size(data) == (size, size), f"Invalid {size}px raster")
        if size in ICON_SIZES:
            require(data == frames[size, size], f"ICO and {size}px raster differ")
    require(png_size((app / "Assets" / "appicon.png").read_bytes()) == (64, 64),
            "Caption icon must be the high-DPI small glyph")
    svg = ET.parse(icons / "icon.svg").getroot()
    require(svg.get("viewBox") == "0 0 1024 1024", "Invalid SVG canvas")
    icns = (icons / "AppIcon.icns").read_bytes()
    require(icns[:4] == b"icns" and struct.unpack_from(">I", icns, 4)[0] == len(icns),
            "Invalid macOS icon container")
    representations = {}
    offset = 8
    while offset < len(icns):
        kind, length = struct.unpack_from(">4sI", icns, offset)
        require(length >= 8 and offset + length <= len(icns), "Invalid macOS icon element")
        require(kind not in representations, "Duplicate macOS icon element")
        representations[kind] = icns[offset + 8:offset + length]
        offset += length
    for kind, size in [(b"ic11", 32), (b"ic12", 64), (b"ic07", 128), (b"ic13", 256),
                       (b"ic08", 256), (b"ic14", 512), (b"ic09", 512), (b"ic10", 1024)]:
        require(png_size(representations[kind]) == (size, size), f"Invalid macOS {kind!r} dimensions")
    for rgb, alpha, size in [(b"is32", b"s8mk", 16), (b"il32", b"l8mk", 32)]:
        require(bool(representations[rgb]) and len(representations[alpha]) == size * size,
                f"Missing classic macOS {size}px icon")

    project = ET.parse(app / "Cgf.CameraControl.App.csproj")
    require(project.findtext(".//ApplicationIcon") == r"Assets\appicon.ico",
            "Windows executable icon is not assigned")
    require(any(node.get("Include") == r"Assets\**" for node in project.findall(".//AvaloniaResource")),
            "Window icons are not embedded as Avalonia resources")
    windows = 0
    for path in (app / "Views").rglob("*.axaml"):
        root = ET.parse(path).getroot()
        if root.tag.endswith("}Window"):
            windows += 1
            require(root.get("Icon") == "/Assets/appicon.ico", f"Window icon is not assigned: {path.name}")
    require(windows > 0, "No application windows found")
    main_window = ET.parse(app / "Views" / "MainWindow.axaml")
    require(main_window.find('.//{https://github.com/avaloniaui}Image[@Source="/Assets/appicon.png"]')
            is not None, "Caption icon is not assigned")
    installer = (ROOT / "packaging" / "windows" / "installer.nsi").read_text()
    for name in ["MUI_ICON", "MUI_UNICON"]:
        require(f'!define {name} "..\\icon\\installer.ico"' in installer,
                f"{name} is not assigned")
    require('"" "$INSTDIR\\${EXENAME}" 0' in installer, "Shortcut icon is not explicit")
    require('\'$\\"$INSTDIR\\${EXENAME}$\\",0\'' in installer, "Installed-app icon path is not quoted")

    desktop_path = ROOT / "packaging" / "linux" / "io.github.sensslen.CgfCameraControl.desktop"
    desktop = configparser.ConfigParser(interpolation=None)
    desktop.read(desktop_path)
    entry = desktop["Desktop Entry"]
    require(entry["Icon"] == entry["StartupWMClass"] == desktop_path.stem, "Linux icon identity differs")
    require(f'WmClass = "{desktop_path.stem}"' in (app / "Program.cs").read_text(),
            "Linux window identity differs from its launcher")
    manifest = desktop_path.with_suffix(".yml").read_text()
    require(f"app-id: {desktop_path.stem}" in manifest, "Flatpak application identity differs")
    for suffix, folder in [("png", "256x256"), ("svg", "scalable")]:
        require(f"/app/share/icons/hicolor/{folder}/apps/{desktop_path.stem}.{suffix}" in manifest,
                f"Flatpak {suffix} icon is not installed")
    metadata = ET.parse(desktop_path.with_suffix(".metainfo.xml"))
    require(metadata.findtext("id") == desktop_path.stem
            and metadata.findtext("launchable") == desktop_path.name, "AppStream identity differs")
    bundle = (ROOT / "packaging" / "macos" / "make-bundle.sh").read_text()
    require("<key>CFBundleIconFile</key><string>AppIcon</string>" in bundle,
            "macOS bundle icon is not assigned")
    require('Contents/Resources/AppIcon.icns"' in bundle, "macOS icon is not copied into the bundle")
    return frames


def verify_windows_executable(path, frames):
    require(os.name == "nt", "Windows resource checks must run on Windows")
    from ctypes import wintypes

    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.LoadLibraryExW.argtypes = [wintypes.LPCWSTR, wintypes.HANDLE, wintypes.DWORD]
    kernel.LoadLibraryExW.restype = wintypes.HMODULE
    kernel.FreeLibrary.argtypes = [wintypes.HMODULE]
    kernel.FindResourceW.argtypes = [wintypes.HMODULE, ctypes.c_void_p, ctypes.c_void_p]
    kernel.FindResourceW.restype = wintypes.HANDLE
    kernel.LoadResource.argtypes = [wintypes.HMODULE, wintypes.HANDLE]
    kernel.LoadResource.restype = wintypes.HANDLE
    kernel.LockResource.argtypes = [wintypes.HANDLE]
    kernel.LockResource.restype = ctypes.c_void_p
    kernel.SizeofResource.argtypes = [wintypes.HMODULE, wintypes.HANDLE]
    kernel.SizeofResource.restype = wintypes.DWORD
    callback_type = ctypes.WINFUNCTYPE(
        wintypes.BOOL, wintypes.HMODULE, ctypes.c_void_p, ctypes.c_void_p, wintypes.LPARAM)
    kernel.EnumResourceNamesW.argtypes = [wintypes.HMODULE, ctypes.c_void_p, callback_type, wintypes.LPARAM]
    kernel.EnumResourceNamesW.restype = wintypes.BOOL
    module = kernel.LoadLibraryExW(str(path.resolve()), None, 0x22)
    if not module:
        raise ctypes.WinError(ctypes.get_last_error())

    def resource(name, kind):
        found = kernel.FindResourceW(module, name, kind)
        loaded = kernel.LoadResource(module, found) if found else None
        address = kernel.LockResource(loaded) if loaded else None
        if not address:
            raise ctypes.WinError(ctypes.get_last_error())
        return ctypes.string_at(address, kernel.SizeofResource(module, found))

    groups, errors = [], []

    @callback_type
    def collect(_, __, name, ___):
        try:
            groups.append(resource(name, 14))
            return True
        except OSError as error:
            errors.append(error)
            return False

    try:
        result = kernel.EnumResourceNamesW(module, 14, collect, 0)
        if errors:
            raise errors[0]
        if not result and ctypes.get_last_error() != 1813:
            raise ctypes.WinError(ctypes.get_last_error())
        require(bool(groups), f"No embedded icon group matches the application artwork: {path}")
        for group in groups:
            require(struct.unpack_from("<HHH", group) == (0, 1, len(frames)),
                    f"Embedded icon group has missing sizes: {path}")
            embedded = {}
            for index in range(len(frames)):
                width, height, _, _, _, _, length, identifier = struct.unpack_from(
                    "<BBBBHHIH", group, 6 + index * 14)
                data = resource(identifier, 3)
                require(len(data) == length, f"Invalid embedded icon length: {path}")
                embedded[width or 256, height or 256] = data
            require(embedded == frames, f"Embedded icon group differs from the application artwork: {path}")
        print(f"Verified embedded application icon: {path}")
    finally:
        kernel.FreeLibrary(module)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows-exe", type=Path, action="append", default=[])
    args = parser.parse_args()
    frames = verify_assets()
    print("Verified icon assets and application/package assignments")
    for path in args.windows_exe:
        verify_windows_executable(path, frames)


if __name__ == "__main__":
    main()
