using System.Collections;
using UnityEngine;
namespace Vampire
{
    public static class AcidToadEvent
    {
        public static IEnumerator Emit(Transform owner,Rect screen,bool left,float warning,float duration,float width,float damage,float cooldown,Character player)
        {
            var panel=ChameleonPortrait.Create(owner,ChameleonKind.Fanta,left,true);
            LineRenderer lane=null;ChameleonSodaWave wave=null;
            try
            {
                yield return null;
                Vector2 direction=left?Vector2.right:Vector2.left;
                lane=AcidJetTelegraph.Make(panel.MouthWorld,direction,screen.width,Mathf.Clamp(width,1,2));
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.DangerWave);
                panel.Sample(9);yield return ChameleonTime.Wait(Mathf.Max(.9f,warning)*.5f);
                panel.Sample(10);yield return ChameleonTime.Wait(Mathf.Max(.9f,warning)*.5f);
                Object.Destroy(lane.gameObject);panel.Sample(11);
                Vector2 from=panel.MouthWorld;
                wave=ChameleonSodaWave.Fire(from,from+direction*(screen.width+2),Mathf.Max(1.4f,duration),Mathf.Clamp(width,1,2),damage,player,true,cooldown);
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.AcidRefluxWavePass);
                yield return ChameleonTime.Wait(.5f);panel.Sample(12);
                while(wave!=null)yield return null;
            }
            finally
            {
                if(lane!=null)Object.Destroy(lane.gameObject);
                if(wave!=null)Object.Destroy(wave.gameObject);
                if(panel!=null)Object.Destroy(panel.gameObject);
            }
        }
    }
    public sealed class AcidToadSpawn : MonoBehaviour
    {
        public float SpawnAt {get;private set;}
        public bool Spawned {get;private set;}
        public ChameleonLottery Lottery {get;private set;}
        public ChameleonKind Selected {get;private set;}
        bool selected;LevelManager level;
        void Awake(){ResetForStage();}
        public void ResetForStage()
        {
            Spawned=false;selected=false;Lottery=new ChameleonLottery();
            var s=ChameleonSettings.Current;SpawnAt=Random.Range(s!=null?s.spawnMin:420,s!=null?s.spawnMax:450);
        }
        public void Record(ChameleonKind kind){if(!Spawned&&!selected)Lottery.Record(kind);}
        void Update()
        {
            if(Spawned||ChameleonTime.Paused)return;
            if(level==null)level=FindObjectOfType<LevelManager>();
            if(level==null||level.CurrentLevelTime<SpawnAt||level.PlayerCharacter==null||level.EntityManager==null)return;
            if(!selected){Selected=Lottery.Pick(Random.value);selected=true;}
            var containers=level.CurrentLevelBlueprint.monsters;
            for(int i=0;i<containers.Length;i++)
                if(containers[i].monstersPrefab!=null&&containers[i].monstersPrefab.GetComponent<AcidToadMonster>()!=null)
                {
                    MonsterBlueprint bp=null;
                    foreach(var candidate in containers[i].monsterBlueprints)if(ChameleonArt.Kind(candidate)==Selected)bp=candidate;
                    if(bp==null)return;
                    Vector2 direction=Random.insideUnitCircle.normalized;if(direction==Vector2.zero)direction=Vector2.right;
                    var point=(Vector2)level.PlayerCharacter.transform.position+direction*8;
                    Spawned=level.EntityManager.SpawnMonster(i,point,bp,0,false)!=null;return;
                }
        }
    }
}
