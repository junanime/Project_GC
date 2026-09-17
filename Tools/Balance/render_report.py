"""Export the authored curves and model results for balance review."""
import csv
import json
import pathlib
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from load_balance import ROOT, load

cfg=load();out=ROOT/'Documentation/Balance'
results=json.loads((out/'simulation.json').read_text())
minutes=np.arange(16)
target=[1,1.2,2,3.5,4,5,4,3,6,6,6,7,7,8,9,10]
times=np.array([p[0] for p in cfg['points']])
weights=np.array([p[1] for p in cfg['points']])
names=['Slime','Blueberry cake','Bacteria','Donut','Shark ice cream','Gummy worm']
with (out/'minute_curve.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.writer(f);w.writerow(['minute','design_target','normal_spawns_per_second',*names,'model_mean_augments','model_effective_hit_damage'])
 for i in minutes:
  rate=np.interp(i*60,*np.array(cfg['rate_points']).T)
  mix=[np.interp(i,times,weights[:,j]) for j in range(6)]
  w.writerow([i,target[i],rate,*mix,results['typical'][str(i)]['picks'],results['typical'][str(i)]['hit']])

plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
fig,axes=plt.subplots(3,1,figsize=(12,11),layout='constrained')
axes[0].plot(minutes,target,'o-',color='#9653bf',label='Difficulty design target (not measured difficulty)')
axes[0].axvspan(6,7,alpha=.15,color='#3cb58b',label='Recovery window')
axes[0].set(xlim=(0,15),ylim=(0,11),ylabel='Design target',title='15-minute balance: authored pacing and analytical estimates')
axes[0].legend(loc='upper left',fontsize=9)
axes[1].stackplot(times,weights.T,labels=names,colors=['#75bd70','#d1a948','#996acf','#ed91b8','#65accb','#6d79bf'])
axes[1].axvline(11,color='#555',linestyle=':')
axes[1].text(13.1,.69,'13:10\n60%',ha='center',color='white',fontsize=9)
axes[1].text(14.35,.72,'14:20\n85%',ha='center',color='white',fontsize=9)
axes[1].set(xlim=(0,15),ylim=(0,1),ylabel='Normal spawn share',xlabel='Game time (minutes)')
axes[1].legend(loc='upper center',ncol=3,fontsize=9)
check=[4,7,10];x=np.arange(3);expected=[5,7,5]
for j,(policy,label) in enumerate([('mixed','Random choice'),('typical','Reference choice model'),('offense','Attack priority')]):
 means=[results[policy][str(m)]['discrete_hits_mean'] for m in check]
 axes[2].bar(x+(j-1)*.24,means,.23,label=label)
 for a,b in zip(x+(j-1)*.24,means):axes[2].text(a,b+.12,f'{b:.1f}',ha='center',fontsize=9)
axes[2].scatter(x,expected,color='black',marker='_',s=650,label='Requested target')
axes[2].set(xticks=x,xticklabels=['4 min / HP 65','7 min / HP 120','10 min / HP 125'],ylabel='Estimated hits to kill',ylim=(0,11))
axes[2].legend(ncol=2,fontsize=9)
fig.savefig(out/'balance_overview.png',dpi=150)
plt.close(fig)
print(out/'balance_overview.png')
