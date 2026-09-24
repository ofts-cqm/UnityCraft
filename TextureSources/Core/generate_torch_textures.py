#!/usr/bin/env python3
"""Generate the three 16x16 atlas maps for a thin voxel torch.

The art is transparent outside a 2px by 12px stick.  The flame intentionally
widens to 4px above it, creating the familiar blocky crown without turning the
torch into a full opaque cube.  Keeping the generator beside its source PNGs
makes silhouette and palette revisions reproducible.
"""

from pathlib import Path

from PIL import Image, ImageColor


SIZE = 16
OUTPUT = Path(__file__).resolve().parent


def tile(fill: str) -> list[list[str]]:
    return [[fill for _ in range(SIZE)] for _ in range(SIZE)]


def set_pixels(grid: list[list[str]], pixels: dict[str, list[tuple[int, int]]]) -> None:
    for colour, points in pixels.items():
        for x, y in points:
            grid[y][x] = colour


def save(name: str, grid: list[list[str]]) -> None:
    image = Image.new("RGBA", (SIZE, SIZE))
    image.putdata([ImageColor.getcolor(pixel, "RGBA") for row in grid for pixel in row])
    image.save(OUTPUT / f"{name}.png")


def torch_side() -> list[list[str]]:
    grid = tile("#00000000")
    # A 2px shaft, exactly twelve pixels high (y=4 through y=15).
    set_pixels(grid, {
        "#2D2425": [(7, y) for y in range(4, 16)],
        "#6B432C": [(8, y) for y in range(4, 16)],
        "#B86A32": [(8, y) for y in (6, 10, 14)],
        # Four-pixel flame crown: the wider shoulder intentionally projects
        # beyond the stick's 2px silhouette.
        "#8F3025": [(7, 0), (8, 0), (6, 1), (9, 1), (6, 2), (9, 2), (6, 3), (9, 3), (7, 4), (8, 4)],
        "#E66A25": [(7, 1), (8, 1), (7, 2), (8, 2), (7, 3), (8, 3)],
        "#FFB23D": [(7, 2), (8, 2), (7, 3), (8, 3)],
        "#FFF0A3": [(7, 3), (8, 3)],
    })
    return grid


def torch_top() -> list[list[str]]:
    grid = tile("#00000000")
    # The four-pixel ember cap repeats the side face's wide crown, while its
    # 2px bright core aligns with the shaft below.
    set_pixels(grid, {
        "#8F3025": [(x, y) for y in range(6, 10) for x in range(6, 10)],
        "#E66A25": [(x, y) for y in range(7, 9) for x in range(6, 10)],
        "#FFB23D": [(7, 7), (8, 7), (7, 8), (8, 8)],
        "#FFF0A3": [(7, 8), (8, 8)],
    })
    return grid


def torch_bottom() -> list[list[str]]:
    grid = tile("#00000000")
    # A compact 2px end-grain cap, matching the shaft width exactly.
    set_pixels(grid, {
        "#2D2425": [(7, 7), (7, 8)],
        "#6B432C": [(8, 7), (8, 8)],
    })
    return grid


if __name__ == "__main__":
    save("torch_side", torch_side())
    save("torch_top", torch_top())
    save("torch_bottom", torch_bottom())
