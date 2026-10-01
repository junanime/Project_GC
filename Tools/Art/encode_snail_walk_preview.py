"""Encode Unity-rendered walk samples; never generates or redraws game artwork."""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("frames", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
paths = sorted(args.frames.glob("walk-??.png"))
if len(paths) != 16:
    raise SystemExit("Expected 16 Unity-rendered frames")
frames = [Image.open(path).convert("RGB").quantize(colors=128) for path in paths]
args.output.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(args.output, save_all=True, append_images=frames[1:],
               duration=60, loop=0, disposal=2)
print(args.output)
