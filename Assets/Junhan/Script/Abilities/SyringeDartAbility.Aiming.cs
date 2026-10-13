using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    public partial class SyringeDartAbility
    {
        private Vector2 lastNeedleDirection = Vector2.right;
        private Vector2 pendingNeedleDirection;
        private bool hasPendingNeedleDirection;
        private readonly List<RaycastResult> aimUiHits = new List<RaycastResult>();

        private void ReadNeedleAimInput()
        {
            if (Time.timeScale <= 0f || AttacksBlocked || cursorControlEnabled || needleShotgunEnabled ||
                (heavySnipeEnabled && (isHeavyCharging || IsHeavySnipeCommandHeld())))
            {
                hasPendingNeedleDirection = false;
                return;
            }
            var touches = Touchscreen.current;
            bool hasTouch = false;
            if (touches != null)
                foreach (var touch in touches.touches)
                {
                    hasTouch |= touch.press.isPressed || touch.press.wasReleasedThisFrame;
                    if (touch.press.wasPressedThisFrame) QueueNeedleAim(touch.position.ReadValue());
                }
            // Avoid a second request from a mouse synthesized from the same touch.
            if (!hasTouch && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                QueueNeedleAim(Mouse.current.position.ReadValue());
        }

        private void QueueNeedleAim(Vector2 screenPosition)
        {
            if (playerCharacter == null || Camera.main == null || IsNeedleAimOverUI(screenPosition)) return;
            var camera = Camera.main;
            if (!camera.pixelRect.Contains(screenPosition)) return;
            var ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.forward, playerCharacter.transform.position);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector2 direction = (Vector2)ray.GetPoint(distance) - GetProjectileSpawnPosition(lastNeedleDirection);
            if (direction.sqrMagnitude < .0001f) return;
            pendingNeedleDirection = direction.normalized;
            hasPendingNeedleDirection = true;
        }

        private bool IsNeedleAimOverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            aimUiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, aimUiHits);
            foreach (var hit in aimUiHits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private Vector2 GetAutomaticNeedleDirection()
        {
            if (entityManager == null || playerCharacter == null) return lastNeedleDirection;
            Vector2 origin = GetProjectileSpawnPosition(lastNeedleDirection);
            float range = GetEffectiveSyringeMaxDistance() + (lifeBurnEnabled ? lifeBurnBonusRange : 0f);
            float closest = range * range;
            Monster target = null;
            foreach (var monster in entityManager.LivingMonsters)
            {
                if (!NeedleTargeting.IsValid(monster, monsterLayer)) continue;
                float distance = (NeedleTargeting.Position(monster) - origin).sqrMagnitude;
                if (distance > closest) continue;
                closest = distance;
                target = monster;
            }
            if (target == null) return lastNeedleDirection;
            Vector2 direction = NeedleTargeting.Position(target) - origin;
            if (direction.sqrMagnitude > .0001f) lastNeedleDirection = direction.normalized;
            return lastNeedleDirection;
        }

        // Only scheduled basic needles consume this request; item echoes and manual
        // legendary weapons must not steal the single-shot override.
        private bool LaunchAimedSyringe(Vector2 direction, bool rear = false, float damageScale = 1f, int polarity = 0)
        {
            bool manual = hasPendingNeedleDirection;
            if (manual) direction = pendingNeedleDirection;
            if (!LaunchSyringeProjectile(direction, rear, damageScale, polarity)) return false;
            if (manual) hasPendingNeedleDirection = false;
            return true;
        }
    }
}
