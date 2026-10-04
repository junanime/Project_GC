"""Build looping first-discovery demos from the game's existing character/monster art.

The GIF review copies and in-game sprite sheets contain the same 12 frames.
Only backgrounds, projectiles and readable hazard cues are drawn here; actors are
composited from their actual game PNGs so their designs are not reinterpreted.
"""
import json
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
CATALOG = json.loads((ROOT / "Assets/Resources/TutorialClips/catalog.json").read_text(encoding="utf-8"))["entries"]
SHEETS = ROOT / "Assets/Resources/TutorialClips/Sheets"
GIFS = ROOT / "Documentation/TutorialUI/Demos"
SIZE = (256, 144)
FRAMES = 12
ACTORS = {
    "hyuki": "Assets/Junhan/Art/CharacterDesigns/Hyuki_Idle.png",
    "takoyaki": "Assets/Art/MonsterRemake/Takoyaki.png",
    "sniper": "Assets/Art/MonsterRemake/Sniper_00.png",
    "thief": "Assets/Art/FoodMonsterAnimations/PopcornPom/PopcornPom_FrontRun_01.png",
    "sugar": "Assets/Art/SugarCubeAnimations/Single/Single_Walk_01.png",
    "elite_sugar": "Assets/Art/SugarCubeAnimations/Elite/Elite_Walk_01.png",
    "salt": "Assets/Art/SupportMonsters/Salt/Salt_Walk_01.png",
    "pepper": "Assets/Art/SupportMonsters/Pepper/Pepper_Walk_01.png",
    "runner": "Assets/Prefabs/Monsters/Monster Sprite/chikiiin.png",
    "trap": "Assets/Art/MonsterRemake/SeaweedFront_00.png",
    "spore": "Assets/Prefabs/Monsters/Monster Sprite/skul.png",
    "blood_clot_closed": "Assets/Junhan/Art/mini_not_open.png",
    "blood_clot_open": "Assets/Junhan/Art/mini_open.png",
}


