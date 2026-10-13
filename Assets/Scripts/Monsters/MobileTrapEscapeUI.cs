using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire
{
    public sealed class MobileTrapEscapeUI : MonoBehaviour
    {
        TrapMonster trap;
        RectTransform safe;
        GameObject panel;
        Image[] faces = new Image[4];
        TMP_Text[] labels = new TMP_Text[4];
        TMP_Text progress;
        public static MobileTrapEscapeUI Create(TrapMonster owner)
        {
            var go = new GameObject("Mobile trap escape",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var ui=go.AddComponent<MobileTrapEscapeUI>();ui.trap=owner;ui.Build();ui.Refresh();return ui;
        }
        void Build()
        {
            safe=AugmentPanelTheme.Rect("Safe area",transform);AugmentPanelTheme.Anchors(safe,Vector2.zero,Vector2.one);
            var box=AugmentPanelTheme.Rect("Escape touch panel",safe);box.anchorMin=box.anchorMax=Vector2.right;box.pivot=Vector2.right;
            box.sizeDelta=new Vector2(420,510);box.anchoredPosition=new Vector2(-24,60);panel=box.gameObject;
            var bg=box.gameObject.AddComponent<Image>();bg.color=new Color(.10f,.17f,.18f,.97f);
            var title=AugmentPanelTheme.Label("Instructions",box,TrainingUITheme.Font,"빛나는 매듭을 터치!",30);
            AugmentPanelTheme.Anchors(title.rectTransform,new Vector2(.03f,.87f),new Vector2(.97f,.98f));
            for(int i=0;i<4;i++)
            {
                int index=i;
                var rect=AugmentPanelTheme.Rect("Knot "+i,box);
                AugmentPanelTheme.Anchors(rect,new Vector2(.06f+i%2*.47f,.49f-i/2*.35f),new Vector2(.47f+i%2*.47f,.81f-i/2*.35f));
                faces[i]=rect.gameObject.AddComponent<Image>();
                var outline=rect.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(4,-4);outline.effectColor=Color.white;
                var button=rect.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;
                var contact=rect.gameObject.AddComponent<TrapTapContact>();contact.Trap=trap;
                button.onClick.AddListener(()=>trap.SubmitMobileEscape(index,contact.Revision));
                labels[i]=AugmentPanelTheme.Label("Target",rect,TrainingUITheme.Font,"",31);
                AugmentPanelTheme.Anchors(labels[i].rectTransform,new Vector2(.04f,.05f),new Vector2(.96f,.95f));
            }
            progress=AugmentPanelTheme.Label("Escape progress",box,TrainingUITheme.Font,"",24);
            AugmentPanelTheme.Anchors(progress.rectTransform,new Vector2(.03f,.01f),new Vector2(.97f,.12f));
        }
        void Update()
        {
            if(trap==null||!trap.IsActive){Destroy(gameObject);return;}
            var area=Screen.safeArea;safe.anchorMin=area.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=area.max/new Vector2(Screen.width,Screen.height);
            panel.SetActive(Time.timeScale>0&&!TutorialGuide.IsOpen);
            Refresh();
        }
        public void Refresh()
        {
            if(trap==null||progress==null)return;
            for(int i=0;i<4;i++)
            {
                bool target=i==trap.EscapeTarget;
                faces[i].color=target?new Color(1,.84f,.28f):new Color(.26f,.39f,.36f);
                faces[i].GetComponent<Outline>().enabled=target;
                labels[i].text=target?"터치!":"매듭";
                labels[i].color=target?OctoberArt.Ink:new Color(.76f,.85f,.80f);
            }
            progress.text=$"탈출 {trap.EscapeProgress} / {trap.EscapeLength}";
        }
    }
    public sealed class TrapTapContact : MonoBehaviour, IPointerDownHandler, IPointerExitHandler
    {
        public TrapMonster Trap;
        public int Revision { get; private set; } = -1;
        public void OnPointerDown(PointerEventData data) => Revision=Trap!=null&&Trap.AcceptsMobileEscape?Trap.EscapeRevision:-1;
        public void OnPointerExit(PointerEventData data) => Revision=-1;
    }
}
