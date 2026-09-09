using System;
using System.Collections.Generic;

namespace Vampire
{
    [Serializable]
    public sealed class RunSceneTransferSnapshot
    {
        public string SourceSceneName;
        public string TargetSceneName;

        public int SourceSceneHandle;
        public int SourceCharacterInstanceId;

        public RunSceneCharacterSnapshot CharacterState;

        public List<RunSceneAbilitySnapshot> AbilityStates =
            new List<RunSceneAbilitySnapshot>();

        public int CoinsGained;
    }

    [Serializable]
    public sealed class RunSceneCharacterSnapshot
    {
        // ------------------------------------------------------------
        // Level / EXP / HP
        // ------------------------------------------------------------

        public int CurrentLevel;

        public float CurrentExp;
        public float NextLevelExp;
        public float ExpToNextLevel;

        public float CurrentHealth;

        // ------------------------------------------------------------
        // Base Runtime Stats
        // ------------------------------------------------------------

        public float MovementSpeed;
        public float Armor;

        public float RangeMultiplier;
        public float AttackSpeedMultiplier;
        public float DamageMultiplier;
        public float MaxHealthBonus;
        public float ProjectileSpeedMultiplier;

        // ------------------------------------------------------------
        // Utility Stats
        // ------------------------------------------------------------

        public float MagnetRangeBonus;
        public float ExpMultiplier;
        public float CritChance;
        public float LuckMultiplier;

        public float LifeSteal;
        public float HealOnKill;
        public float ProjectileSizeMultiplier;

        // ------------------------------------------------------------
        // Combat / Utility State
        // ------------------------------------------------------------

        public int AdditionalProjectiles;

        public bool HasShield;
        public int ReviveCount;

        public float InvincibilityTimeBonus;

        public bool HasAntibioticBomb;

        public float HealOnIdlePerSecond;

        public bool AutoCollectItems;

        public float SlowChance;
        public int AdditionalPierce;
        public float BurnChance;

        // ------------------------------------------------------------
        // Rare Item State
        // ------------------------------------------------------------

        public bool HasGinsengStick;

        public float AntibioticBombChance;

        public bool HasThermometer;

        public int MouthwashCount;
        public int ReflexHammerCount;

        // ------------------------------------------------------------
        // Dash State
        // ------------------------------------------------------------

        public float DashDistance;
        public float DashRechargeTime;

        public int MaxDashCharges;
        public int CurrentDashCharges;

        // ------------------------------------------------------------
        // Runtime Stack
        // ------------------------------------------------------------

        public int ThermometerStacks;
    }

    [Serializable]
    public sealed class RunSceneAbilitySnapshot
    {
        public string AbilityTypeName;
        public string AbilityObjectName;

        public int Level;
    }

    public static class CrossSceneData
    {
        public static CharacterBlueprint CharacterBlueprint
        {
            get;
            set;
        }

        public static MerchantItemBlueprint[] StartingLobbyItems
        {
            get;
            set;
        } = new MerchantItemBlueprint[0];

        /// <summary>
        /// 실제 씬 전환 테스트 중 다음 씬으로 넘길 현재 런 상태입니다.
        ///
        /// SceneManager.LoadScene으로 씬이 교체되어도
        /// static 데이터는 같은 Play Session 안에서는 유지됩니다.
        ///
        /// 플레이어 GameObject 자체를 보존하지 않고
        /// 값만 전달하기 위해 사용합니다.
        /// </summary>
        public static RunSceneTransferSnapshot PendingRunSceneTransfer
        {
            get;
            set;
        }

        public static bool HasPendingRunSceneTransfer =>
            PendingRunSceneTransfer != null;

        public static void ClearStartingLobbyItems()
        {
            StartingLobbyItems =
                new MerchantItemBlueprint[0];
        }

        public static void ClearPendingRunSceneTransfer()
        {
            PendingRunSceneTransfer = null;
        }
    }
}