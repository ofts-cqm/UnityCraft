"""Generate matching snow and snowy grass-side atlas tiles.

The grass-side variant preserves every dirt pixel from grass_side.png so a
snow-covered grass block keeps the same side texture below its snow fringe.
"""

from pathlib import Path

from PIL import Image


CORE = Path(__file__).resolve().parent
SIZE = 16

# A restrained cool-white palette keeps snow readable beside the existing
# saturated grass and warm dirt without making the top look flat.
SNOW = {
    "base": (222, 235, 238, 255),
    "light": (241, 248, 246, 255),
    "bright": (251, 253, 248, 255),
    "shade": (189, 211, 219, 255),
    "deep": (164, 190, 202, 255),
}


def paint(image, color, points):
    for x, y in points:
        image.putpixel((x, y), SNOW[color])


def make_snow():
    image = Image.new("RGBA", (SIZE, SIZE), SNOW["base"])
    paint(image, "light", [
        (1, 0), (2, 0), (8, 0), (9, 0), (13, 0),
        (0, 1), (1, 1), (6, 1), (7, 1), (13, 1), (14, 1),
        (3, 2), (4, 2), (10, 2), (11, 2), (15, 2),
        (3, 3), (9, 3), (10, 3), (0, 4), (1, 4), (6, 4),
        (7, 4), (13, 4), (14, 4), (4, 5), (5, 5), (11, 5),
        (12, 5), (1, 6), (2, 6), (8, 6), (9, 6), (15, 6),
        (0, 7), (6, 7), (7, 7), (12, 7), (13, 7),
        (3, 8), (4, 8), (10, 8), (11, 8), (15, 8),
        (1, 9), (2, 9), (7, 9), (8, 9), (13, 9), (14, 9),
        (4, 10), (5, 10), (10, 10), (11, 10), (0, 11),
        (1, 11), (7, 11), (8, 11), (14, 11), (15, 11),
        (3, 12), (4, 12), (10, 12), (11, 12),
        (0, 13), (1, 13), (6, 13), (7, 13), (13, 13),
        (14, 13), (4, 14), (5, 14), (10, 14), (11, 14),
        (1, 15), (2, 15), (8, 15), (9, 15), (15, 15),
    ])
    paint(image, "bright", [
        (1, 0), (7, 1), (3, 2), (14, 4), (5, 5),
        (9, 6), (6, 7), (4, 8), (13, 9), (1, 11),
        (11, 12), (7, 13), (4, 14), (2, 15),
    ])
    paint(image, "shade", [
        (5, 0), (6, 0), (11, 0), (12, 1), (2, 2),
        (7, 3), (8, 3), (14, 3), (3, 4), (10, 4),
        (0, 5), (7, 5), (14, 5), (5, 6), (11, 6),
        (3, 7), (9, 7), (14, 7), (1, 8), (7, 8),
        (12, 8), (5, 9), (10, 9), (15, 9), (2, 10),
        (8, 10), (13, 10), (4, 11), (11, 11),
        (15, 12), (2, 13), (9, 13), (5, 14),
        (12, 14), (0, 15), (6, 15), (13, 15),
    ])
    paint(image, "deep", [
        (6, 0), (14, 3), (0, 5), (11, 6), (3, 7),
        (12, 8), (2, 10), (15, 12), (13, 15),
    ])
    image.save(CORE / "snow.png")
    return image


def make_snowy_grass_side(snow):
    image = Image.open(CORE / "grass_side.png").convert("RGBA")
    if image.size != (SIZE, SIZE):
        raise ValueError("grass_side.png must be 16x16")
    for y in range(SIZE):
        for x in range(SIZE):
            red, green, blue, alpha = image.getpixel((x, y))
            if alpha == 255 and green > red and green > blue:
                color = snow.getpixel((x, y))
                # A slightly shaded lower lip gives the snow fringe depth.
                if y >= 5 and color == SNOW["base"]:
                    color = SNOW["shade"]
                image.putpixel((x, y), color)
    image.save(CORE / "grass_side_snow.png")


if __name__ == "__main__":
    make_snowy_grass_side(make_snow())
