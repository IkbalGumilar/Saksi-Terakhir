"""Generate repeatable rope and paper-band textures with system Python/Pillow."""

from pathlib import Path
import math
import random

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
TEXTURES = ROOT / "Textures"
TEXTURES.mkdir(parents=True, exist_ok=True)
SIZE = 512


def clamp(value):
    return max(0, min(255, round(value)))


def fiber_tile(filename, base, amplitude, seed, nylon=False):
    rng = random.Random(seed)
    image = Image.new("RGB", (SIZE, SIZE))
    pixels = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            # Integer cycles across both axes make the braid repeat cleanly.
            twist = 2 * math.pi * (3*x/SIZE + 7*y/SIZE)
            ridge = math.sin(twist) + .35 * math.sin(2*twist + .3)
            fine = 2.5 * math.sin(2 * math.pi * (16*x/SIZE + 23*y/SIZE))
            grain = rng.uniform(-5, 5)
            level = amplitude * ridge + fine + grain
            if nylon:
                pixels[x, y] = (clamp(base[0] + level*.65),
                                clamp(base[1] + level),
                                clamp(base[2] + level*.87))
            else:
                pixels[x, y] = tuple(clamp(c + level) for c in base)
    if nylon:
        draw = ImageDraw.Draw(image)
        for _ in range(70):
            x = rng.randrange(SIZE)
            y = rng.randrange(SIZE)
            length = rng.randrange(12, 24)
            color = (rng.randrange(150, 190), rng.randrange(43, 70),
                     rng.randrange(36, 58))
            draw.line((x, y, x+length, y-length//3), fill=color,
                      width=rng.choice((6, 7, 8)))
    image.save(TEXTURES / filename, optimize=True)


fiber_tile("Tali_Tambang_Serat.png", (164, 132, 89), 15, 104)
fiber_tile("Tali_Nilon_Hijau_Merah.png", (18, 85, 72), 14, 308, nylon=True)


# The photographed 10 m nylon bundle has a handwritten paper label. This
# bitmap is wrapped onto the band, so its lettering never floats above it.
paper = Image.new("RGB", (1024, 256), (222, 224, 216))
draw = ImageDraw.Draw(paper)
rng = random.Random(713)
for _ in range(18000):
    x = rng.randrange(1024)
    y = rng.randrange(256)
    shade = rng.randrange(-8, 9)
    paper.putpixel((x, y), tuple(clamp(c+shade) for c in (222, 224, 216)))
font_file = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf"
font = ImageFont.truetype(font_file, 50)
draw.text((450, 34), "8 mm", font=font, fill=(30, 55, 98))
draw.text((450, 137), "10 m", font=font, fill=(30, 55, 98))
paper.save(TEXTURES / "Pita_Kertas_Nilon_10m.png", optimize=True)
print("Generated rope textures:", TEXTURES)
