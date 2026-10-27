"""Read an annotated reference image and export registered Unity collision geometry.

This analyzes red annotation pixels; it never modifies the manor artwork.
Usage: python Verification/TraceManorCollision.py annotation.png output.json
"""
import collections
import json
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import ConvexHull


def skeleton(mask):
    grid = np.pad(mask, 1)
    for _ in range(40):
        changed = 0
        for phase in range(2):
            p = [grid[:-2, 1:-1], grid[:-2, 2:], grid[1:-1, 2:], grid[2:, 2:],
                 grid[2:, 1:-1], grid[2:, :-2], grid[1:-1, :-2], grid[:-2, :-2]]
            degree = sum(v.astype(np.uint8) for v in p)
            transitions = sum((~p[i] & p[(i + 1) % 8]).astype(np.uint8) for i in range(8))
            if phase == 0:
                clear = ~(p[0] & p[2] & p[4]) & ~(p[2] & p[4] & p[6])
            else:
                clear = ~(p[0] & p[2] & p[6]) & ~(p[0] & p[4] & p[6])
            remove = grid[1:-1, 1:-1] & (degree >= 2) & (degree <= 6) & (transitions == 1) & clear
            changed += int(remove.sum())
            grid[1:-1, 1:-1][remove] = False
        if not changed:
            break
    return set(zip(*np.where(grid[1:-1, 1:-1])))


def trace_open(mask, start_xy):
    points = skeleton(mask)
    start = min(points, key=lambda p: (p[1] - start_xy[0]) ** 2 + (p[0] - start_xy[1]) ** 2)
    previous = {start: None}
    queue = collections.deque([start])
    last = start
    while queue:
        last = queue.popleft()
        y, x = last
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nxt = (y + dy, x + dx)
                if (not dx and not dy) or nxt not in points or nxt in previous:
                    continue
                if dx and dy and ((y + dy, x) in points or (y, x + dx) in points):
                    continue
                previous[nxt] = last
                queue.append(nxt)
    path = []
    while last is not None:
        path.append((int(last[1]), int(last[0])))
        last = previous[last]
    return path[::-1]


def simplify(points, tolerance=1.5):
    if len(points) < 3:
        return points
    p = np.asarray(points, dtype=float)
    direction = p[-1] - p[0]
    denom = np.dot(direction, direction)
    if denom == 0:
        distances = np.linalg.norm(p - p[0], axis=1)
    else:
        t = np.clip((p - p[0]) @ direction / denom, 0, 1)
        distances = np.linalg.norm(p - p[0] - t[:, None] * direction, axis=1)
    index = int(np.argmax(distances))
    if distances[index] <= tolerance:
        return [points[0], points[-1]]
    return simplify(points[:index + 1], tolerance)[:-1] + simplify(points[index:], tolerance)


def solid_outline(mask):
    filled = ndimage.binary_fill_holes(ndimage.binary_closing(mask, np.ones((3, 3))))
    filled = ndimage.binary_erosion(filled)
    y, x = np.where(filled)
    points = np.column_stack((x, y))
    return [(int(x), int(y)) for x, y in points[ConvexHull(points).vertices]]


def navigation_report(boundary, table):
    image = Image.new("1", (1536, 1024))
    draw = ImageDraw.Draw(image)
    draw.polygon(boundary, fill=1)
    draw.polygon(table, fill=0)
    distance = ndimage.distance_transform_edt(np.asarray(image))
    # Player radius 0.29 plus a 0.03-world-unit edge radius, at 32 reference pixels/unit.
    free = distance >= (0.29 + 0.03) * 32
    labels, _ = ndimage.label(free)
    region = labels[520, 670]
    for name, x, y in [("spawn", 670, 520), ("training", 1240, 370), ("bedroom", 360, 555),
                       ("hall", 790, 220), ("meditation", 340, 280), ("gate", 680, 950)]:
        print(name, "connected=" + str(bool(region and labels[y, x] == region)),
              "clearance=" + str(round(float(distance[y, x]), 2)))


def main():
    image = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(int)
    r, g, b = image[:, :, 0], image[:, :, 1], image[:, :, 2]
    red = (r > 180) & (g < 130) & (b < 150) & (r - g > 85) & (r - b > 70) & (abs(g - b) < 22)
    labels, _ = ndimage.label(red, np.ones((3, 3)))
    areas = np.bincount(labels.ravel())
    components = np.argsort(areas[1:])[-3:][::-1] + 1
    right = simplify(trace_open(labels == components[0], (735, 697)))
    left = simplify(trace_open(labels == components[1], (596, 691)))
    # The two large strokes meet beside the meditation room. The gate stays open.
    boundary = left + right[::-1] + [(735, 758), (778, 822), (820, 860), (820, 1008),
                                    (555, 1008), (555, 860), (580, 820), (596, 758), left[0]]
    # The hand-drawn passage is only 12 pixels wide at its throat. Retreat each
    # opposing edge by at most six pixels so the unchanged 0.29-radius actor fits.
    clearance = {(503, 239): (503, 245), (488, 227): (488, 221),
                 (503, 227): (503, 221), (515, 221): (515, 218)}
    boundary = [clearance.get(point, point) for point in boundary]
    table = solid_outline(labels == components[2])
    data = {"referenceWidth": 1536, "referenceHeight": 1024,
            "perimeter": [{"x": x, "y": y} for x, y in boundary],
            "table": [{"x": x, "y": y} for x, y in table]}
    with open(sys.argv[2], "w", encoding="utf-8") as output:
        json.dump(data, output, indent=2)
        output.write("\n")
    print("Traced", len(boundary), "perimeter points; table", len(table))
    print("Stroke endpoints:", left[0], left[-1], right[0], right[-1])
    navigation_report(boundary, table)


if __name__ == "__main__":
    main()
