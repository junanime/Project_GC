using UnityEngine;

namespace Vampire
{
    public class TemperatureStatus : MonoBehaviour, ICombatStatus
    {
        public CombatStatusTag ActiveStatusTags => timer > 0 && currentTemperature > 36.5f ? CombatStatusTag.Vulnerability : CombatStatusTag.None;
        private float currentTemperature = 36.5f;
        private const float maxTemperature = 41.5f; // 최대 5스택 제한 (36.5 -> 37.5 -> 38.5 -> 39.5 -> 40.5 -> 41.5)
        private float duration = 4.0f; // 4초 동안 추가 타격이 없으면 체온이 내려가며 컴포넌트 삭제
        private float timer;

        private void Start()
        {
            ResetTimer();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                Destroy(this); // 타임아웃 시 체온계 버프 해제 및 컴포넌트 자가 파괴 (최적화)
            }
        }

        public void IncreaseTemperature()
        {
            ResetTimer();
            if (currentTemperature < maxTemperature)
            {
                currentTemperature += 1.0f; // 한 대 맞을 때마다 1도씩 상승
                Debug.Log($"<color=orange>[체온 측정]</color> {gameObject.name}의 현재 체온: <b>{currentTemperature}°C</b>");
            }
        }

        public float GetDamageMultiplier()
        {
            // 정상 체온(36.5) 기준, 1°C 상승할 때마다 받는 피해 15%씩 증가 (합연산)
            float degreesAboveNormal = currentTemperature - 36.5f;
            return 1.0f + (degreesAboveNormal * 0.15f); // 최대 41.5°C 일 때 데미지 1.75배(75% 증폭)
        }

        private void ResetTimer()
        {
            timer = duration;
        }
    }
}