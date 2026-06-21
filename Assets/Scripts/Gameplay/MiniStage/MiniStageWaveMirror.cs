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
    /// 이번 수정 핵심:
    /// - 거울 중심축은 고정합니다.
    /// - 실제 회전은 Rotation Root만 회전시킵니다.
    /// - 피격 위치가 거울 끝에 가까울수록 크게 회전합니다.
    /// - 피격 위치가 중심에 가까울수록 작게 회전합니다.
    /// - Rigidbody2D가 Dynamic이라 밀리는 상황을 막기 위해 Kinematic/Freeze를 강제할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniStageWaveMirror : MonoBehaviour
    {
        [Header("Mirror State")]
        [Tooltip("이 거울이 현재 빔을 반사할 수 있는지 여부입니다.")]
        [SerializeField] private bool isReflective = true;

        [Tooltip("빔 반사 노멀을 어떤 기준으로 계산할지 정합니다. 보통 TransformUp을 추천합니다.")]
        [SerializeField] private MiniStageMirrorNormalMode normalMode = MiniStageMirrorNormalMode.TransformUp;

        [Header("Pivot / Center Lock")]
        [Tooltip("거울이 시소처럼 회전할 중심축입니다. 비워두면 이 오브젝트 Transform을 사용합니다. 권장 구조에서는 MirrorPivot 자식을 만들어 연결하세요.")]
        [SerializeField] private Transform rotationRoot;

        [Tooltip("거울 중심 위치를 고정할지 여부입니다. true면 물리 충돌로 루트가 밀려나도 원래 위치로 되돌립니다.")]
        [SerializeField] private bool lockMirrorCenterPosition = true;

        [Tooltip("거울 중심 위치를 저장할 기준입니다. true면 방 시작/활성화 시점의 위치를 고정 위치로 저장합니다.")]
        [SerializeField] private bool lockPositionOnEnable = true;

        [Tooltip("거울에 연결된 Rigidbody2D입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private Rigidbody2D mirrorRigidbody;

        [Tooltip("Rigidbody2D를 Kinematic으로 강제해서 투사체/몬스터 충돌로 밀리지 않게 합니다.")]
        [SerializeField] private bool forceKinematicRigidbody = true;

        [Tooltip("Rigidbody2D의 위치/회전을 Freeze All로 강제합니다. 실제 거울 회전은 Rotation Root 자식에서 처리하므로 루트는 고정해도 됩니다.")]
        [SerializeField] private bool freezeRigidbodyAll = true;

        [Header("Projectile Hit Rotation")]
        [Tooltip("끝부분을 맞았을 때 한 번에 회전할 최대 각도입니다.")]
        [SerializeField] private float maxRotationStepAtEdge = 10f;

        [Tooltip("중심 근처를 맞았을 때 한 번에 회전할 최소 각도입니다. 0으로 두면 중심 근처 피격은 거의 회전하지 않습니다.")]
        [SerializeField] private float minRotationStepNearCenter = 1.5f;

        [Tooltip("이 값보다 중심에 가까운 피격은 최소 회전량만 적용합니다. 0.15면 중심에서 전체 반폭의 15% 이내는 최소 회전입니다.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float centerDeadZoneNormalized = 0.15f;

        [Tooltip("피격 위치를 계산할 때 사용할 거울의 반쪽 길이입니다. Auto Calculate Hit Half Width가 꺼져 있을 때 사용합니다.")]
        [SerializeField] private float manualHitHalfWidth = 0.8f;

        [Tooltip("콜라이더 크기를 기준으로 거울의 반쪽 길이를 자동 계산합니다.")]
        [SerializeField] private bool autoCalculateHitHalfWidth = true;

        [Tooltip("거울 회전량 계산 기준이 될 콜라이더입니다. 보통 ProjectileHitbox의 BoxCollider2D를 연결하면 됩니다. 비워두면 자식 Collider2D 중 가장 넓은 것을 찾습니다.")]
        [SerializeField] private Collider2D hitWidthReferenceCollider;

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
        [SerializeField] private bool useLocalRotationClamp = true;

        [Tooltip("로컬 Z 최소 각도입니다. Use Local Rotation Clamp가 true일 때만 사용합니다.")]
        [SerializeField] private float minLocalZAngle = -80f;

        [Tooltip("로컬 Z 최대 각도입니다. Use Local Rotation Clamp가 true일 때만 사용합니다.")]
        [SerializeField] private float maxLocalZAngle = 80f;

        [Header("Debug")]
        [Tooltip("회전 시 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("피격 위치/중심 거리/회전량 계산 로그를 자세히 출력합니다.")]
        [SerializeField] private bool debugDetailedHitLog = false;

        private readonly Dictionary<int, float> projectileHitCooldownUntil = new Dictionary<int, float>();
        private readonly List<int> removeProjectileIdBuffer = new List<int>();

        private Vector3 lockedWorldPosition;
        private bool lockedPositionInitialized;

        public bool IsReflective => isReflective;

        private void Awake()
        {
            ResolveReferences();
            ApplyPhysicsLockSettings();
            SaveLockedPositionIfNeeded(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyPhysicsLockSettings();
            SaveLockedPositionIfNeeded(lockPositionOnEnable);
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            ApplyPhysicsLockSettings();

            if (lockMirrorCenterPosition)
            {
                ForceCenterPosition();
            }
        }

        private void LateUpdate()
        {
            if (lockMirrorCenterPosition)
            {
                ForceCenterPosition();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandleProjectileHit(other, GetHitPointFromCollider(other));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : GetHitPointFromCollider(collision.collider);

            TryHandleProjectileHit(collision.collider, hitPoint);
        }

        public void ResetRuntimeState()
        {
            projectileHitCooldownUntil.Clear();
            removeProjectileIdBuffer.Clear();

            SaveLockedPositionIfNeeded(true);
            ApplyPhysicsLockSettings();
            ForceCenterPosition();
        }

        public Vector2 GetReflectionNormal(Vector2 hitPoint, Vector2 incomingDirection, Vector2 physicsHitNormal)
        {
            Transform target = GetRotationTarget();

            Vector2 normal;

            switch (normalMode)
            {
                case MiniStageMirrorNormalMode.TransformRight:
                    normal = target != null ? (Vector2)target.right : (Vector2)transform.right;
                    break;

                case MiniStageMirrorNormalMode.PhysicsHitNormal:
                    normal = physicsHitNormal;
                    break;

                default:
                    normal = target != null ? (Vector2)target.up : (Vector2)transform.up;
                    break;
            }

            if (normal.sqrMagnitude < 0.001f)
            {
                normal = Vector2.up;
            }

            normal.Normalize();

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

            RotateLikeSeesawByHitPoint(hitPoint, projectileId);
        }

        private bool PassProjectileLayerCheck(int layer)
        {
            if (projectileLayerMask.value == 0)
            {
                return true;
            }

            return (projectileLayerMask.value & (1 << layer)) != 0;
        }

        private void RotateLikeSeesawByHitPoint(Vector2 hitPoint, int projectileId)
        {
            Transform target = GetRotationTarget();

            if (target == null)
            {
                return;
            }

            Vector3 localHitPoint = target.InverseTransformPoint(hitPoint);

            float hitSide = localHitPoint.x >= 0f ? 1f : -1f;

            // 오른쪽 피격 -> 반대 방향으로 기울기
            // 왼쪽 피격 -> 반대 방향으로 기울기
            float rotationDirection = hitSide > 0f ? -1f : 1f;

            if (invertHitRotationDirection)
            {
                rotationDirection *= -1f;
            }

            float halfWidth = GetEffectiveHitHalfWidth(target);
            float normalizedDistanceFromCenter = Mathf.Clamp01(Mathf.Abs(localHitPoint.x) / Mathf.Max(0.01f, halfWidth));

            float t = Mathf.InverseLerp(
                Mathf.Clamp01(centerDeadZoneNormalized),
                1f,
                normalizedDistanceFromCenter
            );

            float rotationAmount = Mathf.Lerp(
                Mathf.Max(0f, minRotationStepNearCenter),
                Mathf.Max(0f, maxRotationStepAtEdge),
                t
            );

            float deltaAngle = rotationDirection * rotationAmount;

            float currentZ = Mathf.DeltaAngle(0f, target.localEulerAngles.z);
            float nextZ = currentZ + deltaAngle;

            if (useLocalRotationClamp)
            {
                float min = Mathf.Min(minLocalZAngle, maxLocalZAngle);
                float max = Mathf.Max(minLocalZAngle, maxLocalZAngle);
                nextZ = Mathf.Clamp(nextZ, min, max);
            }

            target.localRotation = Quaternion.Euler(0f, 0f, nextZ);

            // 루트 중심은 고정. 회전은 Rotation Root만 처리.
            if (lockMirrorCenterPosition)
            {
                ForceCenterPosition();
            }

            if (debugLog)
            {
                string sideText = hitSide > 0f ? "오른쪽" : "왼쪽";

                Debug.Log(
                    $"[MiniStageWaveMirror] 플레이어 공격 피격. " +
                    $"mirror={name}, hitSide={sideText}, " +
                    $"centerDistance={normalizedDistanceFromCenter:0.00}, " +
                    $"delta={deltaAngle:0.00}, nextZ={nextZ:0.00}, projectileId={projectileId}"
                );
            }

            if (debugDetailedHitLog)
            {
                Debug.Log(
                    $"[MiniStageWaveMirror] 상세 피격 계산 | " +
                    $"mirror={name}, worldHit={hitPoint}, localHit={localHitPoint}, " +
                    $"halfWidth={halfWidth:0.00}, deadZone={centerDeadZoneNormalized:0.00}, " +
                    $"rotationAmount={rotationAmount:0.00}"
                );
            }
        }

        private float GetEffectiveHitHalfWidth(Transform target)
        {
            if (!autoCalculateHitHalfWidth)
            {
                return Mathf.Max(0.01f, manualHitHalfWidth);
            }

            if (hitWidthReferenceCollider != null)
            {
                return CalculateHalfWidthFromCollider(hitWidthReferenceCollider, target);
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

            float bestHalfWidth = 0f;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];

                if (collider == null)
                {
                    continue;
                }

                // 트리거/비트리거 모두 후보로 사용.
                // 보통 ProjectileHitbox가 가장 적절하다.
                float halfWidth = CalculateHalfWidthFromCollider(collider, target);

                if (halfWidth > bestHalfWidth)
                {
                    bestHalfWidth = halfWidth;
                }
            }

            if (bestHalfWidth <= 0.01f)
            {
                bestHalfWidth = manualHitHalfWidth;
            }

            return Mathf.Max(0.01f, bestHalfWidth);
        }

        private float CalculateHalfWidthFromCollider(Collider2D collider, Transform target)
        {
            if (collider == null || target == null)
            {
                return 0f;
            }

            Bounds bounds = collider.bounds;

            Vector3 left = target.InverseTransformPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));
            Vector3 right = target.InverseTransformPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z));
            Vector3 bottom = target.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            Vector3 top = target.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));

            float xWidth = Mathf.Abs(right.x - left.x) * 0.5f;
            float yAsXWidth = Mathf.Abs(top.x - bottom.x) * 0.5f;

            return Mathf.Max(xWidth, yAsXWidth);
        }

        private Vector2 GetHitPointFromCollider(Collider2D other)
        {
            if (other == null)
            {
                return transform.position;
            }

            Collider2D myCollider = hitWidthReferenceCollider;

            if (myCollider == null)
            {
                myCollider = GetComponentInChildren<Collider2D>();
            }

            if (myCollider != null)
            {
                return myCollider.ClosestPoint(other.transform.position);
            }

            return other.transform.position;
        }

        private Transform GetRotationTarget()
        {
            if (rotationRoot != null)
            {
                return rotationRoot;
            }

            return transform;
        }

        private void ResolveReferences()
        {
            if (rotationRoot == null)
            {
                rotationRoot = transform;
            }

            if (mirrorRigidbody == null)
            {
                mirrorRigidbody = GetComponent<Rigidbody2D>();
            }

            if (hitWidthReferenceCollider == null)
            {
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

                float bestSize = 0f;
                Collider2D bestCollider = null;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D collider = colliders[i];

                    if (collider == null)
                    {
                        continue;
                    }

                    Bounds bounds = collider.bounds;
                    float size = Mathf.Max(bounds.size.x, bounds.size.y);

                    if (size > bestSize)
                    {
                        bestSize = size;
                        bestCollider = collider;
                    }
                }

                hitWidthReferenceCollider = bestCollider;
            }
        }

        private void ApplyPhysicsLockSettings()
        {
            if (mirrorRigidbody == null)
            {
                return;
            }

            if (forceKinematicRigidbody)
            {
                mirrorRigidbody.bodyType = RigidbodyType2D.Kinematic;
                mirrorRigidbody.gravityScale = 0f;
            }

            if (freezeRigidbodyAll)
            {
                mirrorRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            mirrorRigidbody.velocity = Vector2.zero;
            mirrorRigidbody.angularVelocity = 0f;
        }

        private void SaveLockedPositionIfNeeded(bool force)
        {
            if (!lockMirrorCenterPosition)
            {
                return;
            }

            if (!force && lockedPositionInitialized)
            {
                return;
            }

            lockedWorldPosition = transform.position;
            lockedPositionInitialized = true;
        }

        private void ForceCenterPosition()
        {
            if (!lockMirrorCenterPosition)
            {
                return;
            }

            if (!lockedPositionInitialized)
            {
                SaveLockedPositionIfNeeded(true);
            }

            transform.position = lockedWorldPosition;

            if (mirrorRigidbody != null)
            {
                mirrorRigidbody.position = lockedWorldPosition;
                mirrorRigidbody.velocity = Vector2.zero;
                mirrorRigidbody.angularVelocity = 0f;
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

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
        }
    }
}