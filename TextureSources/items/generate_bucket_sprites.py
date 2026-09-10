"""Generate original 16x16 item sprites for UnityCraft.

The sprites use a compact cool-metal palette and transparent background.
`water_bucket.png` shares the same bucket silhouette and adds a visibly
contained blue water surface. The search and backpack silhouettes use the
same dark outline so all four icons read as one item set.
"""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets" / "Textures" / "items"
SIZE = 16

TRANSPARENT = (0, 0, 0, 0)
OUTLINE = "#293747"
METAL_DARK = "#536978"
METAL = "#91a8b2"
METAL_LIGHT = "#c8d7d4"
WATER_DARK = "#1d668c"
WATER = "#328fbe"
WATER_LIGHT = "#75c8dd"
SEARCH_DARK = "#283a4e"
SEARCH = "#829ead"
SEARCH_LIGHT = "#d1e2dd"
PACK_DARK = "#34334c"
PACK_SHADOW = "#6b4d65"
PACK = "#b97078"
PACK_LIGHT = "#e8aaa0"


def draw_bucket(water: bool) -> Image.Image:
    """Return one 16x16 item sprite; water controls its contained fill."""
    image = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
    pixel = image.load()

    def put(points: list[tuple[int, int]], color: str) -> None:
        for x, y in points:
            pixel[x, y] = (*bytes.fromhex(color[1:]), 255)

    # Curved handle: deliberately broken at the bottom so it sits behind the pail.
    put([(6, 2), (7, 2), (8, 2), (9, 2), (5, 3), (10, 3), (4, 4), (11, 4),
         (3, 5), (12, 5), (3, 6), (12, 6), (3, 7), (12, 7), (3, 8), (12, 8)], OUTLINE)
    put([(6, 3), (7, 3), (8, 3), (9, 3), (5, 4), (10, 4), (4, 5), (11, 5),
         (4, 6), (11, 6), (4, 7), (11, 7)], METAL)
    put([(6, 3), (7, 3), (5, 4), (4, 5), (4, 6)], METAL_LIGHT)

    # Tapered pail silhouette, one dark side and one small rim highlight.
    put([(4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8), (11, 8),
         (4, 9), (11, 9), (4, 10), (11, 10), (5, 11), (10, 11), (5, 12),
         (10, 12), (6, 13), (7, 13), (8, 13), (9, 13)], OUTLINE)
    put([(5, 9), (6, 9), (7, 9), (8, 9), (9, 9), (10, 9), (5, 10), (6, 10),
         (7, 10), (8, 10), (9, 10), (10, 10), (6, 11), (7, 11), (8, 11),
         (9, 11), (6, 12), (7, 12), (8, 12), (9, 12)], METAL)
    put([(5, 9), (5, 10), (6, 10), (6, 11), (6, 12)], METAL_LIGHT)
    put([(10, 9), (10, 10), (9, 11), (9, 12)], METAL_DARK)

    if water:
        put([(5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8)], WATER_DARK)
        put([(6, 8), (7, 8), (8, 8)], WATER_LIGHT)
        put([(5, 9), (6, 9), (7, 9), (8, 9), (9, 9), (10, 9), (6, 10),
             (7, 10), (8, 10), (9, 10)], WATER)
        put([(5, 9), (6, 9), (7, 9), (6, 10)], WATER_LIGHT)
        put([(10, 9), (9, 10)], WATER_DARK)

    return image


