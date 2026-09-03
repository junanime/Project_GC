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

            ApplyColor(color);
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelDuration);
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!configured || visualOnly)
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
                if (Time.time - lastDamageTime < damageCooldownPerTarget)
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
            lastDamageTimeByTarget[damageableBehaviour] = Time.time;
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