using UnityEngine;
namespace Vampire
{
    /// <summary>Shared body footprint; only the edible shell varies. Motion follows travelled distance.</summary>
    public sealed class FoodSnailMonster : MeleeMonster
    {
        public const float BodyWidth = 1.2f, ShellWidth = .84f;
        public SpriteRenderer Shell { get; private set; }
        public float WalkPhase { get; private set; }
        Vector3 previousPosition;
        bool facingRight;
        protected override void Awake()
        {
            base.Awake();
            Shell = new GameObject("Food shell").AddComponent<SpriteRenderer>();
            Shell.transform.SetParent(transform, false);
        }
        public override void Setup(int index, Vector2 position, MonsterBlueprint blueprint, float hpBuff = 0)
        {
            base.Setup(index, position, blueprint, hpBuff);
            var food = (FoodSnailBlueprint)blueprint;
            Shell.sprite = food.shell;
            Shell.sortingLayerID = monsterSpriteRenderer.sortingLayerID;
            Shell.sortingOrder = monsterSpriteRenderer.sortingOrder - 1;
            monsterSpriteRenderer.enabled = true;
            monsterSpriteRenderer.transform.localScale = Vector3.one;
            monsterSpriteRenderer.transform.localPosition = Vector3.zero;
            if (shadow != null)
            {
                shadow.SetActive(true);
                var sr = shadow.GetComponent<SpriteRenderer>();
                sr.sortingLayerID = monsterSpriteRenderer.sortingLayerID;
                sr.sortingOrder = monsterSpriteRenderer.sortingOrder - 2;
                sr.transform.localPosition = new Vector3(0, .02f, 0);
                sr.transform.localScale = new Vector3(1.05f / sr.sprite.bounds.size.x, .25f / sr.sprite.bounds.size.y, 1);
            }
            previousPosition = transform.position; WalkPhase = 0; facingRight = false;
            Pose(false); FitVisibleBody();
            if (monsterLegsCollider != null) monsterLegsCollider.radius = .36f;
        }
        protected override void FixedUpdate()
        {
            if (IsFieldRuntimeSuspended) return;
            base.FixedUpdate();
        }
        void LateUpdate()
        {
            Vector2 movement = transform.position - previousPosition;
            previousPosition = transform.position;
            Shell.enabled = monsterSpriteRenderer.enabled;
            Shell.sharedMaterial = monsterSpriteRenderer.sharedMaterial;
            Shell.color = monsterSpriteRenderer.color;
            if (IsFieldRuntimeSuspended || Time.timeScale <= 0 || !alive) return;
            bool walking = movement.sqrMagnitude > .000001f && movement.sqrMagnitude < 2.25f;
            if (walking)
            {
                if (Mathf.Abs(movement.x) > .0001f) facingRight = movement.x > 0;
                WalkPhase = Mathf.Repeat(WalkPhase + movement.magnitude / .8f, 1);
            }
            Pose(walking);
        }
        void Pose(bool walking)
        {
            float wave = walking ? Mathf.Sin(WalkPhase * Mathf.PI * 2) : 0;
            monsterSpriteRenderer.flipX = facingRight;
            monsterSpriteRenderer.transform.localScale = new Vector3(1 + wave * .045f, 1 - wave * .025f, 1);
            monsterSpriteRenderer.transform.localPosition = new Vector3(-wave * .009f, 0, 0);
            Shell.flipX = facingRight;
            Shell.transform.localPosition = new Vector3(facingRight ? -.12f : .12f, .12f + Mathf.Abs(wave) * .004f, 0);
            Shell.transform.localRotation = Quaternion.Euler(0, 0, wave * 1.4f);
        }
    }
}
