using UnityEngine;
namespace Vampire {
 public sealed class ColonSpecialDirector:MonoBehaviour {
  public static readonly float[] Introductions={120,240,360,450,540};
  ColonMonsterRoster roster;LevelManager level;readonly float[] next=new float[5];
  public void Configure(ColonMonsterRoster r,LevelManager l){roster=r;level=l;for(int i=0;i<5;i++)next[i]=Introductions[i];}
  void Update(){if(roster==null||!ColonMonsterRoster.InColon||level==null||level.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage||!level.InitialFieldObjectsReady)return;
   for(int i=0;i<5;i++){if(level.CurrentLevelTime<next[i])continue;next[i]=level.CurrentLevelTime+(roster.DirectStart?100:80)+Random.Range(-8f,8f);
    int total=0,same=0;foreach(var m in level.EntityManager.LivingMonsters){if(m==null||m.HP<=0)continue;var s=m.GetComponent<ColonSpecialRuntime>();if(s!=null&&s.ControlsMovement){total++;if(s.Kind==ColonMonsterRoster.NewKinds[i])same++;}}
    if(total>=(roster.DirectStart?4:7)||same>=(i==2?1:2))continue;
    level.EntityManager.SpawnMonsterRandomPosition(roster.PoolIndex,roster.NewSpecials[i]);
   }
  }
  public static int LegacyCount(int original,bool corn,bool pair){var r=ColonMonsterRoster.Active;if(r==null)return original;float desired=original*(r.DirectStart?.85f:1.25f)*(corn?.5f:1);if(pair)desired*=.5f;int n=Mathf.FloorToInt(desired);return n+(Random.value<desired-n?1:0);}
 }
}
