using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        RectTransform bossBubble;
        Transform bossTarget;
        BossController targetBossController;
        Monster targetBossMonster;
        SnailBossRuntime targetSnail;
        float nextBossScan;
        void UpdateBossCompass()
        {
            if(level==null||level.PlayerCharacter==null||Page!="hud"||level.IsLevelEnded)
            {if(bossBubble!=null)bossBubble.gameObject.SetActive(false);return;}
            if(Time.unscaledTime>=nextBossScan)
            {
                nextBossScan=Time.unscaledTime+.5f;
                var p=level.PlayerCharacter.transform.position;
                targetSnail=FindObjectsOfType<SnailBossRuntime>().Where(b=>!b.Dead).OrderBy(b=>(b.transform.position-p).sqrMagnitude).FirstOrDefault();
                targetBossController=FindObjectsOfType<BossController>().Where(b=>!b.IsDead).OrderBy(b=>(b.transform.position-p).sqrMagnitude).FirstOrDefault();
                targetBossMonster=targetBossController!=null?targetBossController.GetComponent<Monster>():FindObjectsOfType<BossMonster>().Where(b=>b.HP>0).OrderBy(b=>(b.transform.position-p).sqrMagnitude).FirstOrDefault();
                bossTarget=targetSnail!=null?targetSnail.transform:targetBossController!=null?targetBossController.transform:targetBossMonster!=null?targetBossMonster.transform:null;
                if(bossTarget!=null)
                {
                    var m=bossTarget.GetComponent<MapMarker>()??bossTarget.gameObject.AddComponent<MapMarker>();m.Configure(MapMarkerKind.Boss,"보스",true);
                }
                foreach(var npc in FindObjectsOfType<MerchantNPC>())(npc.GetComponent<MapMarker>()??npc.gameObject.AddComponent<MapMarker>()).Configure(MapMarkerKind.Shop,"수상한 아저씨");
                foreach(var vending in FindObjectsOfType<EliteMonsterSummonInteractable>())(vending.GetComponent<MapMarker>()??vending.gameObject.AddComponent<MapMarker>()).Configure(MapMarkerKind.EliteSpawner,"엘리트 자판기");
            }
            var cam=Camera.main;
            if(bossTarget==null||cam==null||(targetSnail!=null&&targetSnail.Dead)||(targetBossController!=null&&targetBossController.IsDead)||(targetBossMonster!=null&&targetBossMonster.HP<=0))
            {if(bossBubble!=null)bossBubble.gameObject.SetActive(false);return;}
            Vector3 screen=cam.WorldToScreenPoint(bossTarget.position);
            bool visible=screen.z>0&&screen.x>=0&&screen.x<=Screen.width&&screen.y>=0&&screen.y<=Screen.height;
            if(visible||Vector2.Distance(bossTarget.position,level.PlayerCharacter.transform.position)<5||MiniStageArenaGeometry.Current!=null)
            {if(bossBubble!=null)bossBubble.gameObject.SetActive(false);return;}
            if(bossBubble==null)
            {
                bossBubble=Rect("Boss direction bubble",root,.5f,.5f,.5f,.5f);bossBubble.sizeDelta=new Vector2(66,72);
                Panel(bossBubble,0,.14f,1,1);ImageAt(bossBubble,OctoberArt.Get("OctoberUI/MapSymbols","Boss"),.07f,.21f,.93f,.93f);
                Label(bossBubble,"▼",.35f,0,.65f,.23f,18).color=new Color(1,.94f,.80f);
            }
            bossBubble.gameObject.SetActive(true);bossBubble.SetAsLastSibling();
            var canvas=root.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:cam,out var local);
            if(screen.z<0)local=-local;
            Vector2 half=new Vector2(594,292);
            float ratio=Mathf.Max(Mathf.Abs(local.x)/half.x,Mathf.Abs(local.y)/half.y);
            bossBubble.anchoredPosition=local/Mathf.Max(1,ratio);
        }
    }
}
