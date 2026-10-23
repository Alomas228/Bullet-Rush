"""Generate original, royalty-free UI art for Arcade Survivor.

This script produces every texture used by the main-menu skin.  All shapes are
built procedurally from primitives, so the output is original work created for
this project (no third-party art is copied into the kit).

Usage: python Tools/ArcadeKit/generate_arcadekit.py
"""

from __future__ import annotations

import math
import os
import random
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

SS = 3
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Game", "UI", "ArcadeKit", "Resources", "ArcadeKit")

random.seed(20261022)


def S(v: float) -> int:
    return int(round(v * SS))


def hex_color(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = value.lstrip("#")
    return (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha)


def mix(a, b, t: float):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


def canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (S(w), S(h)), (0, 0, 0, 0))


def downsample(image: Image.Image, w: int, h: int) -> Image.Image:
    return image.resize((w, h), Image.LANCZOS)


def vertical_gradient(w: int, h: int, top, bottom) -> Image.Image:
    image = Image.new("RGBA", (w, h), top)
    draw = ImageDraw.Draw(image)
    for y in range(h):
        t = y / max(1, h - 1)
        draw.line([(0, y), (w, y)], fill=mix(top, bottom, t))
    return image


def rounded_mask(w: int, h: int, radius: float, inset: float = 0.0) -> Image.Image:
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    box = (inset, inset, w - 1 - inset, h - 1 - inset)
    draw.rounded_rectangle(box, radius=max(0.0, radius - inset), fill=255)
    return mask


def apply_mask(image: Image.Image, mask: Image.Image) -> Image.Image:
    result = image.copy()
    result.putalpha(ImageChops.multiply(image.getchannel("A"), mask))
    return result


def bevel(
    w: int,
    h: int,
    radius: float,
    top: int,
    bottom: int,
    rim: int = 46,
    edge: int = 26,
    gloss: int = 26,
    inner_shadow: int = 24,
    grain: int = 10,
    base_alpha: int = 255,
) -> Image.Image:
    """Return a white, tintable, beveled 9-slice surface."""
    W, H = S(w), S(h)
    R = radius * SS
    gradient = vertical_gradient(
        W, H,
        (top, top, top, base_alpha),
        (bottom, bottom, bottom, base_alpha),
    )
    mask = rounded_mask(W, H, R)
    base = apply_mask(gradient, mask)

    # Soft inner highlight along the top edge.
    if gloss > 0:
        shine = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        sd = ImageDraw.Draw(shine)
        for y in range(S(2), max(S(3), int(H * 0.46))):
            t = 1.0 - (y - S(2)) / max(1.0, H * 0.44)
            a = int(gloss * max(0.0, t) ** 1.5)
            sd.line([(S(2), y), (W - S(2), y)], fill=(255, 255, 255, a))
        shine = apply_mask(shine, rounded_mask(W, H, R, S(2)))
        base = Image.alpha_composite(base, shine)

    # Inner bottom shade makes the surface feel raised.
    if inner_shadow > 0:
        shade = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        sd = ImageDraw.Draw(shade)
        start = int(H * 0.78)
        for y in range(start, H):
            t = (y - start) / max(1.0, H - start)
            sd.line([(0, y), (W, y)], fill=(0, 0, 0, int(inner_shadow * t)))
        shade = apply_mask(shade, rounded_mask(W, H, R, S(1)))
        base = Image.alpha_composite(base, shade)

    # Outer dark contour and bright rim.
    contour = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cd = ImageDraw.Draw(contour)
    cd.rounded_rectangle(
        (S(0.7), S(0.7), W - S(0.7), H - S(0.7)),
        radius=max(1.0, R - S(0.7)),
        outline=(0, 0, 0, edge),
        width=max(1, S(1.4)),
    )
    base = Image.alpha_composite(base, contour)

    light = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(light)
    ld.rounded_rectangle(
        (S(1.5), S(1.5), W - S(1.5), H - S(1.5)),
        radius=max(1.0, R - S(1.5)),
        outline=(255, 255, 255, rim),
        width=max(1, S(1.35)),
    )
    base = Image.alpha_composite(base, light)

    # Fine grain, masked to the surface.
    if grain > 0:
        noise = Image.effect_noise((W, H), 34).convert("L")
        noise = noise.filter(ImageFilter.GaussianBlur(0.55))
        noise_rgba = Image.new("RGBA", (W, H), (255, 255, 255, 0))
        noise_rgba.putalpha(noise.point(lambda v: int(v * grain / 255)))
        noise_rgba = apply_mask(noise_rgba, mask)
        base = Image.alpha_composite(base, noise_rgba)

    return base


