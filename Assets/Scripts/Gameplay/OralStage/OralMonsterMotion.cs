using UnityEngine;

namespace Vampire
{
    [DefaultExecutionOrder(9000)]
    public sealed class OralMonsterMotion : MonoBehaviour
    {
        Monster owner;
        SpriteRenderer body;
        OralFoodSkin skin;
        Sprite original;
        Vector3 rootScale, scale, position;
        Quaternion rotation;
        float phase, impact, direction;
        int kind;

        public void Configure(Monster monster, int foodKind, OralFoodSkin[] variants)
        {
            ResetForPool();
            owner = monster; kind = foodKind; body = monster.BodyRenderer;
            if (body == null) return;
            original = body.sprite;
            scale = body.transform.localScale; position = body.transform.localPosition;
            rotation = body.transform.localRotation; rootScale = transform.localScale;
            if (variants != null && variants.Length > 0)
            {
                skin = variants[OralMonsterRoster.ChoosePalette(monster, kind, variants.Length)];
                body.sprite = skin.Body;
                // Scale the actor: body, hurtbox, ground collider and shadow stay together.
                transform.localScale = rootScale * Random.Range(.94f, 1.065f);
            }
            phase = Random.value * Mathf.PI * 2f; impact = 0;
            enabled = true;
        }

        public bool React(float damage, Vector2 knockback, bool lethal)
        {
            if (!isActiveAndEnabled || skin == null || body == null || kind > 1 || damage <= 0) return false;
            var player = OralAmbientContent.Instance?.Level?.PlayerCharacter;
            float x = Mathf.Abs(knockback.x) > .02f ? knockback.x : player != null ? owner.Position.x - player.Position.x : 1f;
            direction = x < 0 ? -1 : 1;
            impact = 1f;
            GStarImpactAudio.MaterialHit(kind == 1, lethal);
            if (lethal && !body.forceRenderingOff && body.enabled && OralAmbientContent.Instance != null)
            {
                OralAmbientContent.Instance.FoodBreaks.Show(skin, kind == 1, body, direction);
                body.enabled = false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (body == null || owner == null || owner.HP <= 0 || owner.IsFieldRuntimeSuspended) return;
            float dt = Time.deltaTime;
            if (skin != null && body.sprite == original) body.sprite = skin.Body;
            phase += dt * (kind == 2 ? 5.2f : 8f);
            impact = Mathf.MoveTowards(impact, 0, dt * 6.5f);
            float wave = Mathf.Sin(phase);
            float squash = wave * .045f;
            float lift = Mathf.Abs(wave) * .012f;
            float tilt = 0;
            if (skin != null && kind == 0)
            {
                tilt = wave * 7f - direction * impact * 11f;
                squash *= .35f;
            }
            else if (skin != null && kind == 1)
            {
                squash = wave * .065f - Mathf.Sin(impact * Mathf.PI * 1.35f) * .16f;
                tilt = -direction * impact * 8f;
                lift *= 1.45f;
            }
            body.transform.localScale = Vector3.Scale(scale, new Vector3(1 - squash, 1 + squash, 1));
            body.transform.localPosition = position + Vector3.up * lift;
            body.transform.localRotation = rotation * Quaternion.Euler(0, 0, tilt);
        }

        public void ResetForPool()
        {
            if (body != null)
            {
                body.transform.localScale = scale; body.transform.localPosition = position;
                body.transform.localRotation = rotation; body.sprite = original;
                transform.localScale = rootScale;
            }
            owner = null; body = null; skin = null; original = null;
            enabled = false;
        }
        void OnDisable() { if (body != null) ResetForPool(); }
    }
}
