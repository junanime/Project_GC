using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BossChargePattern : BossPatternBase
    {
        [Header("Charge Telegraph")]
        [SerializeField] private GameObject warningLinePrefab;
        [SerializeField] private float warningTime = 0.9f;
        [SerializeField] private float warningWidth = 1.5f;
        [SerializeField] private Color warningColor = new Color(0.4f, 0.85f, 1f, 0.45f);

        [Header("Charge Settings")]
        [SerializeField] private float chargeDistancePhase1 = 6f;
        [SerializeField] private float chargeDistancePhase2 = 8f;
        [SerializeField] private float chargeSpeedPhase1 = 14f;
        [SerializeField] private float chargeSpeedPhase2 = 18f;
        [SerializeField] private float chargeDamagePhase1 = 15f;
        [SerializeField] private float chargeDamagePhase2 = 25f;
        [SerializeField] private float endLag = 0.4f;

        [Header("Hit Settings")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.2f, 1.2f);
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private bool showDebugHitbox = false;

        [Header("Debug")]
        [SerializeField] private bool debugCharge = false;

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null || bossController.PlayerCharacter == null || bossController.IsDead)
            {
                yield break;
            }

            bossController.SetExternalMovementLock(true);
            bossController.SetSuppressContactDamage(true);

            Vector2 startPosition = bossController.BossCenterPosition;
            Vector2 targetPosition = bossController.PlayerCharacter.transform.position;
            Vector2 direction = (targetPosition - startPosition).normalized;

            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            float chargeDistance = bossController.CurrentPhase == 1 ? chargeDistancePhase1 : chargeDistancePhase2;
            float chargeSpeed = bossController.CurrentPhase == 1 ? chargeSpeedPhase1 : chargeSpeedPhase2;
            float chargeDamage = bossController.CurrentPhase == 1 ? chargeDamagePhase1 : chargeDamagePhase2;

            GameObject warningObject = CreateWarningLine(startPosition, direction, chargeDistance);

            yield return new WaitForSeconds(warningTime);

            if (warningObject != null)
            {
                Destroy(warningObject);
            }

            yield return StartCoroutine(ChargeForward(startPosition, direction, chargeDistance, chargeSpeed, chargeDamage));

            yield return new WaitForSeconds(endLag);

            bossController.SetSuppressContactDamage(false);
            bossController.SetExternalMovementLock(false);
        }

        private GameObject CreateWarningLine(Vector2 startPosition, Vector2 direction, float chargeDistance)
        {
            if (warningLinePrefab == null)
            {
                Debug.LogWarning("[BossChargePattern] Warning Line Prefab이 비어 있습니다.");
                return null;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 centerPosition = startPosition + direction * (chargeDistance * 0.5f);

            GameObject warning = Instantiate(
                warningLinePrefab,
                centerPosition,
                Quaternion.Euler(0f, 0f, angle)
            );

            warning.transform.localScale = new Vector3(chargeDistance, warningWidth, 1f);

            SpriteRenderer sr = warning.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = warning.GetComponentInChildren<SpriteRenderer>();
            }

            if (sr != null)
            {
                sr.color = warningColor;
            }

            return warning;
        }

        private IEnumerator ChargeForward(
            Vector2 startPosition,
            Vector2 direction,
            float chargeDistance,
            float chargeSpeed,
            float chargeDamage)
        {
            Rigidbody2D rb = bossController.Rigidbody;
            float traveledDistance = 0f;
            HashSet<Character> hitTargets = new HashSet<Character>();

            while (traveledDistance < chargeDistance && !bossController.IsDead)
            {
                float step = chargeSpeed * Time.fixedDeltaTime;
                Vector2 currentPosition = rb != null ? rb.position : (Vector2)bossController.transform.position;
                Vector2 nextPosition = currentPosition + direction * step;

                if (rb != null)
                {
                    rb.MovePosition(nextPosition);
                }
                else
                {
                    bossController.transform.position = nextPosition;
                }

                traveledDistance += step;

                CheckChargeHit(nextPosition, chargeDamage, hitTargets);

                yield return new WaitForFixedUpdate();
            }

            if (debugCharge)
            {
                Debug.Log("[BossChargePattern] Charge complete.");
            }
        }

        private void CheckChargeHit(Vector2 center, float damage, HashSet<Character> hitTargets)
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitboxSize, 0f, playerLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                Character character = hits[i].GetComponentInParent<Character>();

                if (character == null)
                {
                    continue;
                }

                if (character != bossController.PlayerCharacter)
                {
                    continue;
                }

                if (hitTargets.Contains(character))
                {
                    continue;
                }

                hitTargets.Add(character);
                character.TakeDamage(damage);

                if (debugCharge)
                {
                    Debug.Log($"[BossChargePattern] Player hit! damage={damage}");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugHitbox)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(transform.position, hitboxSize);
        }
    }
}