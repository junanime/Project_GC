using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class CharacterSkillRuntime : MonoBehaviour
    {
        Character owner;
        public CharacterSkillDefinition Definition => owner != null && owner.Blueprint != null ? owner.Blueprint.skills : null;
        public float PassiveRemaining { get; private set; }
        public float ActiveRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CutinElapsed { get; private set; }
        public bool IsCutin { get; private set; }
        public bool IsShini => Definition != null && Definition.kind == CharacterSkillDefinition.SkillKind.Shini;
        public bool AshiActive => !IsShini && Active;
        public bool AshiPassive => !IsShini && PassiveActive;
        public bool PassiveActive => PassiveRemaining > 0;
        public bool Active => ActiveRemaining > 0;
        public bool CanActivate => Definition != null && owner.IsAlive && !owner.IsTrapBound && !owner.IsDashing && !IsCutin && CooldownRemaining <= 0 && Time.timeScale > 0 && (ApothecaryUI.Instance == null || ApothecaryUI.Instance.Page == "hud");
        float previousTimeScale = 1;
        SkillWindVisual aura;
        public void Initialize(Character character) { owner=character; aura=gameObject.AddComponent<SkillWindVisual>(); aura.Bind(this); gameObject.AddComponent<ShiniSkillRuntime>().Bind(character,this); }
        public void DashFinished() { if (Definition != null && owner.IsAlive) PassiveRemaining=Definition.passiveDuration; }
        public int ProjectileCount(int count) => Mathf.Max(1,count) * (AshiActive ? 2 : 1);
        public bool TryActivate()
        {
            if (!CanActivate) return false;
            if(IsShini) { ActiveRemaining=Definition.activeDuration; CooldownRemaining=Definition.cooldown; return true; }
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
            bool wasActive=Active;
            PassiveRemaining=Mathf.Max(0,PassiveRemaining-delta);
            ActiveRemaining=Mathf.Max(0,ActiveRemaining-delta);
            CooldownRemaining=Mathf.Max(0,CooldownRemaining-delta);
            if(wasActive!=Active)owner.UpdateMoveSpeed();
        }
        public void Restore(float passive,float active,float cooldown)
        {
            if(Definition==null)return;
            PassiveRemaining=Mathf.Clamp(passive,0,Definition.passiveDuration);
            ActiveRemaining=Mathf.Clamp(active,0,Definition.activeDuration);
            CooldownRemaining=Mathf.Clamp(cooldown,0,Definition.cooldown);
            owner.UpdateMoveSpeed();
        }
        void Clear()
        {
            if(IsCutin){ IsCutin=false; Time.timeScale=previousTimeScale; }
            bool changed=Active; PassiveRemaining=ActiveRemaining=CooldownRemaining=0;
            if(changed && owner!=null)owner.UpdateMoveSpeed();
        }
        void OnDisable(){Clear();}
    }
}
