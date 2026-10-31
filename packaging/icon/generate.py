#!/usr/bin/env python3
"""Generates the application icon in every form the platforms need.

One script so the SVG and the rasters cannot drift: both are built from the same numbers. Run it from
the repository root after changing anything here:

    python packaging/icon/generate.py

The mark is a lens seen head on, ringed by four pan and tilt chevrons, with a tally light. Below
SMALL points the chevrons and the glint turn to mush, so those sizes draw a bolder ring and a red
dot instead, which is the smallest thing that still says "a camera, and it is live".
"""

import io
import os
import struct
from PIL import IcnsImagePlugin, Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))

# Geometry on a 1024 canvas. Every size below is a fraction of it, so the SVG and the rasters agree.
CANVAS = 1024
CORNER = 228
CENTRE = (512, 540)
LENS_OUTER = 236
LENS_RING = 44
BARREL = 176
BARREL_RING = 10
PUPIL = 112
GLINT_CENTRE = (466, 492)
GLINT_RADIUS = 34
CHEVRON_GAP = 300
CHEVRON_ARM = 64
CHEVRON_WIDTH = 34
TALLY_CENTRE = (812, 212)
TALLY_RADIUS = 82
TALLY_GLOW = 70

# Below this many points the small glyph replaces the full drawing.
SMALL = 32
SMALL_LENS_OUTER = 330
SMALL_LENS_RING = 120
SMALL_PUPIL = 150
SMALL_TALLY_CENTRE = (800, 224)
SMALL_TALLY_RADIUS = 168

# Apple's grid leaves a 100 unit margin around an 824 unit shape. Windows and Linux icons fill
# their canvas, so only the icns uses it.
MAC_MARGIN = 100

TOP = (22, 44, 70)
BOTTOM = (16, 92, 128)
RING = (234, 240, 247)
BARREL_COLOUR = (20, 34, 48)
BARREL_RIM = (64, 96, 124)
PUPIL_COLOUR = (6, 14, 22)
GLINT = (255, 255, 255, 90)
CHEVRON = (234, 240, 247, 170)
TALLY = (232, 58, 44)
TALLY_RIM = (255, 150, 138)

RASTERS = [16, 24, 32, 48, 64, 128, 256, 512, 1024]
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
DIRECTIONS = [(0, -1), (1, 0), (0, 1), (-1, 0)]


def chevron_points(direction):
    """A V pointing away from the lens along the unit vector direction."""
    dx, dy = direction
    cx, cy = CENTRE
    reach = CHEVRON_GAP + CHEVRON_ARM / 2
    tip = (cx + dx * reach, cy + dy * reach)
    base = (tip[0] - dx * CHEVRON_ARM, tip[1] - dy * CHEVRON_ARM)
    px, py = -dy, dx
    return [(base[0] + px * CHEVRON_ARM, base[1] + py * CHEVRON_ARM), tip,
            (base[0] - px * CHEVRON_ARM, base[1] - py * CHEVRON_ARM)]


