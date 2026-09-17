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
        public Sprite activeBody;
        public Sprite normalBody;
        public UnityEvent onContinue = new UnityEvent();
        public UnityEvent onSettings = new UnityEvent();
        private readonly TitleMenuSurface[] surfaces = new TitleMenuSurface[4];
        public int ActiveIndex { get; private set; }
        private bool ready;
        private TMP_FontAsset runtimeFont;

        private void OnDestroy()
        {
            if (runtimeFont == null) return;
            foreach (var atlas in runtimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
            Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }

        private void OnEnable()
        {
            if (!ready) Build();
            Select(0);
            if (ready && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        private void Build()
        {
            if (startButton == null) return;
            // Build a clean atlas from the source face instead of inheriting stale localized glyphs.
            if (font != null && font.sourceFontFile != null) runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
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
                var surface = new GameObject("Button surface", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<TitleMenuSurface>();
                surface.transform.SetParent(rect, false);
                surface.rectTransform.anchorMin = Vector2.zero; surface.rectTransform.anchorMax = Vector2.one;
                surface.rectTransform.offsetMin = surface.rectTransform.offsetMax = Vector2.zero;
                surface.raycastTarget = false;
                var input = button.gameObject.AddComponent<TitleMenuButtonInput>(); input.owner = this; input.index = i;
                surface.owner = this; surface.index = i; surface.button = button;
                surfaces[i] = surface; button.targetGraphic = oldImage;
                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(rect, false); label.font = runtimeFont != null ? runtimeFont : font;
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

    [RequireComponent(typeof(CanvasRenderer))]
    public class TitleMenuSurface : MaskableGraphic
    {
        public TitleMenuButtonStyle owner;
        public Button button;
        public TextMeshProUGUI label;
        public int index;
        public bool Selected { get; private set; }
        private static readonly string[] Icons = {
            "1100000/1111000/1111100/1111111/1111100/1111000/1100000",
            "0011100/0100010/1100001/1110001/0000001/0100010/0011100",
            "0010100/0111110/1100011/0100010/1100011/0111110/0010100",
            "0111110/0100010/0101010/0100010/0100010/0100010/0111110" };

        public void SetActiveStyle(bool value) {
            Selected = value;
            var image = button.targetGraphic as Image;
            var sprite = value ? owner.activeBody : owner.normalBody;
            if (image != null && sprite != null) { image.sprite = sprite; image.color = Color.white; image.type = Image.Type.Sliced; }
            Layout(); SetVerticesDirty();
        }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (label != null) label.fontSize = rectTransform.rect.height * .55f;
        }
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
            var image = button.targetGraphic as Image;
            if (image != null && image.sprite != null && (owner.activeBody != null || owner.normalBody != null))
                image.pixelsPerUnitMultiplier = image.sprite.rect.height / Mathf.Max(1, buttonRect.rect.height);
            if (label != null) { label.fontSize = rectTransform.rect.height * .55f; label.color = Selected ? Color.white : new Color(.74f,.67f,.93f); }
        }
        private Vector2[] Outline(float inset)
        {
            Rect r = rectTransform.rect; float l=r.xMin+inset, b=r.yMin+inset, t=r.yMax-inset, right=r.xMax-inset;
            float c=(t-b)*.24f, s=c/3f;
            // Three square steps at each corner preserve the rounded pixel-art silhouette.
            return new[] {new Vector2(l+c,t),new Vector2(right-c,t),
                new Vector2(right-c,t-s),new Vector2(right-s,t-s),new Vector2(right-s,t-c),new Vector2(right,t-c),
                new Vector2(right,b+c),new Vector2(right-s,b+c),new Vector2(right-s,b+s),new Vector2(right-c,b+s),new Vector2(right-c,b),
                new Vector2(l+c,b),new Vector2(l+c,b+s),new Vector2(l+s,b+s),new Vector2(l+s,b+c),new Vector2(l,b+c),
                new Vector2(l,t-c),new Vector2(l+s,t-c),new Vector2(l+s,t-s),new Vector2(l+c,t-s)};
        }
        private void Fill(VertexHelper vh, Vector2[] p, Color c, Color? bottom = null)
        {
            Color low = bottom ?? c;
            int start=vh.currentVertCount; vh.AddVert(Vector3.zero,Color.Lerp(low,c,.5f),Vector2.zero);
            foreach(var v in p) vh.AddVert(v,Color.Lerp(low,c,Mathf.InverseLerp(rectTransform.rect.yMin,rectTransform.rect.yMax,v.y)),Vector2.zero);
            for(int i=0;i<p.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%p.Length);
        }
        private Vector2 PathPoint(Vector2[] path, float distance)
        {
            for(int i=0;i<path.Length;i++){
                Vector2 a=path[i],b=path[(i+1)%path.Length];float length=Vector2.Distance(a,b);
                if(distance<=length)return Vector2.Lerp(a,b,distance/Mathf.Max(.001f,length));distance-=length;
            }
            return path[0];
        }
        private void Glow(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            const int segments=24;int first=vh.currentVertCount;
            vh.AddVert(center,color,Vector2.zero);Color edge=color;edge.a=0;
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);}
            for(int i=0;i<segments;i++)vh.AddTriangle(first,first+i+1,first+i+2);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int s=vh.currentVertCount;
            vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddVert(a-n,c,Vector2.zero);vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float px=rectTransform.rect.height/58f;
            if (owner == null || owner.activeBody == null || owner.normalBody == null) {
            Fill(vh,Outline(0),new Color(.09f,.055f,.22f));
            Fill(vh,Outline(1.6f*px),Selected?new Color(1,.86f,.26f):new Color(.55f,.49f,.77f),Selected?new Color(.91f,.57f,.1f):new Color(.31f,.25f,.55f));
            Fill(vh,Outline(3.2f*px),new Color(.12f,.07f,.27f));
            Fill(vh,Outline(4.8f*px),Selected?new Color(1,.73f,.81f):new Color(.43f,.37f,.66f));
            Fill(vh,Outline(6.3f*px),Selected?new Color(1,.33f,.53f):new Color(.22f,.16f,.4f),Selected?new Color(.91f,.105f,.35f):new Color(.15f,.1f,.29f));
            }
            var path=Outline(2.2f*px);float total=0;for(int i=0;i<path.Length;i++)total+=Vector2.Distance(path[i],path[(i+1)%path.Length]);
            if(Selected){
                float head=Mathf.Repeat(Time.unscaledTime*.25f,1)*total;
                for(int i=9;i>0;i--)Glow(vh,PathPoint(path,Mathf.Repeat(head-i*2.5f*px,total)),4*px,new Color(1,.77f,.15f,.26f*(1-i/10f)));
                Vector2 pos=PathPoint(path,head);float pulse=.9f+.1f*Mathf.Sin(Time.unscaledTime*3);
                Glow(vh,pos,12*px,new Color(1,.74f,.13f,.35f*pulse));
                Glow(vh,pos,6.5f*px,new Color(1,.9f,.32f,.9f*pulse));
                Glow(vh,pos,3.2f*px,new Color(1,1,.92f,1));
                Line(vh,pos-Vector2.right*3*px,pos+Vector2.right*3*px,px,new Color(1,1,.9f,.85f));
                Line(vh,pos-Vector2.up*3*px,pos+Vector2.up*3*px,px,new Color(1,1,.9f,.85f));
            }
            var rows=Icons[Mathf.Clamp(index,0,3)].Split('/');float unit=px*3.3f;Vector2 origin=new Vector2(-rectTransform.rect.width*.24f-unit*3.5f,unit*3.5f);
            Color ink=Selected?Color.white:new Color(.74f,.67f,.93f);
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)if(rows[y][x]=='1'){Vector2 a=origin+new Vector2(x*unit,-y*unit);Line(vh,a+new Vector2(0,-unit/2),a+new Vector2(unit,-unit/2),unit,ink);}
        }
    }
}
