"""Build static, openly licensed menu fonts for Saksi Terakhir.

Requires fontTools 4.63.0. Upstream font files and OFL licenses are stored in
Upstream/ beside this script. This script never changes Unity scenes or TMP
settings; the Editor importer creates the corresponding TMP assets.
"""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont


ROOT = Path(__file__).resolve().parents[2]
UPSTREAM = Path(__file__).resolve().parent / "Upstream"
OUTPUT = ROOT / "Assets/_Project/UI/Fonts"
SOURCE = OUTPUT / "Source"
WIDTH_SCALE = 0.58


def names(font: TTFont, family: str, style: str, postscript: str) -> None:
    values = {
        1: family,
        2: style,
        3: f"SaksiTerakhir:1.000:{postscript}",
        4: f"{family} {style}",
        5: "Version 1.000",
        6: postscript,
        16: family,
        17: style,
    }
    table = font["name"]
    table.names = [item for item in table.names if item.nameID not in values]
    for name_id, value in values.items():
        table.setName(value, name_id, 3, 1, 0x409)
        table.setName(value, name_id, 1, 0, 0)


def scale_positioning(value: object, visited: set[int]) -> None:
    if isinstance(value, (str, bytes, int, float, bool, type(None))):
        return
    if id(value) in visited:
        return
    visited.add(id(value))
    if isinstance(value, (list, tuple)):
        for item in value:
            scale_positioning(item, visited)
    elif hasattr(value, "__dict__"):
        for key, item in vars(value).items():
            if key in {"XAdvance", "XPlacement", "XCoordinate"} and isinstance(item, int):
                setattr(value, key, round(item * WIDTH_SCALE))
            else:
                scale_positioning(item, visited)


def make_display_font() -> Path:
    font = instantiateVariableFont(
        TTFont(UPSTREAM / "BodoniModa-Variable.ttf"),
        {"wght": 500, "opsz": 72},
        inplace=True,
    )
    glyph_set = font.getGlyphSet()
    glyphs = {}
    for name in font.getGlyphOrder():
        recording = DecomposingRecordingPen(glyph_set)
        glyph_set[name].draw(recording)
        pen = TTGlyphPen(None)
        recording.replay(TransformPen(pen, (WIDTH_SCALE, 0, 0, 1, 0, 0)))
        glyphs[name] = pen.glyph()
    font["glyf"].glyphs = glyphs
    font["hmtx"].metrics = {
        name: (round(advance * WIDTH_SCALE), round(bearing * WIDTH_SCALE))
        for name, (advance, bearing) in font["hmtx"].metrics.items()
    }
    font["OS/2"].xAvgCharWidth = round(font["OS/2"].xAvgCharWidth * WIDTH_SCALE)
    font["OS/2"].usWidthClass = 2
    for table_name in ("fpgm", "prep", "cvt "):
        if table_name in font:
            del font[table_name]
    if "GPOS" in font:
        scale_positioning(font["GPOS"].table, set())
    if "kern" in font:
        for table in font["kern"].kernTables:
            table.kernTable = {
                pair: round(distance * WIDTH_SCALE)
                for pair, distance in table.kernTable.items()
            }
    for glyph in font["glyf"].glyphs.values():
        glyph.recalcBounds(font["glyf"])
    font["hhea"].recalc(font)
    names(font, "Saksi Terakhir Display", "Regular", "SaksiTerakhirDisplay-Regular")
    font["name"].setName(
        "Derived from Bodoni Moda (Copyright 2020 The Bodoni Moda Project Authors). "
        "Static optical-size 72, weight 500; horizontally condensed to 58 percent; "
        "renamed Saksi Terakhir Display. SIL Open Font License 1.1.",
        10, 3, 1, 0x409,
    )
    path = SOURCE / "SaksiTerakhirDisplay-Regular.ttf"
    font.save(path)
    return path


def make_menu_font(italic: bool = False) -> Path:
    suffix = "-Italic" if italic else ""
    font = instantiateVariableFont(
        TTFont(UPSTREAM / f"CormorantGaramond{suffix}-Variable.ttf"),
        {"wght": 500},
        inplace=True,
    )
    style = "Medium Italic" if italic else "Medium"
    postscript = "CormorantGaramond-MediumItalic" if italic else "CormorantGaramond-Medium"
    names(font, "Cormorant Garamond", style, postscript)
    path = SOURCE / f"{postscript}.ttf"
    font.save(path)
    return path


def glyph_characters(path: Path) -> str:
    font = TTFont(path)
    requested = set(range(0x20, 0x7F)) | set(range(0xA0, 0x180))
    requested.update(map(ord, "‘’“”–—…•°©®™€←→↑↓↵‹›◀▶"))
    cmap = font.getBestCmap()
    return "".join(chr(code) for code in sorted(requested) if code in cmap)


def main() -> None:
    SOURCE.mkdir(parents=True, exist_ok=True)
    licensing = OUTPUT / "Licenses"
    licensing.mkdir(parents=True, exist_ok=True)
    display = make_display_font()
    menu = make_menu_font()
    italic = make_menu_font(italic=True)
    for name in ("Bodoni-OFL.txt", "Cormorant-OFL.txt"):
        shutil.copyfile(UPSTREAM / name, licensing / name)
    (SOURCE / "DisplayCharacters.txt").write_text(glyph_characters(display), encoding="utf-8")
    (SOURCE / "MenuCharacters.txt").write_text(glyph_characters(menu), encoding="utf-8")
    (SOURCE / "MenuItalicCharacters.txt").write_text(glyph_characters(italic), encoding="utf-8")
    summary = {
        "generator": "fontTools 4.63.0",
        "display": {"base": "Bodoni Moda", "wght": 500, "opsz": 72, "widthScale": WIDTH_SCALE},
        "menu": {"base": "Cormorant Garamond", "wght": 500},
        "upstreamCommits": {
            "Bodoni": "30ce6cdc354ef179a3b72ba0f0e71826e599348c",
            "Cormorant": "6d210fd3550b7358ca62d6ba3e354ec1ec940813",
        },
        "files": [
            {"name": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
             "unicodeGlyphs": len(TTFont(path).getBestCmap())}
            for path in (display, menu, italic)
        ],
    }
    (OUTPUT / "font-provenance.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
