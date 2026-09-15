using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(
        fileName = "Level Spawn Balance Profile",
        menuName = "Blueprints/Level/Spawn Balance Profile",
        order = 20)]
    public class LevelSpawnBalanceProfile : ScriptableObject
    {
        [Serializable]
        public class SpawnRatePoint
        {
            [Tooltip("몇 분 시점인지 입력합니다. 예: 5 = 5분")]
            public float minute;

            [Tooltip("초당 일반 몬스터 스폰 수입니다. 3이면 대략 1초에 3마리입니다.")]
            public float spawnRate = 1f;
        }

        [Serializable]
        public class MonsterSpawnEntry
        {
            [Header("Info")]
            public string memo;

            [Tooltip("Level 1 Blueprint의 Monster Settings 안에 들어있는 MonsterBlueprint를 넣습니다.")]
            public MonsterBlueprint monsterBlueprint;

            [Header("Spawn Weight By Minute")]
            [Tooltip("분 단위 스폰 가중치입니다. 값은 자동 정규화됩니다.")]
            public AnimationCurve spawnWeightByMinute = AnimationCurve.Constant(0f, 15f, 0f);

        }

        [Header("Level Time")]
        [Tooltip("체크하면 이 프로필 적용 시 LevelBlueprint의 Level Time을 아래 분 단위 값으로 변경합니다.")]
        public bool overrideLevelTime = true;

        [Tooltip("스테이지 전체 플레이 시간입니다. 15이면 15분입니다.")]
        public float levelDurationMinutes = 15f;

        [Header("Difficulty Design Reference")]
        [Tooltip("기획 난이도(1~10)입니다. 실제 출현량은 Spawn Rate, 구성 비율, 고정 HP 및 증강 성장으로 조정합니다.")]
        public AnimationCurve difficultyTargetByMinute = AnimationCurve.Linear(0f, 1f, 15f, 10f);

        [Header("Spawn Rate")]
        [Tooltip("전체 일반 몬스터 물량 증가 곡선입니다.")]
        public SpawnRatePoint[] spawnRatePoints =
        {
            new SpawnRatePoint { minute = 0f,  spawnRate = 1.0f },
            new SpawnRatePoint { minute = 1f,  spawnRate = 1.4f },
            new SpawnRatePoint { minute = 3f,  spawnRate = 2.2f },
            new SpawnRatePoint { minute = 5f,  spawnRate = 3.0f },
            new SpawnRatePoint { minute = 8f,  spawnRate = 4.0f },
            new SpawnRatePoint { minute = 12f, spawnRate = 5.0f },
            new SpawnRatePoint { minute = 13f, spawnRate = 6.2f },
            new SpawnRatePoint { minute = 15f, spawnRate = 7.2f }
        };

        [Header("Normal Monster Entries")]
        [Tooltip("일반 몬스터을 여기에 넣습니다. 여기에 없는 몬스터는 일반 스폰 테이블에서 나오지 않습니다.")]
        public List<MonsterSpawnEntry> normalMonsterEntries = new List<MonsterSpawnEntry>();

        [Header("Sampling Minutes")]
        [Tooltip("Spawn Chance Keyframes로 변환할 분 단위 샘플 시점입니다.")]
        public float[] chanceSampleMinutes =
        {
            0f, 1f, 2f, 3f, 4f, 5f, 6f, 8f, 10f, 12f, 15f, 18f, 15f
        };


        [Header("Options")]
        [Tooltip("스폰 가중치 합계를 자동으로 1로 맞춥니다. 켜두는 것을 추천합니다.")]
        public bool normalizeSpawnChances = true;

        [Tooltip("빌드된 SpawnTable 정보를 Console에 출력합니다.")]
        public bool debugLog = true;

        public void ApplyTo(LevelBlueprint levelBlueprint)
        {
            if (levelBlueprint == null)
            {
                Debug.LogWarning("[LevelSpawnBalanceProfile] LevelBlueprint가 비어 있어 적용하지 못했습니다.", this);
                return;
            }

            if (overrideLevelTime)
            {
                levelBlueprint.levelTime = Mathf.Max(1f, levelDurationMinutes) * 60f;
            }

            levelBlueprint.monsterSpawnTable = BuildSpawnTable(levelBlueprint);

            if (debugLog)
            {
                Debug.Log(
                    $"[LevelSpawnBalanceProfile] 적용 완료 | Level={levelBlueprint.name} | " +
                    $"LevelTime={levelBlueprint.levelTime:0.#}s | " +
                    $"FlatMonsterCount={GetTotalFlatMonsterCount(levelBlueprint)}",
                    this
                );
            }
        }

        public MonsterSpawnTable BuildSpawnTable(LevelBlueprint levelBlueprint)
        {
            int flatMonsterCount = GetTotalFlatMonsterCount(levelBlueprint);

            MonsterSpawnTable table = new MonsterSpawnTable
            {
                spawnRateKeyframes = BuildSpawnRateKeyframes(),
                spawnChanceKeyframes = BuildSpawnChanceKeyframes(levelBlueprint, flatMonsterCount)
            };

            return table;
        }

        private MonsterSpawnTable.SpawnRateKeyframe[] BuildSpawnRateKeyframes()
        {
            if (spawnRatePoints == null || spawnRatePoints.Length == 0)
            {
                return new[]
                {
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 0f, spawnRate = 1f },
                    new MonsterSpawnTable.SpawnRateKeyframe { t = 1f, spawnRate = 1f }
                };
            }

            List<SpawnRatePoint> sortedPoints = new List<SpawnRatePoint>(spawnRatePoints);
            sortedPoints.Sort((a, b) => a.minute.CompareTo(b.minute));

            List<MonsterSpawnTable.SpawnRateKeyframe> keyframes = new List<MonsterSpawnTable.SpawnRateKeyframe>();

            for (int i = 0; i < sortedPoints.Count; i++)
            {
                float t = MinuteToNormalizedTime(sortedPoints[i].minute);

                keyframes.Add(new MonsterSpawnTable.SpawnRateKeyframe
                {
                    t = t,
                    spawnRate = Mathf.Max(0f, sortedPoints[i].spawnRate)
                });
            }

            EnsureFirstAndLastSpawnRateKeyframes(keyframes);

            return keyframes.ToArray();
        }

        private MonsterSpawnTable.SpawnChanceKeyframe[] BuildSpawnChanceKeyframes(
            LevelBlueprint levelBlueprint,
            int flatMonsterCount)
        {
            float[] minutes = GetSafeSampleMinutes(chanceSampleMinutes);

            MonsterSpawnTable.SpawnChanceKeyframe[] keyframes =
                new MonsterSpawnTable.SpawnChanceKeyframe[minutes.Length];

            for (int i = 0; i < minutes.Length; i++)
            {
                float minute = minutes[i];
                float[] chances = new float[flatMonsterCount];

                float totalWeight = 0f;

                if (normalMonsterEntries != null)
                {
                    for (int entryIndex = 0; entryIndex < normalMonsterEntries.Count; entryIndex++)
                    {
                        MonsterSpawnEntry entry = normalMonsterEntries[entryIndex];

                        if (entry == null || entry.monsterBlueprint == null)
                        {
                            continue;
                        }

                        if (!TryFindFlatIndex(levelBlueprint, entry.monsterBlueprint, out int flatIndex))
                        {
                            if (debugLog)
                            {
                                Debug.LogWarning(
                                    $"[LevelSpawnBalanceProfile] MonsterBlueprint를 LevelBlueprint에서 찾지 못했습니다: {entry.memo} / {entry.monsterBlueprint.name}",
                                    this
                                );
                            }

                            continue;
                        }

                        float weight = Mathf.Max(0f, entry.spawnWeightByMinute.Evaluate(minute));
                        chances[flatIndex] += weight;
                        totalWeight += weight;
                    }
                }

                if (totalWeight <= 0.0001f)
                {
                    int fallbackIndex = FindFirstValidEntryFlatIndex(levelBlueprint);

                    if (fallbackIndex >= 0 && fallbackIndex < chances.Length)
                    {
                        chances[fallbackIndex] = 1f;
                        totalWeight = 1f;
                    }
                }

                if (normalizeSpawnChances && totalWeight > 0.0001f)
                {
                    for (int j = 0; j < chances.Length; j++)
                    {
                        chances[j] /= totalWeight;
                    }
                }

                keyframes[i] = new MonsterSpawnTable.SpawnChanceKeyframe
                {
                    t = MinuteToNormalizedTime(minute),
                    spawnChances = chances
                };
            }

            return keyframes;
        }

        private float[] GetSafeSampleMinutes(float[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new[] { 0f, levelDurationMinutes };
            }

            List<float> minutes = new List<float>(source);

            if (!ContainsApproximately(minutes, 0f))
            {
                minutes.Add(0f);
            }

            if (!ContainsApproximately(minutes, levelDurationMinutes))
            {
                minutes.Add(levelDurationMinutes);
            }

            minutes.Sort();

            for (int i = 0; i < minutes.Count; i++)
            {
                minutes[i] = Mathf.Clamp(minutes[i], 0f, levelDurationMinutes);
            }

            // Clamping out-of-range keys used to create duplicate end timestamps.
            for (int i = minutes.Count - 1; i > 0; i--)
                if (Mathf.Approximately(minutes[i], minutes[i - 1])) minutes.RemoveAt(i);
            return minutes.ToArray();
        }

        private bool ContainsApproximately(List<float> values, float target)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Mathf.Abs(values[i] - target) < 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private int FindFirstValidEntryFlatIndex(LevelBlueprint levelBlueprint)
        {
            if (normalMonsterEntries == null)
            {
                return -1;
            }

            for (int i = 0; i < normalMonsterEntries.Count; i++)
            {
                MonsterSpawnEntry entry = normalMonsterEntries[i];

                if (entry == null || entry.monsterBlueprint == null)
                {
                    continue;
                }

                if (TryFindFlatIndex(levelBlueprint, entry.monsterBlueprint, out int flatIndex))
                {
                    return flatIndex;
                }
            }

            return -1;
        }

        private int GetTotalFlatMonsterCount(LevelBlueprint levelBlueprint)
        {
            if (levelBlueprint == null || levelBlueprint.monsters == null)
            {
                return 0;
            }

            int count = 0;

            for (int poolIndex = 0; poolIndex < levelBlueprint.monsters.Length; poolIndex++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                count += container.monsterBlueprints.Length;
            }

            return count;
        }

        private bool TryFindFlatIndex(
            LevelBlueprint levelBlueprint,
            MonsterBlueprint targetBlueprint,
            out int flatIndex)
        {
            flatIndex = -1;

            if (levelBlueprint == null || levelBlueprint.monsters == null || targetBlueprint == null)
            {
                return false;
            }

            int currentFlatIndex = 0;

            for (int poolIndex = 0; poolIndex < levelBlueprint.monsters.Length; poolIndex++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                for (int blueprintIndex = 0; blueprintIndex < container.monsterBlueprints.Length; blueprintIndex++)
                {
                    if (container.monsterBlueprints[blueprintIndex] == targetBlueprint)
                    {
                        flatIndex = currentFlatIndex;
                        return true;
                    }

                    currentFlatIndex++;
                }
            }

            return false;
        }

        private float MinuteToNormalizedTime(float minute)
        {
            float safeDuration = Mathf.Max(0.01f, levelDurationMinutes);
            return Mathf.Clamp01(minute / safeDuration);
        }

        private void EnsureFirstAndLastSpawnRateKeyframes(List<MonsterSpawnTable.SpawnRateKeyframe> keyframes)
        {
            if (keyframes == null || keyframes.Count == 0)
            {
                return;
            }

            keyframes.Sort((a, b) => a.t.CompareTo(b.t));

            if (keyframes[0].t > 0f)
            {
                keyframes.Insert(0, new MonsterSpawnTable.SpawnRateKeyframe
                {
                    t = 0f,
                    spawnRate = keyframes[0].spawnRate
                });
            }

            if (keyframes[keyframes.Count - 1].t < 1f)
            {
                keyframes.Add(new MonsterSpawnTable.SpawnRateKeyframe
                {
                    t = 1f,
                    spawnRate = keyframes[keyframes.Count - 1].spawnRate
                });
            }
        }

    }
}