def draw(size, points=None, margin=0):
    """Draws at four times the requested size and downsamples, which is the cheapest anti-aliasing.

    points is the size the icon is shown at and picks the glyph: the 32 pixel image in a 16 point
    Retina slot still has to read at 16 points.
    """
    small = (points or size) < SMALL
    n = size * 4
    k = n / CANVAS
    offset = margin * k
    shrink = (CANVAS - 2 * margin) / CANVAS

    def at(x, y):
        return (offset + x * k * shrink, offset + y * k * shrink)

    def length(v):
        return v * k * shrink

    inner = round(n - 2 * offset)
    background = Image.new("RGB", (1, CANVAS))
    for y in range(CANVAS):
        t = y / (CANVAS - 1)
        background.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    body = background.resize((inner, inner), Image.Resampling.BILINEAR).convert("RGBA")
    mask = Image.new("L", body.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, inner - 1, inner - 1], radius=length(CORNER), fill=255)
    body.putalpha(mask)

    def layer():
        return Image.new("RGBA", (n, n), (0, 0, 0, 0))

    image = layer()
    if margin and not small:
        shadow = layer()
        shadow.paste((0, 0, 0, 90), (round(offset), round(offset + length(12))), mask)
        image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(length(14))))
    image.alpha_composite(body, (round(offset), round(offset)))

    def circle(pen, centre, radius, width=None, **kwargs):
        cx, cy = at(*centre)
        r = length(radius)
        if width is not None:
            kwargs["width"] = max(1, round(length(width)))
        pen.ellipse([cx - r, cy - r, cx + r, cy + r], **kwargs)

    if small:
        overlay = layer()
        pen = ImageDraw.Draw(overlay)
        circle(pen, CENTRE, SMALL_LENS_OUTER, outline=RING, width=SMALL_LENS_RING)
        circle(pen, CENTRE, SMALL_PUPIL, fill=PUPIL_COLOUR)
        circle(pen, SMALL_TALLY_CENTRE, SMALL_TALLY_RADIUS, fill=TALLY)
        image.alpha_composite(overlay)
        return image.resize((size, size), Image.Resampling.LANCZOS)

    glow = layer()
    circle(ImageDraw.Draw(glow), TALLY_CENTRE, TALLY_RADIUS + TALLY_GLOW / 2, fill=TALLY + (150,))
    glow = glow.filter(ImageFilter.GaussianBlur(length(TALLY_GLOW / 2)))
    # The glow stays inside the rounded body, as the SVG clips it.
    body_mask = Image.new("L", (n, n), 0)
    body_mask.paste(mask, (round(offset), round(offset)))
    glow.putalpha(ImageChops.multiply(glow.getchannel("A"), body_mask))
    image.alpha_composite(glow)

    overlay = layer()
    pen = ImageDraw.Draw(overlay)
    for direction in DIRECTIONS:
        start, tip, end = chevron_points(direction)
        pen.line([at(*start), at(*tip), at(*end)], fill=CHEVRON,
                 width=max(1, round(length(CHEVRON_WIDTH))), joint="curve")
        # Round caps, as the SVG's stroke-linecap draws them.
        for cap in (start, end):
            circle(pen, cap, CHEVRON_WIDTH / 2, fill=CHEVRON)
    image.alpha_composite(overlay)

    overlay = layer()
    pen = ImageDraw.Draw(overlay)
    circle(pen, CENTRE, LENS_OUTER, outline=RING, width=LENS_RING)
    circle(pen, CENTRE, BARREL, fill=BARREL_COLOUR, outline=BARREL_RIM, width=BARREL_RING)
    circle(pen, CENTRE, PUPIL, fill=PUPIL_COLOUR)
    circle(pen, GLINT_CENTRE, GLINT_RADIUS, fill=GLINT)
    circle(pen, TALLY_CENTRE, TALLY_RADIUS, fill=TALLY, outline=TALLY_RIM, width=12)
    image.alpha_composite(overlay)

    return image.resize((size, size), Image.Resampling.LANCZOS)


def svg():
    cx, cy = CENTRE
    chevrons = "\n".join(
        '    <polyline points="' + " ".join(f"{x:g},{y:g}" for x, y in chevron_points(d)) + '"/>'
        for d in DIRECTIONS)
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {CANVAS} {CANVAS}" width="{CANVAS}" height="{CANVAS}">
  <defs>
    <linearGradient id="body" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="rgb{TOP}"/>
      <stop offset="1" stop-color="rgb{BOTTOM}"/>
    </linearGradient>
    <clipPath id="rounded">
      <rect x="0" y="0" width="{CANVAS}" height="{CANVAS}" rx="{CORNER}" ry="{CORNER}"/>
    </clipPath>
    <filter id="glow" x="-1" y="-1" width="3" height="3">
      <feGaussianBlur stdDeviation="{TALLY_GLOW / 2:g}"/>
    </filter>
  </defs>
  <g clip-path="url(#rounded)">
    <rect x="0" y="0" width="{CANVAS}" height="{CANVAS}" fill="url(#body)"/>
    <circle cx="{TALLY_CENTRE[0]}" cy="{TALLY_CENTRE[1]}" r="{TALLY_RADIUS + TALLY_GLOW / 2:g}"
            fill="rgb{TALLY}" opacity="0.59" filter="url(#glow)"/>
  </g>
  <g fill="none" stroke="rgb{CHEVRON[:3]}" stroke-opacity="{CHEVRON[3] / 255:.2f}"
     stroke-width="{CHEVRON_WIDTH}" stroke-linecap="round" stroke-linejoin="round">
{chevrons}
  </g>
  <circle cx="{cx}" cy="{cy}" r="{LENS_OUTER - LENS_RING / 2:g}" fill="none"
          stroke="rgb{RING}" stroke-width="{LENS_RING}"/>
  <circle cx="{cx}" cy="{cy}" r="{BARREL - BARREL_RING / 2:g}" fill="rgb{BARREL_COLOUR}"
          stroke="rgb{BARREL_RIM}" stroke-width="{BARREL_RING}"/>
  <circle cx="{cx}" cy="{cy}" r="{PUPIL}" fill="rgb{PUPIL_COLOUR}"/>
  <circle cx="{GLINT_CENTRE[0]}" cy="{GLINT_CENTRE[1]}" r="{GLINT_RADIUS}" fill="#ffffff"
          opacity="{GLINT[3] / 255:.2f}"/>
  <circle cx="{TALLY_CENTRE[0]}" cy="{TALLY_CENTRE[1]}" r="{TALLY_RADIUS - 6}" fill="rgb{TALLY}"
          stroke="rgb{TALLY_RIM}" stroke-width="12"/>