def save(image: Image.Image, name: str, w: int, h: int) -> None:
    os.makedirs(OUT, exist_ok=True)
    image = downsample(image, w, h)
    image.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, image.size)


def hex_points(w: int, h: int, inset: float = 0.0):
    cx, cy = w / 2.0, h / 2.0
    radius = min(h / 2.0, w / math.sqrt(3.0)) - inset
    return [
        (
            cx + radius * math.cos(math.radians(90 + i * 60)),
            cy - radius * math.sin(math.radians(90 + i * 60)),
        )
        for i in range(6)
    ]


def make_hex() -> None:
    w, h = 192, 216
    W, H = S(w), S(h)
    points = [(x * SS, y * SS) for x, y in hex_points(W, H)]
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    gradient = vertical_gradient(W, H, (255, 255, 255, 255), (205, 205, 205, 255))
    base = apply_mask(gradient, mask)

    shade = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    sd.line(points + [points[0]], fill=(255, 255, 255, 90), width=S(2))
    base = Image.alpha_composite(base, shade)

    inner = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(inner).line(
        [(x * SS, y * SS) for x, y in hex_points(W, H, S(2.4))] * 2,
        fill=(0, 0, 0, 55),
        width=S(1.6),
    )
    base = Image.alpha_composite(base, inner)
    save(base, "hex_fill", w, h)

    ring = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ring)
    rd.line(
        [(x * SS, y * SS) for x, y in hex_points(W, H, S(2.8))],
        fill=(255, 255, 255, 255),
        width=S(2.2),
        joint="curve",
    )
    save(ring, "hex_ring", w, h)


def make_brushes() -> None:
    # Dark brushed smear, used behind the logotype.
    w, h = 1024, 448
    W, H = S(w), S(h)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(95):
        t = i / 94.0
        y = H * (0.28 + 0.42 * t) + random.uniform(-H * 0.16, H * 0.16)
        x0 = W * (0.02 + random.uniform(0.0, 0.16))
        x1 = W * (0.82 + random.uniform(0.0, 0.16))
        hh = random.uniform(H * 0.018, H * 0.075)
        alpha = random.randint(24, 82)
        color = (8, 14, 27, alpha)
        d.line([(x0, y), (x1, y + random.uniform(-H * 0.03, H * 0.03))], fill=color, width=S(hh))
    layer = layer.filter(ImageFilter.GaussianBlur(S(2.2)))
    # Ragged edges: multiply by a rough horizontal mask.
    noise = Image.effect_noise((W, H), 26).convert("L").filter(ImageFilter.GaussianBlur(S(2.4)))
    alpha = layer.getchannel("A").point(lambda v: min(255, int(v * 1.35)))
    alpha = ImageChops.multiply(alpha, noise.point(lambda v: int(110 + v * 0.57)))
    layer.putalpha(alpha)
    save(layer, "brush_dark", w, h)

    # Warm accent stroke.
    w, h = 768, 144
    W, H = S(w), S(h)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(55):
        t = i / 54.0
        y = H * (0.42 + 0.18 * t) + random.uniform(-H * 0.08, H * 0.08)
        x0 = W * (0.03 + random.uniform(0, 0.1))
        x1 = W * (0.87 + random.uniform(0, 0.09))
        hh = random.uniform(H * 0.035, H * 0.13)
        d.line([(x0, y), (x1, y + random.uniform(-6, 6))], fill=(255, 170, 38, random.randint(70, 130)), width=S(hh))
    layer = layer.filter(ImageFilter.GaussianBlur(S(1.4)))
    noise = Image.effect_noise((W, H), 30).convert("L").filter(ImageFilter.GaussianBlur(S(1.2)))
    alpha = ImageChops.multiply(layer.getchannel("A"), noise.point(lambda v: int(150 + v * 0.41)))
    layer.putalpha(alpha)
    save(layer, "brush_orange", w, h)


