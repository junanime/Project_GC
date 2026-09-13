using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Vampire
{
    [RequireComponent(typeof(SpriteRenderer), typeof(SpriteAnimator))]
    public sealed class SyringeAugmentVfx : MonoBehaviour
    {
        [SerializeField, Tooltip("Frames ordered left to right, top to bottom.")]
        private Sprite[] frames;
        [SerializeField, Tooltip("Seconds each frame is displayed.")]
        private float frameTime = 0.06f;
        [SerializeField, Tooltip("Keep playing until the poison status releases this effect.")]
        private bool loop;
        [SerializeField, Tooltip("Opacity of the effect; keeps the target readable.")]
        private float opacity = 0.75f;
        [SerializeField, Tooltip("Place this loop in world space instead of fitting a target.")]
        private bool worldSpace;
        [SerializeField, Tooltip("Place a small marker above the target instead of wrapping its body.")]
        private bool overhead;
        [SerializeField, Tooltip("World size of an overhead marker.")]
        private float markerSize = 0.55f;
        private Transform destination;
        private Transform anchor;
        private Vector3 anchorOffset;
        private bool followRotation;
        private Vector3 travelStart;
        private SpriteAnimator animator;
        private SpriteRenderer visual;
        private SpriteRenderer target;
        private ObjectPool<SyringeAugmentVfx> ownerPool;
        private bool leased;
        private float elapsed;
        private static Transform poolRoot;
        private static readonly Dictionary<string, ObjectPool<SyringeAugmentVfx>> pools = new Dictionary<string, ObjectPool<SyringeAugmentVfx>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPools() { pools.Clear(); poolRoot = null; }

        public static SpriteRenderer FindTarget(Component owner)
        {
            if (owner == null) return null;
            foreach (var renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.GetComponent<SyringeAugmentVfx>() == null &&
                    renderer.GetComponentInParent<StuckNeedleVisual>() == null &&
                    !renderer.name.ToLowerInvariant().Contains("shadow")) return renderer;
            return null;
        }

        public static SyringeAugmentVfx Play(string effect, Vector3 position, SpriteRenderer target = null)
        {
            if (poolRoot == null)
            {
                pools.Clear();
                poolRoot = new GameObject("Syringe VFX Pool").transform;
                DontDestroyOnLoad(poolRoot.gameObject);
            }
            if (!pools.TryGetValue(effect, out var pool))
            {
                var prefab = Resources.Load<SyringeAugmentVfx>("SyringeVfx/" + effect);
                if (prefab == null) { Debug.LogError("Missing syringe VFX: " + effect); return null; }
                pool = new ObjectPool<SyringeAugmentVfx>(
                    () => Instantiate(prefab, poolRoot), null,
                    item => { item.transform.SetParent(poolRoot, false); item.gameObject.SetActive(false); },
                    item => Destroy(item.gameObject), true, 16, 128);
                pools.Add(effect, pool);
            }
            var instance = pool.Get();
            instance.ownerPool = pool;
            instance.leased = true;
            instance.elapsed = 0f;
            instance.target = target;
            instance.destination = null;
            instance.anchor = null;
            instance.gameObject.SetActive(true);
            instance.visual = instance.GetComponent<SpriteRenderer>();
            instance.animator = instance.GetComponent<SpriteAnimator>();
            instance.visual.enabled = true;
            instance.visual.color = new Color(1f, 1f, 1f, instance.opacity);
            instance.visual.sortingLayerID = target != null ? target.sortingLayerID : SortingLayer.NameToID("Monster Full");
            instance.visual.sortingOrder = target != null ? target.sortingOrder + 5 : 5;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.animator.Init(instance.frames, instance.frameTime, false);
            instance.animator.StartAnimation(true);
            instance.FollowTarget();
            return instance;
        }

        private void LateUpdate()
        {
            if (!leased) return;
            elapsed += Time.deltaTime;
            if (!loop && elapsed >= frames.Length * frameTime)
            { Release(); return; }
            if (destination != null)
            {
                float t = Mathf.Clamp01(elapsed / (frames.Length * frameTime));
                Vector3 end = destination.position;
                Vector3 delta = end - travelStart;
                Vector3 side = new Vector3(-delta.y, delta.x, 0f).normalized;
                transform.position = Vector3.Lerp(travelStart, end, t) + side * Mathf.Sin(t * Mathf.PI) * 0.25f;
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
            if (anchor != null)
            {
                transform.position = anchor.position + anchorOffset;
                if (followRotation) transform.rotation = anchor.rotation;
            }
            FollowTarget();
        }

        public static bool IsLiving(Component owner)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy) return false;
            var monster = owner.GetComponentInParent<Monster>();
            if (monster != null && monster.HP <= 0f) return false;
            var part = owner.GetComponentInParent<BossPartDamageTestPart>();
            return part == null || part.CurrentHealth > 0f;
        }

        public static SyringeAugmentVfx PlayDirected(string effect, Vector3 position, Vector2 direction, SpriteRenderer sortingTarget = null)
        {
            var vfx = Play(effect, position, sortingTarget);
            if (vfx != null) vfx.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return vfx;
        }

        public static void PlayAbsorption(Component source, Character player)
        {
            if (source == null || player == null || player.CenterTransform == null) return;
            var vfx = Play("Mosquito", source.transform.position, FindTarget(player));
            if (vfx == null) return;
            vfx.travelStart = source.transform.position;
            vfx.destination = player.CenterTransform;
        }

        private void FollowTarget()
        {
            if (!loop || worldSpace) return;
            if (target == null || !target.gameObject.activeInHierarchy) { visual.enabled = false; return; }
            if (visual.sprite == null) return;
            transform.position = target.bounds.center;
            transform.rotation = Quaternion.identity;
            // The empty centre occupies roughly half the sheet cell.
            Vector3 desired = new Vector3(target.bounds.size.x * 2.1f / visual.sprite.bounds.size.x,
                target.bounds.size.y * 1.7f / visual.sprite.bounds.size.y, 1f);
            if (overhead)
            {
                transform.position = new Vector3(target.bounds.center.x, target.bounds.max.y + markerSize * 0.6f, target.bounds.center.z);
                desired = Vector3.one * (markerSize / visual.sprite.bounds.size.x);
            }
            transform.localScale = desired;
            visual.sortingLayerID = target.sortingLayerID;
            visual.sortingOrder = target.sortingOrder + 5;
            visual.enabled = target.enabled;
        }

        // Owners keep and release persistent leases; never automatically recycle a referenced loop.
        public void SetStrength(float strength)
        {
            if (visual != null) visual.color = new Color(1f, 1f, 1f, opacity * Mathf.Clamp01(strength));
        }

        public void BindTo(Transform owner, bool rotate = false)
        {
            anchor = owner;
            anchorOffset = owner != null ? transform.position - owner.position : Vector3.zero;
            followRotation = rotate;
        }

        public void SetGroundRadius(float radius, float artDiameter = 0.9f)
        {
            if (visual == null || visual.sprite == null) return;
            visual.sortingLayerName = "Default";
            visual.sortingOrder = 760;
            var size = visual.sprite.bounds.size;
            transform.localScale = new Vector3(2f * radius / (size.x * artDiameter),
                2f * radius / (size.y * artDiameter), 1f);
        }

        public static void PlayTransfer(string effect, Vector3 start, Transform end, SpriteRenderer sortingTarget)
        {
            if (end == null) return;
            var vfx = Play(effect, start, sortingTarget);
            if (vfx == null) return;
            vfx.travelStart = start;
            vfx.destination = end;
        }

        public static void PlayHungerHit(Component target, Character player, int maxStacks)
        {
            if (target == null || player == null) return;
            var renderer = FindTarget(target);
            var vfx = Play("HungerNeedle", renderer != null ? renderer.bounds.center : target.transform.position, renderer);
            if (vfx == null) return;
            vfx.BindTo(target.transform);
            vfx.SetStrength(Mathf.Lerp(0.35f, 1f, HungerNeedleRuntime.GetCurrentStacks(player) / (float)Mathf.Max(1, maxStacks)));
        }

        public static void ReleaseOwned(ref SyringeAugmentVfx effect)
        {
            if (effect != null) effect.Release();
            effect = null;
        }

        public void Release()
        {
            if (!leased) return;
            leased = false;
            target = null;
            animator.StopAnimation();
            ownerPool.Release(this);
        }
    }
}