</svg>
"""


def packbits(channel):
    """The run-length scheme of is32 and il32: a run is 0x80 + count - 3, a literal stretch count - 1."""
    out = bytearray()
    i, n = 0, len(channel)
    while i < n:
        run = 1
        while i + run < n and run < 130 and channel[i + run] == channel[i]:
            run += 1
        if run >= 3:
            out += bytes([0x80 + run - 3, channel[i]])
            i += run
            continue
        start = i
        while i < n and i - start < 128:
            if i + 2 < n and channel[i] == channel[i + 1] == channel[i + 2]:
                break
            i += 1
        out += bytes([i - start - 1]) + channel[start:i]
    return bytes(out)


def write_icns(path):
    """A minimal icns writer: PIL only saves this format on macOS, and the release builds on Linux too.

    The 16 and 32 point slots carry the classic run-length and mask elements, because Finder's list
    and column views do not draw PNG data stored there (icp4, icp5). Every point size also has its
    @2x element, without which a Retina display upscales the 1x one.
    """

    def element(kind, payload):
        return kind + struct.pack(">I", len(payload) + 8) + payload

    def png(image):
        buffer = io.BytesIO()
        image.save(buffer, "PNG")
        return buffer.getvalue()

    chunks = b""
    for points, rgb, alpha in [(16, b"is32", b"s8mk"), (32, b"il32", b"l8mk")]:
        image = draw(points, margin=MAC_MARGIN)
        chunks += element(rgb, b"".join(packbits(image.getchannel(c).tobytes()) for c in "RGB"))
        chunks += element(alpha, image.getchannel("A").tobytes())
    for points, scale, kind in [(16, 2, b"ic11"), (32, 2, b"ic12"), (128, 1, b"ic07"), (128, 2, b"ic13"),
                                (256, 1, b"ic08"), (256, 2, b"ic14"), (512, 1, b"ic09"), (512, 2, b"ic10")]:
        chunks += element(kind, png(draw(points * scale, points, margin=MAC_MARGIN)))
    with open(path, "wb") as handle:
        handle.write(b"icns" + struct.pack(">I", len(chunks) + 8) + chunks)


def main():
    images = {size: draw(size) for size in RASTERS}

    with open(os.path.join(HERE, "icon.svg"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(svg())

    for size in RASTERS:
        images[size].save(os.path.join(HERE, f"icon-{size}.png"))

    assets = os.path.join(ROOT, "src", "Cgf.CameraControl.App", "Assets")
    for ico in [os.path.join(assets, "appicon.ico"), os.path.join(HERE, "installer.ico")]:
        images[256].save(ico, sizes=[(s, s) for s in ICO_SIZES],
                         append_images=[images[s] for s in ICO_SIZES if s != 256])
    draw(64, points=16).save(os.path.join(assets, "appicon.png"))
    icns = os.path.join(HERE, "AppIcon.icns")
    write_icns(icns)

    # Pillow decodes the run-length elements itself, so a malformed stream fails here, not in Finder.
    with open(icns, "rb") as handle:
        check = IcnsImagePlugin.IcnsFile(handle)
        for size in [16, 32]:
            assert check.getimage((size, size, 1)).tobytes() == draw(size, margin=MAC_MARGIN).tobytes(), \
                f"the {size} point element decodes wrongly"

    print(f"wrote icon.svg, {len(RASTERS)} rasters, appicon.ico, installer.ico, appicon.png and AppIcon.icns")


if __name__ == "__main__":
    main()
