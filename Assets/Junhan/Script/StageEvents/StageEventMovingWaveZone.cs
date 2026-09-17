using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 산성 역류 파도 / 커피 파도처럼 화면을 가로지르는 이동형 파도 영역입니다.
    /// visualOnly 모드면 단순 연출만 하고,
    /// damageWave 모드면 IDamageable 대상에게 피해와 넉백을 줍니다.
    /// </summary>
    public class StageEventMovingWaveZone : MonoBehaviour
    {
        private Vector3 startPosition;
        private Vector3 endPosition;
        private float travelDuration = 1f;
        private float elapsed;
        private bool configured;
        private bool visualOnly = true;

        private float damage;
        private float damageCooldownPerTarget;
        private Vector2 knockback;
        private bool affectPlayer;
        private bool affectMonsters;
        private static Sprite[] acidFrames;
        private SpriteRenderer acidVisual;
        private Vector3 acidVisualScale;
        private const float AcidFrameTime = 0.11f;

        // Only the acid event calls this. Coffee and the warning retain their own visuals.
        public void EnableAcidAnimation(bool moveLeftToRight)
        {
            if (acidFrames == null)
            {
                acidFrames = new Sprite[6];
                for (int i = 0; i < acidFrames.Length; i++)
                    acidFrames[i] = Resources.Load<Sprite>("FieldEventAnimations/AcidWave/AcidWave_" + (i + 1).ToString("00"));
            }
            foreach (var frame in acidFrames) if (frame == null) return;
            if (acidVisual == null)
            {
                var visualObject = new GameObject("Rolling acid crest");
                visualObject.transform.SetParent(transform, false);
                acidVisual = visualObject.AddComponent<SpriteRenderer>();
            }
            var source = GetComponent<SpriteRenderer>();
            if (source != null)
            {
                acidVisual.sharedMaterial = source.sharedMaterial;
                source.enabled = false;
            }
            acidVisual.sprite = acidFrames[0];
            acidVisual.color = Color.white;
            acidVisual.flipX = !moveLeftToRight;
            GroundVisualSorting.Apply(acidVisual);
            // Preserve the artwork's proportions; the leading edge follows the original damage box.
            Vector3 scale = transform.lossyScale;
            float height = Mathf.Abs(scale.y);
            float width = height * acidFrames[0].bounds.size.x / acidFrames[0].bounds.size.y;
            acidVisualScale = new Vector3(width / Mathf.Abs(scale.x) / acidFrames[0].bounds.size.x,
                height / Mathf.Abs(scale.y) / acidFrames[0].bounds.size.y, 1f);
            acidVisual.transform.localScale = acidVisualScale;
            acidVisual.transform.localPosition = new Vector3((moveLeftToRight ? -1f : 1f)
                * (width / Mathf.Abs(scale.x) - 1f) * 0.5f, 0f, 0f);
        }

        private readonly Dictionary<MonoBehaviour, float> lastDamageTimeByTarget =
            new Dictionary<MonoBehaviour, float>();

        public void InitVisualOnly(
            Vector3 startPosition,
            Vector3 endPosition,
            float travelDuration,
            Color color)
        {
            this.startPosition = startPosition;
            this.endPosition = endPosition;
            this.travelDuration = Mathf.Max(0.05f, travelDuration);
            this.visualOnly = true;

            elapsed = 0f;
            configured = true;

            lastDamageTimeByTarget.Clear();

            ApplyColor(color);
        }

        public void InitDamageWave(
            Vector3 startPosition,
            Vector3 endPosition,
            float travelDuration,
            Color color,
            float damage,
            float damageCooldownPerTarget,
            Vector2 knockback,
            bool affectPlayer,
            bool affectMonsters)
        {
            this.startPosition = startPosition;
            this.endPosition = endPosition;
            this.travelDuration = Mathf.Max(0.05f, travelDuration);
            this.damage = Mathf.Max(0f, damage);
            this.damageCooldownPerTarget = Mathf.Max(0.05f, damageCooldownPerTarget);
            this.knockback = knockback;
            this.affectPlayer = affectPlayer;
            this.affectMonsters = affectMonsters;
            this.visualOnly = false;

            elapsed = 0f;
            configured = true;

            lastDamageTimeByTarget.Clear();

            ApplyColor(color);
        }

        private void Update()
        {
            if (!configured || MiniStageRuntimeState.IsInsideMiniStage)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelDuration);
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            if (acidVisual != null)
                acidVisual.sprite = acidFrames[Mathf.FloorToInt(elapsed / AcidFrameTime) % acidFrames.Length];
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!configured || visualOnly || MiniStageRuntimeState.IsInsideMiniStage)
            {
                return;
            }

            MonoBehaviour damageableBehaviour = FindDamageableBehaviour(other);

            if (damageableBehaviour == null)
            {
                return;
            }

            bool isPlayer = damageableBehaviour is Character;
            bool isMonster = damageableBehaviour is Monster;

            if (isPlayer && !affectPlayer)
            {
                return;
            }

            if (isMonster && !affectMonsters)
            {
                return;
            }

            if (!isPlayer && !isMonster)
            {
                return;
            }

            if (lastDamageTimeByTarget.TryGetValue(damageableBehaviour, out float lastDamageTime))
            {
                if (elapsed - lastDamageTime < damageCooldownPerTarget)
                {
                    return;
                }
            }

            IDamageable damageable = damageableBehaviour as IDamageable;

            if (damageable == null)
            {
                return;
            }

            damageable.TakeDamage(damage, knockback, false);
            // 재피격 간격도 이동과 같은 필드 시계를 사용합니다.
            lastDamageTimeByTarget[damageableBehaviour] = elapsed;
        }

        private MonoBehaviour FindDamageableBehaviour(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable)
                {
                    return behaviours[i];
                }
            }

            return null;
        }

        private void ApplyColor(Color color)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }
    }
}
