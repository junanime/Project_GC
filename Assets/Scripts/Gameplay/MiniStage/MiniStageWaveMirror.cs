using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum MiniStageMirrorNormalMode
    {
        TransformUp,
        TransformRight,
        PhysicsHitNormal
    }

    /// <summary>
    /// 소화 파동 반사방의 거울 오브젝트.
    ///
    /// 역할:
    /// - 소화 파동 빔을 반사합니다.
    /// - 플레이어 투사체에 맞으면 맞은 위치에 따라 회전합니다.
    /// - 맞은 쪽의 반대 방향으로 rotationStepDegrees만큼 회전합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniStageWaveMirror : MonoBehaviour
    {
        [Header("Mirror State")]
        [Tooltip("이 거울이 현재 빔을 반사할 수 있는지 여부입니다.")]
        [SerializeField] private bool isReflective = true;

        [Tooltip("빔 반사 노멀을 어떤 기준으로 계산할지 정합니다. 보통 TransformUp을 추천합니다.")]
        [SerializeField] private MiniStageMirrorNormalMode normalMode = MiniStageMirrorNormalMode.TransformUp;

        [Header("Projectile Hit Rotation")]
        [Tooltip("플레이어 공격에 맞을 때 한 번에 회전할 각도입니다.")]
        [SerializeField] private float rotationStepDegrees = 10f;

        [Tooltip("맞은 위치 기준 회전 방향이 반대로 느껴질 때 체크하세요.")]
        [SerializeField] private bool invertHitRotationDirection = false;

        [Tooltip("같은 투사체가 한 번에 여러 번 충돌해서 거울이 과도하게 돌아가는 것을 막기 위한 쿨타임입니다.")]
        [SerializeField] private float sameProjectileHitCooldown = 0.12f;

        [Tooltip("Projectile 컴포넌트가 있는 오브젝트에 맞았을 때만 회전합니다. 일반적으로 true를 추천합니다.")]
        [SerializeField] private bool requireProjectileComponent = true;

        [Tooltip("투사체 레이어를 따로 제한하고 싶을 때 사용합니다. Nothing이면 레이어 검사를 하지 않습니다.")]
        [SerializeField] private LayerMask projectileLayerMask;

        [Header("Rotation Clamp")]
        [Tooltip("거울의 로컬 Z 회전 각도를 제한할지 여부입니다.")]
        [SerializeField] private bool useLocalRotationClamp = false;

        [Tooltip("로컬 Z 최소 각도입니다. Use Local Rotation Clamp가 true일 때만 사용합니다.")]
        [SerializeField] private float minLocalZAngle = -80f;

        [Tooltip("로컬 Z 최대 각도입니다. Use Local Rotation Clamp가 true일 때만 사용합니다.")]
        [SerializeField] private float maxLocalZAngle = 80f;

        [Header("Visual")]
        [Tooltip("거울이 회전할 때 같이 돌릴 시각 오브젝트입니다. 비워두면 이 오브젝트 Transform이 회전합니다.")]
        [SerializeField] private Transform rotationRoot;

        [Tooltip("회전 시 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly Dictionary<int, float> projectileHitCooldownUntil = new Dictionary<int, float>();
        private readonly List<int> removeProjectileIdBuffer = new List<int>();

        public bool IsReflective => isReflective;

        private void Awake()
        {
            if (rotationRoot == null)
            {
                rotationRoot = transform;
            }
        }

        private void OnValidate()
        {
            if (rotationRoot == null)
            {
                rotationRoot = transform;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandleProjectileHit(other, other.transform.position);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)collision.collider.transform.position;

            TryHandleProjectileHit(collision.collider, hitPoint);
        }

        public void ResetRuntimeState()
        {
            projectileHitCooldownUntil.Clear();
            removeProjectileIdBuffer.Clear();
        }

        public Vector2 GetReflectionNormal(Vector2 hitPoint, Vector2 incomingDirection, Vector2 physicsHitNormal)
        {
            Vector2 normal;

            switch (normalMode)
            {
                case MiniStageMirrorNormalMode.TransformRight:
                    normal = rotationRoot != null ? (Vector2)rotationRoot.right : (Vector2)transform.right;
                    break;

                case MiniStageMirrorNormalMode.PhysicsHitNormal:
                    normal = physicsHitNormal;
                    break;

                default:
                    normal = rotationRoot != null ? (Vector2)rotationRoot.up : (Vector2)transform.up;
                    break;
            }

            if (normal.sqrMagnitude < 0.001f)
            {
                normal = Vector2.up;
            }

            normal.Normalize();

            // incomingDirection과 같은 방향을 바라보는 노멀은 반대쪽으로 뒤집어야 반사가 안정적입니다.
            if (Vector2.Dot(incomingDirection.normalized, normal) > 0f)
            {
                normal = -normal;
            }

            return normal;
        }

        private void TryHandleProjectileHit(Collider2D hitCollider, Vector2 hitPoint)
        {
            if (hitCollider == null)
            {
                return;
            }

            if (!PassProjectileLayerCheck(hitCollider.gameObject.layer))
            {
                return;
            }

            Projectile projectile = hitCollider.GetComponentInParent<Projectile>();

            if (requireProjectileComponent && projectile == null)
            {
                return;
            }

            int projectileId = projectile != null
                ? projectile.GetInstanceID()
                : hitCollider.GetInstanceID();

            CleanupProjectileCooldowns();

            float blockedUntil;

            if (projectileHitCooldownUntil.TryGetValue(projectileId, out blockedUntil))
            {
                if (Time.time < blockedUntil)
                {
                    return;
                }
            }

            projectileHitCooldownUntil[projectileId] = Time.time + Mathf.Max(0.01f, sameProjectileHitCooldown);

            RotateByHitPoint(hitPoint, projectileId);
        }

        private bool PassProjectileLayerCheck(int layer)
        {
            if (projectileLayerMask.value == 0)
            {
                return true;
            }

            return (projectileLayerMask.value & (1 << layer)) != 0;
        }

        private void RotateByHitPoint(Vector2 hitPoint, int projectileId)
        {
            Transform target = rotationRoot != null ? rotationRoot : transform;

            Vector3 localHitPoint = target.InverseTransformPoint(hitPoint);

            // 거울의 로컬 X 기준:
            // 오른쪽을 맞으면 반대 방향으로 회전,
            // 왼쪽을 맞으면 반대 방향으로 회전.
            float hitSide = localHitPoint.x >= 0f ? 1f : -1f;

            // 기본값:
            // 오른쪽 피격 -> -10도
            // 왼쪽 피격 -> +10도
            float rotationDirection = hitSide > 0f ? -1f : 1f;

            if (invertHitRotationDirection)
            {
                rotationDirection *= -1f;
            }

            float deltaAngle = rotationDirection * Mathf.Abs(rotationStepDegrees);
            float currentZ = Mathf.DeltaAngle(0f, target.localEulerAngles.z);
            float nextZ = currentZ + deltaAngle;

            if (useLocalRotationClamp)
            {
                float min = Mathf.Min(minLocalZAngle, maxLocalZAngle);
                float max = Mathf.Max(minLocalZAngle, maxLocalZAngle);
                nextZ = Mathf.Clamp(nextZ, min, max);
            }

            target.localRotation = Quaternion.Euler(0f, 0f, nextZ);

            if (debugLog)
            {
                string sideText = hitSide > 0f ? "오른쪽" : "왼쪽";

                Debug.Log(
                    $"[MiniStageWaveMirror] 플레이어 공격 피격. " +
                    $"mirror={name}, hitSide={sideText}, delta={deltaAngle}, nextZ={nextZ}, projectileId={projectileId}"
                );
            }
        }

        private void CleanupProjectileCooldowns()
        {
            removeProjectileIdBuffer.Clear();

            foreach (KeyValuePair<int, float> pair in projectileHitCooldownUntil)
            {
                if (Time.time >= pair.Value)
                {
                    removeProjectileIdBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeProjectileIdBuffer.Count; i++)
            {
                projectileHitCooldownUntil.Remove(removeProjectileIdBuffer[i]);
            }

            removeProjectileIdBuffer.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Transform target = rotationRoot != null ? rotationRoot : transform;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(target.position, target.position + target.up * 1.2f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position, target.position + target.right * 1.2f);
        }
    }
}