def draw_search() -> Image.Image:
    """Return a magnifying-glass inventory icon with a diagonal handle."""
    image = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
    pixel = image.load()

    def put(points: list[tuple[int, int]], color: str) -> None:
        for x, y in points:
            pixel[x, y] = (*bytes.fromhex(color[1:]), 255)

    # Round lens with a cool glass interior and an unambiguous handle.
    put([(5, 2), (6, 2), (7, 2), (8, 2), (3, 3), (4, 3), (9, 3), (10, 3),
         (2, 4), (11, 4), (2, 5), (11, 5), (2, 6), (11, 6), (3, 7), (4, 7),
         (9, 7), (10, 7), (5, 8), (6, 8), (7, 8), (8, 8), (9, 9), (10, 9),
         (10, 10), (11, 10), (11, 11), (12, 11), (12, 12), (13, 12), (13, 13)], SEARCH_DARK)
    put([(5, 3), (6, 3), (7, 3), (8, 3), (4, 4), (5, 4), (6, 4), (7, 4),
         (8, 4), (9, 4), (3, 5), (4, 5), (5, 5), (6, 5), (7, 5), (8, 5),
         (9, 5), (10, 5), (3, 6), (4, 6), (5, 6), (6, 6), (7, 6), (8, 6),
         (9, 6), (10, 6), (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7)], SEARCH)
    put([(5, 3), (6, 3), (4, 4), (5, 4), (4, 5), (5, 5)], SEARCH_LIGHT)
    put([(9, 10), (10, 11), (11, 12), (12, 13)], SEARCH)
    put([(10, 10), (11, 11), (12, 12)], SEARCH_LIGHT)
    return image


def draw_backpack() -> Image.Image:
    """Return a small canvas backpack with flap, side pockets, and straps."""
    image = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
    pixel = image.load()

    def put(points: list[tuple[int, int]], color: str) -> None:
        for x, y in points:
            pixel[x, y] = (*bytes.fromhex(color[1:]), 255)

    # Straps and rounded outline give the pack a clear wearable shape.
    put([(6, 2), (7, 2), (8, 2), (9, 2), (5, 3), (10, 3), (4, 4), (11, 4),
         (3, 5), (12, 5), (3, 6), (12, 6), (3, 7), (12, 7), (2, 8), (3, 8),
         (12, 8), (13, 8), (2, 9), (13, 9), (3, 10), (12, 10), (3, 11),
         (12, 11), (4, 12), (11, 12), (4, 13), (5, 13), (6, 13), (7, 13),
         (8, 13), (9, 13), (10, 13), (11, 13)], PACK_DARK)
    put([(6, 3), (7, 3), (8, 3), (9, 3), (5, 4), (6, 4), (7, 4), (8, 4),
         (9, 4), (10, 4), (4, 5), (5, 5), (6, 5), (7, 5), (8, 5), (9, 5),
         (10, 5), (11, 5), (4, 6), (5, 6), (6, 6), (7, 6), (8, 6), (9, 6),
         (10, 6), (11, 6), (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7),
         (10, 7), (11, 7), (4, 8), (5, 8), (10, 8), (11, 8), (4, 9), (5, 9),
         (6, 9), (7, 9), (8, 9), (9, 9), (10, 9), (11, 9), (4, 10), (5, 10),
         (6, 10), (7, 10), (8, 10), (9, 10), (10, 10), (11, 10), (4, 11),
         (5, 11), (6, 11), (7, 11), (8, 11), (9, 11), (10, 11), (11, 11),
         (5, 12), (6, 12), (7, 12), (8, 12), (9, 12), (10, 12)], PACK)
    put([(5, 5), (6, 5), (7, 5), (8, 5), (5, 6), (6, 6)], PACK_LIGHT)
    put([(5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8)], PACK_SHADOW)
    put([(3, 9), (4, 9), (3, 10), (4, 10), (11, 9), (12, 9), (11, 10), (12, 10)], PACK_SHADOW)
    put([(10, 6), (10, 7), (10, 10), (10, 11), (9, 12)], PACK_SHADOW)
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    draw_bucket(water=False).save(OUTPUT / "bucket.png")
    draw_bucket(water=True).save(OUTPUT / "water_bucket.png")
    draw_search().save(OUTPUT / "search.png")
    draw_backpack().save(OUTPUT / "backpack.png")


if __name__ == "__main__":
    main()
