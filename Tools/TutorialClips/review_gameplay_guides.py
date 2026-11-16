"""Build a local review gallery from recorded game frames, without inventing animation."""
import argparse, html, json, math
from pathlib import Path
from PIL import Image, ImageDraw

p=argparse.ArgumentParser()
p.add_argument('--output',required=True)
p.add_argument('--capture-root',required=True)
a=p.parse_args();out=Path(a.output);root=Path(a.capture_root)
repo=Path(__file__).resolve().parents[2]
catalog=json.loads((repo/'Assets/Resources/TutorialClips/catalog.json').read_text(encoding='utf-8-sig'))
entries={e['id']:e for e in catalog['entries']}
manifest=json.loads((out/'provenance.json').read_text(encoding='utf-8'))
esc=html.escape
ui=[]
for directory in sorted((root/'ui-motion').glob('*')):
    frames=[Image.open(f).convert('RGB') for f in sorted(directory.glob('*.png'))]
    if not frames:continue
    frames=[f.resize((1280,720),Image.Resampling.LANCZOS) for f in frames]
    name='panel-'+directory.name+'.gif'
    frames[0].save(out/name,save_all=True,append_images=frames[1:],duration=100,loop=0,optimize=True)
    ui.append(name)
shots=sorted((root/'panels').glob('*.png'))
for page in range(math.ceil(len(shots)/12)):
    sheet=Image.new('RGB',(1280,1000),'#241b26');draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(shots[page*12:(page+1)*12]):
        x=(i%3)*426;y=(i//3)*250
        frame=Image.open(path).convert('RGB').resize((426,240),Image.Resampling.LANCZOS)
        sheet.paste(frame,(x,y));draw.text((x+4,y+236),path.stem,fill='white')
    sheet.save(out/f'panel-review-{page+1:02}.jpg',quality=92)
cards=[]
for m in manifest:
    e=entries[m['id']];name=Path(m['gif']).name
    cards.append(f'<article><h2>{esc(e["title"])}</h2><a href="{esc(name)}"><img loading="lazy" src="{esc(name)}" alt="{esc(e["title"])}"></a><p>{esc(e["what"])}</p><p class="tip">{esc(e["tip"]).replace(chr(10),"<br>")}</p><small>{esc(e["id"])}</small></article>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>지스타 첫 등장 안내 · 실제 게임 영상</title>
<style>body{margin:0;background:#f8f0e6;color:#30232b;font:17px/1.6 system-ui}main{max-width:1440px;margin:auto;padding:32px}h1{color:#9b3459}header{background:#fff9f0;border-radius:18px;padding:24px;margin-bottom:24px}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:18px}article{padding:20px;background:white;border:2px solid #dfb9ba;border-radius:16px}h2{font-size:21px;margin:0 0 12px}img{width:100%;height:auto;border-radius:8px}small{color:#786373}.tip{background:#e0f1e4;padding:14px;border-radius:8px}input{font:inherit;padding:12px;width:80%;margin-bottom:24px}.panels{display:grid;grid-template-columns:1fr;gap:18px;margin-bottom:28px}button{font:inherit;padding:10px 20px}</style>
<main><header><h1>지스타 첫 등장 안내 · 실제 게임 영상</h1><p>필드·이벤트·특수 몬스터·상호작용 64종의 실제 플레이 장면입니다. 기존 무기·아이템 안내를 포함하면 총 88개입니다.</p><p><b>확인 클릭 / Space</b>로 닫기 · 안내 중 전투 일시정지 · 같은 참가자의 재도전에서는 자동 안내 반복 없음</p><p>아래 첫 세 영상은 게임 안에서 안내 패널이 실제 재생되는 화면입니다. 나머지는 패널 안에 사용한 영상입니다.</p></header>'''
page+='<div class="panels">'+''.join(f'<img src="{esc(n)}" alt="실제 안내 화면">' for n in ui)+'</div>'
page+='<input id="filter" placeholder="이름 또는 stage / colon / oral 검색"><section>'+''.join(cards)+'</section>'
page+='''<script>document.querySelector('#filter').addEventListener('input',e=>{const q=e.target.value.toLowerCase();document.querySelectorAll('article').forEach(c=>c.hidden=!c.textContent.toLowerCase().includes(q))})</script></main></html>'''
(out/'index.html').write_text(page,encoding='utf-8')
print(f'Review ready: {len(manifest)} clips, {len(ui)} animated panels, {len(shots)} captured panels')
