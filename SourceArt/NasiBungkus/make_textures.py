"""Generate original, reproducible diffuse textures for the nasi-bungkus prop.

Run with ``python3 make_textures.py``. Requires Pillow. All generated graphics
are procedural; the reference photograph is not copied into the game asset.
"""

from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageFilter


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Textures"
OUT.mkdir(parents=True, exist_ok=True)


def smooth_noise(width: int, height: int, cells_x: int, cells_y: int, seed: int) -> Image.Image:
    rng = random.Random(seed)
    pixels = bytes(rng.randrange(256) for _ in range(cells_x * cells_y))
    miniature = Image.frombytes("L", (cells_x, cells_y), pixels)
    return miniature.resize((width, height), Image.Resampling.BICUBIC)


def make_kraft() -> Path:
    width = height = 1024
    rng = random.Random(31814)
    broad = smooth_noise(width, height, 25, 25, 721)
    fine = smooth_noise(width, height, 142, 142, 722)
    out = Image.new("RGB", (width, height))
    dst = out.load()
    a = broad.load()
    b = fine.load()
    for y in range(height):
        for x in range(width):
            variation = (a[x, y] - 128) * 0.090 + (b[x, y] - 128) * 0.037
            grain = rng.randrange(-3, 4)
            dst[x, y] = (
                max(0, min(255, round(171 + variation + grain))),
                max(0, min(255, round(130 + variation * 0.86 + grain))),
                max(0, min(255, round(83 + variation * 0.74 + grain))),
            )

    fibers = Image.new("RGBA", (width, height))
    draw = ImageDraw.Draw(fibers, "RGBA")
    for _ in range(8000):
        x = rng.randrange(width)
        y = rng.randrange(height)
        length = rng.randrange(4, 38)
        dx = rng.randrange(-5, 6)
        color = (104, 77, 47, rng.randrange(7, 22)) if rng.random() < 0.63 else (255, 238, 205, rng.randrange(6, 22))
        draw.line((x, y, x + dx, y + length), fill=color, width=1)
    # Broken diagonal flecks suggest folded paper without baking a specific fold
    # into the material; the model's geometry supplies the visible creases.
    for _ in range(110):
        x = rng.randrange(width)
        y = rng.randrange(height)
        length = rng.randrange(12, 95)
        draw.line((x, y, x + length, y + rng.randrange(-9, 10)), fill=(106, 81, 55, 8), width=1)
    out = Image.alpha_composite(out.convert("RGBA"), fibers).convert("RGB")
    path = OUT / "Kertas_Kraft.png"
    out.save(path, optimize=True)
    return path


def make_leaf() -> Path:
    width, height = 512, 1024
    rng = random.Random(72219)
    broad = smooth_noise(width, height, 32, 63, 9301)
    fine = smooth_noise(width, height, 90, 190, 9302)
    out = Image.new("RGB", (width, height))
    dst = out.load()
    a = broad.load()
    b = fine.load()
    for y in range(height):
        for x in range(width):
            edge = abs((x + 0.5) / width - 0.5) * 2
            central_light = 7 * (1 - edge * edge)
            soft_waves = 2.8 * math.sin(x * 0.031 + y * 0.006)
            var = (a[x, y] - 128) * 0.039 + (b[x, y] - 128) * 0.018
            grain = rng.randrange(-2, 3)
            dst[x, y] = (
                max(0, min(255, round(40 + central_light * 0.32 + soft_waves * 0.4 + var * 0.5 + grain))),
                max(0, min(255, round(105 + central_light + soft_waves + var + grain))),
                max(0, min(255, round(57 + central_light * 0.48 + soft_waves * 0.7 + var * 0.7 + grain))),
            )

    veins = Image.new("RGBA", (width, height))
    draw = ImageDraw.Draw(veins, "RGBA")
    # Fine vertical, slightly wandering leaf fibers. Image Y follows the strip's
    # length; the central midrib is intentionally subdued on a narrow cut strip.
    for x0 in range(6, width - 6, 11):
        segments = []
        phase = rng.random() * math.tau
        for y in range(-8, height + 9, 32):
            segments.append((x0 + math.sin(y / 127 + phase) * 1.6, y))
        pale = rng.random() < 0.5
        color = (216, 229, 128, rng.randrange(12, 30)) if pale else (4, 49, 30, rng.randrange(15, 34))
        draw.line(segments, fill=color, width=1)
    draw.line((width // 2, 0, width // 2, height), fill=(183, 205, 109, 28), width=3)
    for y0 in range(20, height + 20, 75):
        for direction in (-1, 1):
            end_x = width // 2 + direction * rng.randrange(130, 240)
            end_y = y0 + rng.randrange(-32, 33)
            draw.line((width // 2, y0, end_x, end_y), fill=(214, 227, 151, 16), width=1)
    out = Image.alpha_composite(out.convert("RGBA"), veins).convert("RGB")
    path = OUT / "Pita_Daun_Pisang.png"
    out.save(path, optimize=True)
    return path


def font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size)


def centered(draw: ImageDraw.ImageDraw, text: str, y: int, typeface: ImageFont.FreeTypeFont, color: tuple[int, int, int, int]) -> None:
    box = draw.textbbox((0, 0), text, font=typeface)
    x = (1024 - (box[2] - box[0])) // 2 - box[0]
    draw.text((x, y - box[1]), text, font=typeface, fill=color)


def make_sticker() -> Path:
    size = 1024
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img, "RGBA")
    dark_green = (33, 65, 42, 255)
    gold = (168, 132, 67, 255)
    cream = (235, 218, 166, 255)
    draw.ellipse((13, 13, 1011, 1011), fill=dark_green)
    draw.ellipse((35, 35, 989, 989), fill=cream)
    draw.ellipse((60, 60, 964, 964), outline=gold, width=16)
    draw.ellipse((85, 85, 939, 939), outline=dark_green, width=6)

    # Very subtle flecking remains confined to the sticker's cream face.
    fleck = Image.new("RGBA", (size, size))
    fd = ImageDraw.Draw(fleck, "RGBA")
    rng = random.Random(78002)
    for _ in range(4400):
        x, y = rng.randrange(100, 924), rng.randrange(100, 924)
        if (x - 512) ** 2 + (y - 512) ** 2 < 395**2:
            fd.point((x, y), fill=(124, 99, 49, rng.randrange(6, 19)))
    img = Image.alpha_composite(img, fleck)
    draw = ImageDraw.Draw(img, "RGBA")

    bold = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"
    regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    centered(draw, "NASI", 315, font(bold, 123), dark_green)
    centered(draw, "UDUK", 453, font(bold, 156), dark_green)
    draw.line((255, 628, 769, 628), fill=gold, width=10)
    # Three plain rice grains, used as decoration rather than a product logo.
    for x, rotation in ((463, -23), (512, 0), (561, 23)):
        grain = Image.new("RGBA", (52, 90))
        ImageDraw.Draw(grain, "RGBA").ellipse((14, 5, 38, 84), fill=gold)
        grain = grain.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
        img.alpha_composite(grain, (x - grain.width // 2, 692 - grain.height // 2))

    # Keep transparent corners: the texture maps to a round sticker mesh.
    path = OUT / "Stiker_Nasi_Uduk.png"
    img.save(path, optimize=True)
    return path


if __name__ == "__main__":
    for generated in (make_kraft(), make_leaf(), make_sticker()):
        print(generated)
