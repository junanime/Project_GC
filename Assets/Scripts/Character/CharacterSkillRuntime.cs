using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class CharacterSkillRuntime : MonoBehaviour
    {
        public const int MaxSleepCrystals=12, SleepStacksPerCrystal=5;
        public const int MaxSleepStacks=MaxSleepCrystals*SleepStacksPerCrystal;
        Character owner;
        public const int MaxLevel = 5;
        public int PassiveMaxLevel => IsShini ? 3 : MaxLevel;
        public int ActiveMaxLevel => IsShini ? 3 : MaxLevel;
        public ShiniEmberRuntime Breath { get; private set; }
        public float StatusDamageBonus => IsHyuki ? HyukiBonus(PassiveLevel) : 0;
        public static float HyukiBonus(int level) => Mathf.Clamp(level,1,5)*.1f;
        public int PassiveLevel { get; private set; } = 1;
        public int ActiveLevel { get; private set; } = 1;
        public float PassivePower => Mathf.Pow(1.3f, PassiveLevel - 1);
        public float ActivePower => IsShini ? 1+.2f*(ActiveLevel-1) : IsAri ? 1 : Mathf.Pow(1.3f, ActiveLevel - 1);
        public float EffectivePassiveDuration => Definition.passiveDuration * (Definition.kind == CharacterSkillDefinition.SkillKind.Ashi ? PassivePower : 1);
        public float EffectiveActiveDuration => Definition.activeDuration + (IsAri ? 2*(ActiveLevel-1) : 0);
        public float EffectiveCooldown => IsShini ? 0 : Definition.kind==CharacterSkillDefinition.SkillKind.Ashi ? Mathf.Max(1,Definition.cooldown-4*(ActiveLevel-1)) : Definition.cooldown / (IsHyuki ? ActivePower : 1);
        public bool TryUpgrade(bool active)
        {
            if (Definition == null || (active ? ActiveLevel : PassiveLevel) >= (active ? ActiveMaxLevel : PassiveMaxLevel)) return false;
            if (active) ActiveLevel++; else PassiveLevel++;
            return true;
        }
        public void RestoreLevels(int passive, int active)
        { PassiveLevel = Mathf.Clamp(passive, 1, PassiveMaxLevel); ActiveLevel = Mathf.Clamp(active, 1, ActiveMaxLevel); }
        public string UpgradeDescription(bool active)
        {
            int current=active?ActiveLevel:PassiveLevel, max=active?ActiveMaxLevel:PassiveMaxLevel;
            int next=Mathf.Min(max,current+1);string label;float a,b;
            if(IsShini){label=active?"분사 피해(%)":"불씨 최대 보유량";a=active?100+20*(current-1):24+6*(current-1);b=active?100+20*(next-1):24+6*(next-1);}
            else if(IsHyuki&&!active){label="상태이상 대상 추가 피해(%)";a=HyukiBonus(current)*100;b=HyukiBonus(next)*100;}
            else if(IsHyuki){label="재사용 대기시간(초)";a=EffectiveCooldown;b=Definition.cooldown/Mathf.Pow(1.3f,next-1);}
            else if(IsAri&&active){label="변신 지속시간(초)";a=EffectiveActiveDuration;b=Definition.activeDuration+2*(next-1);}
            else if(IsAri){label="대쉬 접촉 피해";a=Definition.ariRollDamage*PassivePower;b=Definition.ariRollDamage*Mathf.Pow(1.3f,next-1);}
            else if(active){label="재사용 대기시간(초)";a=EffectiveCooldown;b=Definition.cooldown-4*(next-1);}
            else{label="효과 지속시간(초)";a=EffectivePassiveDuration;b=Definition.passiveDuration*Mathf.Pow(1.3f,next-1);}
            return $"{label}\n{a:0.##} → {b:0.##}";
        }

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
        public int IceProcFailures { get; private set; }
        public float IceProcChance => Mathf.Min(1f,IceSkillRules.InstantFreezeChance + IceProcFailures * .01f);
        // Only the instant-freeze roll owns this counter; four-hit/active freezes never reset it.
        public bool RollInstantFreeze(float roll)
        {
            bool success=roll<IceProcChance || IceProcChance>=1f;
            IceProcFailures=success?0:Mathf.Min(90,IceProcFailures+1);
            return success;
        }
        public void RestoreIceProcFailures(int failures) { IceProcFailures=Mathf.Clamp(failures,0,90); }
        public float MovementMultiplier => AshiActive ? 2 : IsShini && Active ? Mathf.Max(1,Definition.shiniActiveMoveMultiplier) : 1;
        public bool PassiveActive => PassiveRemaining > 0;
        public bool Active => IsShini ? Breath!=null&&Breath.Busy : ActiveRemaining > 0;
        public bool CanActivate => (!IsShini || Breath!=null&&Breath.CanStart) && Definition != null && owner.IsAlive && !owner.IsPortalTravelling && !owner.IsTrapBound && !owner.IsDashing && !IsCutin && !IsSummoning && CooldownRemaining <= 0 && Time.timeScale > 0 && (ApothecaryUI.Instance == null || ApothecaryUI.Instance.Page == "hud");
        float previousTimeScale = 1;
        SkillWindVisual aura;
        public void Initialize(Character character) { owner=character; aura=gameObject.AddComponent<SkillWindVisual>(); aura.Bind(this); if(IsShini){Breath=gameObject.AddComponent<ShiniEmberRuntime>();Breath.Bind(character,this);} gameObject.AddComponent<PhoenixSkillVisual>().Bind(character,this); if(IsAri)gameObject.AddComponent<AriSkillRuntime>().Bind(character,this); }
        public void DashFinished() { if (Definition != null && !IsHyuki && !IsShini && !IsAri && owner.IsAlive) PassiveRemaining=EffectivePassiveDuration; }
        public void DashStarted()
        {
            owner.GetComponent<PrescriptionRuntime>()?.RecordDash();
            if(IsShini)Breath?.Cancel();
            if(IsAri) GetComponent<AriSkillRuntime>()?.BeginDash();
        }
        public void DashStep(Vector2 from,Vector2 to) { if(IsAri)GetComponent<AriSkillRuntime>()?.Sweep(from,to); }
        public int ProjectileCount(int count) => Mathf.Max(1,count) * (AshiActive ? 2 : 1);
        public bool TryActivate()
        {
            if (!CanActivate) return false;
            if(IsShini)return Breath!=null&&Breath.Tap();
            owner.GetComponent<PrescriptionRuntime>()?.Record(PrescriptionRuntime.Goal.ActiveUses);
            if(IsAri)
            {
                ActiveRemaining=EffectiveActiveDuration;CooldownRemaining=EffectiveCooldown;
                owner.RefreshSkillDashRecharge(true);
                GetComponent<AriSkillRuntime>()?.BeginTransform();
                return true;
            }
            if(IsShini || IsHyuki)
            {
                ActiveRemaining=EffectiveActiveDuration; CooldownRemaining=EffectiveCooldown;
                SummonRemaining=IceSkillRules.BlizzardFreezeDelay;
                GetComponent<PhoenixSkillVisual>()?.Play();
                return true;
            }
            IsCutin=true; CutinElapsed=0; previousTimeScale=Time.timeScale; Time.timeScale=0;
            GetComponent<PhoenixSkillVisual>()?.Play();
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
                    ActiveRemaining=EffectiveActiveDuration; CooldownRemaining=EffectiveCooldown; owner.UpdateMoveSpeed();
                }
                return;
            }
            Tick(Time.deltaTime);
            if(!IsShini && GameInput.GetKeyDown(KeyCode.R)) TryActivate();
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
                if(IsHyuki && SummonRemaining<=0 && owner.IsAlive)
                {
                    var needle=FindObjectOfType<SyringeDartAbility>();
                    if(needle!=null)
                        foreach(var target in Ver4HitEffects.Nearby(transform.position,IceSkillRules.ActiveRadius,needle.SkillTargetLayer,owner))
                            if(!(target is Monster monster) || !monster.IsFieldRuntimeSuspended) IceSkillRules.Freeze(target);
                }
            }
            if(!Mathf.Approximately(oldMovement,MovementMultiplier))owner.UpdateMoveSpeed();
            if(ariWasActive&&!Active)owner.RefreshSkillDashRecharge(false);
        }
        public void Restore(float passive,float active,float cooldown,float summon=0)
        {
            if(Definition==null)return;
            PassiveRemaining=IsHyuki?0:Mathf.Clamp(passive,0,EffectivePassiveDuration);
            ActiveRemaining=Mathf.Clamp(active,0,EffectiveActiveDuration);
            CooldownRemaining=Mathf.Clamp(cooldown,0,EffectiveCooldown);
            float summonDuration=IsShini?ShiniSkillRuntime.SummonDuration:IsHyuki?IceSkillRules.BlizzardFreezeDelay:0;
            SummonRemaining=Mathf.Clamp(summon,0,summonDuration);
            if(IsSummoning)
            {
                GetComponent<PhoenixSkillVisual>()?.Play(summonDuration-SummonRemaining);
            }
            owner.UpdateMoveSpeed();
            if(IsAri){owner.RefreshSkillDashRecharge(false);GetComponent<AriSkillRuntime>()?.RestoreForm();}
        }
        public void RestoreSleep(float seconds,int consumed)
        {
            // Legacy save fields remain readable, but the retired sleep passive cannot reactivate.
            SleepSeconds=0;
            ConsumedSleepStacks=0;
            owner.UpdateMoveSpeed();
        }
        void Clear()
        {
            Breath?.Cancel();
            if(IsCutin){ IsCutin=false; Time.timeScale=previousTimeScale; }
            bool changed=Active || PassiveActive; PassiveRemaining=ActiveRemaining=CooldownRemaining=SleepSeconds=SummonRemaining=0; ConsumedSleepStacks=0; IceProcFailures=0;
            if(changed && owner!=null)owner.UpdateMoveSpeed();
        }
        void OnDisable(){Clear();}
    }
}
