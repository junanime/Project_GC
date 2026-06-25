using System;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public struct SyringeSpecialRuntime
    {
        // Poison
        public bool poisonEnabled;
        public float poisonDuration;
        public float poisonTickInterval;
        public float poisonTickDamage;

        // Explosion
        public bool explosionEnabled;
        public float explosionRadius;
        public float explosionDamage;
        public float explosionChance;

        // Homing
        public bool homingEnabled;
        public float homingRange;
        public float homingLerpSpeed;

        // Pierce
        // pierceCount는 "추가로 관통 가능한 횟수"입니다.
        // 예: pierceCount = 2라면 첫 적중 후 추가로 2번 더 관통 가능.
        public bool pierceEnabled;
        public int pierceCount;

        // Honey Needle
        // 꿀침: 적중한 몬스터를 일정 시간 둔화시킨다.
        public bool honeyEnabled;
        public float honeyDuration;
        public float honeySlowMultiplier;

        // Mosquito Needle
        // 모기침: 적중 시 플레이어 HP를 회복한다.
        public bool mosquitoEnabled;
        public float mosquitoHealPerHit;
        public float mosquitoBossHealMultiplier;

        // Return Needle / 침귀환
        // 침이 적중한 뒤 플레이어에게 되돌아오며, 귀환 경로의 적에게 피해를 준다.
        public bool returnNeedleEnabled;
        public float returnNeedleSpeedMultiplier;
        public float returnNeedleDamageMultiplier;
        public float returnNeedleArriveDistance;
        public float returnNeedleMaxDuration;

        // Fiber Needle / 섬유침
        // 침이 지나간 자리에 얇은 섬유질 선을 남기고, 선을 밟은 적에게 지속 피해를 준다.
        public bool fiberEnabled;
        public float fiberTrailLifetime;
        public float fiberTrailDamagePerSecond;
        public float fiberTrailTickInterval;
        public float fiberTrailWidth;
        public float fiberTrailMinSegmentDistance;
        public Color fiberTrailColor;

        // Corrosion Needle / 부식침
        // 적의 방어 성분을 녹여 이후 받는 피해를 증가시킨다.
        public bool corrosionEnabled;
        public float corrosionDuration;
        public float corrosionDamageTakenBonusPerStack;
        public float corrosionBossDamageTakenBonusPerStack;
        public int corrosionMaxStacks;

        // Pressure Needle / 압력침
        // 침이 날아간 거리에 비례해 피해량이 증가한다.
        public bool pressureEnabled;
        public float pressureDamageBonusPerDistance;
        public float pressureMaxDamageBonus;

        // Mark Needle / 표식침
        // 첫 피격 시 표식을 남기고, 다음 피격 시 표식을 소모해 추가 피해를 준다.
        public bool markEnabled;
        public float markDuration;
        public float markBonusDamageMultiplier;

        // Healing Block
        // HP 1 전설 증강처럼 회복이 금지되는 상태일 때 true.
        // true이면 모기침 회복이 발동하지 않는다.
        public bool healingBlocked;

        // Legendary bonus
        public float rangeBonus;

        // slow item
        public float slowChance;

        // burn item
        public float burnChance;

        // 항생제 폭탄 확률 칸 생성
        public float explosionChanceFromItem;

        // 전자체온계
        public bool thermometerEnabled;

        // 구강청결제
        public int reflectCount;
    }
}