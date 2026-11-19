"""Package real Unity captures as pause-safe atlases and shareable GIFs. No invented frames."""
import argparse,json,math,hashlib
from pathlib import Path
from PIL import Image,ImageSequence,ImageDraw

p=argparse.ArgumentParser();p.add_argument('--source-root',required=True);p.add_argument('--output',required=True);p.add_argument('--only',default='');a=p.parse_args()
repo=Path(__file__).resolve().parents[2];src=Path(a.source_root);out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
catalog_path=repo/'Assets/Resources/TutorialClips/catalog.json';catalog=json.loads(catalog_path.read_text(encoding='utf-8-sig'));entries={e['id']:dict(e) for e in catalog['entries']}
gallery=src/'game-explanation-gifs-20261113';lib=repo/'Library';jobs={}
def add(id,title,what,tip,source,start=0):
    entry=entries.get(id,{}).copy();entry.update({'id':id,'kind':id.split('/')[0],'title':title,'what':what,'tip':tip,'effect':'live_'+id.replace('/','_').replace('-','_'),'gameplay':True});entries[id]=entry
    jobs[id]=(Path(source),start)
def replace(id,source,start=0):
    e=entries[id];add(id,e['title'],e['what'],e['tip'],source,start)

oral=[
('jaw','와작! · 치아 압박','경고 뒤 치아가 맞물리며 큰 피해를 줍니다.','치아 사이 빈틈이나 혀로 피하세요.','jaw'),
('food','한입 들어온다!','그림자 위로 음식이 떨어지고 음식 몬스터가 쏟아집니다.','낙하 그림자에서 벗어난 뒤 몬스터를 처치하세요.','food'),
('water','물이 들어온다!','입안으로 들어온 물살이 캐릭터와 몬스터를 밀어냅니다.','흐르는 방향을 보고 빈 길로 움직이세요.','water'),
('coffee','커피수혈','커피가 지나간 뒤 몬스터의 추격이 빨라집니다.','몬스터가 다가오기 전에 퇴로를 확보하세요.','coffee'),
('soda','탄산 보호막','탄산에 닿은 적은 20초 동안 침을 막는 거품에 싸입니다.','침을 세 번 맞혀 보호막을 깨세요.','soda'),
('sneeze','에취! · 재채기','짧은 경고 뒤 강한 바람이 한 방향으로 붑니다.','바람 방향을 보고 치아 주변으로 피하세요.','sneeze'),
('ceiling','혀가 뒤집힌다!','혀에서 입천장으로 이동해 박쥐들과 싸웁니다.','입천장 몬스터를 모두 처치하면 내려옵니다.','ceiling'),
('hiccup','딸꾹!','몬스터가 공중으로 들렸다가 다시 내려옵니다.','착지할 위치를 살피며 공격을 이어가세요.','hiccup'),
('brush','칫솔 청소','칫솔이 가로 → 세로 → 가로로 세 번 쓸고 갑니다.','빗금 경고선 밖으로 피하세요.','brush'),
('balloondelivery','풍선껌 보물배송','풍선에 달린 보상이 천천히 날아옵니다.','풍선을 공격해 레벨업 상자를 떨어뜨리세요.','balloon'),
('sweet','단맛 · 벌꿀집','몬스터가 벌꿀집에 모여 먹기 시작합니다.','먹느라 멈췄을 때 모인 몬스터를 공격하세요.','honey'),
('salty','짠맛 · 소금결정','소금결정 무리가 혀로 몰려옵니다.','좁은 곳에 갇히지 말고 무리의 빈틈으로 이동하세요.','salt'),
('sour','신맛 · 레몬 스프링','통레몬을 밟으면 가장 가까운 이빨로 튀어 오릅니다.','적에게 몰렸을 때 레몬을 밟아 혀에서 벗어나세요.','lemon'),
('bitter','쓴 한입','쓴맛을 견디면 회복을 얻을 수 있습니다.','적과 거리를 벌린 뒤 선택해서 먹으세요.','bitter'),
('spicy','매운맛 · 화상 원','경고 뒤 혀의 붉은 원이 뜨거워집니다. 위치는 매번 달라집니다.','원 사이 빈 길이나 치아 쪽으로 피하세요. 화상이 남을 수 있어요.','spicy'),
('boulder','치석 굴림돌','혀 곳곳에 놓인 치석을 밀면 부딪힌 적이 피해를 입습니다.','적을 향해 치석을 밀거나 대시로 세게 차세요.','boulder'),
('guard','민트 찹쌀 수호대','수호대가 혀 점프대를 향해 이동합니다. 체력은 80입니다.','몬스터를 막아 수호대가 살아서 점프하도록 호위하세요.','biker')]
for id,t,w,h,f in oral:add('event/oral-'+id,t,w,h,gallery/'stage0/gif'/f'{f}.gif',5 if id=='spicy' else 0)
# Preserve the discovery key when repackaging; this tool is permanently on the field.
entries['event/oral-boulder']['kind']='mechanic'
jobs['event/oral-boulder']=(lib/'GStarUIQA/field-balance/oral-remix',0)
# Every other frame of the 18 fps oral-remix proof.
sequence_settings={'event/oral-boulder':('bowling-[0-9][0-9].png',9)}
jobs['event/oral-guard']=(lib/'OralTasteContentProof/0-ashi/08-mint-guard',0)
jobs['event/oral-sour']=(lib/'OralCeilingLemonProof/0-ashi/02-whole-lemon-spring',0)
jobs['event/oral-spicy']=(lib/'GameplayGuideProof/captures/spicy',0)
add('event/oral-swallow','꿀꺽! · 소용돌이','구강 곳곳에 소용돌이가 생기고 마지막 삼키기가 시작됩니다.','소용돌이 사이를 피하고, 휩쓸릴 때는 대쉬로 탈출하세요.',gallery/'stage0/gif/spiral-dash.gif')
for id,t,w,h,f in [
('expulsion','대변 배출','통로를 따라 강한 바람이 불며 캐릭터와 몬스터를 밀어냅니다.','반대로 이동하면 저항할 수 있습니다. 벽과 적에 몰리지 않게 움직이세요.','01-wind'),
('absorption','수분 흡수','마른 바닥에서는 이동이 느려집니다.','촉촉하게 남은 길을 따라 이동하세요.','03-dry-floor'),
('residue','찌꺼기 대이동','통로에 부술 수 있는 찌꺼기가 굴러옵니다.','찌꺼기를 공격해 안전한 길을 만드세요.','04-residue'),
('nutrients','잔여 영양분 노출','회복과 경험치 영양분이 바닥에 나타납니다.','공격하지 말고 가까이에서 E로 상호작용해 바로 얻으세요.','05-nutrients')]:add('event/colon-'+id,t,w,h,src/'colon-events-runtime-20261110'/f'{f}.gif')
events=['monster-surge','gold-rush','acid-secretion','acid-reflux','peristaltic-drift','coffee-transfusion','antacid-bubble']
for i,id in enumerate(events):replace('event/'+id,lib/f'ExplanationStage1Gallery/event-{i}')
add('event/acid-flood','위산 범람','시간이 지나면 맵 아래쪽부터 위산이 차오릅니다.','위산에 오래 닿으면 큰 피해를 입습니다. 맵 위쪽의 마른 땅으로 피하세요.',lib/'ExplanationStage1Gallery/event-7-acid-flood')
specials={'sniper':'SniperMonster-8-view','nutrition-thief':'NutritionThiefBacteriaMonster-18','acid-spore':'ExplodingMonster-9','sugar-cube':'SugarCubeMonster-2','elite-sugar-cube':'SugarCubeMonster-2','speed-buffer':'MonsterSupportCaster-14','armor-buffer':'MonsterSupportCaster-15','attack-debuffer':'AttackSpeedDebufferMonster-16','treasure-runner':'TreasureRunnerMonster-17','trap':'TrapMonster-10-view'}
for id,name in specials.items():replace('monster/'+id,lib/('ExplanationStage1Gallery/special-'+name))
entries['monster/attack-debuffer'].update(title='방향 반전 몬스터',what='효과를 받으면 이동 입력 방향이 반대로 바뀝니다.',tip='상태 표시를 확인하고, 효과가 끝날 때까지 이동 방향에 주의하세요.')
entries['monster/sugar-cube'].update(what='큰 몸을 처치하면 작은 조각으로 분열합니다.',tip='한 번 쓰러뜨려도 끝이 아니에요. 작은 조각까지 처리하세요.')
entries['monster/elite-sugar-cube'].update(what='강한 몸을 처치해도 작은 조각으로 다시 분열합니다.',tip='공간을 벌리며 나뉜 조각들까지 처치하세요.')
entries['monster/trap']['tip']='표시된 방향키를 순서대로 4번 입력하면 탈출합니다. 모바일은 빛나는 매듭을 터치하세요.'
for id,i in [('acid-toad',2),('chameleon-drift',0),('chameleon-foam',1),('chameleon-latte',3)]:replace('monster/'+id,lib/f'ExplanationStage1Gallery/chameleon-{i}-skill')
colon=[
('S01','옥수수 팝콘포수','조준선을 따라 멀리서 팝콘 탄을 발사합니다.','조준선이 멈추면 옆으로 피하세요.'),
('S02','갈라진 밤알','다가온 뒤 짧게 경고하고 자폭합니다.','경고가 보이면 거리를 벌리거나 대쉬로 피하세요.'),
('S03','완두 꼬투리 형제','처치하면 작은 콩들이 나뉘어 나옵니다.','작아진 적까지 처리하며 이동할 공간을 남기세요.'),
('S04','검은목이 팔랑버퍼','버섯을 펼쳐 흔들며 주변 적을 빠르게 만듭니다. 파란 +가 표시됩니다.','버프를 퍼뜨리는 검은목이를 먼저 처치하세요.'),
('S05','흰목이 방패균','주변 적의 체력을 25% 높입니다. 빨간 +가 표시됩니다.','흰목이를 우선 처치해 추가 버프를 막으세요.'),
('S06','겹꼬투리 콩군단','큰 꼬투리가 여러 작은 콩으로 나뉩니다.','처치 뒤 생기는 조각에 포위되지 않게 빈 공간을 확보하세요.'),
('S07','가스주머니균','가스를 분출해 다른 몬스터를 플레이어 쪽으로 밀어냅니다.','가스보다 밀려오는 몬스터의 위치를 살피세요.'),
('S08','요구르트 발효균','가까이 온 뒤 주위를 돌며 기포를 남깁니다.','기포가 없는 쪽으로 이동해 둘러싸이지 않도록 하세요.'),
('S09','균사 봉합사','살아 있는 몬스터에 균사를 연결해, 죽으면 되살립니다.','연결된 적보다 봉합사를 먼저 처치해 부활을 끊으세요.'),
('S10','소금결정균','체력 60%와 30%에 균열이 생기고, 죽을 때 네 조각이 날아갑니다.','죽기 직전에 거리를 벌려 파편을 피하세요.'),
('S11','똥폭탄 쇠똥구리','3초간 준비한 뒤 똥폭탄을 던지고 도망갑니다. 맞으면 느려집니다.','던지기 전에 공격하면 똥을 떨어뜨리고 도망갑니다.'),
('S12','비트 두더지','경험치를 먹고 땅속으로 숨는 행동을 반복합니다.','먹는 2초 동안 공격하세요. 땅속에서는 직접 공격이 닿지 않습니다.')]
for id,t,w,h in colon:add('monster/colon-'+id,t,w,h,gallery/'stage2/raw'/('special-'+id))
add('monster/colon-trap','미역 함정','밟으면 붙잡혀 움직일 수 없게 됩니다.','표시된 방향키를 순서대로 입력해 탈출하세요.',gallery/'stage2/raw/special-seaweed')
add('monster/colon-squirrel','호두 다람쥐 도둑','가까이 다가오면 빠르게 도망갑니다.','탈출하기 전에 처치해 점수를 얻으세요.',gallery/'stage2/raw/special-squirrel')
add('mechanic/oral-travel','구강 탐험 · 혀와 치아','혀의 점프대를 이용해 치아로 이동할 수 있습니다.','치아와 혀 사이의 길을 익혀 위험한 바닥을 피하세요.',gallery/'stage0/gif/jump.gif')
add('mechanic/cavity','충치 치료','충치 주변에 머물러 치료하면 보상을 얻습니다.','치료 범위를 지키며 다가오는 몬스터를 막으세요.',gallery/'stage0/gif/cavity.gif')
add('mechanic/colon-habitats','대장 탐험 · 선택 미니보스','여섯 거주지에서 원하는 미니보스를 소환할 수 있습니다.','미니보스는 선택입니다. 좌측 하단 왕좌에서 변비왕을 깨워 처치하고 지팡이 열쇠를 챙기세요.',gallery/'stage2/raw-view-final/boss-Flamingo-arrival')
add('mechanic/boss-part','달콤한 향기 · 보스의 흔적','노란 향기가 오는 쪽을 따라가면 바닥에 묻힌 보스 파츠가 있습니다.','준비가 되면 파츠 가까이에서 E를 눌러 보스를 소환하세요.',gallery/'stage1/spawn-rolpangi.gif')
add('mechanic/elite-vending','엘리트 자판기','상호작용하면 강한 몬스터들이 나타납니다.','회복과 스킬을 준비한 뒤 E를 누르세요.',lib/'GameplayGuideProof/captures/elite-vending')
replace('mechanic/blood-clot',lib/'GameplayGuideProof/captures/blood-clot')
entries['mechanic/blood-clot']['tip']='Q로 과충전 시작 → 범위 안에서 처치 과제 완료 → E로 격화 입장!\n스킬 강화·레벨업 상자 보상.'

