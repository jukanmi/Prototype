"""Rebuild Nun's static Unity sprite from the supplied Spine setup pose.

Run from the repository root with Python and Pillow installed.
The slot order and attachment positions come from Nun.json, not hand placement.
"""
import json
import math
from pathlib import Path

from PIL import Image


def build():
    folder = Path(__file__).resolve().parents[1] / "Assets/Art/Character/Party/Nun"
    data = json.loads((folder / "Nun.json").read_text(encoding="utf-8-sig"))
    attachments = data["skins"][0]["attachments"]
    layers = []
    for slot in data["slots"]:
        name = slot["attachment"]
        item = attachments[slot["name"]][name]
        image = Image.open(folder / "images" / f"{name}.png").convert("RGBA")
        assert image.size == (item["width"], item["height"]), name
        left = item["x"] - image.width / 2
        top = item["y"] + image.height / 2
        layers.append((image, left, top))

    left = math.floor(min(x for image, x, y in layers))
    right = math.ceil(max(x + image.width for image, x, y in layers))
    top = math.ceil(max(y for image, x, y in layers))
    bottom = math.floor(min(y - image.height for image, x, y in layers))
    canvas = Image.new("RGBA", (right - left, top - bottom))
    for image, x, y in layers:
        canvas.alpha_composite(image, (round(x - left), round(top - y)))

    # Keep x=0 at the middle and the lowest visible foot at the bottom pivot.
    bbox = canvas.getbbox()
    half_width = math.ceil(max(abs(left + bbox[0]), abs(left + bbox[2])))
    cropped = Image.new("RGBA", (half_width * 2, bbox[3] - bbox[1]))
    cropped.alpha_composite(canvas.crop(bbox), (left + bbox[0] + half_width, 0))
    height = 1024
    width = round(cropped.width * height / cropped.height)
    cropped.resize((width, height), Image.Resampling.LANCZOS).save(folder / "Nun_Setup.png")
    print(f"Nun_Setup.png: {width} x {height}, {len(layers)} layers")


if __name__ == "__main__":
    build()
