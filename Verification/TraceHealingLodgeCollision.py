"""Trace the user's red medical lodge boundary; leave the artwork untouched.

Usage: python Verification/TraceHealingLodgeCollision.py annotation.png
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from TraceManorCollision import simplify, trace_open


def main():
    project = Path(__file__).resolve().parent.parent
    source = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(int)
    r, g, b = source[:, :, 0], source[:, :, 1], source[:, :, 2]
    red = (r > 180) & (g < 130) & (b < 150) & (r - g > 85) & (r - b > 70) & (abs(g - b) < 22)
    labels, _ = ndimage.label(red, np.ones((3, 3)))

    def stroke(seed, start):
        x, y = seed
        label = labels[y, x]
        assert label, seed
        return simplify(trace_open(labels == label, start), 2.5)

    # The short spur at the upper bed's left corner is annotation, not a wall
    # across the passage between beds. Trace from the lower bed instead.
    north = stroke((734, 164), (420, 383))
    west_room = stroke((543, 519), (401, 381))
    west_court = stroke((938, 1023), (880, 519))
    east_stairs = stroke((991, 812), (1095, 1019))
    inner_gate = stroke((985, 800), (985, 800))
    outer_gate = stroke((1280, 729), (1530, 1000))
    # Join pen lifts at the door jamb, stairs and gate. Both roads continue to
    # the image edge, and the courtyard-to-room doorway remains open.
    boundary = (north + outer_gate[::-1] + [(1535, 1005), (1535, 1023)]
                + inner_gate[::-1] + east_stairs[::-1] + west_court[::-1]
                + [(883, 490), (883, 446), (862, 415)] + west_room[::-1])
    boundary.append(boundary[0])
    data = {"referenceWidth": 1536, "referenceHeight": 1024,
            "entry": {"x": 1050, "y": 600},
            "perimeter": [{"x": x, "y": y} for x, y in boundary]}
    output = project / "Assets/SlashDemo/Resources/SlashDemo/HealingLodgeCollision-v1.json"
    output.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

    mask = Image.new("1", (1536, 1024))
    ImageDraw.Draw(mask).polygon(boundary, fill=1)
    distance = ndimage.distance_transform_edt(np.asarray(mask))
    regions, _ = ndimage.label(distance >= (0.29 + 0.03) * 32)
    region = regions[600, 1050]
    assert region
    for name, x, y in [("spawn 1", 1050, 600), ("room", 800, 360),
                       ("between beds", 550, 310), ("front of beds", 600, 400),
                       ("gate", 1235, 800), ("outside gate", 1400, 950),
                       ("lower stairs", 960, 900), ("bottom of stairs", 1010, 1000)]:
        connected = regions[y, x] == region
        print(name, "connected=" + str(connected), "clearance=" + str(round(distance[y, x], 1)))
        assert connected, name
    preview = Image.open(project / "Assets/SlashDemo/Resources/SlashDemo/HealingLodge-open-v1.png").convert("RGB")
    draw = ImageDraw.Draw(preview)
    draw.line(boundary, fill=(255, 60, 70), width=3)
    draw.ellipse((1039, 589, 1061, 611), outline=(50, 255, 140), width=3)
    folder = project / "Verification/Artifacts"
    folder.mkdir(exist_ok=True)
    preview.save(folder / "HealingLodge-collision.png")
    print("Saved", len(boundary), "perimeter vertices")


if __name__ == "__main__":
    main()
