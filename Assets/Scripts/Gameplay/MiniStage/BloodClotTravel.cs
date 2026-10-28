using System.Collections;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    /// <summary>Only the travelling actor is locked. No global clock or monster suspension.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class BloodClotTravel : MonoBehaviour
    {
        public const float Duration = 1.8f;
        public const float ExitInvincibilityDuration = .5f;
        public const float HyukiWalkEnd = .42f, HyukiLaunch = .54f, HyukiApex = .74f;
        public const int HyukiCurledFrame = 4;
        public bool Busy { get; private set; }
        public bool HoldingPose { get; private set; }
        // Separate from the movement lock: landing protection must not prevent movement.
        public bool IsDamageProtected => isActiveAndEnabled && (Busy || Time.time < protectedUntil);
        float protectedUntil = -1f;
        bool reservedPose;
        public void HoldForInteraction(){RestorePhysics();Busy=false;HoldingPose=true;reservedPose=true;protectedUntil=float.PositiveInfinity;}
        public string CharacterKey { get; private set; }
        public SpriteRenderer Visual { get; private set; }
        public float ReferenceHeight => height;
        Character owner;
        SpriteRenderer original;
        Rigidbody2D body;
        bool simulated, originalHidden;
        Sprite[] frames;
        Sprite swimSprite;
        float height, scale;
        Vector3 footOffset;
        SpriteRenderer[] chicks;
        Transform animatedPortal;
        Vector3 portalScale;

        public static BloodClotTravel Ensure(Character actor) => actor.GetComponent<BloodClotTravel>() ?? actor.gameObject.AddComponent<BloodClotTravel>();
        static Sprite[] RawFrames(string key) => Resources.LoadAll<Sprite>("BloodClotTravel/" + key).OrderBy(s => s.name).ToArray();
        public static Sprite[] Frames(string key) => RawFrames(key == "Hyuki" ? "HyukiApproved" : key);
        public static bool CanTravel(Character actor) => actor != null && actor.IsAlive && !actor.IsDashing && !actor.IsTrapBound &&
            !(actor.GetComponent<PlayerMiniStageStunRuntime>()?.IsStunned ?? false) && !(actor.Skills?.IsCutin ?? false) && !actor.IsPortalTravelling;

        public void Begin()
        {
            if (Busy) return;
            owner = GetComponent<Character>();
            if (HoldingPose) ReleasePose();
            original = GetComponentInChildren<SpriteAnimator>(true)?.GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            CharacterKey = OctoberArt.CharacterKey(owner.Blueprint);
            frames = Frames(CharacterKey);
            if (frames.Length == 0 || original == null) throw new System.InvalidOperationException("Missing blood clot travel art: " + CharacterKey);
            // Renderer.bounds includes the transparent cell around the standing sprite. Travel
            // sheets are tightly cropped, so matching those rectangles inflated every character.
            // Use one baked idle silhouette for the whole journey; never renormalize each pose.
            var idle = owner.Blueprint?.idleSpriteSequence?.FirstOrDefault(s => s != null) ?? original.sprite;
            var visible = VisibleBodyGeometry.Bounds(idle);
            if(swimSprite!=null)Destroy(swimSprite);
            float ppu=idle.pixelsPerUnit;
            var crop=new Rect(idle.rect.x+idle.pivot.x+visible.xMin*ppu,idle.rect.y+idle.pivot.y+visible.yMin*ppu,visible.width*ppu,visible.height*ppu);
            crop.xMin=Mathf.Max(idle.rect.xMin,crop.xMin);crop.yMin=Mathf.Max(idle.rect.yMin,crop.yMin);crop.xMax=Mathf.Min(idle.rect.xMax,crop.xMax);crop.yMax=Mathf.Min(idle.rect.yMax,crop.yMax);
            swimSprite=Sprite.Create(idle.texture,crop,Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);
            height = original.transform.TransformVector(Vector3.up * visible.height).magnitude;
            var foot = new Vector3(visible.center.x, visible.yMin, 0);
            if (original.flipX) foot.x = -foot.x;
            footOffset = original.transform.TransformPoint(foot) - transform.position;
            // Approved Hyuki poses share the gameplay PPU/pivot. Atlas padding and
            // tucked legs must not resize his head when switching into travel.
            scale = CharacterKey == "Hyuki" ? Mathf.Abs(original.transform.lossyScale.y) : height / Mathf.Max(.001f, frames[0].bounds.size.y);
            if (Visual == null)
            {
                var go = new GameObject("Blood clot travel visual");
                Visual = go.AddComponent<SpriteRenderer>();
                Visual.sharedMaterial = original.sharedMaterial;
            }
            Visual.sortingLayerID = original.sortingLayerID;
            Visual.sortingOrder = original.sortingOrder + 3;
            Visual.gameObject.SetActive(true);
            originalHidden = original.forceRenderingOff;
            original.forceRenderingOff = true;
            if (body != null) { simulated = body.simulated; body.velocity = Vector2.zero; body.angularVelocity = 0; body.simulated = false; }
            Busy = true;
            HoldingPose = false;
        }

        public IEnumerator Dive(Transform portal)
        {
            Begin();
            Vector3 start = transform.position, end = portal != null ? portal.position : start;
            yield return Animate(false, start, end, portal);
        }
        public void SwimPose(Vector3 position,float time)
        {
            if(!Busy)Begin();SetPosition(position);
            Visual.sprite=swimSprite;
            Visual.transform.localScale=Vector3.one*(height/swimSprite.bounds.size.y);
            Visual.transform.position=position+footOffset+Vector3.up*height*.5f;
            Visual.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(time*7)*4-12);
            Visual.flipX=true;Visual.color=Color.white;
        }

        public void CapsulePose(Vector3 position,float alpha)
        {
            SwimPose(position,0);Visual.transform.rotation=Quaternion.identity;Visual.color=new Color(1,1,1,alpha);
            Visual.sortingLayerName="Monster Full";Visual.sortingOrder=32011;
        }

        public IEnumerator Eject(Transform portal, Vector3 landing)
        {
            if (!Busy) Begin();
            Vector3 source = portal != null ? portal.position : landing + Vector3.right;
            yield return Animate(true, source, landing, portal);
            if(!Busy)yield break;
            SetPosition(landing);
            RestorePhysics();
            // Reactivating a simulated body can restore its previous physics pose this frame.
            // Commit the landing again before room logic / collision callbacks observe it.
            SetPosition(landing);Physics2D.SyncTransforms();
            protectedUntil = Time.time + ExitInvincibilityDuration;
            Busy = false;
            HoldingPose = true;
        }

        IEnumerator Animate(bool eject, Vector3 source, Vector3 destination, Transform portal)
        {
            var portalRenderer = portal != null ? portal.GetComponentInChildren<SpriteRenderer>() : null;
            float mouth = portalRenderer != null ? Mathf.Max(height * .2f, portalRenderer.bounds.max.y - portal.position.y - portalRenderer.bounds.size.y * .22f) : height * .65f;
            if (portalRenderer != null) { animatedPortal = portalRenderer.transform; portalScale = animatedPortal.localScale; }
            for (float age = 0; age < Duration && Busy && isActiveAndEnabled && owner != null && owner.IsAlive; age += Time.deltaTime)
            {
                Draw(eject, Mathf.Clamp01(age / Duration), source, destination, mouth);
                if (animatedPortal != null)
                {
                    float phase = age / Duration;
                    float pulse = eject ? Mathf.Sin(Mathf.Clamp01(phase / .18f) * Mathf.PI) : Mathf.Sin(Mathf.Clamp01((phase - .72f) / .28f) * Mathf.PI);
                    animatedPortal.localScale = Vector3.Scale(portalScale, new Vector3(1 + pulse * .1f, 1 - pulse * .08f, 1));
                }
                yield return null;
            }
            RestorePortal();
            if(!Busy)yield break;
            Draw(eject, 1, source, destination, mouth);
        }

        public struct Pose { public float progress, lift, angle, alpha; public int frame; }
        static float Arc(float t) => 4 * Mathf.Clamp01(t) * (1 - Mathf.Clamp01(t));
        public static Pose Sample(string key, bool eject, float t, float mouth, float bodyHeight)
        {
            t = Mathf.Clamp01(t);
            var p = new Pose { progress = t, alpha = 1, frame = (eject ? 8 : 0) + Mathf.Min(7, Mathf.FloorToInt(t * 8)) };
            if (key == "Hyuki")
            {
                p.frame = HyukiCurledFrame; // Keep the selected upright curl from apex through all ejection.
                if (!eject)
                {
                    float peak = mouth + bodyHeight * .35f;
                    if(t < HyukiWalkEnd)
                    {
                        p.frame = Mathf.FloorToInt(t * Duration / .14f) % 2;
                        p.progress = .72f * t / HyukiWalkEnd;
                        p.lift = 0;
                    }
                    else if(t < HyukiLaunch)
                    {
                        p.frame = 2; p.progress = .72f; p.lift = 0;
                    }
                    else if(t < HyukiApex)
                    {
                        float u = (t - HyukiLaunch) / (HyukiApex - HyukiLaunch);
                        p.frame = 3; p.progress = .72f + .14f * u;
                        p.lift = peak * (1 - (1-u)*(1-u));
                    }
                    else
                    {
                        float u = (t - HyukiApex) / (1-HyukiApex);
                        p.progress = .86f + .14f * Mathf.SmoothStep(0,1,u);
                        p.lift = Mathf.Lerp(peak,mouth,u*u);
                    }
                    p.alpha = 1 - Mathf.Clamp01((t - .90f) / .10f);
                }
                else if (t < .44f)
                {
                    float u = t / .44f; p.progress = .68f * u;
                    p.lift = mouth * (1 - u) + Arc(u) * bodyHeight * .7f;
                }
                else if (t < .70f)
                {
                    float u = (t - .44f) / .26f; p.progress = .68f + .22f * u; p.lift = Arc(u) * bodyHeight * .32f;
                }
                else if (t < .88f)
                {
                    float u = (t - .70f) / .18f; p.progress = .90f + .10f * u; p.lift = Arc(u) * bodyHeight * .12f;
                }
                else { p.progress = 1; p.lift = 0; }
                return p;
            }
            if (!eject)
            {
                float launch = key == "Shini" ? .18f : .36f;
                float u = Mathf.Clamp01((t - launch) / (1 - launch));
                p.progress = Mathf.Lerp(t * .25f, 1, u);
                p.lift = mouth * u + Arc(u) * bodyHeight * (key == "Shini" ? 2.2f : key == "Ari" ? .95f : .8f);
                if (key == "Ari") p.angle = t < .36f ? -360 * t / .36f : -360;
                p.alpha = 1 - Mathf.Clamp01((t - .90f) / .10f);
            }
            else
            {
                float u = Mathf.Clamp01(t / .64f);
                p.progress = u; p.lift = mouth * (1 - u) + Arc(u) * bodyHeight * (key == "Shini" ? 1.5f : .75f);
                p.frame = t < .64f ? 8 + Mathf.Min(3, (int)(u * 4)) : 12 + Mathf.Min(3, (int)((t - .64f) / .36f * 4));
                if (key == "Shini" || key == "Ari") p.angle = 360 * Mathf.Clamp01(t / .5f);
                if (key == "Ari" && t >= .68f) p.angle = Mathf.Sin((t - .68f) * 36) * 9 * (1 - t);
                p.alpha = Mathf.Clamp01(t / .06f);
            }
            return p;
        }

        void Draw(bool eject, float t, Vector3 source, Vector3 destination, float mouth)
        {
            var p = Sample(CharacterKey, eject, t, mouth, height);
            SetPosition(Vector3.Lerp(source, destination, p.progress));
            Visual.sprite = frames[Mathf.Clamp(p.frame, 0, frames.Length - 1)];
            if(CharacterKey=="Hyuki"&&!eject&&t<HyukiWalkEnd)
            {
                var walk=owner.Blueprint.walkSpriteSequence;
                Visual.sprite=walk[Mathf.FloorToInt(t*Duration/owner.Blueprint.walkFrameTime)%walk.Length];
            }
            Visual.flipX = CharacterKey == "Hyuki" ? !eject && t < HyukiApex && destination.x < source.x : eject;
            Visual.transform.localScale = Vector3.one * scale;
            Visual.transform.rotation = Quaternion.Euler(0, 0, p.angle);
            Visual.transform.position = transform.position + footOffset + Vector3.up * (p.lift + GroundPivotOffset());
            Visual.color = new Color(1, 1, 1, p.alpha);
            SetChicks(eject && CharacterKey == "Ari" && t >= .72f, t * Duration);
        }

        void SetPosition(Vector3 position)
        {
            position.z = transform.position.z;
            transform.position = position;
            if (body != null) body.position = position;
        }

        void SetChicks(bool visible, float time)
        {
            if (visible && chicks == null)
            {
                chicks = new SpriteRenderer[3];
                var sprite = Frames("Ashi").FirstOrDefault();
                for (int i = 0; i < chicks.Length; i++)
                {
                    var go = new GameObject("Dizzy chick " + i); go.transform.SetParent(Visual.transform, false);
                    var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingLayerID = Visual.sortingLayerID;
                    chicks[i] = sr;
                }
            }
            if (chicks == null) return;
            for (int i = 0; i < chicks.Length; i++)
            {
                var sr = chicks[i]; sr.enabled = visible;
                if (!visible || sr.sprite == null) continue;
                float angle = time * 6 + i * Mathf.PI * 2 / 3;
                sr.transform.position = Visual.bounds.center + Vector3.up * height * .62f + new Vector3(Mathf.Cos(angle) * height * .48f, Mathf.Sin(angle) * height * .12f);
                sr.transform.localScale = Vector3.one * (height * .16f / (sr.sprite.bounds.size.y * scale));
                sr.sortingOrder = Visual.sortingOrder + (Mathf.Sin(angle) > 0 ? -1 : 1);
            }
        }

        void LateUpdate()
        {
            if ((Busy || HoldingPose) && original != null) original.forceRenderingOff=true;
            if (!HoldingPose) return;
            if(reservedPose)return;
            if (owner == null || !owner.IsAlive || owner.EffectiveMoveDirection.sqrMagnitude > .0001f) { ReleasePose(); return; }
            Visual.transform.position=transform.position+footOffset+Vector3.up*GroundPivotOffset();
            // Last frame stays frozen, but the orbiting dizziness indicator remains alive.
            SetChicks(CharacterKey == "Ari", Time.time);
        }
        void RestorePhysics() { if (body != null) { body.velocity = Vector2.zero; body.angularVelocity = 0; body.simulated = simulated; } }
        float GroundPivotOffset()=>CharacterKey=="Hyuki"?.27f*scale:Visual.sprite.bounds.size.y*scale*.5f;
        void RestorePortal() { if (animatedPortal != null) animatedPortal.localScale = portalScale; animatedPortal = null; }
        public void ReleasePose()
        {
            reservedPose=false;if(float.IsPositiveInfinity(protectedUntil))protectedUntil=Time.time+ExitInvincibilityDuration;
            HoldingPose = false;
            if (original != null) original.forceRenderingOff = originalHidden;
            if (Visual != null) Visual.gameObject.SetActive(false);
            if (owner != null) { owner.StopWalkAnimation(); owner.StartIdleAnimation(); }
        }
        public void Cancel()
        {
            StopAllCoroutines(); RestorePortal();
            if (Busy) { RestorePhysics(); protectedUntil = Time.time + ExitInvincibilityDuration; }
            Busy = false; ReleasePose();
        }
        void OnDisable() { Cancel(); protectedUntil = -1f; }
        void OnDestroy() { if (Visual != null) Destroy(Visual.gameObject);if(swimSprite!=null)Destroy(swimSprite); }

        public static Vector3 LeftLanding(Transform portal, Character actor)
        {
            var obstacle = portal.GetComponent<BloodClotObstacle>()?.Solid;
            float portalRadius = obstacle != null ? obstacle.bounds.extents.x : .5f;
            var collider = actor.GetComponent<Collider2D>();
            float playerRadius = collider != null ? collider.bounds.extents.x : .4f;
            var renderer=actor.GetComponentInChildren<SpriteAnimator>(true)?.GetComponent<SpriteRenderer>();
            if(renderer!=null)playerRadius=Mathf.Max(playerRadius,renderer.bounds.extents.x*.6f);
            return portal.position + Vector3.left * (portalRadius + Mathf.Max(playerRadius, .35f) + .2f);
        }
    }
}
