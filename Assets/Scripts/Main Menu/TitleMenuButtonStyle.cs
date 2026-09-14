using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire
{
    public class TitleMenuButtonStyle : MonoBehaviour
    {
        public Button startButton;
        public TMP_FontAsset font;
        public UnityEvent onContinue = new UnityEvent();
        public UnityEvent onSettings = new UnityEvent();
        private readonly TitleMenuSurface[] surfaces = new TitleMenuSurface[4];
        public int ActiveIndex { get; private set; }
        private bool ready;

        private void OnEnable()
        {
            if (!ready) Build();
            Select(0);
            if (ready && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        private void Build()
        {
            if (startButton == null) return;
            string[] names = { "게임 시작", "이어하기", "설정", "종료" };
            for (int i = 0; i < 4; i++)
            {
                Button button = i == 0 ? startButton : new GameObject("Menu " + names[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Button)).GetComponent<Button>();
                if (i != 0) button.transform.SetParent(transform, false);
                foreach (Transform child in button.transform) child.gameObject.SetActive(false);
                var oldImage = button.GetComponent<Image>();
                if (oldImage == null) oldImage = button.gameObject.AddComponent<Image>();
                oldImage.color = Color.clear; oldImage.raycastTarget = true;
                button.transition = Selectable.Transition.None;
                var rect = (RectTransform)button.transform;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.pivot = new Vector2(.5f, .5f);
                var surface = new GameObject("Button surface", typeof(RectTransform)).AddComponent<TitleMenuSurface>();
                surface.transform.SetParent(rect, false);
                surface.rectTransform.anchorMin = Vector2.zero; surface.rectTransform.anchorMax = Vector2.one;
                surface.rectTransform.offsetMin = surface.rectTransform.offsetMax = Vector2.zero;
                surface.raycastTarget = false;
                var input = button.gameObject.AddComponent<TitleMenuButtonInput>(); input.owner = this; input.index = i;
                surface.owner = this; surface.index = i; surface.button = button;
                surfaces[i] = surface; button.targetGraphic = oldImage;
                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(rect, false); label.font = font;
                label.text = names[i]; label.alignment = TextAlignmentOptions.Midline;
                label.fontStyle = FontStyles.Bold; label.raycastTarget = false;
                var lr = label.rectTransform; lr.anchorMin = new Vector2(.28f, 0); lr.anchorMax = new Vector2(.9f, 1); lr.offsetMin = lr.offsetMax = Vector2.zero;
                surface.label = label;
                if (i == 1) button.onClick.AddListener(() => onContinue.Invoke());
                if (i == 2) button.onClick.AddListener(() => onSettings.Invoke());
                if (i == 3) button.onClick.AddListener(Application.Quit);
            }
            ready = true;
            for (int i = 0; i < 4; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = surfaces[(i + 3) % 4].button,
                    selectOnDown = surfaces[(i + 1) % 4].button };
                surfaces[i].button.navigation = nav;
            }
        }

        public void Select(int index)
        {
            if (!ready || index < 0 || index >= 4 || !surfaces[index].button.IsInteractable()) return;
            ActiveIndex = index;
            for (int i = 0; i < 4; i++) surfaces[i].SetActiveStyle(i == index);
        }
    }

    public class TitleMenuButtonInput : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public TitleMenuButtonStyle owner; public int index;
        public void OnPointerEnter(PointerEventData data) {
            if (!GetComponent<Button>().IsInteractable()) return;
            owner.Select(index);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnSelect(BaseEventData data) { owner.Select(index); }
    }

    public class TitleMenuSurface : MaskableGraphic
    {
        public TitleMenuButtonStyle owner;
        public Button button;
        public TextMeshProUGUI label;
        public int index;
        public bool Selected { get; private set; }
        private static readonly string[] Icons = {
            "1000000/1100000/1111000/1111110/1111000/1100000/1000000",
            "0011100/0100010/1100001/1110001/0000001/0100010/0011100",
            "0010100/0111110/1100011/0100010/1100011/0111110/0010100",
            "0111110/0100010/0101010/0100010/0100010/0100010/0111110" };

        public void SetActiveStyle(bool value) { Selected = value; Layout(); SetVerticesDirty(); }
        private void Update()
        {
            if (owner == null) return;
            Layout();
            if (Selected) SetVerticesDirty();
        }
        private void Layout()
        {
            float width = (Selected ? 336f : 298f) / 1672f;
            float center = 1f - (645f + index * 65f) / 941f;
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(.5f - width / 2, center - 29f / 941f);
            buttonRect.anchorMax = new Vector2(.5f + width / 2, center + 29f / 941f);
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            if (label != null) { label.fontSize = rectTransform.rect.height * .55f; label.color = Selected ? Color.white : new Color(.74f,.67f,.93f); }
        }
        private Vector2[] Outline(float inset)
        {
            Rect r = rectTransform.rect; float l=r.xMin+inset, b=r.yMin+inset, t=r.yMax-inset, right=r.xMax-inset;
            float c=(t-b)*.2f;
            return new[] {new Vector2(l+c,t),new Vector2(right-c,t),new Vector2(right,t-c),new Vector2(right,b+c),new Vector2(right-c,b),new Vector2(l+c,b),new Vector2(l,b+c),new Vector2(l,t-c)};
        }
        private void Fill(VertexHelper vh, Vector2[] p, Color c)
        {
            int start=vh.currentVertCount; vh.AddVert(Vector3.zero,c,Vector2.zero);
            foreach(var v in p) vh.AddVert(v,c,Vector2.zero);
            for(int i=0;i<p.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%p.Length);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int s=vh.currentVertCount;
            vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddVert(a-n,c,Vector2.zero);vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float px=rectTransform.rect.height/58f;
            Fill(vh,Outline(0),new Color(.1f,.065f,.24f));
            Fill(vh,Outline(2*px),Selected?new Color(1,.65f,.75f):new Color(.47f,.42f,.7f));
            Fill(vh,Outline(4*px),Selected?new Color(.97f,.19f,.4f):new Color(.18f,.12f,.34f));
            var path=Outline(px);float total=0;for(int i=0;i<8;i++)total+=Vector2.Distance(path[i],path[(i+1)%8]);
            if(Selected){
                float head=Mathf.Repeat(Time.unscaledTime*.25f,1)*total;float travelled=0;
                for(int i=0;i<8;i++){
                    Vector2 a=path[i],b=path[(i+1)%8];float len=Vector2.Distance(a,b);
                    Line(vh,a,b,px,new Color(1,.78f,.17f));
                    if(head>=travelled&&head<travelled+len){Vector2 pos=Vector2.Lerp(a,b,(head-travelled)/len);Line(vh,pos-Vector2.right*4*px,pos+Vector2.right*4*px,px,new Color(1,1,.78f));Line(vh,pos-Vector2.up*3*px,pos+Vector2.up*3*px,px,Color.white);}
                    travelled+=len;
                }
            }
            var rows=Icons[Mathf.Clamp(index,0,3)].Split('/');float unit=px*3.3f;Vector2 origin=new Vector2(-rectTransform.rect.width*.24f-unit*3.5f,unit*3.5f);
            Color ink=Selected?Color.white:new Color(.74f,.67f,.93f);
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)if(rows[y][x]=='1'){Vector2 a=origin+new Vector2(x*unit,-y*unit);Line(vh,a+new Vector2(0,-unit/2),a+new Vector2(unit,-unit/2),unit,ink);}
        }
    }
}
