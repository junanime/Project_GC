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
        public bool honeyEnabled;
        public float honeyDuration;
        public float honeySlowMultiplier;

        // Mosquito Needle
        public bool mosquitoEnabled;
        public float mosquitoHealPerHit;
        public float mosquitoBossHealMultiplier;

        // Return Needle / 침귀환
        public bool returnNeedleEnabled;
        public float returnNeedleSpeedMultiplier;
        public float returnNeedleDamageMultiplier;
        public float returnNeedleArriveDistance;
        public float returnNeedleMaxDuration;

        // Fiber Needle / 섬유침
        public bool fiberEnabled;
        public float fiberTrailLifetime;
        public float fiberTrailDamagePerSecond;
        public float fiberTrailTickInterval;
        public float fiberTrailWidth;
        public float fiberTrailMinSegmentDistance;
        public Color fiberTrailColor;

        // Corrosion Needle / 부식침
        public bool corrosionEnabled;
        public float corrosionDuration;
        public float corrosionDamageTakenBonusPerStack;
        public float corrosionBossDamageTakenBonusPerStack;
        public int corrosionMaxStacks;

        // Pressure Needle / 압력침
        public bool pressureEnabled;
        public float pressureDamageBonusPerDistance;
        public float pressureMaxDamageBonus;

        // Mark Needle / 표식침
        public bool markEnabled;
        public float markDuration;
        public float markBonusDamageMultiplier;

        // Digestive Acid Sac Needle / 소화액낭침
        // 적중한 적이 사망할 때 소화액 웅덩이를 생성한다.
        public bool digestiveAcidSacEnabled;
        public float digestiveAcidPuddleLifetime;
        public float digestiveAcidPuddleRadius;
        public float digestiveAcidPuddleDamagePerSecond;
        public float digestiveAcidPuddleTickInterval;
        public Color digestiveAcidPuddleColor;

        // Hunger Needle / 공복침
        // 침 적중 시 플레이어 공격속도 임시 스택을 쌓는다.
        public bool hungerNeedleEnabled;
        public float hungerStackDuration;
        public float hungerAttackSpeedBonusPerStack;
        public int hungerMaxStacks;
        public bool debugHungerNeedle;

        // Gut Bacteria Needle / 장내균침
        // 적중한 적에게 장내균 스택을 쌓고, 일정 스택 이상에서 사망하면 추가 경험치를 생성한다.
        public bool gutBacteriaEnabled;
        public float gutBacteriaStackDuration;
        public int gutBacteriaRequiredStacks;
        public int gutBacteriaMaxStacks;
        public int gutBacteriaBonusGemCount;
        public GemType gutBacteriaBonusGemType;
        public float gutBacteriaBonusGemSpawnRadius;
        public bool debugGutBacteria;

        // Healing Block
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