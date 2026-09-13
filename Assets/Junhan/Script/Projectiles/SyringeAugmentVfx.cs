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
            instance.gameObject.SetActive(true);
            instance.visual = instance.GetComponent<SpriteRenderer>();
            instance.animator = instance.GetComponent<SpriteAnimator>();
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
            FollowTarget();
        }

        private void FollowTarget()
        {
            if (!loop) return;
            if (target == null || !target.gameObject.activeInHierarchy) { visual.enabled = false; return; }
            if (visual.sprite == null) return;
            transform.position = target.bounds.center;
            transform.rotation = Quaternion.identity;
            // The empty centre occupies roughly half the sheet cell.
            Vector3 desired = new Vector3(target.bounds.size.x * 2.1f / visual.sprite.bounds.size.x,
                target.bounds.size.y * 1.7f / visual.sprite.bounds.size.y, 1f);
            transform.localScale = desired;
            visual.sortingLayerID = target.sortingLayerID;
            visual.sortingOrder = target.sortingOrder + 5;
            visual.enabled = target.enabled;
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
