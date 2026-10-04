"""Review original monster frame padding and contact points without changing assets."""
import json
import re
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Library/MonsterShadowReview"


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "compare":
        before = json.loads((OUT/"before/report.json").read_text(encoding="utf-8"))["entries"]
        after = json.loads((OUT/"after/report.json").read_text(encoding="utf-8"))["entries"]
        lookup = {r["blueprint"]:r for r in after}
        selected = [6, 10, 7, 19, 20, 22, 23, 15, 5]
        sheet = Image.new("RGB", (960, len(selected)*240), (66,62,72))
        d = ImageDraw.Draw(sheet)
        for row,index in enumerate(selected):
            record = before[index]
            d.text((8,row*240+8),Path(record["blueprint"]).stem[:32],fill="white")
            for col, (folder,r) in enumerate([("before",record),("after",lookup[record["blueprint"]])]):
                x = 320 + col*320
                d.text((x+8,row*240+8),folder.upper(),fill="white")
                im = Image.open(OUT/folder/r["image"])
                im.thumbnail((320,210),Image.Resampling.NEAREST)
                sheet.paste(im,(x+(320-im.width)//2,row*240+30))
        destination = ROOT/"Documentation/MonsterShadowQA"
        destination.mkdir(parents=True,exist_ok=True)
        sheet.save(destination/"comparison.png")
        print(destination/"comparison.png")
        return
    if len(sys.argv) > 1:
        folder = OUT / sys.argv[1]
        report = folder / "report.json"
        records = json.loads(report.read_text(encoding="utf-8"))["entries"] if report.exists() else [
            dict(image=p.name, blueprint=p.stem) for p in sorted(folder.glob("[0-9][0-9].png"))]
        sheet = Image.new("RGB", (1280, ((len(records)+3)//4)*310), (66,62,72))
        d = ImageDraw.Draw(sheet)
        for i,r in enumerate(records):
            x,y = (i%4)*320,(i//4)*310
            sheet.paste(Image.open(folder / r["image"]),(x,y+30))
            d.text((x+6,y+7),str(i)+" "+Path(r["blueprint"]).stem[:42],fill="white")
        sheet.save(folder / "contact-sheet.png")
        print(folder / "contact-sheet.png")
        return
    paths = {}
    for p in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: (\w+)", p.read_text(encoding="utf-8-sig"), re.M)
        if match:
            paths[match[1]] = p
    entries = []
    for p in sorted((ROOT / "Assets/Prefabs/Monsters").rglob("*.asset")):
        text = p.read_text(encoding="utf-8-sig")
        match = re.search(r"walkSpriteSequence:\s*\n\s*- \{fileID: (-?\d+), guid: (\w+)", text)
        if not match or match[2] not in paths:
            continue
        meta = paths[match[2]]
        source = Path(str(meta)[:-5])
        if source.suffix.lower() != ".png":
            continue
        im = Image.open(source).convert("RGBA")
        data = meta.read_text(encoding="utf-8-sig")
        pm = re.search(r"spritePivot: \{x: ([\d.-]+), y: ([\d.-]+)", data)
        pivot = dict(x=float(pm[1]), y=float(pm[2])) if pm else dict(x=.5,y=.5)
        for block in data.split("    - serializedVersion:")[1:]:
            if re.search(r"internalID: " + match[1] + r"\s", block):
                rect = re.search(r"rect:\s*\n\s*serializedVersion: \d+\s*\n\s*x: ([\d.]+)\s*\n\s*y: ([\d.]+)\s*\n\s*width: ([\d.]+)\s*\n\s*height: ([\d.]+)", block)
                if not rect:
                    continue
                x,y,w,h = [int(float(v)) for v in rect.groups()]
                im = im.crop((x, im.height-y-h, x+w, im.height-y))
                pm = re.search(r"pivot: \{x: ([\d.-]+), y: ([\d.-]+)",block)
                if pm:
                    pivot = dict(x=float(pm[1]),y=float(pm[2]))
                break
        alpha = im.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
        bbox = alpha.getbbox()
        if not bbox:
            continue
        record = dict(blueprint=str(p.relative_to(ROOT)).replace("\\", "/"),
                      source=str(source.relative_to(ROOT)).replace("\\", "/"),
                      size=im.size, bbox=bbox, pivot=pivot)
        entries.append((record, im))
    OUT.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGB", (1200, ((len(entries)+4)//5)*210), (66, 62, 72))
    d = ImageDraw.Draw(sheet)
    for i, (r, im) in enumerate(entries):
        ox, oy = (i % 5)*240, (i // 5)*210
        scale = min(200/im.width, 158/im.height)
        size = (max(1, round(im.width*scale)), max(1, round(im.height*scale)))
        px, py = ox+(240-size[0])//2, oy+26
        sheet.paste(im.resize(size, Image.Resampling.NEAREST), (px, py), im.resize(size).getchannel("A"))
        y = py+(1-r["pivot"]["y"])*size[1]
        d.line((ox+4, y, ox+235, y), fill=(58, 216, 212), width=1)
        x = px+r["pivot"]["x"]*size[0]
        d.line((x-4,y,x+4,y),fill="white",width=2)
        b = r["bbox"]
        d.rectangle((px+b[0]*scale,py+b[1]*scale,px+b[2]*scale,py+b[3]*scale),outline=(237,144,117))
        d.text((ox+5,oy+5),Path(r["blueprint"]).stem[:33],fill="white")
        d.text((ox+5,oy+190),f'{im.width}x{im.height} alpha bottom={im.height-b[3]}',fill="white")
    sheet.save(OUT / "source-sheet.png")
    (OUT / "sources.json").write_text(json.dumps([r for r,_ in entries],indent=2),encoding="utf-8")
    print(f"Reviewed {len(entries)} blueprint frames: {OUT}")


if __name__ == "__main__":
    main()
