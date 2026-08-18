#!/usr/bin/env python3
"""Generates the application icon in every form the platforms need.

One script so the SVG and the rasters cannot drift: both are built from the same numbers. Run it from
the repository root after changing anything here:

    python packaging/icon/generate.py

The mark is a lens seen head on with a tally light. At sixteen pixels that reduces to a ring and a
red dot, which is the smallest thing that still says "a camera, and it is live".
"""

import os
import struct
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))

# Geometry on a 1024 canvas. Every size below is a fraction of it, so the SVG and the rasters agree.
CANVAS = 1024
CORNER = 224
LENS_CENTRE = (472, 552)
LENS_OUTER = 300
LENS_RING = 54
LENS_INNER = 150
GLINT_CENTRE = (378, 452)
GLINT_RADIUS = 46
TALLY_CENTRE = (760, 268)
TALLY_RADIUS = 104

TOP = (18, 39, 62)
BOTTOM = (28, 86, 122)
RING = (232, 238, 245)
PUPIL = (8, 22, 33)
GLINT = (255, 255, 255, 58)
TALLY = (226, 59, 46)
TALLY_RIM = (255, 156, 146)

RASTERS = [16, 24, 32, 48, 64, 128, 256, 512, 1024]
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]


def draw(size):
    """Draws at four times the requested size and downsamples, which is the cheapest anti-aliasing."""
    scale = 4
    n = size * scale
    k = n / CANVAS

    background = Image.new("RGB", (1, CANVAS))
    for y in range(CANVAS):
        t = y / (CANVAS - 1)
        background.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    image = background.resize((n, n), Image.Resampling.BILINEAR).convert("RGBA")

    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=CORNER * k, fill=255)
    image.putalpha(mask)

    overlay = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    pen = ImageDraw.Draw(overlay)

    def circle(centre, radius, **kwargs):
        cx, cy = centre[0] * k, centre[1] * k
        r = radius * k
        pen.ellipse([cx - r, cy - r, cx + r, cy + r], **kwargs)

    circle(LENS_CENTRE, LENS_OUTER, outline=RING, width=max(1, round(LENS_RING * k)))
    circle(LENS_CENTRE, LENS_INNER, fill=PUPIL)
    circle(GLINT_CENTRE, GLINT_RADIUS, fill=GLINT)
    circle(TALLY_CENTRE, TALLY_RADIUS, fill=TALLY, outline=TALLY_RIM, width=max(1, round(14 * k)))

    image.alpha_composite(overlay)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def svg():
    cx, cy = LENS_CENTRE
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {CANVAS} {CANVAS}" width="{CANVAS}" height="{CANVAS}">
  <defs>
    <linearGradient id="body" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="rgb{TOP}"/>
      <stop offset="1" stop-color="rgb{BOTTOM}"/>
    </linearGradient>
    <clipPath id="rounded">
      <rect x="0" y="0" width="{CANVAS}" height="{CANVAS}" rx="{CORNER}" ry="{CORNER}"/>
    </clipPath>
  </defs>
  <g clip-path="url(#rounded)">
    <rect x="0" y="0" width="{CANVAS}" height="{CANVAS}" fill="url(#body)"/>
    <circle cx="{cx}" cy="{cy}" r="{LENS_OUTER - LENS_RING / 2}" fill="none"
            stroke="rgb{RING}" stroke-width="{LENS_RING}"/>
    <circle cx="{cx}" cy="{cy}" r="{LENS_INNER}" fill="rgb{PUPIL}"/>
    <circle cx="{GLINT_CENTRE[0]}" cy="{GLINT_CENTRE[1]}" r="{GLINT_RADIUS}" fill="#ffffff" opacity="0.23"/>
    <circle cx="{TALLY_CENTRE[0]}" cy="{TALLY_CENTRE[1]}" r="{TALLY_RADIUS - 7}" fill="rgb{TALLY}"
            stroke="rgb{TALLY_RIM}" stroke-width="14"/>
  </g>
</svg>
"""


def write_icns(path, images):
    """A minimal icns writer: PIL only saves this format on macOS, and the release builds on Linux too."""
    types = {16: b"icp4", 32: b"icp5", 64: b"icp6", 128: b"ic07", 256: b"ic08", 512: b"ic09", 1024: b"ic10"}
    chunks = b""
    for size, kind in sorted(types.items()):
        png = os.path.join(HERE, f"icon-{size}.png")
        images[size].save(png)
        with open(png, "rb") as handle:
            payload = handle.read()
        chunks += kind + struct.pack(">I", len(payload) + 8) + payload
    with open(path, "wb") as handle:
        handle.write(b"icns" + struct.pack(">I", len(chunks) + 8) + chunks)


def main():
    images = {size: draw(size) for size in RASTERS}

    with open(os.path.join(HERE, "icon.svg"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(svg())

    for size in RASTERS:
        images[size].save(os.path.join(HERE, f"icon-{size}.png"))

    assets = os.path.join(ROOT, "src", "Cgf.CameraControl.App", "Assets")
    images[1024].save(
        os.path.join(assets, "appicon.ico"),
        sizes=[(s, s) for s in ICO_SIZES],
    )
    images[512].save(os.path.join(assets, "appicon.png"))
    write_icns(os.path.join(HERE, "AppIcon.icns"), images)

    print(f"wrote icon.svg, {len(RASTERS)} rasters, appicon.ico, appicon.png and AppIcon.icns")


if __name__ == "__main__":
    main()
