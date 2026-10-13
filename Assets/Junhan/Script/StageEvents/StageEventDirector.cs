using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public class StageEventDirector : RuntimeModuleHost
    {
        [Header("Random Event Windows")]
        [SerializeField] private bool useWindowSchedule = true;
        [SerializeField] private StageEventWindowTrack primaryTrack = new StageEventWindowTrack { windowSeconds = 120f };
        [SerializeField] private StageEventWindowTrack secondaryTrack = new StageEventWindowTrack { windowSeconds = 360f };
        [Tooltip("LevelManager가 없는 테스트 씬에서만 사용하는 일정 길이입니다.")]
        [SerializeField] private float fallbackDurationSeconds = 900f;
        [SerializeField] private List<StageEventSchedule> basicEvents = new List<StageEventSchedule>();
        [SerializeField] private List<AdvancedStageFieldEventDirector> advancedEvents = new List<AdvancedStageFieldEventDirector>();
        [SerializeField] private List<AntacidBubbleSurgeEventController> antacidEvents = new List<AntacidBubbleSurgeEventController>();
        private readonly List<StageEventPresentation> presentations = new List<StageEventPresentation>();
        private StageEventCornerUI cornerUI;
        private LevelManager presentationLevel;
        List<StageEventSchedule> initialBasic;
        List<AdvancedStageFieldEventDirector> initialAdvanced;
        List<AntacidBubbleSurgeEventController> initialAntacid;
        public void RestartForStage()
        {
            foreach (var module in Modules) module?.Dispose();
            basicEvents = initialBasic.ConvertAll(StageEventTemplate.Copy);
            advancedEvents = initialAdvanced.ConvertAll(StageEventTemplate.Copy);
            antacidEvents = initialAntacid.ConvertAll(StageEventTemplate.Copy);
            if (useWindowSchedule) PrepareWindowSchedule();
            base.Awake();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (presentationLevel == null) presentationLevel = FindObjectOfType<LevelManager>();
            presentations.Clear();
            if (presentationLevel == null || !presentationLevel.IsLevelEnded)
            {
                foreach (var e in basicEvents) e?.CollectPresentations(presentations);
                foreach (var e in advancedEvents) e?.CollectPresentations(presentations);
                foreach (var e in antacidEvents) e?.CollectPresentations(presentations);
            }
            if (cornerUI == null && presentations.Count > 0) cornerUI = gameObject.AddComponent<StageEventCornerUI>();
            if (cornerUI != null) cornerUI.Sync(presentations, MiniStageRuntimeState.IsInsideMiniStage, SchedulesPaused);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (cornerUI != null) cornerUI.Hide();
        }
        protected override void Awake()
        {
            initialBasic = basicEvents.ConvertAll(StageEventTemplate.Copy);
            initialAdvanced = advancedEvents.ConvertAll(StageEventTemplate.Copy);
            initialAntacid = antacidEvents.ConvertAll(StageEventTemplate.Copy);
            if (GetComponent<AcidToadSpawn>() == null) gameObject.AddComponent<AcidToadSpawn>();
            if (useWindowSchedule) PrepareWindowSchedule();
            base.Awake();
        }

        private void PrepareWindowSchedule()
        {
            var templates = new List<StageEventTemplate>();
            foreach (var module in basicEvents) if (module != null && module.enabled) module.CollectWindowTemplates(templates);
            foreach (var module in advancedEvents) if (module != null && module.enabled) module.CollectWindowTemplates(templates);
            var antacidTemplates = antacidEvents.ToArray();
            antacidEvents.Clear();
            foreach (var module in antacidTemplates)
            {
                if (module == null || !module.enabled || !module.EventEnabled) continue;
                var source = module;
                templates.Add(new StageEventTemplate { kind = "Antacid", duration = source.WindowDuration,
                    schedule = time => { var copy = StageEventTemplate.Copy(source); copy.ScheduleAt(time); antacidEvents.Add(copy); } });
            }
            if (templates.Count == 0) { Debug.LogWarning("[StageEventDirector] 활성 이벤트 후보가 없습니다.", this); return; }
            var level = FindObjectOfType<LevelManager>();
            float duration = level != null && level.CurrentLevelBlueprint != null ? level.CurrentLevelBlueprint.levelTime : fallbackDurationSeconds;
            var slots = StageEventWindowPlanner.Build(duration, new[] { primaryTrack, secondaryTrack }, () => Random.value);
            var busyUntil = new Dictionary<string, float>();
            foreach (var slot in slots)
            {
                // Different events may overlap. The same effect must not stop/reset its running instance.
                var available = templates.FindAll(t => !busyUntil.TryGetValue(t.kind, out float end) || end <= slot.time);
                if (available.Count == 0)
                {
                    Debug.LogWarning("[StageEventDirector] 모든 후보가 실행 중이라 이벤트 구간을 건너뜁니다.", this);
                    continue;
                }
                var selected = available[Random.Range(0, available.Count)];
                selected.schedule(slot.time);
                busyUntil[selected.kind] = slot.time + selected.duration;
            }
        }
        protected override IEnumerable<RuntimeModule> Modules
        {
            get
            {
                if (basicEvents != null) foreach (var module in basicEvents) yield return module;
                if (advancedEvents != null) foreach (var module in advancedEvents) yield return module;
                if (antacidEvents != null) foreach (var module in antacidEvents) yield return module;
            }
        }
    }
}
