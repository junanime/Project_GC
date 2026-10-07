using System.Collections;
using UnityEngine;

namespace Vampire
{
    public static class AcidToadEvent
    {
        public static IEnumerator Emit(Transform owner,Rect screen,bool left,float warning,float duration,float width,float damage,float cooldown,Character player)
        {
            var root=new GameObject("환타 두꺼비 · 화면 가장자리 얼굴");root.transform.SetParent(owner,false);
            float y=player!=null?Mathf.Clamp(player.transform.position.y,screen.yMin+2,screen.yMax-2):screen.center.y;
            root.transform.position=new Vector3(left?screen.xMin+.5f:screen.xMax-.5f,y,0);
            var sr=root.AddComponent<SpriteRenderer>();sr.sortingOrder=530;sr.flipX=!left;
            var motion=root.AddComponent<FoodAtlasMotion>();motion.Configure(sr);
            var mouth=new GameObject("Cutaway mouth").transform;mouth.SetParent(root.transform,false);
            mouth.localPosition=new Vector3(left?1.15f:-1.15f,0,0);
            Vector2 direction=left?Vector2.right:Vector2.left;
            var lane=AcidJetTelegraph.Make(mouth.position,direction,screen.width-1,Mathf.Clamp(width,1,2));
            AcidJet jet=null;
            try
            {
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.DangerWave);
                for(float t=0;t<Mathf.Max(.9f,warning);)
                {
                    sr.enabled=!MiniStageRuntimeState.IsInsideMiniStage;lane.enabled=sr.enabled;
                    if(sr.enabled){t+=Time.deltaTime;motion.Sample("ToadHead",0,8,t/Mathf.Max(.9f,warning));}
                    yield return null;
                }
                Object.Destroy(lane.gameObject);
                jet=AcidJet.Create(mouth,direction,screen.width-1,Mathf.Clamp(width,1,2),Mathf.Max(1.6f,duration),damage,player,true,cooldown);
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.AcidRefluxWavePass);
                while(jet!=null){sr.enabled=!MiniStageRuntimeState.IsInsideMiniStage;motion.Sample("ToadHead",8,8,jet.Progress);yield return null;}
                motion.Sample("ToadHead",15,1,1);
                for(float t=0;t<.25f;){if(!MiniStageRuntimeState.IsInsideMiniStage){t+=Time.deltaTime;sr.color=new Color(1,1,1,1-t/.25f);}yield return null;}
            }
            finally
            {
                if(lane!=null)Object.Destroy(lane.gameObject);
                if(jet!=null)Object.Destroy(jet.gameObject);
                if(root!=null)Object.Destroy(root);
            }
        }
    }
    public sealed class AcidToadSpawn : MonoBehaviour
    {
        public float SpawnAt {get;private set;}
        public bool Spawned {get;private set;}
        LevelManager level;
        void Awake(){ResetForStage();}
        public void ResetForStage(){Spawned=false;SpawnAt=Random.Range(420f,450f);}
        void Update()
        {
            if(Spawned||Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage)return;
            if(level==null)level=FindObjectOfType<LevelManager>();
            if(level==null||level.IsRunFlowPaused||level.IsLevelEnded||level.CurrentLevelTime<SpawnAt||level.PlayerCharacter==null||level.EntityManager==null)return;
            var containers=level.CurrentLevelBlueprint.monsters;
            for(int i=0;i<containers.Length;i++)
                if(containers[i].monstersPrefab!=null && containers[i].monstersPrefab.GetComponent<AcidToadMonster>()!=null)
                {
                    var point=(Vector2)level.PlayerCharacter.transform.position+Random.insideUnitCircle.normalized*8;
                    Spawned=level.EntityManager.SpawnMonster(i,point,containers[i].monsterBlueprints[0],0,false)!=null;
                    return;
                }
        }
    }
}
