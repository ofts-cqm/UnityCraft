"""Measure temporal shadow noise in LightingValidationRun captures (requires Pillow).

Sum of absolute frame changes minus net first-to-last change measures reversals:
smooth, monotonic shadow movement contributes zero, while flashing contributes twice
its return excursion. The frozen-sun sequence measures unrelated rendering noise.
The crops exclude the sky, tower and HUD in the checked-in shadow fixture.
Morning shadows recede and brighten the ground; evening shadows extend and darken
it. Changes in the opposite direction quantify unwanted edge oscillation separately
from the intended shadow movement. Values are 8-bit image luminance, not linear light.
"""

import argparse
import json
from pathlib import Path

from PIL import Image


def measure(directory, phase):
    paths = sorted(directory.glob(f"shadow-{phase}-*.png"))
    if len(paths) != 90:
        raise ValueError(f"{directory}/{phase}: expected 90 frames, got {len(paths)}")
    first = previous = None
    variation = 0
    peak_step = 0
    large_steps = 0
    reverse_steps = 0
    peak_reverse = 0
    crop = (500, 320, 1190, 415) if phase == "evening" else (80, 320, 790, 415)
    for path in paths:
        with Image.open(path) as image:
            if image.size != (1280, 720):
                raise ValueError(f"Unexpected capture dimensions: {image.size}")
            current = image.convert("L").crop(crop).tobytes()
        if first is None:
            first = current
        else:
            signed = [a - b for a, b in zip(current, previous)]
            changes = [abs(change) for change in signed]
            reversed_changes = signed if phase == "evening" else [-change for change in signed]
            reverse_steps += sum(change >= 4 for change in reversed_changes)
            peak_reverse = max(peak_reverse, max(reversed_changes))
            variation += sum(changes)
            peak_step = max(peak_step, max(changes))
            large_steps += sum(change >= 8 for change in changes)
        previous = current
    net = sum(abs(a - b) for a, b in zip(first, previous))
    samples = len(first) * (len(paths) - 1)
    return {
        "reversal_per_pixel_frame": (variation - net) / samples,
        "mean_absolute_step": variation / samples,
        "peak_step": peak_step,
        "steps_at_least_8": large_steps,
        "reverse_steps_at_least_4": reverse_steps,
        "peak_reverse_step": peak_reverse,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+", type=Path)
    args = parser.parse_args()
    print(json.dumps({str(directory): {
        phase: measure(directory, phase)
        for phase in ("frozen", "morning", "midmorning", "evening")
    } for directory in args.directories}, indent=2))
