"""Original, deterministic texture maps for the blue A5 agenda.

Run with system Python and Pillow. No reference-photo pixels are reused.
Images use the usual UV orientation: image top corresponds to V=1.
"""

from pathlib import Path
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Textures"


def noise(size, cells, seed):
    rng = random.Random(seed)
    return Image.frombytes("L", cells, bytes(rng.randrange(256) for _ in range(cells[0] * cells[1]))).resize(size, Image.Resampling.BICUBIC)


def leather():
    size = (1024, 1024)
    broad = noise(size, (13, 13), 39204).load()
    medium = noise(size, (63, 63), 62123).load()
    grain_map = noise(size, (540, 540), 10327)
    fine = grain_map.load()
    rng = random.Random(14287)
    albedo = Image.new("RGB", size)
    height = Image.new("L", size)
    roughness = Image.new("L", size)
    a, h, r = albedo.load(), height.load(), roughness.load()
    for y in range(size[1]):
        for x in range(size[0]):
            b = broad[x, y] - 128
            m = medium[x, y] - 128
            g = fine[x, y] - 128
            lum = b * 0.080 + m * 0.052 + g * 0.030 + rng.uniform(-1.2, 1.2)
            a[x, y] = tuple(round(max(0, min(255, base + lum * factor))) for base, factor in ((63, .74), (122, .93), (168, 1)))
            h[x, y] = round(max(0, min(255, 128 + g * .48 + m * .035)))
            r[x, y] = round(max(0, min(255, 173 + g * .060 + b * .025)))
    # Minute pores are material texture, not sculpted cracks or folds.
    pore_layer = Image.new("RGBA", size)
    p = ImageDraw.Draw(pore_layer, "RGBA")
    for _ in range(13500):
        x, y = rng.randrange(1024), rng.randrange(1024)
        p.line((x, y, x + rng.choice((0, 1, 2)), y + 1), fill=(18, 40, 64, rng.randrange(8, 20)), width=1)
    albedo = Image.alpha_composite(albedo.convert("RGBA"), pore_layer).convert("RGB")
    albedo.save(OUT / "Kulit_Biru_BaseColor.png", optimize=True)
    height.save(OUT / "Kulit_Biru_Height.png", optimize=True)
    roughness.save(OUT / "Kulit_Biru_Roughness.png", optimize=True)


def paper():
    size = (1024, 1448)
    base = Image.new("RGB", size)
    pix = base.load()
    broad = noise(size, (35, 49), 99713).load()
    rng = random.Random(63474)
    for y in range(size[1]):
        for x in range(size[0]):
            delta = (broad[x, y] - 128) * .011 + rng.uniform(-.9, .9)
            pix[x, y] = (round(247 + delta), round(241 + delta), round(221 + delta))
    # A second blank map is useful for pasted endpapers and cut sheet edges.
    base.save(OUT / "Kertas_Krem_Polos.png", optimize=True)
    draw = ImageDraw.Draw(base)
    ink = (136, 142, 136)
    lines = (208, 207, 187)
    faint = (178, 182, 168)
    regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    small = ImageFont.truetype(regular, 13)
    tiny = ImageFont.truetype(regular, 10)
    header = ImageFont.truetype(regular, 14)
    # Blank printed controls at top left; no dates, story prose, or ownership.
    for i in range(3):
        xx = 81 + 24 * i
        draw.rectangle((xx, 76, xx + 14, 90), outline=faint, width=1)
    for i, day in enumerate(("Mo", "Tu", "We", "Th", "Fr", "Sa", "Su")):
        xx = 80 + i * 27
        draw.rectangle((xx, 105, xx + 27, 125), outline=faint, width=1)
        draw.text((xx + 4, 109), day, font=tiny, fill=ink)
    draw.text((717, 74), "Memo No.", font=header, fill=ink)
    draw.line((791, 92, 942, 92), fill=faint, width=1)
    draw.text((717, 104), "Date", font=header, fill=ink)
    draw.line((763, 122, 942, 122), fill=faint, width=1)
    draw.text((819, 105), "/", font=small, fill=faint)
    draw.text((880, 105), "/", font=small, fill=faint)
    draw.line((79, 144, 944, 144), fill=faint, width=2)
    for y in range(202, 1320, 50):
        draw.line((80, y, 944, y), fill=lines, width=1)
    draw.line((80, 1360, 944, 1360), fill=faint, width=1)
    base.save(OUT / "Kertas_Agenda_Bergaris.png", optimize=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    leather()
    paper()
    for path in sorted(OUT.glob("*.png")):
        with Image.open(path) as img:
            print(f"{path.name}: {img.width} x {img.height} ({img.mode})")


if __name__ == "__main__":
    main()
