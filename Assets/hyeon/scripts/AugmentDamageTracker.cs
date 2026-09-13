using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class AugmentDamageTracker : MonoBehaviour
    {
        public static AugmentDamageTracker Instance { get; private set; }


        // =========================================================
        // Damage Data
        // =========================================================

        // 증강 이름 -> 이번 판 누적 피해량
        private readonly Dictionary<string, float> augmentDamages =
            new Dictionary<string, float>();


        // 읽기 전용 접근
        public IReadOnlyDictionary<string, float> AugmentDamages =>
            augmentDamages;


        // =========================================================
        // Unity
        // =========================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "[AugmentDamageTracker] 중복 인스턴스가 생성되어 제거합니다."
                );

                Destroy(gameObject);
                return;
            }

            Instance = this;
        }


        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }


        // =========================================================
        // Record Damage
        // =========================================================

        /// <summary>
        /// 특정 증강이 준 피해량을 누적합니다.
        /// </summary>
        public void RecordDamage(string augmentName, float damage)
        {
            if (string.IsNullOrWhiteSpace(augmentName))
                return;

            if (damage <= 0f)
                return;


            if (augmentDamages.ContainsKey(augmentName))
            {
                augmentDamages[augmentName] += damage;
            }
            else
            {
                augmentDamages.Add(augmentName, damage);
            }
        }


        // =========================================================
        // Top Damage Augment
        // =========================================================

        /// <summary>
        /// 이번 판에서 가장 많은 피해를 준 증강의 이름을 반환합니다.
        /// </summary>
        public string GetTopDamageAugmentName()
        {
            string topAugmentName = null;
            float highestDamage = 0f;


            foreach (KeyValuePair<string, float> pair in augmentDamages)
            {
                if (pair.Value > highestDamage)
                {
                    highestDamage = pair.Value;
                    topAugmentName = pair.Key;
                }
            }


            if (string.IsNullOrEmpty(topAugmentName))
            {
                return "-";
            }


            return topAugmentName;
        }


        /// <summary>
        /// 가장 많은 피해를 준 증강의 누적 피해량을 반환합니다.
        /// </summary>
        public float GetTopDamageAmount()
        {
            float highestDamage = 0f;


            foreach (KeyValuePair<string, float> pair in augmentDamages)
            {
                if (pair.Value > highestDamage)
                {
                    highestDamage = pair.Value;
                }
            }


            return highestDamage;
        }


        // =========================================================
        // Individual Damage
        // =========================================================

        /// <summary>
        /// 특정 증강의 누적 피해량을 가져옵니다.
        /// </summary>
        public float GetDamage(string augmentName)
        {
            if (string.IsNullOrWhiteSpace(augmentName))
                return 0f;


            if (augmentDamages.TryGetValue(
                augmentName,
                out float damage
            ))
            {
                return damage;
            }


            return 0f;
        }


        // =========================================================
        // Reset
        // =========================================================

        /// <summary>
        /// 기록된 모든 피해량을 초기화합니다.
        /// </summary>
        public void ResetDamageData()
        {
            augmentDamages.Clear();
        }


        // =========================================================
        // Debug
        // =========================================================

        [ContextMenu("Debug - Print Augment Damages")]
        private void DebugPrintAugmentDamages()
        {
            Debug.Log("===== 증강별 피해량 =====");


            if (augmentDamages.Count == 0)
            {
                Debug.Log("기록된 증강 피해량 없음");
                return;
            }


            foreach (KeyValuePair<string, float> pair in augmentDamages)
            {
                Debug.Log(
                    $"{pair.Key} : {pair.Value:0.##}"
                );
            }


            Debug.Log(
                $"[최고 피해 증강] " +
                $"{GetTopDamageAugmentName()} : " +
                $"{GetTopDamageAmount():0.##}"
            );
        }
    }
}