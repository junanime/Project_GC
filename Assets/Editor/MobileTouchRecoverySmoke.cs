using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using Phase = UnityEngine.InputSystem.TouchPhase;

namespace Vampire.Editor
{
    // Fault injection: omit UI pointer-up/drag callbacks while the real input device changes.
    public static class MobileTouchRecoverySmoke
    {
        public static void Run()
        {
            int passed = 0, exit = 1;
            float scale = Time.timeScale;
            var mode = InputSystem.settings.updateMode;
            Touchscreen screen = null;
            GameObject go = null, lifecycleGo = null;
            void Check(bool value, string name)
            { if (!value) throw new Exception("TOUCH_RECOVERY_FAIL " + name); passed++; Debug.Log("TOUCH_RECOVERY_PASS " + name); }
            try
            {
                Time.timeScale = 1;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                screen = InputSystem.AddDevice<Touchscreen>();
                go = new GameObject("Recovery joystick", typeof(RectTransform));
                var control = go.AddComponent<MobileTouchControl>(); control.Joystick = true;
                control.Knob = new GameObject("Knob", typeof(RectTransform)).GetComponent<RectTransform>();
                control.Knob.SetParent(go.transform, false);
                Vector2 movement = Vector2.zero; int releases = 0;
                control.Changed = value => movement = value;
                control.Released = () => releases++;
                void Device(int id, Phase phase, float x = -80)
                { InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = new Vector2(x, 0) }); InputSystem.Update(); }
                ExtendedPointerEventData Event(int id, float x = -80) => new ExtendedPointerEventData(null)
                    { device = screen, touchId = id, pointerId = (screen.deviceId << 24) + id, position = new Vector2(x, 0) };
                void Tick() => typeof(MobileTouchControl).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, null);
                void Down(int id) { Device(id, Phase.Began); control.OnPointerDown(Event(id)); Tick(); }
                Down(11); Check(movement.x < -.5f, "owner starts left movement");
                Device(11, Phase.Ended); Tick();
                Check(movement == Vector2.zero && control.Knob.anchoredPosition == Vector2.zero && releases == 1, "lost pointer-up releases movement and knob");
                control.Cancel(); Check(releases == 1, "duplicate cancellation is idempotent");
                Down(12); Check(movement.x < -.5f, "new gesture recovers immediately");
                Device(22, Phase.Began, 300); control.OnPointerUp(Event(22)); Tick();
                Check(movement.x < -.5f, "another finger cannot release the owner");
                Device(22, Phase.Ended, 300); Tick();
                Check(movement.x < -.5f, "another touch ending preserves movement");
                for (int i = 0; i < 100; i++) { InputSystem.Update(); Tick(); }
                Check(movement.x < -.5f, "stationary held contact has no timeout");
                Device(12, Phase.Moved, 80); Tick();
                Check(movement.x > .5f, "device polling recovers a missing drag callback");
                Device(23, Phase.Began, 300); Device(12, Phase.Ended); Tick();
                Check(movement == Vector2.zero, "owner release clears even while another finger stays held");
                Down(13); Device(13, Phase.Canceled); Tick();
                Check(movement == Vector2.zero, "OS canceled touch clears movement");
                Down(14); Device(14, Phase.Ended); Device(15, Phase.Began); control.OnPointerDown(Event(15)); Tick();
                Check(movement.x < -.5f, "fresh pointer takes over stale ownership without waiting for update");
                control.OnEndDrag(Event(15)); Check(movement == Vector2.zero, "end drag cancels its owner");
                Device(15, Phase.Ended); Down(16); Time.timeScale = 0; Tick();
                Check(movement == Vector2.zero, "pause cancels even without a drag event"); Time.timeScale = 1;
                Device(16, Phase.Ended); Down(17);
                // This runner is edit-mode; invoke the Unity lifecycle callback explicitly.
                control.SendMessage("OnDisable", SendMessageOptions.RequireReceiver);
                Check(movement == Vector2.zero, "disabled UI callback cancels");
                Device(17, Phase.Ended); Down(18); control.OnCancel(new BaseEventData(null));
                Check(movement == Vector2.zero, "UI cancellation cancels");
                Device(18, Phase.Ended); Down(19);
                lifecycleGo = new GameObject("Recovery browser lifecycle");
                var lifecycle = lifecycleGo.AddComponent<MobileWebLifecycle>();
                void BrowserTick() => typeof(MobileWebLifecycle).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lifecycle, null);
                lifecycle.SetBrowserTouchCount("1"); BrowserTick(); Check(movement.x < -.5f, "browser does not cancel remaining touches");
                MobileGameplayInput.RequestInteraction();
                lifecycle.SetBrowserTouchCount("0"); BrowserTick();
                Check(movement == Vector2.zero && Time.timeScale == 1, "browser lost contact clears without pausing");
                Check(MobileGameplayInput.ConsumeInteraction(), "browser release preserves queued interaction");
                Device(19, Phase.Ended); Down(20);
                lifecycle.SetBrowserTouchCount("0"); lifecycle.SetBrowserTouchCount("1"); BrowserTick();
                Check(movement.x < -.5f, "new browser touch supersedes pending all-up event");
                InputSystem.RemoveDevice(screen); Tick();
                Check(movement == Vector2.zero, "removed input device releases ownership"); screen = null;
                exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (lifecycleGo != null) UnityEngine.Object.DestroyImmediate(lifecycleGo);
                if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
                InputSystem.settings.updateMode = mode; Time.timeScale = scale;
            }
            Debug.Log($"TOUCH_RECOVERY_DONE passed={passed} exit={exit}");
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
