"""Generate original 16x16 bucket item sprites for UnityCraft.

The sprites use a compact cool-metal palette and transparent background.
`water_bucket.png` shares the same bucket silhouette and adds a visibly
contained blue water surface, so it reads clearly in the inventory.
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


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    draw_bucket(water=False).save(OUTPUT / "bucket.png")
    draw_bucket(water=True).save(OUTPUT / "water_bucket.png")


if __name__ == "__main__":
    main()
