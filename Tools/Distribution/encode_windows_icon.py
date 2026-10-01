"""Encode the approved, unaltered skill artwork as a multi-resolution Windows ICO.

This is asset format conversion, not image generation: no cropping, redrawing,
new background, or source-file modification. Pillow generates standard ICO sizes.
"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = root / "Assets/Junhan/Art/PhoenixSkills/HyukiActive.png"
target = Path(__file__).with_name("HyukiActive.ico")
with Image.open(source) as image:
    if image.width != image.height:
        raise ValueError("Expected the existing square Hyuki active icon; do not crop it.")
    image.save(target, format="ICO", sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
with Image.open(target) as icon:
    assert icon.ico.sizes() == {(n, n) for n in (16, 24, 32, 48, 64, 128, 256)}
print(f"Encoded 7 Windows icon sizes: {target}")
