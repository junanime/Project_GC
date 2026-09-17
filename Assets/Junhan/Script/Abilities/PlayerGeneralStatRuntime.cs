using UnityEngine;

namespace Vampire
{
    // 일반 증강에서 기존 Character에 직접 없는 수치들을 보관하는 런타임 스탯 주머니.
    // 기존 Character.cs를 크게 수정하지 않기 위해 별도 컴포넌트로 관리한다.
    [DisallowMultipleComponent]
    public class PlayerGeneralStatRuntime : MonoBehaviour
    {
        [Header("General Runtime Stats")]
        [SerializeField] private float pickupRangeMultiplier = 1f;
        [SerializeField] private float goldGainMultiplier = 1f;
        [SerializeField] private float goldDropChanceBonus = 0f;
        [SerializeField] private float bossDamageMultiplier = 1f;

        [Header("Combat Runtime Stats")]
        [Tooltip("기본 치명타 피해 배율입니다. 1.5 = 150%")]
        [SerializeField] private float critDamageMultiplier = 1.5f;

        [SerializeField] private float knockbackMultiplier = 1f;
        [SerializeField] private float defensePierceBonus = 0f;
        [SerializeField] private float damageReductionBonus = 0f;
        [SerializeField] private float evasionBonus = 0f;

        [Header("Status Runtime Stats")]
        [SerializeField] private float statusDurationMultiplier = 1f;
        [SerializeField] private float statusDamageMultiplier = 1f;

        [Header("Clone Runtime Stats")]
        [SerializeField] private float cloneDamageMultiplier = 1f;
        [SerializeField] private float cloneAttackSpeedMultiplier = 1f;
        [SerializeField] private int extraCloneCount = 0;

        [Header("Debug")]
        [SerializeField] private bool debugCriticalLog = false;

        private Character ownerCharacter;
        private Collider2D pickupCollider;
        private CircleCollider2D pickupCircleCollider;
        private BoxCollider2D pickupBoxCollider;
        private float baseCircleRadius;
        private Vector2 baseBoxSize;
        private float goldGainRemainder;
        private bool pickupColliderCached = false;

        public float PickupRangeMultiplier => pickupRangeMultiplier;
        public float GoldGainMultiplier => goldGainMultiplier;
        public float GoldDropChanceBonus => goldDropChanceBonus;
        public float BossDamageMultiplier => bossDamageMultiplier;

        public float CritDamageMultiplier => critDamageMultiplier;
        public float KnockbackMultiplier => knockbackMultiplier;
        public float DefensePierceBonus => defensePierceBonus;
        public float DamageReductionBonus => damageReductionBonus;
        public float EvasionBonus => evasionBonus;

        public float StatusDurationMultiplier => statusDurationMultiplier;
        public float StatusDamageMultiplier => statusDamageMultiplier;

        public float CloneDamageMultiplier => cloneDamageMultiplier;
        public float CloneAttackSpeedMultiplier => cloneAttackSpeedMultiplier;
        public int ExtraCloneCount => extraCloneCount;

        public static PlayerGeneralStatRuntime GetOrCreate(Character character)
        {
            if (character == null)
            {
                return null;
            }

            PlayerGeneralStatRuntime runtime = character.GetComponent<PlayerGeneralStatRuntime>();

            if (runtime == null)
            {
                runtime = character.gameObject.AddComponent<PlayerGeneralStatRuntime>();
            }

            runtime.Init(character);
            return runtime;
        }

        public void Init(Character character)
        {
            ownerCharacter = character;
            CachePickupColliderIfNeeded();
        }

        public void AddPickupRangeMultiplier(float amount)
        {
            pickupRangeMultiplier += amount;
            pickupRangeMultiplier = Mathf.Max(0.1f, pickupRangeMultiplier);

            SyncPickupCollider();
        }

        public void AddGoldGainMultiplier(float amount)
        {
            goldGainMultiplier += amount;
            goldGainMultiplier = Mathf.Max(0f, goldGainMultiplier);
        }

        public int ApplyGoldGainMultiplier(int baseAmount)
        {
            if (baseAmount <= 0)
            {
                return 0;
            }

            float scaledAmount = baseAmount * goldGainMultiplier + goldGainRemainder;
            int wholeAmount = Mathf.FloorToInt(scaledAmount + 0.0001f);
            goldGainRemainder = Mathf.Max(0f, scaledAmount - wholeAmount);
            return wholeAmount;
        }

        public void AddGoldDropChance(float amount)
        {
            goldDropChanceBonus += amount;
            goldDropChanceBonus = Mathf.Clamp(goldDropChanceBonus, 0f, 0.95f);
        }

        public void AddBossDamageMultiplier(float amount)
        {
            bossDamageMultiplier += amount;
            bossDamageMultiplier = Mathf.Max(0f, bossDamageMultiplier);
        }