def actor(name):
    image = Image.open(ROOT / ACTORS[name]).convert("RGBA")
    if name == "hyuki":
        image = image.crop((0, 0, image.width // 4, image.height // 2))
    elif name in ("runner", "spore") and image.width > image.height * 2:
        image = image.crop((0, 0, image.width // 4, image.height))
    box = image.getbbox()
    return image.crop(box) if box else image


ART = {name: actor(name) for name in ACTORS}


def paste(im, name, x, y, width, wobble=0):
    source = ART[name]
    height = max(1, round(width * source.height / source.width))
    sprite = source.resize((width, height), Image.Resampling.LANCZOS)
    im.alpha_composite(sprite, (int(x - width / 2), int(y - height / 2 + wobble)))


def base(t):
    im = Image.new("RGBA", SIZE, (166, 83, 92, 255))
    d = ImageDraw.Draw(im, "RGBA")
    for y in range(144):
        v = math.sin(y * .052) * 7
        d.line((0, y, 256, y), fill=(173 + int(v), 91 + int(v / 2), 98 + int(v / 2), 255))
    for j in range(6):
        x = (j * 57 + 18) % 256
        y = 18 + j * 21
        d.ellipse((x - 8, y - 3, x + 8, y + 3), outline=(125, 55, 67, 90), width=2)
    d.rounded_rectangle((3, 3, 252, 140), radius=7, outline=(83, 30, 46, 200), width=3)
    return im


def circle(d, x, y, radius, color, outline=None, width=2):
    d.ellipse((x-radius, y-radius, x+radius, y+radius), fill=color, outline=outline, width=width)


def needle(d, x, y, color=(147, 218, 255, 255), length=24):
    d.line((x-length/2, y, x+length/2, y), fill=(35, 47, 83, 255), width=5)
    d.line((x-length/2, y, x+length/2, y), fill=color, width=2)
    d.polygon([(x+length/2, y-2), (x+length/2+8, y), (x+length/2, y+2)], fill=(238, 243, 239, 255))
    d.rectangle((x-length/2-4, y-5, x-length/2, y+5), fill=(237, 182, 65, 255))


def hazard(d, x, y, r, color):
    d.ellipse((x-r, y-r*.42, x+r, y+r*.42), fill=color, outline=(255, 230, 184, 230), width=2)


def render(effect, frame):
    t = frame / FRAMES
    phase = min(1, t * 1.35)
    im = base(t)
    d = ImageDraw.Draw(im, "RGBA")
    monster_effects = {"sniper", "thief", "spore", "sugar", "salt", "pepper", "runner", "elite_sugar", "trap", "armor"}
    event_effects = {"surge", "gold", "acid_event", "wave", "drift", "coffee", "bubble"}

    if effect == "blood_clot":
        # Show the real game flow: defeat the clot, approach its portal, then E.
        hx = [54, 58, 62, 68, 75, 85, 99, 116, 136, 153, 168, 178][frame]
        d.ellipse((130, 54, 220, 122), fill=(245, 110, 126, 43))
        for j in range(3):
            d.arc((145-j*5, 58-j*4, 208+j*5, 114+j*4),
                  45+frame*15, 245+frame*15,
                  fill=(255, 183, 139, 72 + j*24), width=2)
        if frame < 4:
            paste(im, "blood_clot_closed", 176, 84, 67, -2 if frame == 3 else 0)
            if frame > 0:
                needle(d, 84 + frame*23, 76 - frame*2, (172, 238, 251, 255), 14)
            if frame == 3:
                for dx, dy in [(-30,-24),(28,-26),(-34,4),(29,12)]:
                    d.line((176+dx*.5,84+dy*.5,176+dx,84+dy),fill=(255,233,170,245),width=3)
        else:
            if frame in (4, 5):
                circle(d, 176, 84, 31 + (frame-4)*9,
                       (249, 179, 137, 75), (255, 227, 161, 180), 2)
            paste(im, "blood_clot_open", 176, 84, 67)
            d.ellipse((163, 58, 189, 72), outline=(255, 226, 175, 170), width=2)
            if frame >= 6:
                d.rounded_rectangle((195, 20, 225, 47), radius=5,
                                    fill=(253, 235, 197, 245), outline=(70, 35, 46, 255), width=2)
                d.text((207, 25), "E", fill=(64, 35, 45, 255), stroke_width=0)
                d.polygon([(202,52),(211,52),(206,60)],fill=(255,235,203,235))
        if frame >= 9:
            d.ellipse((155, 61, 197, 107), outline=(255, 242, 189, 160+(frame-9)*30), width=3)
        paste(im, "hyuki", hx, 94, 68 if frame < 9 else [53, 39, 23][frame-9],
              math.sin(t*math.pi*4)*2)

    elif effect in monster_effects:
        hx = 58 + (16 if effect in {"sniper", "trap"} else 8) * math.sin(t * math.pi)
        hy = 88 + (13 if effect in {"sniper", "trap"} else 5) * math.sin(t * math.pi)
        enemy = {"elite_sugar": "elite_sugar", "armor": "salt"}.get(effect, effect)
        ex = 188 - (20 * t if effect in {"runner", "thief"} else 0)
        if effect == "sniper":
            d.line((ex-8, 75, hx, hy), fill=(255, 40, 52, 170), width=2)
            if frame >= 7: circle(d, ex-(frame-6)*15, 78+(frame-6)*2, 4, (255, 210, 74, 255))
        if effect in {"salt", "pepper", "armor"}:
            for offset in (-24, 26):
                circle(d, ex+offset, 102, 17, (88, 220, 157, 55) if effect == "salt" else (179, 118, 226, 60), (91, 238, 170, 140) if effect == "salt" else (220, 148, 245, 170))
        if effect == "trap":
            d.arc((hx-21, hy-18, hx+21, hy+18), 20, 340, fill=(51, 126, 77, 250), width=4)
            if frame >= 5: d.line((hx+6, hy-19, hx+15, hy+10), fill=(68, 159, 85, 220), width=5)
        if effect == "spore" and frame >= 5:
            circle(d, ex, 79, 19+(frame-5)*5, (247, 75, 86, 55), (255, 168, 154, 225), 3)
        if effect in {"sugar", "elite_sugar"}:
            for j in range(2 if effect == "sugar" else 1): paste(im, "sugar", ex-30+j*26, 108, 24)
        if effect == "thief":
            for j in range(3): circle(d, 137+j*14+4*t, 119, 4, (148, 215, 255, 210))
        if effect == "runner": circle(d, ex+23, 94, 9, (251, 211, 77, 230))
        if effect == "armor": d.arc((ex-28, 52, ex+28, 109), 0, 360, fill=(132, 207, 246, 220), width=4)
        paste(im, enemy, ex, 79, 55 if effect == "elite_sugar" else 43, math.sin(t*math.pi*4)*2)
        paste(im, "hyuki", hx, hy, 70)

    elif effect in event_effects:
        hx = 65 + 18*math.sin(t*math.pi)
        hy = 87 + 10*math.sin(t*math.pi)
        if effect == "surge":
            for j in range(5): paste(im, "sugar", 144+j*18-20*t, 60+j%2*36, 29)
        if effect == "gold":
            paste(im, "takoyaki", 177, 75, 48)
            for j in range(7):
                x=140+j*15+6*t; y=105+j%2*12
                circle(d,x,y,6,(255,219,87,245),(158,105,39,255))
        if effect == "acid_event":
            for x,y in [(144,64),(200,102),(153,117)]: hazard(d,x,y,17+3*math.sin(t*math.pi*2)**2,(112,192,55,195))
        if effect == "wave":
            x=245-230*t
            d.polygon([(x-25,8),(x+18,8),(x+18,136),(x-25,136)],fill=(94,183,67,145))
            for y in range(20,135,20): d.arc((x-36,y-12,x+30,y+12),0,180,fill=(201,255,124,220),width=3)
        if effect == "drift":
            for j in range(4):
                y=33+j*26
                d.line((129,y,209,y),fill=(250,223,164,190),width=3)
                d.polygon([(217,y),(204,y-6),(204,y+6)],fill=(250,223,164,230))
            hx += 12*t
        if effect == "coffee":
            for j in range(4):
                x=159+j*19
                circle(d,x,68+j%2*29,8,(104,52,37,220),(240,179,94,240))
            paste(im,"takoyaki",190,95,43)
            for j in range(3): d.line((169+j*10,38,174+j*10,24),fill=(240,218,184,140),width=2)
        if effect == "bubble":
            circle(d,170,81,45,(163,245,218,80),(210,255,241,240),4)
            for j in range(4): circle(d,129+j*25,38+j%2*78,5,(184,246,235,95),(228,255,250,240))
            hx=155+13*math.sin(t*math.pi)
        paste(im,"hyuki",hx,hy,70)

    else:
        hx, hy = 58, 88
        ex, ey = 205, 85
        paste(im,"takoyaki",ex,ey,47)
        if effect in {"formation", "bipolar"}:
            count=6 if effect=="formation" else 2
            for j in range(count):
                a=(j/count)*math.pi*2
                px=hx+math.cos(a)*(24+30*t); py=hy+math.sin(a)*(20+22*t)
                needle(d,px,py,(120,221,249,255),16)
        else:
            px=83+132*phase
            py=84+(-20*math.sin(t*math.pi) if effect=="homing" else 0)
            if effect == "return" and frame>=7: px=217-(frame-7)*24
            if effect == "pressure": needle(d,px,py,(255,217,97,255),17+int(t*13))
            else: needle(d,px,py, (126,213,247,255),19)
            if effect == "wind":
                for j in range(3): needle(d,px-21*j,py-7+j*7,(178,246,249,190),16)
            if effect == "fiber": d.line((hx+28, hy, px-5, py),fill=(207,238,245,230),width=3)
            if effect == "bacteria":
                for j in range(3): circle(d,ex-17+j*10,ey-26,3,(170,231,123,255))
            if effect == "poison":
                for j in range(3): circle(d,ex-15+j*8,ey-23-j%2*4,4,(143,204,71,200))
            if effect in {"explosion","vibration"} and frame>=6:
                radius=(frame-5)*5
                circle(d,ex,ey,radius,(254,171,69,55) if effect=="explosion" else (139,207,254,55),
                       (255,231,121,220) if effect=="explosion" else (193,238,255,220),3)
            if effect == "ice" and frame>=6: circle(d,ex,ey,25,(151,218,255,108),(212,246,255,220),3)
            if effect == "fire" and frame>=6:
                for j in range(3): d.polygon([(ex-17+j*12,ey-10),(ex-8+j*12,ey-37-4*math.sin(t*7)),(ex+j*12,ey-8)],fill=(255,154,59,210))
            if effect == "honey" and frame>=6: hazard(d,ex,ey+19,20,(241,189,63,160))
            if effect == "acid" and frame>=6: hazard(d,ex,ey+21,24,(123,206,73,170))
            if effect == "corrosion" and frame>=6:
                for j in range(3): circle(d,ex-16+j*12,ey+20,5,(114,215,92,215))
            if effect == "mark" and frame>=5:
                d.line((ex-9,ey-36,ex+9,ey-20),fill=(255,222,72,250),width=4)
                d.line((ex+9,ey-36,ex-9,ey-20),fill=(255,222,72,250),width=4)
            if effect == "wood" and frame>=6:
                d.line((ex,ey-10,ex+27,ey-28),fill=(91,170,91,255),width=4)
                circle(d,ex+27,ey-28,5,(115,213,99,255))
            if effect == "mosquito" and frame>=6:
                d.line((ex-10,ey-15,hx+14,hy-10),fill=(254,111,142,180),width=3)
                circle(d,hx,hy-37,8,(237,105,126,220))
            if effect == "hunger" and frame>=6:
                for j in range(3): needle(d,130+j*18,58+j%2*9,(249,232,144,220),13)
            if effect == "pierce" and frame>=6: paste(im,"takoyaki",150,95,30)
        paste(im,"hyuki",hx,hy,70,math.sin(t*math.pi*4)*2)

    return im.convert("RGB")


def main():
    SHEETS.mkdir(parents=True, exist_ok=True)
    GIFS.mkdir(parents=True, exist_ok=True)
    selected = set(sys.argv[1:])
    for entry in CATALOG:
        effect = entry["effect"]
        if selected and effect not in selected:
            continue
        frames = [render(effect, i) for i in range(FRAMES)]
        sheet = Image.new("RGB", (SIZE[0]*4, SIZE[1]*3))
        for i, frame in enumerate(frames):
            sheet.paste(frame, ((i%4)*SIZE[0], (i//4)*SIZE[1]))
        sheet.save(SHEETS / (effect+".png"), optimize=True)
        frames[0].save(GIFS / (effect+".gif"), save_all=True,
                       append_images=frames[1:], duration=115, loop=0, optimize=True)
    print(f"Generated {len(selected) if selected else len(CATALOG)} loops, sheets in {SHEETS}, GIFs in {GIFS}")


if __name__ == "__main__":
    main()
