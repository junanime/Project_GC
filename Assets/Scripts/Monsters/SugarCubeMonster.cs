using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 설탕 큐브 몬스터.
    /// 기존 근접 몬스터처럼 플레이어를 추격하고,
    /// 사망 시 설정된 다음 단계 분열체를 자기 위치 주변에 생성한다.
    /// </summary>
    public class SugarCubeMonster : MeleeMonster
    {
        private SugarCubeMonsterBlueprint sugarCubeBlueprint;

        // 수정된 부분
        private Vector3 sugarCubeOriginalLocalScale = Vector3.one;

        private float lastHpBuff = 0f;
        private bool splitSpawned = false;
        private bool originalScaleCached = false;

        protected override void Awake()
        {
            base.Awake();

            sugarCubeOriginalLocalScale = transform.localScale;
            originalScaleCached = true;
        }

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            SugarCubeMonsterBlueprint incomingSugarCubeBlueprint =
                monsterBlueprint as SugarCubeMonsterBlueprint;

            if (!originalScaleCached)
            {
                sugarCubeOriginalLocalScale = transform.localScale;
                originalScaleCached = true;
            }

            if (incomingSugarCubeBlueprint != null)
            {
                float scaleMultiplier =
                    Mathf.Max(0.05f, incomingSugarCubeBlueprint.visualScaleMultiplier);

                transform.localScale = sugarCubeOriginalLocalScale * scaleMultiplier;
            }
            else
            {
                transform.localScale = sugarCubeOriginalLocalScale;
            }

            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            // Base.Setup resets pooled scale; apply the sugar stage scale afterwards.
            if (incomingSugarCubeBlueprint != null)
                transform.localScale = sugarCubeOriginalLocalScale * Mathf.Max(0.05f, incomingSugarCubeBlueprint.visualScaleMultiplier);

            sugarCubeBlueprint = incomingSugarCubeBlueprint;
            lastHpBuff = hpBuff;
            splitSpawned = false;

            if (sugarCubeBlueprint == null)
            {
                Debug.LogError(
                    "[SugarCubeMonster] SugarCubeMonsterBlueprint가 연결되지 않았습니다.\n" +
                    "SugarCubeMonster 프리팹에는 Sugar Cube Monster Blueprint를 넣어야 합니다.",
                    this
                );

                return;
            }

            ApplySugarCubeWalkAnimation();

            if (sugarCubeBlueprint.debugLog)
            {
                Debug.Log(
                    $"[SugarCubeMonster] 스폰 완료 | " +
                    $"Stage={sugarCubeBlueprint.splitStage} | " +
                    $"HP={currentHealth:0.##} | " +
                    $"Scale={sugarCubeBlueprint.visualScaleMultiplier:0.##} | " +
                    $"CanSplit={sugarCubeBlueprint.CanSplit()}",
                    this
                );
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            TrySpawnSplitChildren(killedByPlayer);
            yield return base.Killed(killedByPlayer);
        }

        protected override void DropLoot()
        {
            if (sugarCubeBlueprint != null && !sugarCubeBlueprint.dropLootOnDeath)
            {
                return;
            }

            base.DropLoot();
        }

        private void ApplySugarCubeWalkAnimation()
        {
            if (sugarCubeBlueprint == null)
            {
                return;
            }

            Sprite[] sprites = sugarCubeBlueprint.GetEffectiveWalkSpriteSequence();
            float frameTime = sugarCubeBlueprint.GetEffectiveWalkFrameTime();

            if (sprites == null || sprites.Length == 0)
            {
                return;
            }

            Sprite firstSprite = GetFirstValidSprite(sprites);

            if (firstSprite == null)
            {
                return;
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.sprite = firstSprite;
            }

            if (monsterSpriteAnimator != null)
            {
                monsterSpriteAnimator.Init(sprites, frameTime, false);
                monsterSpriteAnimator.StartAnimating(true);
            }

            RefreshColliderSizeFromSprite();
            RefreshGroundShadow(firstSprite);
        }

        private void RefreshGroundShadow(Sprite sprite)
        {
            if (shadow == null || sprite == null || monsterSpriteRenderer == null) return;
            var renderer = shadow.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) return;

            // Use the whole animation's landing footprint, not the current jumping frame.
            // Pixel-normalized data keeps this aligned when PPU or stage scale changes.
            Vector2 uv = sugarCubeBlueprint.shadowGroundCenterUV;
            Vector2 localGround = new Vector2(
                (uv.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit,
                (uv.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit);
            Transform body = monsterSpriteRenderer.transform;
            float width = sprite.rect.width / sprite.pixelsPerUnit
                * sugarCubeBlueprint.shadowGroundWidthUV * Mathf.Abs(body.lossyScale.x);
            Vector3 target = body.TransformPoint(localGround);
            target.y += width * 0.025f;
            target.z = renderer.bounds.center.z;

            Vector3 parentScale = renderer.transform.parent != null
                ? renderer.transform.parent.lossyScale : Vector3.one;
            Vector3 spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width * 0.94f / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x)),
                width * 0.235f / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y)),
                renderer.transform.localScale.z);
            renderer.transform.position += target - renderer.bounds.center;
            renderer.sortingLayerID = monsterSpriteRenderer.sortingLayerID;
            renderer.sortingOrder = monsterSpriteRenderer.sortingOrder - 1;
        }

        private Sprite GetFirstValidSprite(Sprite[] sprites)
        {
            if (sprites == null)
            {
                return null;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    return sprites[i];
                }
            }

            return null;
        }

        private void RefreshColliderSizeFromSprite()
        {
            if (monsterHitbox != null && monsterSpriteRenderer != null)
            {
                monsterHitbox.enabled = true;
                Bounds bounds = monsterSpriteRenderer.bounds;
                Vector3 min = monsterHitbox.transform.InverseTransformPoint(bounds.min);
                Vector3 max = monsterHitbox.transform.InverseTransformPoint(bounds.max);
                monsterHitbox.size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
                monsterHitbox.offset = (min + max) * 0.5f;
            }

            if (monsterLegsCollider != null && monsterHitbox != null)
            {
                monsterLegsCollider.radius =
                    Mathf.Max(0.05f, monsterHitbox.size.x / 2.5f);
            }

            if (centerTransform != null && monsterHitbox != null)
            {
                centerTransform.position =
                    monsterHitbox.transform.TransformPoint(monsterHitbox.offset);
            }
        }

        private void TrySpawnSplitChildren(bool killedByPlayer)
        {
            if (splitSpawned)
            {
                return;
            }

            if (sugarCubeBlueprint == null)
            {
                return;
            }

            if (!sugarCubeBlueprint.CanSplit())
            {
                return;
            }

            if (sugarCubeBlueprint.splitOnlyWhenKilledByPlayer && !killedByPlayer)
            {
                return;
            }

            if (entityManager == null)
            {
                Debug.LogWarning(
                    "[SugarCubeMonster] EntityManager가 없어 분열체를 생성하지 못했습니다.",
                    this
                );

                return;
            }

            splitSpawned = true;

            Vector2 origin = transform.position;
            int childCount = Mathf.Max(1, sugarCubeBlueprint.splitChildCount);
            float angleOffset = Random.Range(0f, 360f);

            for (int i = 0; i < childCount; i++)
            {
                float angle = angleOffset + (360f / childCount) * i;
                Vector2 direction = DegreeToVector2(angle);
                Vector2 spawnPosition =
                    origin + direction * Mathf.Max(0f, sugarCubeBlueprint.splitSpawnRadius);

                float childHpBuff =
                    sugarCubeBlueprint.childIgnoresParentHpBuff ? 0f : lastHpBuff;

                Monster childMonster = entityManager.SpawnMonster(
                    monsterIndex,
                    spawnPosition,
                    sugarCubeBlueprint.splitChildBlueprint,
                    childHpBuff
                );

                if (childMonster != null && sugarCubeBlueprint.splitScatterKnockback > 0f)
                {
                    childMonster.Knockback(direction * sugarCubeBlueprint.splitScatterKnockback);
                }

                if (sugarCubeBlueprint.debugLog)
                {
                    Debug.Log(
                        $"[SugarCubeMonster] 분열체 생성 | " +
                        $"Parent={sugarCubeBlueprint.splitStage} | " +
                        $"Child={sugarCubeBlueprint.splitChildBlueprint.splitStage} | " +
                        $"Index={i + 1}/{childCount} | " +
                        $"Position={spawnPosition}",
                        this
                    );
                }
            }
        }

        private Vector2 DegreeToVector2(float degree)
        {
            float rad = degree * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(rad),
                Mathf.Sin(rad)
            ).normalized;
        }
    }
}
