"""Generate a flat, printed sleeve texture for the generic 18650 cell.

Run with the system Python (Pillow is required). The Blender build script
calls this automatically before rebuilding the battery.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "LiIon_18650_Label.png"
WIDTH, HEIGHT = 1024, 2048

# One blue PVC sleeve: the dark text is painted into the same bitmap, so it
# follows the cylinder's UVs and has no physical offset above the plastic.
texture = Image.new("RGB", (WIDTH, HEIGHT), (17, 84, 190))
draw = ImageDraw.Draw(texture)
for y in range(HEIGHT):
    # Very subtle variation in the wrap, with no metallic stripe or branding.
    light = round(3 * (y / HEIGHT))
    draw.line((0, y, WIDTH, y), fill=(17 + light, 84 + light, 190 + light))

font_path = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
font = ImageFont.truetype(font_path, 72)
text = "+  ICR 18650 2200mAh 3.7V"
box = font.getbbox(text)
horizontal = Image.new("RGBA", (box[2] - box[0] + 30, box[3] - box[1] + 30))
ink = ImageDraw.Draw(horizontal)
ink.text((15 - box[0], 15 - box[1]), text, font=font, fill=(5, 17, 36, 235))
vertical = horizontal.rotate(-90, expand=True, resample=Image.Resampling.BICUBIC)

# Camera used for the preview looks at approximately 300 degrees around the
# cylinder. Keep the print centered there and well clear of the UV seam.
x_center = round(WIDTH * 0.833)
position = (x_center - vertical.width // 2, (HEIGHT - vertical.height) // 2)
texture.paste(vertical, position, vertical)
texture.save(OUTPUT, optimize=True)
print(OUTPUT)
