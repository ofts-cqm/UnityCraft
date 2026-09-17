#!/usr/bin/env python3
"""Generate the shared pixel skin for UnityCraft's runtime-built menus.

The menu layouts scale independently from their artwork, so these are small
9-sliced surfaces rather than full-screen bitmaps. Editable sparse JSON stays
beside this generator; Unity-ready PNGs are the only generated files placed in
Assets.
"""

from __future__ import annotations

import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "TextureSources" / "ui" / "menu" / "_source"
OUTPUT = ROOT / "Assets" / "Textures" / "ui" / "menu"
PYTHON = ROOT / ".pixel-art-venv" / "bin" / "python"
RENDERER = Path("/Users/qianmuchen/.codex/skills/pixel-art-gen-1.0.0/scripts/render_pixel_art.py")

# Eight colors shared with the existing inventory UI. Error and warning text
# remain semantic runtime colors and are deliberately not baked into sprites.
CLEAR = "transparent"
INK = "#0D202B"
DEEP = "#142E3B"
SHADOW = "#1C3A46"
PLATE = "#315664"
MID = "#4E7C81"
LIGHT = "#A8D3C5"
BRASS = "#D5A04E"
BRASS_DARK = "#8E6334"


def grid(width: int = 16, height: int = 16):
    return [[CLEAR for _ in range(width)] for _ in range(height)]


def put(canvas, x: int, y: int, color: str):
    if 0 <= y < len(canvas) and 0 <= x < len(canvas[0]):
        canvas[y][x] = color


def rect(canvas, x: int, y: int, width: int, height: int, color: str):
    for py in range(y, y + height):
        for px in range(x, x + width):
            put(canvas, px, py, color)


def hline(canvas, x: int, y: int, width: int, color: str):
    rect(canvas, x, y, width, 1, color)


def vline(canvas, x: int, y: int, height: int, color: str):
    rect(canvas, x, y, 1, height, color)


def chamfer(canvas):
    for x, y in ((0, 0), (1, 0), (0, 1), (15, 0), (14, 0), (15, 1),
                 (0, 15), (1, 15), (0, 14), (15, 15), (14, 15), (15, 14)):
        put(canvas, x, y, CLEAR)


def framed_surface(fill: str, top: str, left: str, bottom: str, right: str,
                   *, brass_corners: bool = False):
    canvas = grid()
    rect(canvas, 0, 0, 16, 16, INK)
    rect(canvas, 1, 1, 14, 14, fill)
    hline(canvas, 3, 1, 10, top)
    vline(canvas, 1, 3, 10, left)
    hline(canvas, 3, 14, 10, bottom)
    vline(canvas, 14, 3, 10, right)
    chamfer(canvas)
    if brass_corners:
        for x, y in ((2, 1), (13, 1), (1, 2), (14, 2),
                     (1, 13), (14, 13), (2, 14), (13, 14)):
            put(canvas, x, y, BRASS)
    return canvas


def panel():
    return framed_surface(PLATE, LIGHT, MID, SHADOW, DEEP)


def modal_panel():
    canvas = framed_surface(PLATE, LIGHT, MID, SHADOW, DEEP, brass_corners=True)
    put(canvas, 7, 1, BRASS)
    put(canvas, 8, 1, BRASS)
    put(canvas, 7, 14, BRASS_DARK)
    put(canvas, 8, 14, BRASS_DARK)
    return canvas


def inset():
    return framed_surface(DEEP, INK, SHADOW, MID, LIGHT)


def row():
    canvas = framed_surface(DEEP, MID, MID, INK, INK)
    put(canvas, 2, 7, BRASS_DARK)
    put(canvas, 2, 8, BRASS_DARK)
    return canvas


def button(state: str):
    styles = {
        "normal": (PLATE, LIGHT, MID, SHADOW, DEEP, BRASS_DARK),
        "highlighted": (MID, BRASS, LIGHT, DEEP, SHADOW, BRASS),
        "pressed": (DEEP, SHADOW, INK, MID, LIGHT, BRASS_DARK),
        "selected": (PLATE, BRASS, BRASS, BRASS_DARK, BRASS_DARK, BRASS),
        "disabled": (SHADOW, MID, SHADOW, INK, DEEP, SHADOW),
    }
    fill, top, left, bottom, right, accent = styles[state]
    canvas = framed_surface(fill, top, left, bottom, right)
    for x, y in ((2, 7), (2, 8), (13, 7), (13, 8)):
        put(canvas, x, y, accent)
    return canvas


def input_surface(focused: bool):
    canvas = framed_surface(DEEP, BRASS if focused else MID,
                            BRASS if focused else SHADOW, INK, INK)
    put(canvas, 3, 4, LIGHT if focused else MID)
    return canvas


def slider_track():
    canvas = grid(16, 8)
    rect(canvas, 0, 1, 16, 6, INK)
    rect(canvas, 2, 2, 12, 4, DEEP)
    hline(canvas, 3, 2, 10, SHADOW)
    hline(canvas, 3, 5, 10, MID)
    return canvas


def slider_fill():
    canvas = grid(16, 8)
    rect(canvas, 0, 1, 16, 6, BRASS_DARK)
    rect(canvas, 2, 2, 12, 4, BRASS)
    hline(canvas, 3, 2, 10, LIGHT)
    return canvas


def slider_handle():
    canvas = grid(10, 16)
    rect(canvas, 0, 1, 10, 14, INK)
    rect(canvas, 1, 2, 8, 12, BRASS_DARK)
    hline(canvas, 2, 2, 6, LIGHT)
    vline(canvas, 2, 4, 8, BRASS)
    hline(canvas, 2, 13, 6, DEEP)
    for y in (6, 8, 10):
        hline(canvas, 4, y, 3, BRASS)
    return canvas


def pixel_json(canvas):
    pixels = []
    for y, line in enumerate(canvas):
        for x, color in enumerate(line):
            if color != CLEAR:
                pixels.append({"x": x, "y": y, "color": color})
    return {
        "width": len(canvas[0]),
        "height": len(canvas),
        "background": CLEAR,
        "grid_lines": False,
        "pixel_size": 1,
        "pixels": pixels,
    }


def render(name: str, canvas):
    SOURCE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = SOURCE / f"{name}.json"
    output = OUTPUT / f"{name}.png"
    source.write_text(json.dumps(pixel_json(canvas), indent=2) + "\n")
    subprocess.run(
        [str(PYTHON), str(RENDERER), str(source), "-o", str(output), "--no-grid-lines"],
        check=True,
    )


def main():
    render("panel", panel())
    render("modal_panel", modal_panel())
    render("inset", inset())
    render("row", row())
    for state in ("normal", "highlighted", "pressed", "selected", "disabled"):
        render(f"button_{state}", button(state))
    render("input_normal", input_surface(False))
    render("input_focused", input_surface(True))
    render("slider_track", slider_track())
    render("slider_fill", slider_fill())
    render("slider_handle", slider_handle())


if __name__ == "__main__":
    main()
