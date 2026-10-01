using System;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public static class MerchantItemEffectUtility
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance;

        public static void ApplyToCharacter(
            MerchantItemBlueprint item,
            Character player,
            AbilityManager abilityManager = null,
            bool debugLog = true)
        {
            if (item == null)
            {
                return;
            }

            if (player == null)
            {
                Debug.LogWarning("[MerchantItemEffectUtility] Character가 없어 아이템 효과를 적용할 수 없습니다.");
                return;
            }

            if(item.octoberId>0){OctoberItemRuntime.Get(player).Give(item);return;}
            string itemName = GetString(item, item.name, "itemName", "displayName", "name");

            if (debugLog)
            {
                string rarity = GetStringFromAny(item, "등급 없음", "itemRarity", "rarity");
                string tag = GetStringFromAny(item, "태그 없음", "itemTag", "tag");

                Debug.Log($"[LobbyItem] Apply Start: {itemName} ({rarity} / {tag})");
            }

            ApplyBasicStats(item, player, debugLog);
            ApplyUtilityStats(item, player, debugLog);
            ApplySpecialStats(item, player, debugLog);
            ApplyAbilityReward(item, abilityManager, debugLog);

            if (debugLog)
            {
                Debug.Log($"[LobbyItem] Apply Complete: {itemName}");
            }
        }

        private static void ApplyBasicStats(MerchantItemBlueprint item, Character player, bool debugLog)
        {
            float atkSpeedBoost = GetFloat(item, "atkSpeedBoost", "attackSpeedBoost");
            if (atkSpeedBoost != 0f)
            {
                player.AddAttackSpeed(atkSpeedBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 공격속도 변화: {atkSpeedBoost}");
                }
            }

            float atkDamageBoost = GetFloat(item, "atkDamageBoost", "damageBoost", "damageMultiplier");
            if (atkDamageBoost != 0f)
            {
                player.AddDamageMultiplier(atkDamageBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 공격력 변화: {atkDamageBoost}");
                }
            }

            float maxHpBoost = GetFloat(item, "maxHpBoost", "maxHealthBoost", "hpBoost");
            if (maxHpBoost != 0f)
            {
                player.AddMaxHealthBonus(maxHpBoost);

                if (maxHpBoost > 0f)
                {
                    player.GainHealth(maxHpBoost);
                }

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 최대체력 변화: {maxHpBoost}");
                }
            }

            float projectileSpeedBoost = GetFloat(item, "projSpeedBoost", "projectileSpeedBoost");
            if (projectileSpeedBoost != 0f)
            {
                player.AddProjectileSpeed(projectileSpeedBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 투사체 속도 변화: {projectileSpeedBoost}");
                }
            }

            float moveSpeedBoost = GetFloat(item, "moveSpeedBoost", "movementSpeedBoost");
            if (moveSpeedBoost != 0f)
            {
                player.AddMoveSpeedBoost(moveSpeedBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 이동속도 변화: {moveSpeedBoost}");
                }
            }
        }

        private static void ApplyUtilityStats(MerchantItemBlueprint item, Character player, bool debugLog)
        {
            float magnetBoost = GetFloat(item, "magnetBoost", "magnetRangeBoost");
            if (magnetBoost != 0f)
            {
                player.AddMagnetRange(magnetBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 자석 범위 변화: {magnetBoost}");
                }
            }

            float expBoost = GetFloat(item, "expBoost", "experienceBoost", "expMultiplier");
            if (expBoost != 0f)
            {
                player.AddExpMultiplier(expBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 경험치 획득량 변화: {expBoost}");
                }
            }

            float critBoost = GetFloat(item, "critBoost", "critChanceBoost", "criticalChanceBoost");
            if (critBoost != 0f)
            {
                player.AddCritChance(critBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 치명타 확률 변화: {critBoost}");
                }
            }

            float luckBoost = GetFloat(item, "luckBoost", "luckMultiplierBoost");
            if (luckBoost != 0f)
            {
                player.AddLuck(luckBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 행운 변화: {luckBoost}");
                }
            }
        }

        private static void ApplySpecialStats(MerchantItemBlueprint item, Character player, bool debugLog)
        {
            int extraProjectiles = GetInt(item, "extraProjectiles", "projectileCountBoost", "additionalProjectiles");
            if (extraProjectiles != 0)
            {
                player.AddProjectileCount(extraProjectiles);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 투사체 수 변화: {extraProjectiles}");
                }
            }

            bool giveShield = GetBool(item, "giveShield", "hasShield");
            if (giveShield)
            {
                player.EnableShield();

                if (debugLog)
                {
                    Debug.Log("[LobbyItem] 보호막 활성화");
                }
            }

            int extraRevives = GetInt(item, "extraRevives", "reviveCount", "additionalRevives");
            if (extraRevives != 0)
            {
                player.AddReviveCount(extraRevives);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 부활 횟수 변화: {extraRevives}");
                }
            }

            float invincibilityBoost = GetFloat(item, "invincibilityBoost", "invincibilityTimeBoost");
            if (invincibilityBoost != 0f)
            {
                player.AddInvincibilityTime(invincibilityBoost);

                if (debugLog)
                {
                    Debug.Log($"[LobbyItem] 피격 무적 시간 변화: {invincibilityBoost}");
                }
            }
        }

        private static void ApplyAbilityReward(MerchantItemBlueprint item, AbilityManager abilityManager, bool debugLog)
        {
            GameObject abilityPrefab = GetObject<GameObject>(item, "abilityPrefab");

            if (abilityPrefab == null)
            {
                return;
            }

            if (debugLog)
            {
                Debug.Log($"[LobbyItem] Ability Prefab 연결됨: {abilityPrefab.name}");
            }

            // 현재는 시작 아이템 효과만 적용.
            // 나중에 로비에서 무기/능력 자체를 들고 가는 구조가 필요하면
            // AbilityManager 쪽 런타임 추가 API에 맞춰 이 부분을 확장하면 된다.
        }

        private static object GetFieldValue(MerchantItemBlueprint item, params string[] fieldNames)
        {
            if (item == null || fieldNames == null)
            {
                return null;
            }

            Type type = item.GetType();

            for (int i = 0; i < fieldNames.Length; i++)
            {
                string fieldName = fieldNames[i];

                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                FieldInfo field = type.GetField(fieldName, FieldFlags);

                if (field != null)
                {
                    return field.GetValue(item);
                }
            }

            return null;
        }

        private static string GetString(MerchantItemBlueprint item, string fallback, params string[] fieldNames)
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return fallback;
            }

            string text = value as string;

            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            return value.ToString();
        }

        private static string GetStringFromAny(MerchantItemBlueprint item, string fallback, params string[] fieldNames)
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return fallback;
            }

            return value.ToString();
        }

        private static float GetFloat(MerchantItemBlueprint item, params string[] fieldNames)
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value);
            }
            catch
            {
                return 0f;
            }
        }

        private static int GetInt(MerchantItemBlueprint item, params string[] fieldNames)
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static bool GetBool(MerchantItemBlueprint item, params string[] fieldNames)
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        private static T GetObject<T>(MerchantItemBlueprint item, params string[] fieldNames) where T : UnityEngine.Object
        {
            object value = GetFieldValue(item, fieldNames);

            if (value == null)
            {
                return null;
            }

            return value as T;
        }
    }
}