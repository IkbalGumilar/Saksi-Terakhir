"""Generate the printed, fabric-like lid and plain vinyl textures for P3K.

The 1024-square lid texture covers the entire rectangular top using UVs from
(0, 0) at its lower-left corner to (1, 1) at its upper-right corner. The
cross is pre-compensated for a roughly 1.55:1 physical lid aspect ratio, so
it remains close to square when that rectangular surface uses the full UV.

Run from anywhere with ``python3 SourceArt/P3K/make_p3k_textures.py``.
Requires Pillow only; the random texture is deterministic.
"""

from __future__ import annotations

import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageFilter


SIZE = 1024
HERE = Path(__file__).resolve().parent
OUTPUT = HERE / "Textures"
RED = (185, 43, 37)
WHITE = (242, 241, 239)


def _soft_grain(seed: int) -> Image.Image:
    """Near-white printed vinyl with broad tonal variation and a fine grain."""
    rng = random.Random(seed)
    broad = Image.new("L", (64, 64))
    broad.putdata([rng.randrange(116, 141) for _ in range(64 * 64)])
    broad = broad.resize((SIZE, SIZE), Image.Resampling.BICUBIC)
    fine = Image.new("L", (256, 256))
    fine.putdata([rng.randrange(112, 145) for _ in range(256 * 256)])
    fine = fine.resize((SIZE, SIZE), Image.Resampling.BILINEAR)

    broad_tone = Image.merge("RGB", tuple(broad.point(
        lambda v, offset=offset: max(0, min(255, v + offset))
    ) for offset in (1, 0, -2)))
    fine_tone = Image.merge("RGB", (fine, fine, fine))
    base = Image.new("RGB", (SIZE, SIZE), WHITE)
    base = Image.blend(base, broad_tone, 0.055)
    base = Image.blend(base, fine_tone, 0.018)
    return base


def _add_wrinkles(base: Image.Image, seed: int, *, dense: bool) -> Image.Image:
    """Subtle, low-relief highlights/shadows like loose white PVC cloth."""
    rng = random.Random(seed)
    shade = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ink = ImageDraw.Draw(shade)

    if dense:
        # Narrow triangular folds gather at the stitched corners.
        for corner_x in (40, SIZE - 40):
            direction = 1 if corner_x < SIZE / 2 else -1
            for i in range(7):
                y0 = 50 + i * 35
                reach = rng.randint(110, 245)
                finish = (corner_x + direction * reach, y0 + rng.randint(10, 170))
                kink = (corner_x + direction * (reach * 0.52), (y0 + finish[1]) * 0.5)
                ink.line([(corner_x, y0), kink, finish], fill=(110, 108, 106, 18), width=4)
                ink.line([(corner_x, y0 + 5), (kink[0], kink[1] + 4),
                          (finish[0], finish[1] + 3)], fill=(255, 255, 255, 22), width=4)
        for _ in range(11):
            x = rng.randrange(85, SIZE - 85)
            y = rng.choice((rng.randrange(65, 200), rng.randrange(790, 960)))
            length = rng.randrange(45, 125)
            ink.line([(x, y), (x + rng.randint(-35, 35), y + length)],
                     fill=(110, 108, 106, 10), width=3)

    shade = shade.filter(ImageFilter.GaussianBlur(3.0 if dense else 5.0))
    result = Image.alpha_composite(base.convert("RGBA"), shade)
    return result.convert("RGB")


def _plus(cx: float, cy: float, width: float, height: float) -> list[tuple[int, int]]:
    """Outline of one orthogonal cross, with physical arm widths preserved."""
    arm_x = width / 3
    arm_y = height / 3
    return [
        (round(cx - arm_x / 2), round(cy - height / 2)),
        (round(cx + arm_x / 2), round(cy - height / 2)),
        (round(cx + arm_x / 2), round(cy - arm_y / 2)),
        (round(cx + width / 2), round(cy - arm_y / 2)),
        (round(cx + width / 2), round(cy + arm_y / 2)),
        (round(cx + arm_x / 2), round(cy + arm_y / 2)),
        (round(cx + arm_x / 2), round(cy + height / 2)),
        (round(cx - arm_x / 2), round(cy + height / 2)),
        (round(cx - arm_x / 2), round(cy + arm_y / 2)),
        (round(cx - width / 2), round(cy + arm_y / 2)),
        (round(cx - width / 2), round(cy - arm_y / 2)),
        (round(cx - arm_x / 2), round(cy - arm_y / 2)),
    ]


def _lid_print(base: Image.Image) -> Image.Image:
    """Flush red cross, thin separated outline and P.P.P.K. ink on the cloth."""
    aa = 2
    marks = Image.new("RGBA", (SIZE * aa, SIZE * aa), (0, 0, 0, 0))
    draw = ImageDraw.Draw(marks)
    cx, cy = 512, 410
    for width, height, color in (
        (270, 400, RED),
        (251, 381, WHITE),
        (232, 362, RED),
    ):
        pts = [(x * aa, y * aa) for x, y in _plus(cx, cy, width, height)]
        draw.polygon(pts, fill=(*color, 249))

    font_path = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    font = ImageFont.truetype(font_path, 61 * aa)
    label = "P.P.P.K."
    bbox = draw.textbbox((0, 0), label, font=font)
    label_width = bbox[2] - bbox[0]
    label_x = (SIZE * aa - label_width) / 2 - bbox[0]
    label_y = 650 * aa - bbox[1]
    draw.text((int(label_x), int(label_y)), label, font=font, fill=(*RED, 249))

    marks = marks.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    return Image.alpha_composite(base.convert("RGBA"), marks).convert("RGB")


def _stitch_lid(base: Image.Image) -> Image.Image:
    """Two rim seams and tiny interrupted thread marks inset from the edge."""
    overlay = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((19, 19, SIZE - 20, SIZE - 20), radius=22,
                           outline=(119, 118, 116, 30), width=5)
    draw.rounded_rectangle((27, 27, SIZE - 28, SIZE - 28), radius=19,
                           outline=(255, 255, 255, 130), width=3)
    for x in range(45, SIZE - 44, 18):
        for y in (41, SIZE - 42):
            draw.line((x, y, x + 8, y), fill=(136, 134, 130, 42), width=2)
    for y in range(45, SIZE - 44, 18):
        for x in (41, SIZE - 42):
            draw.line((x, y, x, y + 8), fill=(136, 134, 130, 42), width=2)
    draw.rounded_rectangle((48, 48, SIZE - 49, SIZE - 49), radius=11,
                           outline=(131, 129, 126, 16), width=2)
    return Image.alpha_composite(base.convert("RGBA"), overlay).convert("RGB")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    vinyl = _soft_grain(4728)
    vinyl = _add_wrinkles(vinyl, 9401, dense=False)
    vinyl.save(OUTPUT / "P3K_Vinyl_1024.png", optimize=True)

    lid = _soft_grain(1729)
    lid = _add_wrinkles(lid, 6831, dense=True)
    lid = _lid_print(lid)
    lid = _stitch_lid(lid)
    lid.save(OUTPUT / "P3K_Lid_1024.png", optimize=True)
    print(OUTPUT / "P3K_Lid_1024.png")
    print(OUTPUT / "P3K_Vinyl_1024.png")


if __name__ == "__main__":
    main()
