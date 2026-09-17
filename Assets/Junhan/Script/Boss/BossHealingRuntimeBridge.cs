using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public static class BossHealingRuntimeBridge
    {
        private static readonly Dictionary<int, int> InvincibleRequestCountByBossId = new Dictionary<int, int>();
        private static readonly Dictionary<int, float> MaxHpCacheByBossId = new Dictionary<int, float>();

        public static void AddExternalInvincibility(BossController bossController)
        {
            if (bossController == null)
            {
                return;
            }

            int id = bossController.GetInstanceID();

            if (!InvincibleRequestCountByBossId.ContainsKey(id))
            {
                InvincibleRequestCountByBossId[id] = 0;
            }

            InvincibleRequestCountByBossId[id]++;

            SetBoolField(bossController, "isInvincibleByPhase", true);
        }

        public static void RemoveExternalInvincibility(BossController bossController)
        {
            if (bossController == null)
            {
                return;
            }

            int id = bossController.GetInstanceID();

            if (!InvincibleRequestCountByBossId.ContainsKey(id))
            {
                return;
            }

            InvincibleRequestCountByBossId[id]--;

            if (InvincibleRequestCountByBossId[id] > 0)
            {
                return;
            }

            InvincibleRequestCountByBossId.Remove(id);

            if (!bossController.IsPhaseTransitioning)
            {
                SetBoolField(bossController, "isInvincibleByPhase", false);
            }
        }

        public static bool TryHealBoss(
            BossController bossController,
            float healAmount,
            out float beforeHp,
            out float afterHp,
            out float maxHp)
        {
            beforeHp = 0f;
            afterHp = 0f;
            maxHp = 0f;

            if (bossController == null || healAmount <= 0f)
            {
                return false;
            }

            var root = bossController.FiveCoreHealthRoot;
            if (root != null && root.UsesFiveCoreHealth)
            {
                beforeHp = root.CurrentBossHealth;
                maxHp = root.TotalMaxHealth;
                root.HealLivingCores(healAmount);
                afterHp = root.CurrentBossHealth;
                bossController.NotifyBossHealthChanged(afterHp, maxHp);
                return afterHp > beforeHp;
            }
            BossMonster bossMonster = FindBossMonster(bossController);

            if (bossMonster == null)
            {
                return false;
            }

            int bossId = bossMonster.GetInstanceID();

            float monsterCurrentHp = GetFloatField(bossMonster, "currentHealth", 0f);
            float controllerCurrentHp = GetFloatField(bossController, "currentHp", monsterCurrentHp);
            float controllerMaxHp = GetFloatField(bossController, "maxHp", 0f);
            float bossMonsterMaxHp = GetFloatField(bossMonster, "bossMaxHealth", controllerMaxHp);

            beforeHp = Mathf.Max(monsterCurrentHp, controllerCurrentHp);

            if (!MaxHpCacheByBossId.ContainsKey(bossId))
            {
                float initialMax = Mathf.Max(controllerMaxHp, bossMonsterMaxHp, beforeHp, 1f);
                MaxHpCacheByBossId[bossId] = initialMax;
            }

            maxHp = Mathf.Max(1f, MaxHpCacheByBossId[bossId]);

            if (controllerMaxHp > maxHp)
            {
                maxHp = controllerMaxHp;
                MaxHpCacheByBossId[bossId] = maxHp;
            }

            afterHp = Mathf.Clamp(beforeHp + healAmount, 0f, maxHp);

            SetFloatField(bossMonster, "currentHealth", afterHp);
            SetFloatField(bossController, "currentHp", afterHp);
            SetFloatField(bossController, "maxHp", maxHp);
            SetFloatField(bossMonster, "bossMaxHealth", maxHp);

            bossController.NotifyBossHealthChanged(afterHp, maxHp);

            return true;
        }

        public static void ClearBossCache(BossController bossController)
        {
            if (bossController == null)
            {
                return;
            }

            BossMonster bossMonster = FindBossMonster(bossController);

            if (bossMonster != null)
            {
                MaxHpCacheByBossId.Remove(bossMonster.GetInstanceID());
            }

            InvincibleRequestCountByBossId.Remove(bossController.GetInstanceID());
        }

        private static BossMonster FindBossMonster(BossController bossController)
        {
            if (bossController == null)
            {
                return null;
            }

            BossMonster bossMonster = bossController.GetComponent<BossMonster>();

            if (bossMonster == null)
            {
                bossMonster = bossController.GetComponentInChildren<BossMonster>(true);
            }

            if (bossMonster == null)
            {
                bossMonster = bossController.GetComponentInParent<BossMonster>();
            }

            return bossMonster;
        }

        private static bool SetBoolField(object target, string fieldName, bool value)
        {
            FieldInfo field = FindField(target, fieldName);

            if (field == null || field.FieldType != typeof(bool))
            {
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        private static bool SetFloatField(object target, string fieldName, float value)
        {
            FieldInfo field = FindField(target, fieldName);

            if (field == null || field.FieldType != typeof(float))
            {
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        private static float GetFloatField(object target, string fieldName, float fallback)
        {
            FieldInfo field = FindField(target, fieldName);

            if (field == null || field.FieldType != typeof(float))
            {
                return fallback;
            }

            return (float)field.GetValue(target);
        }

        private static FieldInfo FindField(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            return FindField(target.GetType(), fieldName);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            System.Type currentType = type;

            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(fieldName, flags);

                if (field != null)
                {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}