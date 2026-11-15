#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Assertions used by the real scene ending smoke; no synthetic clone of the state machine.
    public static class ColonExitQualityChecks
    {
        public static void Gate(ColonExitSequence exit, Action<bool, string> check)
        {
            var map = FiniteStomachMap.Instance;
            check(exit.GateVisual != null && exit.GateVisual.Anchors.Length == 4, "Seal has four physical wall attachments");
            foreach (var point in exit.GateVisual.Anchors)
            {
                check(map.WorldRect.Contains(point), "Gold anchor remains inside the map bounds");
                float distance = ColonOpeningGeometry.WallClearance(map, point, false, out _);
                check(distance < -.05f, "Gold anchor is embedded in solid doorway tissue, not empty map exterior");
            }
        }
        public static IEnumerator Planted(ColonExitSequence exit, Action<bool, string> check)
        {
            check(exit.State == ColonExitSequence.ExitState.Dropped && exit.Key != null, "Landed key is ready for a real pickup");
            check(Mathf.Abs(Mathf.DeltaAngle(exit.Key.eulerAngles.z, 0)) < .1f, "Landed scepter stands upright instead of lying flat");
            check(Vector2.Distance(exit.KeyTipPosition, exit.PlantedPoint) < .07f, "The lower tip is planted in the marked ground contact");
            check(FiniteStomachMap.Instance.FloorClearance(exit.PlantedPoint, true) >= .75f, "Planted key is on reachable floor with a full interaction footprint");
            Vector3 position = exit.Key.position; Quaternion rotation = exit.Key.rotation;
            yield return new WaitForSeconds(.16f);
            check(Vector3.Distance(position, exit.Key.position) < .001f && Quaternion.Angle(rotation, exit.Key.rotation) < .01f,
                "Uncollected key stays planted without floating or jittering");
        }
        public static IEnumerator PickupPause(ColonExitSequence exit, Character player, Action<bool, string> check)
        {
            check(exit.State == ColonExitSequence.ExitState.PickingUp && player.IsColonExitProtected, "Pull-out phase locks input and protects the current character");
            check(!exit.CanPickup && !exit.CanInteract && !exit.TakeKey(player), "Pull-out rejects duplicate pickup and gate interactions");
            float progress = exit.PickupProgress, scale = Time.timeScale; var position = exit.Key.position; var rotation = exit.Key.rotation;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.16f);
            check(exit.PickupProgress == progress && exit.Key.position == position && exit.Key.rotation == rotation,
                "Pause freezes extraction position, rotation and phase progress");
            Time.timeScale = scale;
        }
        public static void Floating(ColonExitSequence exit, SpriteRenderer body, string character, Action<bool, string> check)
        {
            var key = exit.Key.GetComponent<SpriteRenderer>();
            var bodyBounds = PromptSpriteGeometry.WorldBounds(body);
            var keyBounds = PromptSpriteGeometry.WorldBounds(key);
            float gap = keyBounds.min.y - bodyBounds.max.y;
            check(gap > .025f && gap < .34f, character + " floating key has a visible air gap above the actual character silhouette");
            check(Mathf.Abs(Mathf.DeltaAngle(exit.Key.eulerAngles.z, -90)) < 3, character + " key floats horizontally with gentle rotation");
            // PromptOwner is the key sprite, so the common E-key prompt and its golden
            // border remain as disabled children after pickup. They are UI, not carry art.
            var renderers = exit.Key.GetComponentsInChildren<Renderer>(true);
            var prompt = exit.Key.GetComponent<PixelInteractionPrompt>();
            var promptRoot = prompt != null ? exit.Key.Find("Pixel E Interaction Prompt") : null;
            bool IsPrompt(Renderer renderer) => promptRoot != null && renderer.transform.IsChildOf(promptRoot);
            var carryArt = renderers.Where(r => !IsPrompt(r)).ToArray();
            string inventory = string.Join(", ", renderers.Select(r => r.name + ":" + r.GetType().Name + (r.enabled && r.gameObject.activeInHierarchy ? " visible" : " hidden")));
            check(carryArt.Length == 1 && carryArt[0] == key &&
                !exit.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Raised arm and glove" || t.name == "두 손으로 지팡이 열쇠 번쩍 들기"),
                character + " floating carry contains only key art, without hands or connecting strips; renderer inventory: " + inventory);
            check(renderers.Where(IsPrompt).All(r => !r.enabled || !r.gameObject.activeInHierarchy),
                character + " collected key keeps its shared E prompt and border hidden during carry");
        }
        public static void Open(ColonExitSequence exit, Action<bool, string> check)
        {
            check(exit.GateVisual.Fractured && exit.UnlockProgress >= .999f, "Stone fracture and chain release finish before the opening accepts interaction");
            var chains = exit.GateVisual.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "interlocking gold links").ToArray();
            check(chains.Length == 4 && chains.All(r => !r.enabled), "All four broken chain strands stop rendering with no ghost streaks across the open doorway");
            check(!exit.Key.GetComponent<SpriteRenderer>().enabled && !exit.ProtectsPlayer, "Inserted key is consumed and input protection is released");
        }
        public static void Cleaned(Action<bool, string> check)
        {
            check(ColonExitSequence.Active == null && Object.FindObjectsOfType<ColonGateSealVisual>(true).Length == 0 &&
                Object.FindObjectsOfType<ColonKeyGroundVisual>(true).Length == 0 && Object.FindObjectsOfType<ColonKeyPickup>(true).Length == 0,
                "Scene exit leaves no old seal, pickup trigger, ground effects or active sequence");
        }
    }
}
#endif
