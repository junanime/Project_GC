using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using E = Vampire.RelicBlueprint.RelicEffectType;

namespace Vampire
{
    [Serializable] public sealed class RelicRunState
    {
        public string id;
        public bool revived;
        public float barrier, rechargeDelay, idleTime, reflectCooldown;
    }

    [DisallowMultipleComponent]
    public sealed class RelicRuntime : MonoBehaviour
    {
        public Character Player { get; private set; }
        public RelicBlueprint Equipped { get; private set; }
        public RelicRunState State { get; private set; } = new RelicRunState();
        public float Bonus(E effect) => Equipped != null && Equipped.effectType == effect ? Mathf.Max(0, Equipped.effectValue) : 0;
        public float MaxHealthBonus => Bonus(E.MaxHealth) + (Player.Blueprint != null ? Player.Blueprint.hp * Bonus(E.MaxHealthPercent) : 0);
        public float MoveSpeedBonus => Bonus(E.MoveSpeed) + (Player.Blueprint != null ? Player.Blueprint.movespeed * Bonus(E.MoveSpeedPercent) : 0);
        public int Revives => Bonus(E.Revival) > 0 && !State.revived ? 1 : 0;
        public static RelicRuntime Ensure(Character player)
        {
            var runtime = player.GetComponent<RelicRuntime>();
            if (runtime != null) return runtime;
            runtime = player.gameObject.AddComponent<RelicRuntime>();
            runtime.Player = player;
            var catalog = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            string id = RelicSaveData.GetEquippedRelicId();
            runtime.Equip(catalog != null && RelicSaveData.IsUnlocked(id) ? catalog.relics.FirstOrDefault(r => r != null && r.relicId == id) : null);
            return runtime;
        }
        public void Equip(RelicBlueprint relic)
        {
            if (Player == null) Player = GetComponent<Character>();
            Equipped = relic;
            State = new RelicRunState { id = relic != null ? relic.relicId : "", barrier = Bonus(E.Barrier) };
        }
        public RelicRunState Capture() => JsonUtility.FromJson<RelicRunState>(JsonUtility.ToJson(State));
        public void Restore(RelicRunState state)
        {
            if (state == null) return;
            var catalog = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Equipped = catalog?.relics.FirstOrDefault(r => r != null && r.relicId == state.id);
            State = JsonUtility.FromJson<RelicRunState>(JsonUtility.ToJson(state));
            State.barrier = Mathf.Clamp(State.barrier, 0, Bonus(E.Barrier));
            PlayerGeneralStatRuntime.GetOrCreate(Player).SyncPickupCollider();
        }
        public bool ConsumeRevival()
        { if (Revives == 0) return false; State.revived = true; return true; }
        public float Absorb(float damage)
        {
            if (damage <= 0) return damage;
            State.rechargeDelay = 8;
            float absorbed = Mathf.Min(State.barrier, damage);
            State.barrier -= absorbed;
            return damage - absorbed;
        }
        public float PickupHealing(float amount) => amount * (1 + Bonus(E.HealingPickup));
        public float DebuffDuration(float seconds) => seconds * (1 - Mathf.Clamp(Bonus(E.DebuffResistance), 0, .75f));
        public int ShopPrice(int original) => original <= 0 ? 0 : Mathf.Max(1, Mathf.CeilToInt(original * (1 - Mathf.Clamp(Bonus(E.ShopDiscount), 0, .5f))));
        public static int Price(Character player, int original) => player != null && player.Relics != null ? player.Relics.ShopPrice(original) : original;
        public void Hurt(float actual)
        {
            if (actual <= 0 || Bonus(E.Thorns) <= 0 || State.reflectCooldown > 0) return;
            State.reflectCooldown = .5f;
            var seen = new HashSet<Monster>();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, 2.5f))
            {
                var monster = hit.GetComponentInParent<Monster>();
                if (monster == null || !monster.gameObject.activeInHierarchy || !seen.Add(monster)) continue;
                monster.TakeItemPureDamage(Mathf.Min(50, actual * Bonus(E.Thorns)));
            }
        }
        void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (Player == null || !Player.IsAlive || dt <= 0) return;
            State.reflectCooldown = Mathf.Max(0, State.reflectCooldown - dt);
            float rechargeTime = Mathf.Max(0, dt - State.rechargeDelay);
            State.rechargeDelay = Mathf.Max(0, State.rechargeDelay - dt);
            State.barrier = Mathf.Min(Bonus(E.Barrier), State.barrier + 5 * rechargeTime);
            bool idle = Player.IsSkillIdle && Player.Velocity.sqrMagnitude < .01f &&
                !(Player.GetComponent<PlayerMiniStageStunRuntime>()?.IsStunned ?? false);
            float idleRecoveryTime = idle ? Mathf.Clamp(State.idleTime + dt - 1.5f, 0, dt) : 0;
            State.idleTime = idle ? State.idleTime + dt : 0;
            float healing = Bonus(E.Regeneration) * dt + Player.MaxHealth * Bonus(E.IdleRecovery) * idleRecoveryTime;
            if (healing > 0 && Player.CurrentHealth < Player.MaxHealth) Player.GainHealth(healing);
        }
    }
}