def load_frames(path, pattern='*.png', fps=15):
    if path.is_dir():
        files=sorted(path.glob(pattern));return [Image.open(f).convert('RGB') for f in files],1/fps
    if path.suffix.lower()=='.gif':
        im=Image.open(path);frames=[];dur=[]
        for f in ImageSequence.Iterator(im):frames.append(f.convert('RGB'));dur.append(f.info.get('duration',80)/1000)
        return frames,sum(dur)/len(dur)
    raise ValueError(str(path))

manifest=json.loads((out/'provenance.json').read_text(encoding='utf-8')) if a.only and (out/'provenance.json').exists() else [];thumbs=[]
for id,(path,start) in jobs.items():
    if a.only and id not in a.only.split(','):continue
    pattern,source_fps=sequence_settings.get(id,('*.png',15))
    if not path.exists() or (path.is_dir() and not list(path.glob(pattern))):
        print('MISSING',id,path);continue
    frames,dt=load_frames(path,pattern,source_fps)
    first=min(int(start/dt),len(frames)-1);duration=min(8,(len(frames)-first)*dt);count=max(2,min(96,round(duration*12)))
    if id in sequence_settings: count=min(count,len(frames)-first)
    def fit(frame):
        # Keep the actual world frame, but focus enemy demonstrations on actors rather than tiny HUD text.
        if id=='event/oral-boulder':
            w,h=frame.size;frame=frame.crop((int(w*.26),int(h*.18),int(w*.90),int(h*.82)))
        if id.startswith('monster/'):
            w,h=frame.size;frame=frame.crop((int(w*.1),int(h*.15),int(w*.8),int(h*.85)))
        return frame.resize((384,216),Image.Resampling.LANCZOS)
    selected=[fit(frames[min(len(frames)-1,first+int(i*(len(frames)-first)/count if duration>=((len(frames)-first)*dt)-.01 else i/(12*dt)))]) for i in range(count)]
    cols=8;rows=math.ceil(count/cols);atlas=Image.new('RGB',(cols*384,rows*216))
    for i,f in enumerate(selected):atlas.paste(f,((i%cols)*384,(i//cols)*216))
    e=entries[id];e.update(columns=cols,rows=rows,frames=count,fps=round(count/duration,4))
    dest=repo/'Assets/Resources/TutorialClips/Sheets'/f"{e['effect']}.bytes";atlas.save(dest,format='PNG')
    gif=out/(e['effect']+'.gif');selected[0].save(gif,save_all=True,append_images=selected[1:],duration=round(1000/e['fps']),loop=0,optimize=True)
    manifest=[m for m in manifest if m['id']!=id];manifest.append({'id':id,'title':e['title'],'source':str(path),'sourceType':'Unity runtime capture','start':start,'frames':count,'fps':e['fps'],'atlas':str(dest.relative_to(repo)),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'gif':str(gif)})
    thumbs.append((id,selected[len(selected)//2].copy()))
    print('PACKED',id,count)
if a.only:
    selected_ids=set(a.only.split(','))
    original_ids={e['id'] for e in catalog['entries']}
    catalog['entries']=[entries[e['id']] if e['id'] in selected_ids else e for e in catalog['entries']]
    catalog['entries'].extend(e for key,e in entries.items() if key in selected_ids and key not in original_ids)
else:
    catalog['entries']=list(entries.values())
catalog_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(out/'provenance.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
for page in range(math.ceil(len(thumbs)/12)):
    contact=Image.new('RGB',(384*3,246*4),(30,25,35));draw=ImageDraw.Draw(contact)
    for k,(id,f) in enumerate(thumbs[page*12:(page+1)*12]):
        x=k%3*384;y=k//3*246;contact.paste(f,(x,y));draw.text((x+8,y+220),id,fill='white')
    contact.save(out/f'review-{ "updated-" if a.only else ""}{page+1:02}.jpg')
print('READY',len(manifest),'of',len(jobs),'gameplay clips;',len(entries),'total guides')
