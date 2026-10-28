using UnityEngine;
namespace Vampire
{
    public enum StatusTintKind { Poison, Corrosion, Ice, Fire }

    // Visuals submit every frame. Only the active slot is displayed, never an additive mix.
    [DefaultExecutionOrder(30000), DisallowMultipleComponent]
    public sealed class StatusTintCycle : MonoBehaviour
    {
        readonly SpriteRenderer[] overlays = new SpriteRenderer[4];
        readonly int[] frames = { -1, -1, -1, -1 };
        SpriteRenderer body;
        Monster monster;
        int selected = -1;
        float elapsed;
        MaterialPropertyBlock sourceProperties, properties;
        public int Selected => selected;
        public static void Withdraw(SpriteRenderer body, SpriteRenderer overlay, StatusTintKind kind)
        {
            if(body==null)return;
            var cycle=body.GetComponent<StatusTintCycle>();int slot=(int)kind;
            if(cycle!=null&&cycle.overlays[slot]==overlay){cycle.overlays[slot]=null;cycle.frames[slot]=-1;}
        }
        public static void Submit(SpriteRenderer body, SpriteRenderer overlay, StatusTintKind kind)
        {
            if (body == null || overlay == null) return;
            var cycle = body.GetComponent<StatusTintCycle>() ?? body.gameObject.AddComponent<StatusTintCycle>();
            cycle.body = body;
            cycle.monster = body.GetComponentInParent<Monster>();
            int slot = (int)kind;
            if (cycle.overlays[slot] != null && cycle.overlays[slot] != overlay) cycle.overlays[slot].enabled = false;
            cycle.overlays[slot] = overlay;
            cycle.frames[slot] = Time.frameCount;
        }
        void LateUpdate()
        {
            bool alive = body != null && body.enabled && SyringeAugmentVfx.IsLiving(body);
            int mask = 0;
            for (int i = 0; i < 4; i++)
                if (overlays[i] != null)
                {
                    overlays[i].enabled = false;
                    if (alive && frames[i] == Time.frameCount && overlays[i].gameObject.activeInHierarchy) mask |= 1 << i;
                }
            if (mask == 0) { selected = -1; elapsed = 0; return; }
            if (monster == null || !monster.IsFieldRuntimeSuspended) elapsed += Time.deltaTime;
            if (selected < 0 || (mask & (1 << selected)) == 0 || elapsed >= .65f)
            {
                for (int step = 1; step <= 4; step++)
                {
                    int next = (selected + step + 4) % 4;
                    if ((mask & (1 << next)) != 0) { selected = next; break; }
                }
                elapsed = 0;
            }
            var active = overlays[selected];
            active.enabled = true;
            // Preserve bespoke boss limb/flight clipping when tinting the same sprite.
            sourceProperties = sourceProperties ?? new MaterialPropertyBlock();
            properties = properties ?? new MaterialPropertyBlock();
            body.GetPropertyBlock(sourceProperties); active.GetPropertyBlock(properties);
            string shader = body.sharedMaterial != null ? body.sharedMaterial.shader.name : "";
            properties.SetFloat("_BodyMask", shader == "FoodBosses/CleanFlightBody" ? 1 : shader == "FoodBosses/ArticulatedBody" ? 2 : 0);
            properties.SetFloat("_Phase", sourceProperties.GetFloat("_Phase"));
            properties.SetVector("_Cell", sourceProperties.GetVector("_Cell"));
            active.SetPropertyBlock(properties);
        }
        void OnDisable()
        {
            for (int i = 0; i < 4; i++) { if (overlays[i] != null) overlays[i].enabled = false; overlays[i] = null; frames[i] = -1; }
            selected = -1; elapsed = 0;
        }
    }
}
