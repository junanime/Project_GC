"""Original short game cues; deterministic, smooth envelopes, no sampled game audio."""
from pathlib import Path
import math, struct, wave
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/OctoberContent/ProcAudio'
OUT.mkdir(parents=True,exist_ok=True)
SR=22050
def tone(t,f,d,a=1):
 if t<0 or t>=d:return 0
 env=(1-math.exp(-t*350))*math.exp(-t*7/d)*(math.sin(math.pi*min(1,t/d))**.25)
 return a*env*(math.sin(2*math.pi*f*t)+.15*math.sin(2*math.pi*f*2*t))
for name,d in [('pop',.18),('bubble',.25),('bloom',.42),('crystal',.3),('chime',.32),('sweep',.32),('acquire',.46)]:
 data=[]
 for i in range(int(SR*d)):
  t=i/SR
  if name=='pop':x=tone(t,230,.18)+.35*tone(t,460,.07)
  elif name=='bubble':
   env=(1-math.exp(-t*400))*math.exp(-t*25)*(1-t/d)**2
   x=math.sin(2*math.pi*(310*t-260*t*t))*env
  elif name=='bloom':x=tone(t,523,.26,.6)+tone(t-.085,659,.26,.55)+tone(t-.17,784,.25,.4)
  elif name=='crystal':x=tone(t,698,.3,.6)+tone(t,1047,.16,.22)
  elif name=='chime':x=tone(t,587,.26,.65)+tone(t-.07,880,.25,.4)
  elif name=='sweep':
   env=math.sin(math.pi*t/d)**2*math.exp(-t*7)
   x=(math.sin(2*math.pi*(440*t-350*t*t))+.18*math.sin(2*math.pi*(660*t-525*t*t)))*env
  else:x=tone(t,523,.27,.55)+tone(t-.09,659,.27,.5)+tone(t-.18,784,.28,.45)
  data.append(x)
 peak=max(abs(x) for x in data) or 1
 # Avoid clicks, excessive loudness and clipping even when effects overlap.
 data=[x/peak*.46*min(1,i/(SR*.006),(len(data)-1-i)/(SR*.012)) for i,x in enumerate(data)]
 with wave.open(str(OUT/(name+'.wav')),'wb') as w:
  w.setparams((1,2,SR,len(data),'NONE','not compressed'))
  w.writeframes(b''.join(struct.pack('<h',round(x*32767)) for x in data))
 print(name, len(data)/SR, 'peak',round(max(abs(x) for x in data),3))