def make_glow() -> None:
    w, h = 512, 512
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = image.load()
    cx, cy = W / 2.0, H / 2.0
    radius = min(cx, cy)
    for y in range(H):
        for x in range(W):
            dx = (x - cx) / radius
            dy = (y - cy) / radius
            dist = math.sqrt(dx * dx + dy * dy)
            a = 0 if dist >= 1 else int(255 * (1 - dist) ** 2.4)
            if a:
                px[x, y] = (255, 255, 255, a)
    save(image, "glow", w, h)

    w, h = 1024, 576
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = image.load()
    for y in range(H):
        v = y / (H - 1)
        for x in range(W):
            u = x / (W - 1)
            dx = (u - 0.5) * 1.08
            dy = (v - 0.5) * 1.25
            dist = math.sqrt(dx * dx + dy * dy)
            edge = max(0.0, min(1.0, (dist - 0.28) / 0.6))
            edge = edge * edge * (3 - 2 * edge)
            top = max(0.0, 1.0 - v / 0.36) ** 1.4
            bottom = max(0.0, (v - 0.66) / 0.34) ** 1.4
            a = int(min(232, 22 + 190 * edge + 60 * top + 50 * bottom))
            # Slightly warmer/darker towards the bottom edge.
            px[x, y] = (7, 12, 24, a)
    save(image, "vignette", w, h)


def make_spark() -> None:
    w, h = 128, 128
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cx, cy = W / 2.0, H / 2.0
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for spread, alpha in ((S(52), 38), (S(30), 80), (S(13), 255)):
        d.polygon(
            [
                (cx, cy - spread),
                (cx + spread * 0.18, cy - spread * 0.18),
                (cx + spread, cy),
                (cx + spread * 0.18, cy + spread * 0.18),
                (cx, cy + spread),
                (cx - spread * 0.18, cy + spread * 0.18),
                (cx - spread, cy),
                (cx - spread * 0.18, cy - spread * 0.18),
            ],
            fill=(255, 234, 170, alpha),
        )
    layer = layer.filter(ImageFilter.GaussianBlur(S(1.0)))
    image = Image.alpha_composite(image, layer)
    save(image, "spark", w, h)


def make_coin() -> None:
    w, h = 128, 128
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    pad = S(5)
    d.ellipse((pad, pad, W - pad, H - pad), fill=(120, 69, 7, 255))
    d.ellipse((pad + S(2), pad + S(2), W - pad - S(2), H - pad - S(2)), fill=(255, 188, 52, 255))
    d.ellipse((pad + S(9), pad + S(8), W - pad - S(9), H - pad - S(10)), fill=(255, 220, 104, 255))
    d.ellipse((pad + S(14), pad + S(13), W - pad - S(14), H - pad - S(16)), fill=(232, 151, 23, 255))
    # Star relief.
    cx, cy = W / 2.0, H / 2.0
    pts = []
    for i in range(10):
        rr = S(25) if i % 2 == 0 else S(11)
        ang = math.radians(90 + i * 36)
        pts.append((cx + rr * math.cos(ang), cy - rr * math.sin(ang)))
    d.polygon(pts, fill=(255, 244, 196, 255))
    d.polygon(pts, outline=(173, 103, 9, 210), width=S(1.4))
    # Highlight arc.
    d.arc((pad + S(7), pad + S(7), W - pad - S(7), H - pad - S(7)), 196, 310, fill=(255, 255, 235, 210), width=S(3))
    save(image, "coin", w, h)


