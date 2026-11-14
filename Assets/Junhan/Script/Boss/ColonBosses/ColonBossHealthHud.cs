using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed class ColonBossHealthHud:MonoBehaviour
    {
        static readonly List<ColonBossRuntime> orderedBosses=new List<ColonBossRuntime>(6);
        static readonly System.Comparison<ColonBossRuntime> distanceComparer=CompareDistance;
        static int orderedFrame=-1;
        ColonBossRuntime boss;GameObject root;RectTransform panel;FoodBossHealthRail rail;TextMeshProUGUI title;
        public void Initialize(ColonBossRuntime b)
        {
            boss=b;root=new GameObject("Optional boss health · "+b.Definition.Name,typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            panel=new GameObject("Health row",typeof(RectTransform)).GetComponent<RectTransform>();panel.SetParent(root.transform,false);panel.anchorMin=new Vector2(.26f,1);panel.anchorMax=new Vector2(.74f,1);panel.pivot=new Vector2(.5f,1);panel.sizeDelta=new Vector2(0,18);
            rail=new GameObject("Shared pixel rail",typeof(RectTransform),typeof(CanvasRenderer),typeof(FoodBossHealthRail)).GetComponent<FoodBossHealthRail>();rail.transform.SetParent(panel,false);rail.rectTransform.anchorMin=Vector2.zero;rail.rectTransform.anchorMax=Vector2.one;rail.rectTransform.offsetMin=rail.rectTransform.offsetMax=Vector2.zero;rail.raycastTarget=false;
            title=new GameObject("Boss name",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();title.transform.SetParent(panel,false);title.rectTransform.anchorMin=Vector2.up;title.rectTransform.anchorMax=Vector2.one;title.rectTransform.offsetMin=new Vector2(0,2);title.rectTransform.offsetMax=new Vector2(0,23);title.alignment=TextAlignmentOptions.Center;title.fontSize=18;title.fontStyle=FontStyles.Bold;title.color=new Color(1,.95f,.8f);title.outlineWidth=.1f;title.outlineColor=new Color(.12f,.07f,.1f);title.raycastTarget=false;
            if(ApothecaryUI.Instance!=null)title.font=ApothecaryUI.Instance.Config.font;
            title.text=b.Definition.Name;
        }
        void LateUpdate()
        {
            if(boss==null||root==null)return;
            if(orderedFrame!=Time.frameCount)
            {
                orderedFrame=Time.frameCount;orderedBosses.Clear();
                foreach(var b in ColonBossRuntime.Active)if(b!=null&&!b.Dead&&!b.Arriving)orderedBosses.Add(b);
                orderedBosses.Sort(distanceComparer);
            }
            int row=orderedBosses.IndexOf(boss);
            bool visible=row>=0&&row<3&&!MiniStageRuntimeState.IsInsideMiniStage&&(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud");
            root.SetActive(visible);if(!visible)return;
            panel.anchoredPosition=new Vector2(0,-103-row*46);rail.Set(boss.Health/boss.MaxHealth,boss.Definition.Warning,0);
        }
        static float Distance(ColonBossRuntime b)=>b.Player==null?0:Vector2.SqrMagnitude((Vector2)b.transform.position-(Vector2)b.Player.transform.position);
        static int CompareDistance(ColonBossRuntime a,ColonBossRuntime b)=>Distance(a).CompareTo(Distance(b));
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
}
