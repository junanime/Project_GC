using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class CharacterSkillRuntime : MonoBehaviour
    {
        public const int MaxSleepCrystals=12, SleepStacksPerCrystal=5;
        public const int MaxSleepStacks=MaxSleepCrystals*SleepStacksPerCrystal;
        Character owner;
        public CharacterSkillDefinition Definition => owner != null && owner.Blueprint != null ? owner.Blueprint.skills : null;
        public float PassiveRemaining { get; private set; }
        public float ActiveRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CutinElapsed { get; private set; }
        public float SummonRemaining { get; private set; }
        public bool IsSummoning => SummonRemaining > 0;
        public bool IsCutin { get; private set; }
        public bool IsShini => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Shini;
        public bool IsHyuki => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Hyuki;
        public bool IsAri => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Ari;
        public bool AshiActive => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Ashi && Active;
        public bool AshiPassive => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Ashi && PassiveActive;
        public float SleepSeconds { get; private set; }
        public int SleepStacks => Mathf.FloorToInt(SleepSeconds);
        public int ConsumedSleepStacks { get; private set; }
        public float MovementMultiplier => AshiActive ? 2 : IsShini && Active ? Mathf.Max(1,Definition.shiniActiveMoveMultiplier) : IsHyuki && PassiveActive ? 1 + ConsumedSleepStacks * .08f : 1;
        public bool PassiveActive => PassiveRemaining > 0;
        public bool Active => ActiveRemaining > 0;
        public bool CanActivate => Definition != null && owner.IsAlive && !owner.IsTrapBound && !owner.IsDashing && !IsCutin && !IsSummoning && CooldownRemaining <= 0 && Time.timeScale > 0 && (ApothecaryUI.Instance == null || ApothecaryUI.Instance.Page == "hud");
        float previousTimeScale = 1;
        SkillWindVisual aura;
        public void Initialize(Character character) { owner=character; aura=gameObject.AddComponent<SkillWindVisual>(); aura.Bind(this); gameObject.AddComponent<ShiniSkillRuntime>().Bind(character,this); gameObject.AddComponent<PhoenixSkillVisual>().Bind(character,this); if(IsAri)gameObject.AddComponent<AriSkillRuntime>().Bind(character,this); }
        public void DashFinished() { if (Definition != null && !IsHyuki && !IsShini && !IsAri && owner.IsAlive) PassiveRemaining=Definition.passiveDuration; }
        public void DashStarted()
        {
            if(IsShini && owner.IsAlive && owner.IsDashing && !owner.IsTrapBound)
                GetComponent<ShiniSkillRuntime>()?.ActivatePools();
            if(IsAri) GetComponent<AriSkillRuntime>()?.BeginDash();
        }
        public void DashStep(Vector2 from,Vector2 to) { if(IsAri)GetComponent<AriSkillRuntime>()?.Sweep(from,to); }
        public int ProjectileCount(int count) => Mathf.Max(1,count) * (AshiActive ? 2 : 1);
        public bool TryActivate()
        {
            if (!CanActivate) return false;
            if(IsAri)
            {
                ActiveRemaining=Definition.activeDuration;CooldownRemaining=Definition.cooldown;
                owner.RefreshSkillDashRecharge(true);
                GetComponent<AriSkillRuntime>()?.BeginTransform();
                return true;
            }
            if(IsShini || IsHyuki)
            {
                if(IsShini)
                {
                    SummonRemaining=ShiniSkillRuntime.SummonDuration;
                    ActiveRemaining=Definition.activeDuration; CooldownRemaining=Definition.cooldown;
                    owner.UpdateMoveSpeed();
                    GetComponent<ShiniSkillRuntime>()?.ActivatePools();
                    GetComponent<PhoenixSkillVisual>()?.Play();
                    return true;
                }
                ActiveRemaining=Definition.activeDuration; CooldownRemaining=Definition.cooldown;
                GetComponent<PhoenixSkillVisual>()?.Play();
                if(IsHyuki)
                {
                    var needle=FindObjectOfType<SyringeDartAbility>();
                    if(needle!=null) foreach(var target in Ver4HitEffects.Nearby(transform.position,IceSkillRules.ActiveRadius,needle.SkillTargetLayer,owner)) IceSkillRules.Freeze(target);
                }
                return true;
            }
            IsCutin=true; CutinElapsed=0; previousTimeScale=Time.timeScale; Time.timeScale=0;
            return true;
        }
        void Update()
        {
            if (Definition==null) return;
            if (!owner.IsAlive) { Clear(); return; }
            if (IsCutin)
            {
                if (!Application.isFocused && GamePreferences.Current.pauseOnFocusLoss) return;
                CutinElapsed+=Time.unscaledDeltaTime;
                if (CutinElapsed>=Definition.cutinDuration)
                {
                    IsCutin=false; Time.timeScale=previousTimeScale;
                    ActiveRemaining=Definition.activeDuration; CooldownRemaining=Definition.cooldown; owner.UpdateMoveSpeed();
                }
                return;
            }
            Tick(Time.deltaTime);
            if(GameInput.GetKeyDown(KeyCode.R)) TryActivate();
        }
        public void Tick(float delta)
        {
            bool ariWasActive=IsAri&&Active;
            float oldMovement=MovementMultiplier;
            PassiveRemaining=Mathf.Max(0,PassiveRemaining-delta);
            ActiveRemaining=Mathf.Max(0,ActiveRemaining-delta);
            CooldownRemaining=Mathf.Max(0,CooldownRemaining-delta);
            if(IsSummoning)
            {
                SummonRemaining=Mathf.Max(0,SummonRemaining-delta);
            }
            if(IsHyuki && delta>0)
            {
                if(owner.IsSkillIdle) SleepSeconds=Mathf.Min(MaxSleepStacks,SleepSeconds+delta);
                else if(SleepSeconds>0)
                {
                    int stacks=SleepStacks; SleepSeconds=0;
                    if(stacks>0) { ConsumedSleepStacks=stacks; PassiveRemaining=Definition.passiveDuration; }
                }
            }
            if(!Mathf.Approximately(oldMovement,MovementMultiplier))owner.UpdateMoveSpeed();
            if(ariWasActive&&!Active)owner.RefreshSkillDashRecharge(false);
        }
        public void Restore(float passive,float active,float cooldown,float summon=0)
        {
            if(Definition==null)return;
            PassiveRemaining=Mathf.Clamp(passive,0,Definition.passiveDuration);
            ActiveRemaining=Mathf.Clamp(active,0,Definition.activeDuration);
            CooldownRemaining=Mathf.Clamp(cooldown,0,Definition.cooldown);
            SummonRemaining=IsShini?Mathf.Clamp(summon,0,ShiniSkillRuntime.SummonDuration):0;
            if(IsSummoning)
            {
                GetComponent<PhoenixSkillVisual>()?.Play(ShiniSkillRuntime.SummonDuration-SummonRemaining);
            }
            owner.UpdateMoveSpeed();
            if(IsAri){owner.RefreshSkillDashRecharge(false);GetComponent<AriSkillRuntime>()?.RestoreForm();}
        }
        public void RestoreSleep(float seconds,int consumed)
        {
            SleepSeconds=IsHyuki?Mathf.Clamp(seconds,0,MaxSleepStacks):0;
            ConsumedSleepStacks=IsHyuki?Mathf.Clamp(consumed,0,MaxSleepStacks):0;
            owner.UpdateMoveSpeed();
        }
        void Clear()
        {
            if(IsCutin){ IsCutin=false; Time.timeScale=previousTimeScale; }
            bool changed=Active || PassiveActive; PassiveRemaining=ActiveRemaining=CooldownRemaining=SleepSeconds=SummonRemaining=0; ConsumedSleepStacks=0;
            if(changed && owner!=null)owner.UpdateMoveSpeed();
        }
        void OnDisable(){Clear();}
    }
}
