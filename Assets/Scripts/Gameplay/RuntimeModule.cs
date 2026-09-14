using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Serialized settings and runtime logic owned by one scene director, not a second component.</summary>
    [Serializable]
    public abstract class RuntimeModule
    {
        [Tooltip("이 설정 묶음을 실행합니다. 기존 비활성 스포너는 꺼진 상태로 이관됩니다.")]
        public bool moduleEnabled = true;
        [HideInInspector] public Transform origin;
        [NonSerialized] protected RuntimeModuleHost Host;
        [NonSerialized] private bool initialized, running, started;
        private sealed class Routine
        {
            public readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            public Coroutine handle;
        }
        [NonSerialized] private readonly List<Routine> routines = new List<Routine>();
        protected Transform transform => origin != null ? origin : Host.transform;
        protected GameObject gameObject => transform.gameObject;
        protected bool isActiveAndEnabled => running && Host != null && Host.isActiveAndEnabled;
        public bool enabled { get => moduleEnabled; set { moduleEnabled = value; if (!value) Suspend(); } }
        protected T GetComponent<T>() => transform.GetComponent<T>();
        protected Coroutine StartCoroutine(IEnumerator routine)
        {
            var state = new Routine();
            state.stack.Push(routine);
            routines.Add(state);
            if (running && Host.gameObject.activeInHierarchy) state.handle = Host.StartCoroutine(AdvanceRoutine(state));
            return state.handle;
        }
        private IEnumerator AdvanceRoutine(Routine state)
        {
            while (state.stack.Count > 0)
            {
                if (!running || !moduleEnabled || Host.SchedulesPaused) { yield return null; continue; }
                IEnumerator current = state.stack.Peek();
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    state.stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator child) state.stack.Push(child);
                else yield return current.Current;
            }
            routines.Remove(state);
        }
        protected void StopCoroutine(Coroutine routine)
        {
            if (routine == null) return;
            if (Host != null) Host.StopCoroutine(routine);
            routines.RemoveAll(state => state.handle == routine);
        }
        protected void StopAllCoroutines()
        {
            if (Host != null) foreach (var routine in routines) if (routine.handle != null) Host.StopCoroutine(routine.handle);
            foreach (var routine in routines) foreach (var iterator in routine.stack) (iterator as IDisposable)?.Dispose();
            routines.Clear();
        }
        internal void Bind(RuntimeModuleHost host)
        {
            Host = host;
            if (!moduleEnabled) return;
            if (!initialized) { initialized = true; OnAwake(); OnModuleEnable(); }
            if (!running)
            {
                running = true;
                OnResumed();
                foreach (var routine in routines.ToArray()) routine.handle = Host.StartCoroutine(AdvanceRoutine(routine));
            }
        }
        internal void Tick(RuntimeModuleHost host, bool late = false)
        {
            if (!moduleEnabled) { Suspend(); return; }
            Bind(host);
            if (!started) { started = true; StartCoroutine(OnStart()); }
            if (late) OnLateTick(); else OnTick();
        }
        internal void Suspend()
        {
            if (!running) return;
            running = false;
            // Preserve iterator progress, active counts and kill listeners across an Inspector toggle.
            foreach (var routine in routines) if (routine.handle != null && Host != null) Host.StopCoroutine(routine.handle);
            OnSuspended();
        }
        internal void Dispose()
        {
            Suspend();
            if (initialized) { OnModuleDisable(); StopAllCoroutines(); OnModuleDestroy(); }
        }
        internal void DrawGizmos(RuntimeModuleHost host) { Host = host; if (moduleEnabled) OnModuleGizmos(); }
        internal void FixedTick(RuntimeModuleHost host)
        {
            if (moduleEnabled && running && started) OnFixedTick();
        }
        protected virtual void OnAwake() { }
        protected virtual void OnModuleEnable() { }
        protected virtual IEnumerator OnStart() { yield break; }
        protected virtual void OnTick() { }
        protected virtual void OnLateTick() { }
        protected virtual void OnFixedTick() { }
        protected virtual void OnModuleDisable() { }
        protected virtual void OnSuspended() { }
        protected virtual void OnResumed() { }
        protected virtual void OnModuleDestroy() { }
        protected virtual void OnModuleGizmos() { }
    }

    public abstract class RuntimeModuleHost : MonoBehaviour
    {
        private LevelManager level;
        internal bool SchedulesPaused
        {
            get
            {
                if (level == null) level = FindObjectOfType<LevelManager>();
                return MiniStageRuntimeState.IsInsideMiniStage || (level != null && (level.IsRunFlowPaused || level.IsLevelEnded));
            }
        }
        protected abstract IEnumerable<RuntimeModule> Modules { get; }
        protected virtual void Awake() { foreach (var module in Modules) module?.Bind(this); }
        protected virtual void OnEnable() { foreach (var module in Modules) module?.Bind(this); }
        protected virtual void Update() { foreach (var module in Modules) module?.Tick(this); }
        protected virtual void LateUpdate() { foreach (var module in Modules) module?.Tick(this, true); }
        protected virtual void FixedUpdate() { foreach (var module in Modules) module?.FixedTick(this); }
        protected virtual void OnDisable() { foreach (var module in Modules) module?.Suspend(); }
        protected virtual void OnDestroy() { foreach (var module in Modules) module?.Dispose(); }
        protected virtual void OnDrawGizmosSelected() { foreach (var module in Modules) module?.DrawGizmos(this); }
    }
}
