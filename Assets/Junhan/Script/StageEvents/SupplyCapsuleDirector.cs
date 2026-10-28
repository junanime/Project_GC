using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class SupplyCapsuleDirector : MonoBehaviour
    {
        public const float NoticeSeconds=10, DescentSeconds=6;
        LevelManager level;
        float elapsed,next;
        int count;
        SupplyCapsule pending;
        float dropAt;
        public bool HasPendingDrop => pending!=null;
        public void RestartForStage(){if(pending!=null)Destroy(pending.gameObject);pending=null;elapsed=0;count=0;next=Random.Range(240f,360f);}
        public static void Ensure(LevelManager level)
        {
            var director=level.GetComponent<SupplyCapsuleDirector>()??level.gameObject.AddComponent<SupplyCapsuleDirector>();
            director.level=level;director.next=Random.Range(240f,360f);
        }
        void Update()
        {
            if(level==null||level.IsLevelEnded||level.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage||Time.timeScale<=0||!level.PlayerCharacter.IsAlive)return;
            elapsed+=Time.deltaTime;
            if(pending!=null)
            {
                float until=dropAt-elapsed;
                if(until<=DescentSeconds&&!pending.StartedDescent)pending.BeginDescent(DescentSeconds);
                if(until<=0)
                {
                    pending.Land();pending=null;
                    FindObjectOfType<StageEventToastUI>()?.ShowSupply("보급 캡슐이 드랍되었습니다!");
                }
            }
            else if(count<2&&elapsed>=next)
            {
                if(SupplyRelicChoices.Available(level.PlayerCharacter).Length==0){next=elapsed+30;return;}
                AnnounceDrop(FindLandingPosition());
                count++;next=Random.Range(540f,660f);
            }
        }
        Vector2 FindLandingPosition()
        {
            Vector2 center=level.PlayerCharacter.transform.position;
            for(int i=0;i<40;i++)
            {
                Vector2 position=center+Random.insideUnitCircle.normalized*Random.Range(6f,9f);
                position=FiniteStomachMap.SafeSpawn(position,2.1f);
                bool blocked=false;
                foreach(var hit in Physics2D.OverlapCircleAll(position,2.1f))
                    if(!hit.isTrigger&&hit.GetComponentInParent<Character>()==null&&hit.GetComponentInParent<Monster>()==null){blocked=true;break;}
                if(!blocked)return position;
            }
            return center+Vector2.up*5;
        }
        public SupplyCapsule AnnounceDrop(Vector2 position)
        {
            position=FiniteStomachMap.SafeSpawn(position,2.1f);
            if(pending!=null)return pending;
            pending=SupplyCapsule.Create(position,level);dropAt=elapsed+NoticeSeconds;
            FindObjectOfType<StageEventToastUI>()?.ShowSupply("잠시 후 보급 캡슐이 드랍됩니다!");
            return pending;
        }
    }
}
