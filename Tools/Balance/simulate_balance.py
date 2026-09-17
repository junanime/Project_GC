"""Seeded analytical combat model, NOT observed player playtest data.
Pickup/aim/choice behaviour is explicit; calculations read authored balance inputs.
"""
import json,random,math,pathlib,statistics
import numpy as np
from load_balance import load, ROOT
W=ROOT/'Documentation/Balance';cfg=load();weapon=cfg['weapon']
normal=cfg['normal'];hp=np.array([cfg['hp'][str(i)] for i in normal]);xp=np.array([sum(v*c for v,c in cfg['gem'][str(i)]) for i in normal])
points=np.array([p[0]*60 for p in cfg['points']]);weights=np.array([p[1] for p in cfg['points']])
special=list(range(16));legend=list(range(12));general=list(range(26))
# Names/order follow SyringeGeneralRandomAugmentAbility.BuildAlwaysGeneralCandidates.
def stats(g,s,l):
 damage=weapon['damage']['value']*(1+.10*g[0])*(weapon['lifeBurnDamageMultiplier'] if 0 in l else 1)*(1+.20*(10 in l))
 crit=1+min(1,.05+.05*g[1])*(.5+.15*g[2])
 pressure=1+min(weapon['pressureMaxDamageBonus'],3*weapon['pressureDamageBonusPerDistance'])*(10 in s) # mean impact distance = 3 units; max +50%
 direct=damage*crit*pressure
 # Corrosion builds on repeated hits; mark pays every second hit. Poison refresh is not stacked.
 hit=direct*(1+2*weapon['corrosionDamageTakenBonusPerStack']*(9 in s))*(1+.5*weapon['markBonusDamageMultiplier']*(11 in s))+weapon['explosionDamage']*(1 in s)+.6*weapon['fiberTrailDamagePerSecond']*(8 in s)
 attack=1+.06*g[3]+.08*g[4]+.16*(14 in s)+.16*(10 in l)
 count=weapon['projectileCount']['value']+g[9]+weapon['lifeBurnBonusProjectiles']*(0 in l)
 dps=hit*count/weapon['cooldown']['value']*attack
 crowd=1+.8*(3 in s)+.35*(1 in s)+.25*(13 in s)+.30*(6 in s)+.55*(1 in l)+.25*(2 in l)+.3*(4 in l)+.2*(8 in l)+.4*(11 in l)
 return direct,hit,dps*crowd
def simulate(seed,mode='mixed'):
 r=random.Random(seed);g=[0]*26;s=set();l=set();exp=25.;cost=5.;step=5.;level=1;picks=0;alive=0.;kills=0
 accuracy=r.uniform(.58,.78);pickup=r.uniform(.65,.85);records={};tier=[.55,.35,.10]
 def roll():
  v=r.random();t=0 if v<tier[0] else 1 if v<tier[0]+tier[1] else 2
  pool=([x for x in special if x not in s] if t==1 else [x for x in legend if x not in l and (mode=='risk' or x!=0)] if t==2 else [x for x in general if g[x]<{9:5,17:3,18:5,23:8,24:10,25:10}.get(x,999)])
  if not pool:t=0;pool=general
  return (t,r.sample(pool,min(2 if t==0 else 1,len(pool))))
 for time in range(0,901,5):
  while exp>=cost:
   exp-=cost;level+=1;step+=10 if level<10 else 13 if level<20 else 16 if level<30 else 20;cost=step;picks+=1
   cards=[roll() for _ in range(3)]
   if mode=='offense' or (mode=='typical' and r.random()<.65):
    score=lambda c:sum((3 if x in [0,1,2,3,4,9] else 0) for x in c[1]) if c[0]==0 else (4 if c[0]==1 and c[1][0] in [1,3,9,10,11] else 2)
    t,chosen=max(cards,key=score)
   else:t,chosen=r.choice(cards)
   if t==0:
    for x in chosen:g[x]+=1
   elif t==1:s.update(chosen)
   else:l.update(chosen)
  direct,hit,dps=stats(g,s,l)
  if time%60==0:records[time//60]=[picks,direct,hit,dps,level,kills,alive]
  w=np.array([np.interp(time,points,weights[:,i]) for i in range(len(normal))]);rate=np.interp(time,[p[0] for p in cfg['rate_points']],[p[1] for p in cfg['rate_points']])
  alive=min(cfg['population_limit'],alive+rate*5)
  # 0.9 area utilization: many spawned projectiles miss the same target or hit empty space.
  dead=min(alive,dps*accuracy*.9*5/np.dot(w,hp));alive-=dead;kills+=dead
  exp+=dead*np.dot(w,xp)*pickup*(1+.10*g[21])
  # Extra gut bacteria pickup, conditional on keeping a multi-hit target alive long enough.
  if 15 in s:exp+=dead*.35*pickup
 return records
def main():
 out={}
 for mode in ['mixed','typical','offense','risk']:
  runs=[simulate(14000+i,mode) for i in range(1500)]
  table={}
  for m in range(16):
   a=np.array([r[m] for r in runs]);table[m]={'picks':np.mean(a[:,0]),'direct':np.mean(a[:,1]),'hit':np.mean(a[:,2]),'dps':np.mean(a[:,3]),'level':np.mean(a[:,4]),'kills':np.mean(a[:,5]),'alive':np.mean(a[:,6]),'hit_p10_p50_p90':np.percentile(a[:,2],[10,50,90]).tolist()}
   if m in [4,7,10]:
    health=hp[{4:2,7:3,10:4}[m]]
    table[m]['continuous_hits_mean']=np.mean(health/a[:,2]);table[m]['discrete_hits_mean']=np.mean(np.ceil(health/a[:,2]))
  out[mode]=table
  print(mode,[(m,{k:round(v,2) for k,v in table[m].items() if k in ['picks','hit','direct','dps','alive','discrete_hits_mean']}) for m in [4,7,10,15]])
 (W/'simulation.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
if __name__=='__main__':main()
