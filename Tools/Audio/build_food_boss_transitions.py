"""Original dry, game-styled phase transition cues. No sampled game cues or ambience."""
from pathlib import Path
import numpy as np
import soundfile as sf
import json, hashlib
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/FoodBosses/Audio/Transitions'
SR=44100
rng=np.random.default_rng(210521)
def note(freq,duration,kind='soft'):
    t=np.arange(int(SR*duration))/SR; u=t/duration
    modes={'soft':[(1,1),(2,.08)],'pastry':[(1,1),(1.71,.23),(2.63,.10)],'candy':[(1,1),(1.58,.32),(2.43,.16)],'noodle':[(1,1),(2,.09)]}[kind]
    phase=2*np.pi*freq*(t-.10*t*t/duration)
    decay=7 if kind in ('pastry','candy') else 4
    x=sum(a*np.sin(phase*r)*np.exp(-decay*(1+(r-1)*.4)*u) for r,a in modes)
    x*=np.minimum(t/.004,1)*np.minimum((duration-t)/.03,1)
    if kind in ('pastry','candy'):
        n=min(len(t),int(SR*.018)); noise=rng.normal(0,1,n); noise=np.r_[0,np.diff(noise)]
        x[:n]+=.055*noise*np.sin(np.linspace(0,np.pi,n))**2*np.exp(-np.linspace(0,5,n))
    return x
spec={
'0-CreamPress':[(0,270,.23,'soft'),(.13,220,.25,'soft')],
'0-CreamSpread':[(0,300,.27,'soft'),(.17,370,.26,'soft'),(.32,425,.20,'soft')],
'0-ToppingSettle':[(0,470,.13,'soft'),(.10,360,.17,'soft')],
'1-Crack':[(0,720,.09,'pastry'),(.055,910,.08,'pastry'),(.13,600,.12,'pastry')],
'1-ShellOpen':[(0,470,.17,'pastry'),(.12,380,.21,'pastry'),(.23,305,.24,'pastry')],
'1-Molt':[(0,830,.10,'pastry'),(.10,650,.13,'pastry'),(.23,440,.19,'pastry')],
'1-ClawSettle':[(0,340,.22,'pastry'),(.16,460,.18,'pastry')],
'2-LidRattle':[(0,950,.08,'pastry'),(.09,1020,.08,'pastry'),(.21,880,.11,'pastry')],
'2-LidPop':[(0,610,.20,'pastry'),(.08,920,.19,'soft')],
'2-Shy':[(0,650,.16,'soft'),(.17,520,.22,'soft')],
'2-Angry':[(0,430,.13,'pastry'),(.10,710,.19,'pastry')],
'3-Stress':[(0,510,.14,'candy'),(.17,590,.15,'candy')],
'3-Crack':[(0,810,.10,'candy'),(.055,630,.14,'candy'),(.17,930,.09,'candy')],
'3-Shatter':[(0,510,.22,'candy'),(.05,720,.15,'candy'),(.13,950,.13,'candy'),(.21,640,.15,'candy')],
'3-CandyClash':[(0,620,.34,'candy')],
'4-Dip':[(0,250,.23,'noodle'),(.15,215,.25,'noodle')],
'4-Unfurl':[(0,300,.19,'noodle'),(.10,355,.20,'noodle'),(.21,410,.19,'noodle')],
'4-NoodleSettle':[(0,320,.15,'noodle'),(.12,275,.20,'noodle')],
}
OUT.mkdir(parents=True,exist_ok=True); report=[]
for name,events in spec.items():
    x=np.zeros(int((max(at+d for at,f,d,k in events)+.06)*SR))
    for at,f,d,k in events:
        a=note(f,d,k); pos=int((at+.008)*SR); x[pos:pos+len(a)]+=a
    x*=min(.62/max(abs(x)),.12/np.sqrt(np.mean(x*x)))
    path=OUT/(name+'.wav');sf.write(path,x,SR,subtype='PCM_16')
    assert max(abs(x))<.7 and abs(x[0])<1e-6 and abs(x[-1])<1e-6
    report.append(dict(file=path.name,seconds=len(x)/SR,peak=float(max(abs(x))),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
(ROOT/'Documentation/FoodBossTransitionAudio.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'PASS: {len(report)} original mono PCM cues; no clipping; silent endpoints.')
