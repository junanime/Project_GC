using TMPro;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>UI version of the field interaction key, with a changeable legend and length.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InteractionKeycapGraphic : MaskableGraphic
    {
        static Sprite keyArt;
        bool attention;
        float attentionStarted, nextGlint;
        public TextMeshProUGUI Legend { get; private set; }
        public bool Highlighted => attention;
        public RectTransform Bounds => rectTransform;
        public override Texture mainTexture => keyArt != null ? keyArt.texture : base.mainTexture;

        public static InteractionKeycapGraphic Create(string name, Transform parent, string legend, float height)
        {
            if (keyArt == null) keyArt = Resources.Load<Sprite>("Interaction/KeycapE_Pixel");
            var holder = AugmentPanelTheme.Rect(name, parent);
            var cap = holder.gameObject.AddComponent<InteractionKeycapGraphic>();
            cap.raycastTarget = false;
            cap.Legend = AugmentPanelTheme.Label("Key", holder, TrainingUITheme.Font, legend, height * .54f);
            AugmentPanelTheme.Anchors(cap.Legend.rectTransform, new Vector2(.08f,.18f), new Vector2(.92f,.92f));
            cap.Legend.color = new Color32(43,42,42,255);
            cap.Legend.fontStyle = FontStyles.Bold;
            cap.Legend.enableWordWrapping = false;
            cap.ShowKey(legend, height);
            return cap;
        }

        public void ShowKey(string legend, float height, bool highlight = false)
        {
            // Keep the actual binding readable even if the visitor has rebound a control.
            float units = legend == "Space" ? 4.3f : legend == "Shift" ? 2f :
                legend == "Tab" ? 1.55f : Mathf.Clamp(.52f + legend.Length * .35f, 1f, 3.8f);
            var size = new Vector2(Mathf.Round(height * units), height);
            if (rectTransform.sizeDelta != size) rectTransform.sizeDelta = size;
            if (Legend.text != legend) Legend.text = legend;
            Legend.fontSizeMax = height * .56f;
            Legend.fontSizeMin = height * .31f;
            if (attention == highlight) return;
            attention = highlight;
            attentionStarted = Time.unscaledTime;
            SetVerticesDirty();
        }

        void Update()
        {
            if (!attention || Time.unscaledTime < nextGlint) return;
            nextGlint = Time.unscaledTime + .035f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (keyArt == null) return;
            Rect r = GetPixelAdjustedRect();
            Vector4 uv = DataUtility.GetOuterUV(keyArt);
            // The field sprite is imported point-filtered on a 32px grid. Its two
            // blank shoulders preserve the stepped silhouette, white glint and
            // deep front lip; only a single unprinted column expands across a long key.
            float shoulder = Mathf.Min(r.height * (8f / 26f), r.width * .4f);
            if (attention) DrawAttention(vh, r, uv);
            Strip(vh, r.xMin, r.xMin + shoulder, r.yMin, r.yMax, uv, 3, 11, 3, 29, Color.white);
            Strip(vh, r.xMin + shoulder, r.xMax - shoulder, r.yMin, r.yMax, uv, 10.5f, 10.5f, 3, 29, Color.white);
            Strip(vh, r.xMax - shoulder, r.xMax, r.yMin, r.yMax, uv, 21, 29, 3, 29, Color.white);
        }

        static void Strip(VertexHelper vh, float x0, float x1, float y0, float y1,
            Vector4 uv, float u0, float u1, float v0, float v1, Color tint)
        {
            var left = Mathf.Lerp(uv.x, uv.z, u0/32f);
            var right = Mathf.Lerp(uv.x, uv.z, u1/32f);
            var bottom = Mathf.Lerp(uv.y, uv.w, v0/32f);
            var top = Mathf.Lerp(uv.y, uv.w, v1/32f);
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(x0,y0), tint, new Vector2(left,bottom));
            vh.AddVert(new Vector2(x0,y1), tint, new Vector2(left,top));
            vh.AddVert(new Vector2(x1,y1), tint, new Vector2(right,top));
            vh.AddVert(new Vector2(x1,y0), tint, new Vector2(right,bottom));
            vh.AddTriangle(i,i+1,i+2);
            vh.AddTriangle(i,i+2,i+3);
        }

        void DrawAttention(VertexHelper vh, Rect r, Vector4 uv)
        {
            // Sample the white reflection on the E key for solid, pixel-edged glints.
            void Bar(float x0, float x1, float y0, float y1, Color tint) =>
                Strip(vh,x0,x1,y0,y1,uv,9,9,25.5f,25.5f,tint);
            float phase = Mathf.Repeat((Time.unscaledTime-attentionStarted) / 1.65f, 1f);
            float unit = Mathf.Max(1f, Mathf.Round(r.height / 24f));
            float inset = 3f*unit;
            var rim = new Color(1f,.78f,.29f,.65f+.30f*Mathf.Cos(2f*Mathf.PI*phase));
            Bar(r.xMin+inset,r.xMax-inset,r.yMax-unit,r.yMax+unit,rim);
            Bar(r.xMin+inset,r.xMax-inset,r.yMin-unit,r.yMin+unit,rim);
            Bar(r.xMin-unit,r.xMin+unit,r.yMin+inset,r.yMax-inset,rim);
            Bar(r.xMax-unit,r.xMax+unit,r.yMin+inset,r.yMax-inset,rim);
            float center = Mathf.Lerp(r.xMin+inset, r.xMax-inset, phase);
            float x0 = Mathf.Max(r.xMin+inset,center-r.width*.09f);
            float x1 = Mathf.Min(r.xMax-inset,center+r.width*.09f);
            Bar(x0,x1,r.yMax,r.yMax+2*unit,new Color(1f,.99f,.84f));
        }
    }
}
