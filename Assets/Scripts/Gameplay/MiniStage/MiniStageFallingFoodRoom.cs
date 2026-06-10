using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageFallingFoodRoom : MiniStageRoomBase
    {
        [System.Serializable]
        private class FallingFoodVariant
        {
            [Tooltip("낙석으로 사용할 음식물 Sprite입니다.")]
            public Sprite foodSprite;

            [Tooltip("이 음식물이 선택될 확률 가중치입니다. 높을수록 더 자주 등장합니다.")]
            public float weight = 1f;

            [Tooltip("음식물 이미지의 최소 크기입니다.")]
            public Vector2 visualScaleMin = new Vector2(0.8f, 0.8f);

            [Tooltip("음식물 이미지의 최대 크기입니다.")]
            public Vector2 visualScaleMax = new Vector2(1.2f, 1.2f);

            [Tooltip("이 음식물의 최소 피격 반지름입니다. 경고 원 크기도 이 값을 기준으로 정해집니다.")]
            public float damageRadiusMin = 0.8f;

            [Tooltip("이 음식물의 최대 피격 반지름입니다. 경고 원 크기도 이 값을 기준으로 정해집니다.")]
            public float damageRadiusMax = 1.2f;

            [Tooltip("음식물 이미지의 최소 회전 각도입니다.")]
            public float rotationMin = 0f;

            [Tooltip("음식물 이미지의 최대 회전 각도입니다.")]
            public float rotationMax = 360f;
        }

        private struct SelectedFallingFood
        {
            public Sprite Sprite;
            public Vector2 VisualScale;
            public float RotationZ;
            public float DamageRadius;
        }

        [Header("Falling Food Prefab")]
        [Tooltip("경고 원과 낙하 판정을 담당하는 낙석 프리팹입니다.")]
        [SerializeField] private MiniStageFallingFoodStrike fallingFoodStrikePrefab;

        [Header("Falling Food Variants")]
        [Tooltip("랜덤으로 떨어질 음식물 종류 목록입니다. 최소 3개 정도 등록하는 것을 추천합니다.")]
        [SerializeField] private FallingFoodVariant[] fallingFoodVariants;

        [Header("Room Duration")]
        [Tooltip("낙석 방이 진행되는 전체 시간입니다.")]
        [SerializeField] private float roomDuration = 30f;

        [Tooltip("방 시작 후 첫 낙석이 생성되기 전 대기 시간입니다.")]
        [SerializeField] private float startDelay = 0.5f;

        [Header("Spawn Timing")]
        [Tooltip("낙석 웨이브 사이의 최소 간격입니다.")]
        [SerializeField] private float waveIntervalMin = 0.65f;

        [Tooltip("낙석 웨이브 사이의 최대 간격입니다.")]
        [SerializeField] private float waveIntervalMax = 1.15f;

        [Tooltip("한 웨이브 안에서 낙석들이 완전히 동시에 떨어지지 않도록 주는 최소 지연 시간입니다.")]
        [SerializeField] private float delayBetweenStrikesMin = 0f;

        [Tooltip("한 웨이브 안에서 낙석들이 완전히 동시에 떨어지지 않도록 주는 최대 지연 시간입니다.")]
        [SerializeField] private float delayBetweenStrikesMax = 0.12f;

        [Header("Wave Count")]
        [Tooltip("한 웨이브에서 생성할 낙석 최소 개수입니다.")]
        [SerializeField] private int strikesPerWaveMin = 3;

        [Tooltip("한 웨이브에서 생성할 낙석 최대 개수입니다.")]
        [SerializeField] private int strikesPerWaveMax = 6;

        [Header("Warning And Hit")]
        [Tooltip("경고 원이 뜬 뒤 낙석이 떨어지기까지의 최소 시간입니다.")]
        [SerializeField] private float warningTimeMin = 0.85f;

        [Tooltip("경고 원이 뜬 뒤 낙석이 떨어지기까지의 최대 시간입니다.")]
        [SerializeField] private float warningTimeMax = 1.25f;

        [Tooltip("음식물 종류가 비어 있을 때 사용할 기본 피격 판정 반지름의 최소값입니다.")]
        [SerializeField] private float fallbackDamageRadiusMin = 0.8f;

        [Tooltip("음식물 종류가 비어 있을 때 사용할 기본 피격 판정 반지름의 최대값입니다.")]
        [SerializeField] private float fallbackDamageRadiusMax = 1.3f;

        [Tooltip("낙석에 맞았을 때 플레이어가 받는 데미지입니다.")]
        [SerializeField] private float damage = 8f;

        [Tooltip("낙석에 맞았을 때 플레이어가 움직이지 못하는 시간입니다.")]
        [SerializeField] private float stunDuration = 1f;

        [Tooltip("낙석에 맞았을 때 적용할 넉백 힘입니다. 스턴 방이면 0으로 두는 것을 추천합니다.")]
        [SerializeField] private float knockbackForce = 0f;

        [Header("Spawn Area")]
        [Tooltip("방 중심 기준 가로 반경입니다. 실제 벽 안쪽보다 작게 잡아야 벽 밖 경고 원이 생기지 않습니다.")]
        [SerializeField] private float arenaHalfWidth = 12f;

        [Tooltip("방 중심 기준 세로 반경입니다. 실제 벽 안쪽보다 작게 잡아야 벽 밖 경고 원이 생기지 않습니다.")]
        [SerializeField] private float arenaHalfHeight = 12f;

        [Tooltip("벽에 너무 붙지 않게 안쪽으로 당기는 거리입니다.")]
        [SerializeField] private float edgePadding = 1.5f;

        [Tooltip("방 루트 위치와 실제 전투장 중심이 다를 때 사용하는 중심 오프셋입니다.")]
        [SerializeField] private Vector2 roomCenterOffset = Vector2.zero;

        [Header("Player Pressure Options")]
        [Tooltip("체크하면 플레이어 주변에 낙석이 더 자주 떨어지도록 일부 낙석을 플레이어 근처에 생성합니다.")]
        [SerializeField] private bool allowTargetPlayerArea = true;

        [Tooltip("플레이어 주변 낙석이 발생할 확률입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float targetPlayerChance = 0.25f;

        [Tooltip("플레이어 주변 낙석이 생성될 때 플레이어 기준 랜덤 반경입니다.")]
        [SerializeField] private float targetPlayerRandomRadius = 2.5f;

        [Header("Debug")]
        [Tooltip("낙석 방 진행 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<MiniStageFallingFoodStrike> activeStrikes = new List<MiniStageFallingFoodStrike>();

        private Coroutine roomRoutine;
        private bool spawnFinished;
        private bool roomCompleted;

        protected override void OnBeginRoom()
        {
            StartFallingFoodRoom();
        }

        private void StartFallingFoodRoom()
        {
            if (fallingFoodStrikePrefab == null)
            {
                Debug.LogWarning("[MiniStageFallingFoodRoom] Falling Food Strike Prefab이 비어 있습니다. 방을 즉시 클리어합니다.");
                CompleteRoom();
                return;
            }

            activeStrikes.Clear();
            spawnFinished = false;
            roomCompleted = false;

            if (roomRoutine != null)
            {
                StopCoroutine(roomRoutine);
            }

            roomRoutine = StartCoroutine(RoomRoutine());

            if (debugLog)
            {
                Debug.Log("[MiniStageFallingFoodRoom] 소화관 낙석 방 시작.");
            }
        }

        private IEnumerator RoomRoutine()
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            float elapsed = 0f;

            while (elapsed < roomDuration)
            {
                SpawnStrikeWave();

                float waitTime = Random.Range(
                    Mathf.Min(waveIntervalMin, waveIntervalMax),
                    Mathf.Max(waveIntervalMin, waveIntervalMax)
                );

                elapsed += waitTime;
                yield return new WaitForSeconds(waitTime);
            }

            spawnFinished = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageFallingFoodRoom] 낙석 생성 종료. 남은 낙석 판정 대기.");
            }

            TryCompleteRoom();
        }

        private void SpawnStrikeWave()
        {
            int strikeCount = Random.Range(
                Mathf.Min(strikesPerWaveMin, strikesPerWaveMax),
                Mathf.Max(strikesPerWaveMin, strikesPerWaveMax) + 1
            );

            if (debugLog)
            {
                Debug.Log($"[MiniStageFallingFoodRoom] 낙석 웨이브 생성. count={strikeCount}");
            }

            StartCoroutine(SpawnStrikeWaveRoutine(strikeCount));
        }

        private IEnumerator SpawnStrikeWaveRoutine(int strikeCount)
        {
            for (int i = 0; i < strikeCount; i++)
            {
                SpawnOneStrike();

                float delay = Random.Range(
                    Mathf.Min(delayBetweenStrikesMin, delayBetweenStrikesMax),
                    Mathf.Max(delayBetweenStrikesMin, delayBetweenStrikesMax)
                );

                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
            }
        }

        private void SpawnOneStrike()
        {
            SelectedFallingFood selectedFood = SelectRandomFallingFood();
            Vector2 spawnPosition = GetRandomStrikePosition(selectedFood.DamageRadius);

            MiniStageFallingFoodStrike strike = Instantiate(
                fallingFoodStrikePrefab,
                spawnPosition,
                Quaternion.identity,
                transform
            );

            if (strike == null)
            {
                return;
            }

            activeStrikes.Add(strike);

            float warningTime = Random.Range(
                Mathf.Min(warningTimeMin, warningTimeMax),
                Mathf.Max(warningTimeMin, warningTimeMax)
            );

            Vector2 knockback = Vector2.zero;

            if (knockbackForce > 0f && playerCharacter != null)
            {
                Vector2 direction = ((Vector2)playerCharacter.transform.position - spawnPosition).normalized;

                if (direction == Vector2.zero)
                {
                    direction = Random.insideUnitCircle.normalized;
                }

                knockback = direction * knockbackForce;
            }

            strike.Setup(
                playerCharacter,
                selectedFood.Sprite,
                selectedFood.VisualScale,
                selectedFood.RotationZ,
                selectedFood.DamageRadius,
                warningTime,
                damage,
                stunDuration,
                knockback,
                OnStrikeFinished
            );
        }

        private SelectedFallingFood SelectRandomFallingFood()
        {
            FallingFoodVariant variant = SelectRandomVariant();

            if (variant == null)
            {
                float fallbackRadius = Random.Range(
                    Mathf.Min(fallbackDamageRadiusMin, fallbackDamageRadiusMax),
                    Mathf.Max(fallbackDamageRadiusMin, fallbackDamageRadiusMax)
                );

                return new SelectedFallingFood
                {
                    Sprite = null,
                    VisualScale = Vector2.one,
                    RotationZ = Random.Range(0f, 360f),
                    DamageRadius = fallbackRadius
                };
            }

            float scaleX = Random.Range(
                Mathf.Min(variant.visualScaleMin.x, variant.visualScaleMax.x),
                Mathf.Max(variant.visualScaleMin.x, variant.visualScaleMax.x)
            );

            float scaleY = Random.Range(
                Mathf.Min(variant.visualScaleMin.y, variant.visualScaleMax.y),
                Mathf.Max(variant.visualScaleMin.y, variant.visualScaleMax.y)
            );

            float radius = Random.Range(
                Mathf.Min(variant.damageRadiusMin, variant.damageRadiusMax),
                Mathf.Max(variant.damageRadiusMin, variant.damageRadiusMax)
            );

            float rotation = Random.Range(
                Mathf.Min(variant.rotationMin, variant.rotationMax),
                Mathf.Max(variant.rotationMin, variant.rotationMax)
            );

            return new SelectedFallingFood
            {
                Sprite = variant.foodSprite,
                VisualScale = new Vector2(scaleX, scaleY),
                RotationZ = rotation,
                DamageRadius = radius
            };
        }

        private FallingFoodVariant SelectRandomVariant()
        {
            if (fallingFoodVariants == null || fallingFoodVariants.Length <= 0)
            {
                return null;
            }

            float totalWeight = 0f;

            for (int i = 0; i < fallingFoodVariants.Length; i++)
            {
                if (fallingFoodVariants[i] == null)
                {
                    continue;
                }

                if (fallingFoodVariants[i].weight <= 0f)
                {
                    continue;
                }

                totalWeight += fallingFoodVariants[i].weight;
            }

            if (totalWeight <= 0f)
            {
                return fallingFoodVariants[Random.Range(0, fallingFoodVariants.Length)];
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            for (int i = 0; i < fallingFoodVariants.Length; i++)
            {
                FallingFoodVariant variant = fallingFoodVariants[i];

                if (variant == null || variant.weight <= 0f)
                {
                    continue;
                }

                currentWeight += variant.weight;

                if (randomValue <= currentWeight)
                {
                    return variant;
                }
            }

            return fallingFoodVariants[fallingFoodVariants.Length - 1];
        }

        private Vector2 GetRandomStrikePosition(float selectedRadius)
        {
            if (allowTargetPlayerArea &&
                playerCharacter != null &&
                Random.value < targetPlayerChance)
            {
                Vector2 nearPlayer = (Vector2)playerCharacter.transform.position +
                                     Random.insideUnitCircle * targetPlayerRandomRadius;

                return ClampPositionInsideArena(nearPlayer, selectedRadius);
            }

            Vector2 center = (Vector2)transform.position + roomCenterOffset;

            float safeRadius = Mathf.Max(0.1f, selectedRadius);

            float minX = center.x - arenaHalfWidth + edgePadding + safeRadius;
            float maxX = center.x + arenaHalfWidth - edgePadding - safeRadius;
            float minY = center.y - arenaHalfHeight + edgePadding + safeRadius;
            float maxY = center.y + arenaHalfHeight - edgePadding - safeRadius;

            if (minX > maxX)
            {
                float middleX = (minX + maxX) * 0.5f;
                minX = middleX;
                maxX = middleX;
            }

            if (minY > maxY)
            {
                float middleY = (minY + maxY) * 0.5f;
                minY = middleY;
                maxY = middleY;
            }

            return new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY)
            );
        }

        private Vector2 ClampPositionInsideArena(Vector2 position, float selectedRadius)
        {
            Vector2 center = (Vector2)transform.position + roomCenterOffset;

            float safeRadius = Mathf.Max(0.1f, selectedRadius);

            float minX = center.x - arenaHalfWidth + edgePadding + safeRadius;
            float maxX = center.x + arenaHalfWidth - edgePadding - safeRadius;
            float minY = center.y - arenaHalfHeight + edgePadding + safeRadius;
            float maxY = center.y + arenaHalfHeight - edgePadding - safeRadius;

            if (minX > maxX)
            {
                float middleX = (minX + maxX) * 0.5f;
                minX = middleX;
                maxX = middleX;
            }

            if (minY > maxY)
            {
                float middleY = (minY + maxY) * 0.5f;
                minY = middleY;
                maxY = middleY;
            }

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY)
            );
        }

        private void OnStrikeFinished(MiniStageFallingFoodStrike strike)
        {
            if (strike != null)
            {
                activeStrikes.Remove(strike);
            }

            TryCompleteRoom();
        }

        private void TryCompleteRoom()
        {
            if (roomCompleted)
            {
                return;
            }

            if (!spawnFinished)
            {
                return;
            }

            if (activeStrikes.Count > 0)
            {
                return;
            }

            roomCompleted = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageFallingFoodRoom] 낙석 방 클리어. 보상 상자 생성.");
            }

            CompleteRoom();
        }

        protected override void OnCleanupRoom()
        {
            if (roomRoutine != null)
            {
                StopCoroutine(roomRoutine);
                roomRoutine = null;
            }

            for (int i = activeStrikes.Count - 1; i >= 0; i--)
            {
                MiniStageFallingFoodStrike strike = activeStrikes[i];

                if (strike != null)
                {
                    Destroy(strike.gameObject);
                }
            }

            activeStrikes.Clear();

            spawnFinished = false;
            roomCompleted = false;
        }
    }
}