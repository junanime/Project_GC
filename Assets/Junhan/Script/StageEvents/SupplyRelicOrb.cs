using System;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public static class SupplyRelicChoices
    {
        public static RelicBlueprint[] Available(Character player)
        {
            var catalog=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            return catalog==null||player==null?Array.Empty<RelicBlueprint>():catalog.relics.Where(r=>RelicSelectionPolicy.IsNewOffer(r)&&r.icon!=null&&!player.Relics.Owned.Any(owned=>RelicSelectionPolicy.SameChoice(owned.relicId,r.relicId))).GroupBy(r=>r.relicId).Select(g=>g.First()).ToArray();
        }
        public static RelicBlueprint[] Roll(Character player)
        {
            var pool=Available(player).ToList();var result=new System.Collections.Generic.List<RelicBlueprint>();
            while(pool.Count>0&&result.Count<3){int n=UnityEngine.Random.Range(0,pool.Count);result.Add(pool[n]);pool.RemoveAt(n);}return result.ToArray();
        }
    }
    public sealed class SupplyRelicOrb : MonoBehaviour
    {
        Character player;
        LevelManager level;
        Action claimed;
        Vector3 origin;
        SpriteRenderer art;
        RelicBlueprint[] choices;
        float age,retry;
        bool choosing,collected;
        public bool Collected=>collected;
        public RelicBlueprint[] Choices=>choices;
        public static SupplyRelicOrb Create(Vector3 position,Character player,LevelManager level,Action claimed)
        {
            var go=new GameObject("보급 황금빛 구슬");go.transform.position=position;
            var orb=go.AddComponent<SupplyRelicOrb>();orb.player=player;orb.level=level;orb.origin=position;orb.claimed=claimed;
            orb.art=go.AddComponent<SpriteRenderer>();orb.art.sprite=SupplyCapsuleArt.Orb;orb.art.sortingLayerName="Monster Full";orb.art.sortingOrder=24;orb.transform.localScale=Vector3.one*.42f;
            orb.choices=SupplyRelicChoices.Roll(player);return orb;
        }
        void Update()
        {
            if(collected||choosing||level==null||level.IsLevelEnded||level.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage||Time.timeScale<=0||player==null||!player.IsAlive)return;
            age+=Time.deltaTime;retry-=Time.deltaTime;
            if(age<1.2f)
            {
                float t=age/1.2f;transform.position=origin+new Vector3(.3f*t,Mathf.Sin(t*Mathf.PI)*1.7f-.65f*t,0);return;
            }
            float radius=player.CollectableCollider!=null?Mathf.Max(player.CollectableCollider.bounds.extents.x,player.CollectableCollider.bounds.extents.y):1;
            float distance=Vector2.Distance(transform.position,player.transform.position);
            if(distance<=radius+.2f&&retry<=0)
            {
                transform.position=Vector3.MoveTowards(transform.position,player.transform.position,6*Time.deltaTime);
                if(distance<.55f)TryCollect();
            }
        }
        public bool TryCollect()
        {
            if(AcidLakeEvent.Contains(transform.position))return false;
            if(collected||choosing||age<1.2f||Time.timeScale<=0||level.IsLevelEnded||!player.IsAlive||ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page!="hud")return false;
            choosing=true;art.enabled=false;
            ApothecaryUI.Instance.ShowSupplyRelics(choices,Accept,Cancel);return true;
        }
        bool Accept(RelicBlueprint relic)
        {
            if(collected||!choices.Contains(relic)||!player.Relics.AddReward(relic))return false;
            collected=true;choosing=false;FindObjectOfType<StatsManager>()?.RecordSupplyCollected();claimed?.Invoke();
            Destroy(gameObject);return true;
        }
        void Cancel(){choosing=false;art.enabled=true;retry=1.5f;}
    }
}
