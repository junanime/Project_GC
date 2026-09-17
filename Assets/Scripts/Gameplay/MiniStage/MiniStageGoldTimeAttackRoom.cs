using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Vampire
{
    public enum GoldTimeAttackGridAnchorMode
    {
        PlayerStartPoint,
        PlayerCurrentPosition,
        CustomGridCenter,
        RoomTransform
    }

    /// <summary>
    /// 골드 타임어택 미니 스테이지 방.
    ///
    /// 경험치 타임어택 방과 같은 구조입니다.
    /// - 방 시작 즉시 지정 영역 전체에 코인을 격자 형태로 배치합니다.
    /// - 제한 시간 동안 플레이어가 코인을 수집합니다.
    /// - 시간이 끝나면 남은 코인을 제거합니다.
    /// - 중앙 귀환 상호작용 오브젝트를 활성화합니다.
    ///
    /// 코인 생성은 우선 EntityManager의 SpawnCoin / SpawnGoldCoin 계열 함수를 Reflection으로 찾아 사용합니다.
    /// 해당 함수가 없거나 시그니처가 맞지 않으면 Fallback Coin Prefab을 직접 Instantiate합니다.
    /// </summary>
    public class MiniStageGoldTimeAttackRoom : MiniStageRoomBase
    {
        [Serializable]
        private class CoinSpawnWeight
        {
            [Tooltip("배치할 코인 종류입니다.")]
            public CoinType coinType = CoinType.Bronze1;

            [Tooltip("이 코인이 선택될 가중치입니다. 값이 높을수록 더 자주 등장합니다.")]
            public int weight = 10;
        }

        private class SpawnedCoinRecord
        {
            public Coin coin;
            public bool spawnedByEntityManager;
        }

        [Header("Time Attack")]
        [Tooltip("플레이어가 골드를 먹을 수 있는 제한 시간입니다.")]
        [SerializeField] private float timeLimit = 15f;

        [Tooltip("시간이 끝났을 때 남아 있는 코인을 제거할지 여부입니다. true면 제한 시간 이후에는 더 이상 코인을 먹을 수 없습니다.")]
        [SerializeField] private bool removeUncollectedCoinsWhenTimeUp = true;

        [Tooltip("시간 종료 후 방 클리어 처리까지 기다리는 시간입니다. 0이면 즉시 귀환 오브젝트가 활성화됩니다.")]
        [SerializeField] private float clearDelayAfterTimeUp = 0.1f;

        [Header("Full Stage Drop Area")]
        [Tooltip("true면 기준점 주변이 아니라 미니 스테이지 전체 영역에 코인을 배치합니다.")]
        [SerializeField] private bool dropAcrossEntireStage = true;

        [Tooltip("전체 드랍 영역을 나타내는 BoxCollider2D 또는 Collider2D입니다. 연결하면 이 콜라이더 Bounds 안에 코인을 배치합니다.")]
        [SerializeField] private Collider2D stageAreaCollider;

        [Tooltip("전체 드랍 영역 기준으로 사용할 배경 SpriteRenderer입니다. Stage Area Collider가 비어 있으면 이 Renderer Bounds를 사용합니다.")]
        [SerializeField] private SpriteRenderer stageAreaSpriteRenderer;

        [Tooltip("Stage Area Collider와 Stage Area Sprite Renderer가 비어 있을 때 사용할 수동 영역 중심입니다. 비워두면 Room Transform 위치를 사용합니다.")]
        [SerializeField] private Transform stageAreaCenter;

        [Tooltip("Collider/Renderer를 쓰지 않을 때 사용할 수동 드랍 영역 크기입니다. 경험치 타임어택과 동일하게 18/18을 추천합니다.")]
        [SerializeField] private Vector2 stageAreaSize = new Vector2(18f, 18f);

        [Tooltip("미니 스테이지 가장자리에서 안쪽으로 얼마나 여백을 둘지 정합니다. 벽에 너무 붙어 스폰되는 것을 막습니다.")]
        [SerializeField] private Vector2 stageAreaPadding = new Vector2(1.1f, 1.1f);

        [Tooltip("미니 스테이지 전체에 생성할 코인 최대 개수입니다. 0 이하이면 제한하지 않습니다.")]
        [SerializeField] private int maxSpawnCount = 120;

        [Tooltip("전체 스테이지 모드에서 후보 격자를 섞은 뒤 생성합니다. Max Spawn Count를 사용할 때 특정 구역에만 몰리는 것을 줄입니다.")]
        [SerializeField] private bool shuffleFullStageCells = true;

        [Header("Anchor Grid Mode")]
        [Tooltip("Drop Across Entire Stage가 false일 때, 코인 격자를 어느 위치를 중심으로 생성할지 정합니다.")]
        [SerializeField] private GoldTimeAttackGridAnchorMode gridAnchorMode = GoldTimeAttackGridAnchorMode.PlayerStartPoint;

        [Tooltip("Grid Anchor Mode가 CustomGridCenter일 때 사용할 격자 중심점입니다.")]
        [SerializeField] private Transform gridCenter;

        [Tooltip("격자 중심 위치에 추가로 더할 오프셋입니다.")]
        [SerializeField] private Vector2 gridAnchorOffset = Vector2.zero;

        [Header("Grid Area")]
        [Tooltip("Drop Across Entire Stage가 false일 때 사용할 가로 방향 격자 칸 수입니다.")]
        [SerializeField] private int gridColumns = 11;

        [Tooltip("Drop Across Entire Stage가 false일 때 사용할 세로 방향 격자 칸 수입니다.")]
        [SerializeField] private int gridRows = 7;

        [Tooltip("격자 한 칸 사이의 간격입니다. 전체 스테이지 모드에서도 이 값을 기준으로 촘촘함이 결정됩니다.")]
        [SerializeField] private Vector2 gridSpacing = new Vector2(1.1f, 1.1f);

        [Tooltip("각 칸마다 코인이 생성될 확률입니다. 1이면 모든 칸에 생성됩니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float spawnChancePerCell = 1f;

        [Tooltip("격자 위치에 약간의 랜덤 흔들림을 줄지 정합니다. 0이면 완전한 격자입니다.")]
        [SerializeField] private float randomJitter = 0.05f;

        [Tooltip("중심 주변을 비워둘 반경입니다. 전체 스테이지 모드에서는 PlayerStartPoint 주변을 비웁니다. 완전히 전체에 깔고 싶으면 0으로 두세요.")]
        [SerializeField] private float emptyCenterRadius = 0.5f;

        [Header("Coin Type Random")]
        [Tooltip("코인 종류별 등장 가중치입니다. 비워두면 Bronze1만 생성됩니다.")]
        [SerializeField]
        private CoinSpawnWeight[] coinSpawnWeights =
        {
            new CoinSpawnWeight { coinType = CoinType.Bronze1, weight = 70 },
            new CoinSpawnWeight { coinType = CoinType.Silver2, weight = 22 },
            new CoinSpawnWeight { coinType = CoinType.Gold5, weight = 7 },
            new CoinSpawnWeight { coinType = CoinType.Pouch30, weight = 1 },
            new CoinSpawnWeight { coinType = CoinType.Bag50, weight = 0 },
        };

        [Header("Coin Spawn")]
        [Tooltip("EntityManager에 위치 지정 코인 스폰 함수가 없을 때 사용할 예비 Coin 프리팹입니다. 기존 Coin 프리팹을 넣어주세요.")]
        [SerializeField] private Coin fallbackCoinPrefab;

        [Tooltip("코인 생성 시 기존 스폰 애니메이션을 사용할지 여부입니다. 입장 즉시 깔리게 하려면 false를 추천합니다.")]
        [SerializeField] private bool useCoinSpawnAnimation = false;

        [Tooltip("스폰 애니메이션 도중에도 코인을 바로 먹을 수 있게 할지 여부입니다. Use Coin Spawn Animation이 false면 큰 의미는 없습니다.")]
        [SerializeField] private bool collectableDuringSpawn = true;

        [Tooltip("생성된 코인들을 이 Transform 아래에 정리할지 여부입니다. 비워두면 Room 오브젝트 아래에 정리합니다.")]
        [SerializeField] private Transform spawnedCoinParent;

        [Header("Optional Timer UI")]
        [Tooltip("남은 시간을 표시할 UI 루트입니다. 없어도 동작합니다.")]
        [SerializeField] private GameObject timerRoot;

        [Tooltip("남은 시간을 표시할 TMP 텍스트입니다. 없어도 동작합니다.")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("타이머 텍스트 앞에 붙일 문구입니다.")]
        [SerializeField] private string timerPrefix = "GOLD TIME ";

       

        private readonly List<SpawnedCoinRecord> spawnedCoins = new List<SpawnedCoinRecord>();

        private Coroutine timeAttackRoutine;
       
        private MethodInfo cachedEntityManagerSpawnCoinMethod;
        private MethodInfo cachedEntityManagerDespawnCoinMethod;

        protected override void OnBeginRoom()
        {
            if (timeAttackRoutine != null)
            {
                StopCoroutine(timeAttackRoutine);
                timeAttackRoutine = null;
            }

            RemoveRemainingActiveCoins();
            spawnedCoins.Clear();

           

            SetTimerVisible(true);
            UpdateTimerUI(timeLimit);

            // BeginRoom은 플레이어가 미니 스테이지 시작 위치로 이동한 직후 호출됩니다.
            // 여기서 바로 코인을 뿌리면 입장하자마자 골드 타임어택이 시작됩니다.
            SpawnCoinGrid();

            timeAttackRoutine = StartCoroutine(TimeAttackRoutine());
        }

        private IEnumerator TimeAttackRoutine()
        {
            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageGoldTimeAttackRoom] 골드 타임어택 시작. " +
                    $"timeLimit={timeLimit}, dropAcrossEntireStage={dropAcrossEntireStage}"
                );
            }

            float remainingTime = Mathf.Max(0f, timeLimit);

            while (remainingTime > 0f)
            {
                UpdateTimerUI(remainingTime);
                remainingTime -= Time.deltaTime;
                yield return null;
            }

            UpdateTimerUI(0f);

            if (removeUncollectedCoinsWhenTimeUp)
            {
                RemoveRemainingActiveCoins();
            }

            if (clearDelayAfterTimeUp > 0f)
            {
                yield return new WaitForSeconds(clearDelayAfterTimeUp);
            }

            
            SetTimerVisible(false);

            if (debugLog)
            {
                Debug.Log("[MiniStageGoldTimeAttackRoom] 제한 시간 종료. 귀환 상호작용을 활성화합니다.");
            }

            CompleteRoomWithoutReward();
            timeAttackRoutine = null;
        }

        private void SpawnCoinGrid()
        {
            if (dropAcrossEntireStage)
            {
                SpawnCoinGridAcrossEntireStage();
            }
            else
            {
                SpawnCoinGridAroundAnchor();
            }
        }

        private void SpawnCoinGridAcrossEntireStage()
        {
            Bounds bounds = GetStageAreaBounds();

            float spacingX = Mathf.Max(0.1f, gridSpacing.x);
            float spacingY = Mathf.Max(0.1f, gridSpacing.y);

            float minX = bounds.min.x + Mathf.Max(0f, stageAreaPadding.x);
            float maxX = bounds.max.x - Mathf.Max(0f, stageAreaPadding.x);
            float minY = bounds.min.y + Mathf.Max(0f, stageAreaPadding.y);
            float maxY = bounds.max.y - Mathf.Max(0f, stageAreaPadding.y);

            if (maxX < minX)
            {
                float centerX = bounds.center.x;
                minX = centerX;
                maxX = centerX;
            }

            if (maxY < minY)
            {
                float centerY = bounds.center.y;
                minY = centerY;
                maxY = centerY;
            }

            List<Vector3> candidatePositions = new List<Vector3>();

            for (float y = minY; y <= maxY + 0.001f; y += spacingY)
            {
                for (float x = minX; x <= maxX + 0.001f; x += spacingX)
                {
                    Vector3 position = new Vector3(x, y, transform.position.z);

                    if (ShouldSkipByEmptyCenter(position))
                    {
                        continue;
                    }

                    candidatePositions.Add(position);
                }
            }

            if (shuffleFullStageCells)
            {
                Shuffle(candidatePositions);
            }

            Transform parent = spawnedCoinParent != null ? spawnedCoinParent : transform;
            int spawnCount = 0;

            for (int i = 0; i < candidatePositions.Count; i++)
            {
                if (maxSpawnCount > 0 && spawnCount >= maxSpawnCount)
                {
                    break;
                }

                if (Random.value > spawnChancePerCell)
                {
                    continue;
                }

                Vector3 worldPosition = candidatePositions[i];

                if (randomJitter > 0f)
                {
                    Vector2 jitter = Random.insideUnitCircle * randomJitter;
                    worldPosition += new Vector3(jitter.x, jitter.y, 0f);
                }

                CoinType selectedCoinType = PickRandomCoinType();
                Coin spawnedCoin = SpawnCoinAt(worldPosition, selectedCoinType, parent);

                if (spawnedCoin != null)
                {
                    spawnCount++;
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageGoldTimeAttackRoom] 미니 스테이지 전체 코인 배치 완료. " +
                    $"count={spawnCount}, candidate={candidatePositions.Count}, boundsCenter={bounds.center}, boundsSize={bounds.size}"
                );
            }
        }

        private void SpawnCoinGridAroundAnchor()
        {
            int safeColumns = Mathf.Max(1, gridColumns);
            int safeRows = Mathf.Max(1, gridRows);

            Vector3 anchorPosition = GetGridAnchorWorldPosition();
            Transform parent = spawnedCoinParent != null ? spawnedCoinParent : transform;

            float startX = -((safeColumns - 1) * gridSpacing.x) * 0.5f;
            float startY = -((safeRows - 1) * gridSpacing.y) * 0.5f;

            int spawnCount = 0;

            for (int y = 0; y < safeRows; y++)
            {
                for (int x = 0; x < safeColumns; x++)
                {
                    if (Random.value > spawnChancePerCell)
                    {
                        continue;
                    }

                    Vector2 localPosition = new Vector2(
                        startX + x * gridSpacing.x,
                        startY + y * gridSpacing.y
                    );

                    Vector3 worldPosition = anchorPosition + new Vector3(
                        localPosition.x,
                        localPosition.y,
                        0f
                    );

                    if (ShouldSkipByEmptyCenter(worldPosition))
                    {
                        continue;
                    }

                    if (randomJitter > 0f)
                    {
                        Vector2 jitter = Random.insideUnitCircle * randomJitter;
                        worldPosition += new Vector3(jitter.x, jitter.y, 0f);
                    }

                    CoinType selectedCoinType = PickRandomCoinType();
                    Coin spawnedCoin = SpawnCoinAt(worldPosition, selectedCoinType, parent);

                    if (spawnedCoin != null)
                    {
                        spawnCount++;
                    }
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageGoldTimeAttackRoom] 기준점 주변 코인 배치 완료. " +
                    $"count={spawnCount}, anchorMode={gridAnchorMode}, anchor={anchorPosition}"
                );
            }
        }

        private bool ShouldSkipByEmptyCenter(Vector3 worldPosition)
        {
            if (emptyCenterRadius <= 0f)
            {
                return false;
            }

            Vector3 center = GetEmptyCenterWorldPosition();
            float sqrDistance = ((Vector2)worldPosition - (Vector2)center).sqrMagnitude;

            return sqrDistance < emptyCenterRadius * emptyCenterRadius;
        }

        private Vector3 GetEmptyCenterWorldPosition()
        {
            if (PlayerStartPoint != null)
            {
                return PlayerStartPoint.position;
            }

            if (playerCharacter != null)
            {
                return playerCharacter.transform.position;
            }

            return GetGridAnchorWorldPosition();
        }

        private Bounds GetStageAreaBounds()
        {
            if (stageAreaCollider != null)
            {
                return stageAreaCollider.bounds;
            }

            if (stageAreaSpriteRenderer != null)
            {
                return stageAreaSpriteRenderer.bounds;
            }

            Vector3 center = stageAreaCenter != null
                ? stageAreaCenter.position
                : transform.position;

            Vector3 size = new Vector3(
                Mathf.Max(0.1f, stageAreaSize.x),
                Mathf.Max(0.1f, stageAreaSize.y),
                0f
            );

            return new Bounds(center, size);
        }

        private Vector3 GetGridAnchorWorldPosition()
        {
            Vector3 anchorPosition;

            switch (gridAnchorMode)
            {
                case GoldTimeAttackGridAnchorMode.PlayerStartPoint:
                    anchorPosition = PlayerStartPoint != null
                        ? PlayerStartPoint.position
                        : transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.PlayerCurrentPosition:
                    anchorPosition = playerCharacter != null
                        ? playerCharacter.transform.position
                        : PlayerStartPoint != null
                            ? PlayerStartPoint.position
                            : transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.CustomGridCenter:
                    anchorPosition = gridCenter != null
                        ? gridCenter.position
                        : PlayerStartPoint != null
                            ? PlayerStartPoint.position
                            : transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.RoomTransform:
                    anchorPosition = transform.position;
                    break;

                default:
                    anchorPosition = PlayerStartPoint != null
                        ? PlayerStartPoint.position
                        : transform.position;
                    break;
            }

            anchorPosition += (Vector3)gridAnchorOffset;
            anchorPosition.z = transform.position.z;

            return anchorPosition;
        }

        private CoinType PickRandomCoinType()
        {
            if (coinSpawnWeights == null || coinSpawnWeights.Length == 0)
            {
                return CoinType.Bronze1;
            }

            int totalWeight = 0;

            for (int i = 0; i < coinSpawnWeights.Length; i++)
            {
                CoinSpawnWeight entry = coinSpawnWeights[i];

                if (entry == null)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0, entry.weight);
            }

            if (totalWeight <= 0)
            {
                return CoinType.Bronze1;
            }

            int roll = Random.Range(0, totalWeight);
            int current = 0;

            for (int i = 0; i < coinSpawnWeights.Length; i++)
            {
                CoinSpawnWeight entry = coinSpawnWeights[i];

                if (entry == null)
                {
                    continue;
                }

                current += Mathf.Max(0, entry.weight);

                if (roll < current)
                {
                    return entry.coinType;
                }
            }

            return CoinType.Bronze1;
        }

        private Coin SpawnCoinAt(Vector3 worldPosition, CoinType coinType, Transform parent)
        {
            Coin coinFromEntityManager = TrySpawnCoinWithEntityManager(worldPosition, coinType);

            if (coinFromEntityManager != null)
            {
                spawnedCoins.Add(new SpawnedCoinRecord
                {
                    coin = coinFromEntityManager,
                    spawnedByEntityManager = true
                });

                return coinFromEntityManager;
            }

            if (fallbackCoinPrefab == null)
            {
                Debug.LogWarning(
                    "[MiniStageGoldTimeAttackRoom] 코인을 생성할 수 없습니다. " +
                    "EntityManager의 SpawnCoin 함수를 찾지 못했고, Fallback Coin Prefab도 비어 있습니다."
                );

                return null;
            }

            Coin fallbackCoin = Instantiate(
                fallbackCoinPrefab,
                worldPosition,
                Quaternion.identity,
                parent
            );

            if (entityManager != null && playerCharacter != null)
            {
                fallbackCoin.Init(entityManager, playerCharacter);
            }
            else
            {
                Debug.LogWarning(
                    "[MiniStageGoldTimeAttackRoom] EntityManager 또는 PlayerCharacter가 비어 있습니다. " +
                    "Fallback Coin의 수집 처리가 정상 동작하지 않을 수 있습니다."
                );
            }

            fallbackCoin.Setup(
                worldPosition,
                coinType,
                useCoinSpawnAnimation,
                collectableDuringSpawn
            );

            spawnedCoins.Add(new SpawnedCoinRecord
            {
                coin = fallbackCoin,
                spawnedByEntityManager = false
            });

            return fallbackCoin;
        }

        /// <summary>
        /// 현재 프로젝트의 EntityManager에 SpawnCoin 계열 함수가 있으면 우선 사용합니다.
        ///
        /// 브랜치별로 함수 시그니처가 달라져도 대응할 수 있게 Reflection을 사용합니다.
        /// 지원 가능한 파라미터:
        /// - Vector2 또는 Vector3 위치
        /// - CoinType 코인 타입
        /// - bool 스폰 애니메이션 / 즉시 수집 가능 여부
        /// </summary>
        private Coin TrySpawnCoinWithEntityManager(Vector3 worldPosition, CoinType coinType)
        {
            if (entityManager == null)
            {
                return null;
            }

            MethodInfo method = GetEntityManagerSpawnCoinMethod();

            if (method == null)
            {
                return null;
            }

            object[] arguments;

            if (!TryBuildSpawnCoinArguments(method, worldPosition, coinType, out arguments))
            {
                return null;
            }

            object result = method.Invoke(entityManager, arguments);

            Coin spawnedCoin = result as Coin;

            if (spawnedCoin != null)
            {
                return spawnedCoin;
            }

            GameObject resultGameObject = result as GameObject;

            if (resultGameObject != null)
            {
                return resultGameObject.GetComponent<Coin>();
            }

            Component resultComponent = result as Component;

            if (resultComponent != null)
            {
                return resultComponent.GetComponent<Coin>();
            }

            Collectable collectable = result as Collectable;

            if (collectable != null)
            {
                return collectable as Coin;
            }

            return null;
        }

        private MethodInfo GetEntityManagerSpawnCoinMethod()
        {
            if (cachedEntityManagerSpawnCoinMethod != null)
            {
                return cachedEntityManagerSpawnCoinMethod;
            }

            if (entityManager == null)
            {
                return null;
            }

            MethodInfo[] methods = entityManager.GetType().GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "SpawnCoin" &&
                    method.Name != "SpawnGoldCoin")
                {
                    continue;
                }

                if (method.ReturnType == typeof(void))
                {
                    continue;
                }

                object[] dummyArguments;

                if (!TryBuildSpawnCoinArguments(
                    method,
                    Vector3.zero,
                    CoinType.Bronze1,
                    out dummyArguments
                ))
                {
                    continue;
                }

                cachedEntityManagerSpawnCoinMethod = method;
                return cachedEntityManagerSpawnCoinMethod;
            }

            return null;
        }

        private bool TryBuildSpawnCoinArguments(
            MethodInfo method,
            Vector3 worldPosition,
            CoinType coinType,
            out object[] arguments
        )
        {
            ParameterInfo[] parameters = method.GetParameters();
            arguments = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;

                if (parameterType == typeof(Vector2))
                {
                    arguments[i] = (Vector2)worldPosition;
                    continue;
                }

                if (parameterType == typeof(Vector3))
                {
                    arguments[i] = worldPosition;
                    continue;
                }

                if (parameterType == typeof(CoinType))
                {
                    arguments[i] = coinType;
                    continue;
                }

                if (parameterType == typeof(bool))
                {
                    // bool 파라미터가 여러 개여도, 기본적으로 즉시 먹을 수 있는 타임어택용 값으로 통일합니다.
                    arguments[i] = useCoinSpawnAnimation ? collectableDuringSpawn : false;
                    continue;
                }

                if (parameters[i].HasDefaultValue)
                {
                    arguments[i] = parameters[i].DefaultValue;
                    continue;
                }

                arguments = null;
                return false;
            }

            return true;
        }

        private void RemoveRemainingActiveCoins()
        {
            for (int i = spawnedCoins.Count - 1; i >= 0; i--)
            {
                SpawnedCoinRecord record = spawnedCoins[i];

                if (record == null || record.coin == null)
                {
                    spawnedCoins.RemoveAt(i);
                    continue;
                }

                Coin coin = record.coin;

                if (!coin.gameObject.activeInHierarchy)
                {
                    spawnedCoins.RemoveAt(i);
                    continue;
                }

                if (record.spawnedByEntityManager)
                {
                    bool despawned = TryDespawnCoinWithEntityManager(coin);

                    if (!despawned)
                    {
                        coin.gameObject.SetActive(false);
                    }
                }
                else
                {
                    Destroy(coin.gameObject);
                }

                spawnedCoins.RemoveAt(i);
            }

            if (debugLog)
            {
                Debug.Log("[MiniStageGoldTimeAttackRoom] 제한 시간 종료로 남은 코인을 제거했습니다.");
            }
        }

        private bool TryDespawnCoinWithEntityManager(Coin coin)
        {
            if (entityManager == null || coin == null)
            {
                return false;
            }

            MethodInfo method = GetEntityManagerDespawnCoinMethod();

            if (method == null)
            {
                return false;
            }

            method.Invoke(entityManager, new object[] { coin });
            return true;
        }

        private MethodInfo GetEntityManagerDespawnCoinMethod()
        {
            if (cachedEntityManagerDespawnCoinMethod != null)
            {
                return cachedEntityManagerDespawnCoinMethod;
            }

            if (entityManager == null)
            {
                return null;
            }

            MethodInfo[] methods = entityManager.GetType().GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "DespawnCoin" &&
                    method.Name != "DespawnGoldCoin")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != 1)
                {
                    continue;
                }

                if (!parameters[0].ParameterType.IsAssignableFrom(typeof(Coin)))
                {
                    continue;
                }

                cachedEntityManagerDespawnCoinMethod = method;
                return cachedEntityManagerDespawnCoinMethod;
            }

            return null;
        }

        private void SetTimerVisible(bool visible)
        {
            if (timerRoot != null)
            {
                timerRoot.SetActive(visible);
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(visible);
            }
        }

        private void UpdateTimerUI(float remainingTime)
        {
            if (timerText == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            timerText.text = $"{timerPrefix}{seconds}";
        }

        protected override void OnCleanupRoom()
        {
            if (timeAttackRoutine != null)
            {
                StopCoroutine(timeAttackRoutine);
                timeAttackRoutine = null;
            }

           
            SetTimerVisible(false);
            RemoveRemainingActiveCoins();
        }

        private void Shuffle(List<Vector3> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int randomIndex = Random.Range(i, list.Count);
                Vector3 temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (dropAcrossEntireStage)
            {
                Bounds bounds = GetStageAreaBounds();

                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(bounds.center, bounds.size);

                Vector3 paddedSize = new Vector3(
                    Mathf.Max(0f, bounds.size.x - stageAreaPadding.x * 2f),
                    Mathf.Max(0f, bounds.size.y - stageAreaPadding.y * 2f),
                    0f
                );

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(bounds.center, paddedSize);

                if (emptyCenterRadius > 0f)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(GetEmptyCenterWorldPosition(), emptyCenterRadius);
                }

                return;
            }

            Vector3 anchorPosition = GetEditorPreviewAnchorPosition();

            int safeColumns = Mathf.Max(1, gridColumns);
            int safeRows = Mathf.Max(1, gridRows);

            float width = (safeColumns - 1) * gridSpacing.x;
            float height = (safeRows - 1) * gridSpacing.y;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                anchorPosition,
                new Vector3(width, height, 0f)
            );

            if (emptyCenterRadius > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(anchorPosition, emptyCenterRadius);
            }
        }

        private Vector3 GetEditorPreviewAnchorPosition()
        {
            Vector3 anchorPosition;

            switch (gridAnchorMode)
            {
                case GoldTimeAttackGridAnchorMode.PlayerStartPoint:
                    anchorPosition = PlayerStartPoint != null
                        ? PlayerStartPoint.position
                        : transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.CustomGridCenter:
                    anchorPosition = gridCenter != null
                        ? gridCenter.position
                        : transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.RoomTransform:
                    anchorPosition = transform.position;
                    break;

                case GoldTimeAttackGridAnchorMode.PlayerCurrentPosition:
                default:
                    anchorPosition = PlayerStartPoint != null
                        ? PlayerStartPoint.position
                        : transform.position;
                    break;
            }

            anchorPosition += (Vector3)gridAnchorOffset;
            anchorPosition.z = transform.position.z;

            return anchorPosition;
        }
    }
}