def make_chest() -> None:
    w, h = 256, 256
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Soft glow.
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((S(24), S(30), W - S(24), H - S(8)), fill=(255, 190, 69, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(S(18)))
    image = Image.alpha_composite(image, glow)

    d = ImageDraw.Draw(image)
    x0, x1 = S(37), W - S(37)
    # Chest body.
    d.rounded_rectangle((x0, S(104), x1, S(210)), radius=S(16), fill=(27, 53, 92, 255), outline=(8, 17, 32, 255), width=S(3))
    # Lid.
    d.rounded_rectangle((x0, S(68), x1, S(124)), radius=S(22), fill=(41, 78, 132, 255), outline=(8, 17, 32, 255), width=S(3))
    # Gold bands.
    for xx in (S(48), x1 - S(30)):
        d.rounded_rectangle((xx, S(72), xx + S(17), S(207)), radius=S(6), fill=(255, 187, 49, 255), outline=(124, 72, 8, 255), width=S(2))
    # Lid highlight.
    d.arc((x0 + S(10), S(74), x1 - S(10), S(148)), 190, 350, fill=(120, 188, 255, 170), width=S(5))
    # Middle band.
    d.rectangle((x0, S(118), x1, S(135)), fill=(255, 196, 63, 255))
    d.line((x0, S(135), x1, S(135)), fill=(135, 79, 9, 255), width=S(3))
    # Lock.
    lx, ly = W // 2, S(139)
    d.rounded_rectangle((lx - S(21), ly, lx + S(21), ly + S(46)), radius=S(8), fill=(255, 207, 79, 255), outline=(112, 65, 7, 255), width=S(3))
    d.arc((lx - S(14), ly - S(19), lx + S(14), ly + S(19)), 180, 360, fill=(229, 174, 47, 255), width=S(6))
    d.ellipse((lx - S(4), ly + S(18), lx + S(4), ly + S(26)), fill=(101, 57, 5, 255))
    # Rivets.
    for xx in (S(44), x1 - S(18)):
        for yy in (S(86), S(192)):
            d.ellipse((xx - S(2), yy - S(2), xx + S(2), yy + S(2)), fill=(255, 228, 153, 255))
    save(image, "chest", w, h)


def make_knob() -> None:
    w, h = 96, 96
    W, H = S(w), S(h)
    image = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    d.ellipse((S(2), S(3), W - S(2), H - S(2)), fill=(7, 14, 27, 180))
    d.ellipse((S(5), S(4), W - S(5), H - S(5)), fill=(255, 255, 255, 255))
    d.ellipse((S(10), S(8), W - S(10), H - S(12)), fill=(226, 238, 255, 255))
    d.arc((S(8), S(6), W - S(8), H - S(8)), 200, 335, fill=(255, 255, 255, 220), width=S(4))
    save(image, "knob", w, h)


def make_track() -> None:
    for name, top, bottom, rim, alpha in (
        ("bar_track", 150, 90, 30, 255),
        ("bar_fill", 255, 210, 60, 255),
    ):
        w, h = 192, 40
        W, H = S(w), S(h)
        layer = bevel(w, h, 20, top, bottom, rim=rim, edge=18, gloss=18, inner_shadow=20, grain=2, base_alpha=alpha)
        save(layer, name, w, h)


def make_pills() -> None:
    save(bevel(320, 96, 48, 255, 212, rim=58, edge=34, gloss=34, inner_shadow=28, grain=4), "pill", 320, 96)
    save(bevel(192, 64, 32, 250, 208, rim=46, edge=26, gloss=24, inner_shadow=22, grain=3), "tag", 192, 64)


def main() -> None:
    os.makedirs(OUT, exist_ok=True)

    save(bevel(512, 512, 64, 255, 206, rim=44, edge=30, gloss=22, inner_shadow=26, grain=8), "panel", 512, 512)
    save(bevel(384, 384, 52, 255, 210, rim=52, edge=28, gloss=24, inner_shadow=22, grain=8), "card", 384, 384)
    save(bevel(384, 384, 52, 250, 214, rim=36, edge=24, gloss=16, inner_shadow=18, grain=6), "card_soft", 384, 384)
    save(bevel(256, 112, 38, 255, 208, rim=62, edge=35, gloss=30, inner_shadow=24, grain=5), "chip", 256, 112)
    save(bevel(512, 176, 48, 255, 232, rim=72, edge=58, gloss=58, inner_shadow=58, grain=4), "button_orange", 512, 176)
    save(bevel(384, 144, 40, 246, 190, rim=52, edge=30, gloss=28, inner_shadow=30, grain=5), "button_dark", 384, 144)
    save(bevel(320, 112, 38, 244, 198, rim=44, edge=26, gloss=20, inner_shadow=20, grain=4), "button_ghost", 320, 112)
    save(bevel(160, 160, 42, 255, 205, rim=58, edge=30, gloss=30, inner_shadow=26, grain=5), "icon_tile", 160, 160)

    make_hex()
    make_brushes()
    make_glow()
    make_spark()
    make_coin()
    make_chest()
    make_knob()
    make_track()
    make_pills()

    print("ArcadeKit generated at", OUT)


if __name__ == "__main__":
    main()
