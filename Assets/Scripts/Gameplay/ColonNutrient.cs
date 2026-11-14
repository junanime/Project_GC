using UnityEngine;

namespace Vampire
{
    // No Monster/IDamageable: needles cannot consume it or award kills. One interaction, one award.
    public sealed class ColonNutrient : MonoBehaviour
    {
        public bool Collected { get; internal set; }
        public bool Healing { get; private set; }
        ColonFieldEvents owner;
        SpriteRenderer visual;
        LineRenderer ring;
        float age, collectAge;
        Vector3 scale;
        public static ColonNutrient Create(ColonFieldEvents events, Vector2 at, bool heal)
        {
            var go = new GameObject(heal ? "영양 덩어리 · 체력 회복" : "영양 덩어리 · 경험치"); go.transform.SetParent(events.transform, false); go.transform.position = at;
            var n = go.AddComponent<ColonNutrient>(); n.owner = events; n.Healing = heal;
            n.visual = ColonEventVisual.Sprite(go.transform, "과육·곡물 영양분", Resources.Load<Sprite>("ColonEvents/Nutrient"), at, .52f);
            n.scale = n.visual.transform.localScale;
            if (heal) n.visual.color = new Color(1, .91f, .91f);
            n.ring = ColonEventVisual.Line(go.transform, "상호작용 표시", .025f, heal ? new Color(1, .65f, .63f, .7f) : new Color(.77f, 1, .70f, .7f));
            n.ring.positionCount = 33;
            for (int i = 0; i < 33; i++) { float a = i * Mathf.PI / 16; n.ring.SetPosition(i, at + new Vector2(Mathf.Cos(a) * .40f, Mathf.Sin(a) * .18f)); }
            go.AddComponent<ZPositioner>(); go.AddComponent<MapMarker>().Configure(MapMarkerKind.Event, heal ? "영양분 · 상호작용 시 체력 회복" : "영양분 · 상호작용 시 경험치");
            InteractionFocus.Register(n, () => n.Available); return n;
        }
        public bool Available => owner != null && owner.Running && owner.Current == ColonEventKind.Nutrients && !Collected && owner.Age > .35f &&
            Vector2.Distance(owner.Player.Position, transform.position) <= 1.25f && ColonAttackWall.Clear(owner.Player.Position, transform.position);
        public bool Interact()
        { if (!Available || !owner.Claim(this)) return false; ring.enabled = false; return true; }
        void Update()
        {
            PixelInteractionPrompt.Show(this, Available && InteractionFocus.IsFocused(this));
            if (owner == null || owner.Paused) return;
            age += Time.deltaTime;
            if (!Collected)
            {
                float pop = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .5f));
                visual.transform.localScale = scale * pop;
                visual.transform.localPosition = Vector3.up * (.06f + Mathf.Sin(age * 3) * .045f);
                if (Available && GameInput.TryConsumeInteraction(KeyCode.E, this)) Interact();
            }
            else
            {
                collectAge += Time.deltaTime; float q = Mathf.Clamp01(collectAge / .45f);
                visual.transform.position = Vector3.Lerp(transform.position + Vector3.up * .12f, owner.Player.CenterTransform.position, q);
                visual.transform.localScale = scale * (1 - q * .7f); var c = visual.color; c.a = 1 - q; visual.color = c;
                if (q >= 1) { gameObject.SetActive(false); }
            }
        }
        void OnDisable() { PixelInteractionPrompt.Show(this, false); }
    }
}