        public void AddCritDamageMultiplier(float amount)
        {
            critDamageMultiplier += amount;
            critDamageMultiplier = Mathf.Max(1f, critDamageMultiplier);
        }

        public void AddKnockbackMultiplier(float amount)
        {
            knockbackMultiplier += amount;
            knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
        }

        public void AddDefensePierce(float amount)
        {
            defensePierceBonus += amount;
            defensePierceBonus = Mathf.Max(0f, defensePierceBonus);
        }

        public void AddDamageReduction(float amount)
        {
            damageReductionBonus += amount;
            damageReductionBonus = Mathf.Clamp(damageReductionBonus, 0f, 0.8f);
        }

        public void AddEvasion(float amount)
        {
            evasionBonus += amount;
            evasionBonus = Mathf.Clamp(evasionBonus, 0f, 0.8f);
        }

        public void AddStatusDurationMultiplier(float amount)
        {
            statusDurationMultiplier += amount;
            statusDurationMultiplier = Mathf.Max(0f, statusDurationMultiplier);
        }

        public void AddStatusDamageMultiplier(float amount)
        {
            statusDamageMultiplier += amount;
            statusDamageMultiplier = Mathf.Max(0f, statusDamageMultiplier);
        }

        public void AddCloneDamageMultiplier(float amount)
        {
            cloneDamageMultiplier += amount;
            cloneDamageMultiplier = Mathf.Max(0f, cloneDamageMultiplier);
        }

        public void AddCloneAttackSpeedMultiplier(float amount)
        {
            cloneAttackSpeedMultiplier += amount;
            cloneAttackSpeedMultiplier = Mathf.Max(0.1f, cloneAttackSpeedMultiplier);
        }

        public void AddExtraCloneCount(int amount)
        {
            extraCloneCount += amount;
            extraCloneCount = Mathf.Max(0, extraCloneCount);
        }

        public float CalculateOffensiveDamage(
            Character sourceCharacter,
            Component targetComponent,
            float baseDamage,
            out bool isCritical)
        {
            isCritical = false;

            float finalDamage = baseDamage;

            if (targetComponent != null && bossDamageMultiplier > 1f && IsBossLikeTarget(targetComponent))
            {
                finalDamage *= bossDamageMultiplier;
            }

            float critChance = sourceCharacter != null
                ? Mathf.Clamp01(sourceCharacter.CritChance)
                : 0f;

            if (Random.value < critChance)
            {
                finalDamage *= critDamageMultiplier;
                isCritical = true;

                if (debugCriticalLog)
                {
                    Debug.Log(
                        $"[치명타] 발생 | 확률 {critChance * 100f:0.##}% | 배율 {critDamageMultiplier * 100f:0.##}% | 피해 {finalDamage:0.##}",
                        this
                    );
                }
            }

            return finalDamage;
        }

        public float ApplyBossDamageBonus(Component targetComponent, float baseDamage)
        {
            if (targetComponent == null)
            {
                return baseDamage;
            }

            if (bossDamageMultiplier <= 1f)
            {
                return baseDamage;
            }

            if (IsBossLikeTarget(targetComponent))
            {
                return baseDamage * bossDamageMultiplier;
            }

            return baseDamage;
        }

        public static bool IsBossLikeTarget(Component targetComponent)
        {
            if (targetComponent == null)
            {
                return false;
            }

            Component[] components = targetComponent.GetComponentsInParent<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;

                if (typeName.Contains("Boss"))
                {
                    return true;
                }
            }

            string objectName = targetComponent.gameObject.name;

            return objectName.Contains("Boss") || objectName.Contains("보스");
        }

        private void CachePickupColliderIfNeeded()
        {
            if (pickupColliderCached)
            {
                return;
            }

            if (ownerCharacter == null)
            {
                return;
            }

            pickupCollider = ownerCharacter.CollectableCollider;

            if (pickupCollider == null)
            {
                return;
            }

            pickupCircleCollider = pickupCollider as CircleCollider2D;
            pickupBoxCollider = pickupCollider as BoxCollider2D;

            if (pickupCircleCollider != null)
            {
                baseCircleRadius = pickupCircleCollider.radius;
            }

            if (pickupBoxCollider != null)
            {
                baseBoxSize = pickupBoxCollider.size;
            }

            pickupColliderCached = true;
        }

        private void SyncPickupCollider()
        {
            CachePickupColliderIfNeeded();

            if (pickupCollider == null)
            {
                return;
            }

            if (pickupCircleCollider != null)
            {
                pickupCircleCollider.radius = baseCircleRadius * pickupRangeMultiplier;
                return;
            }

            if (pickupBoxCollider != null)
            {
                pickupBoxCollider.size = baseBoxSize * pickupRangeMultiplier;
            }
        }
    }
}
