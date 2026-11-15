using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    // Keep the authored boss/world scale. Only widen the view during nearby colon encounters.
    // Runs before additive impact shake and yields completely to the stage-travel establishing shot.
    [DefaultExecutionOrder(9000)]
    [DisallowMultipleComponent]
    public sealed class ColonBossEncounterCamera : MonoBehaviour
    {
        const float EncounterRange = 10f;
        const float HabitatRange = 5.5f;
        const float MinimumZoom = 1.35f, MaximumZoom = 1.60f;
        const float TallBossMaximumZoom = 1.90f;
        Camera view;
        LevelManager level;
        ColonBossDirector director;
        [System.Serializable] class ArtSize { public float referencePixelHeight = 180, referencePixelWidth = 240; }
        static readonly Dictionary<ColonBossKind,float> referenceHeights = new Dictionary<ColonBossKind,float>();
        Vector3 originalLocalPosition;
        Vector2 offset, offsetVelocity;
        float originalSize, zoomVelocity;
        bool controlling, yieldingToTravel;
        public bool FramingFinalBoss { get; private set; }
        public bool FramingExit { get; private set; }
        public bool FramingKeyDrop { get; private set; }
        bool followingKeyDrop;
        Vector2 keyDropOrigin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            Install();
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

        static void Install()
        {
            var camera = Camera.main;
            if (camera == null || !camera.orthographic || camera.GetComponent<ColonBossEncounterCamera>() != null) return;
            var owner = FindObjectOfType<LevelManager>();
            if (owner == null) return;
            var framing = camera.gameObject.AddComponent<ColonBossEncounterCamera>();
            framing.view = camera;
            framing.level = owner;
            framing.originalSize = camera.orthographicSize;
            framing.originalLocalPosition = camera.transform.localPosition;
        }

        void LateUpdate()
        {
            if (view == null || level == null || !view.orthographic) return;
            // Stage one never acquires camera control. Approach the dormant stage-two home
            // with a wider view already settling before the interaction/emergence begins.
            bool colonMap = FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.IsColon;
            if (!controlling && !yieldingToTravel && ColonBossRuntime.Active.Count == 0 && ConstipationKingRuntime.Active.Count == 0 && !colonMap) return;
            // StageTravelCamera owns both position and zoom, including its return animation.
            if (view.GetComponent<StageTravelCamera>() != null)
            {
                yieldingToTravel = true;
                FramingFinalBoss = FramingExit = FramingKeyDrop = false;
                return;
            }
            if (yieldingToTravel)
            {
                yieldingToTravel = false;
                offset = view.transform.localPosition - originalLocalPosition;
                offsetVelocity = Vector2.zero;
                zoomVelocity = 0;
                controlling = true;
            }
            if (StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage)
            {
                Restore();
                return;
            }

            var player = level.PlayerCharacter;
            var exit = colonMap ? level.GetComponent<ColonExitSequence>() : null;
            bool kingEmerging = level.GetComponent<ConstipationKingDirector>()?.Summoning == true;
            bool exitPresentation = exit != null && (exit.ProtectsPlayer || exit.State == ColonExitSequence.ExitState.Dropping);
            bool playing = player != null && player.IsAlive && !level.IsLevelEnded && (!level.IsRunFlowPaused || kingEmerging || exitPresentation);
            FramingExit = FramingKeyDrop = false;
            if (exit == null || exit.State != ColonExitSequence.ExitState.Dropping) followingKeyDrop = false;
            ColonBossRuntime nearest = null;
            ConstipationKingRuntime nearestKing = null;
            float distanceSquared = EncounterRange * EncounterRange;
            if (playing)
            {
                foreach (var boss in ColonBossRuntime.Active)
                {
                    // Widen during emergence too; a submerged combat boss keeps the wider view.
                    if (boss == null || boss.Dead || !boss.isActiveAndEnabled || boss.Visual == null) continue;
                    float d = ((Vector2)boss.transform.position - (Vector2)player.transform.position).sqrMagnitude;
                    if (d > distanceSquared) continue;
                    distanceSquared = d;
                    nearest = boss;
                }
                foreach (var king in ConstipationKingRuntime.Active)
                {
                    if (king == null || king.Dead || !king.isActiveAndEnabled || king.Visual == null) continue;
                    float d = ((Vector2)king.transform.position - (Vector2)player.transform.position).sqrMagnitude;
                    if (d > distanceSquared) continue;
                    distanceSquared = d; nearestKing = king; nearest = null;
                }
            }

            ColonBossHabitat nearbyHome = null;
            if (playing && nearest == null && nearestKing == null && colonMap)
            {
                if (director == null) director = level.GetComponent<ColonBossDirector>();
                float homeDistance = HabitatRange * HabitatRange;
                if (director != null && director.Ready)
                    foreach (var home in director.Habitats)
                    {
                        if (home == null || !director.CanSummon(home)) continue;
                        float d = ((Vector2)home.transform.position - (Vector2)player.transform.position).sqrMagnitude;
                        if (d > homeDistance) continue;
                        homeDistance = d; nearbyHome = home;
                    }
                if (nearbyHome != null) distanceSquared = homeDistance;
            }

            float targetSize = originalSize;
            Vector2 targetOffset = Vector2.zero;
            bool framing = nearest != null || nearestKing != null || nearbyHome != null;
            FramingFinalBoss = nearestKing != null;
            if (framing)
            {
                // Ease the range boundary as well, so stepping across ten units cannot pump the camera.
                bool combat = nearest != null || nearestKing != null;
                float range = combat ? EncounterRange : HabitatRange;
                float proximity = Mathf.SmoothStep(0, 1, (range - Mathf.Sqrt(distanceSquared)) / (combat ? 2f : 1.5f));
                Vector2 root = nearestKing != null ? (Vector2)nearestKing.transform.position : nearest != null ? (Vector2)nearest.transform.position : (Vector2)nearbyHome.transform.position;
                Vector2 relative = root - (Vector2)player.transform.position;
                float ratio;
                if (nearestKing != null)
                {
                    // Include the complete fixed source frame, not only opaque body pixels.
                    // Its transparent margins rotate with the five-degree A lean; a union
                    // with the player's footprint keeps both actors visible in any direction.
                    // Attack sprites and pose changes do not affect this stable envelope.
                    float frame = nearestKing.Visual.ReferenceHeight * (480f / 380f);
                    float width = frame * 1.10f, height = frame + .65f;
                    float left = Mathf.Min(-.8f, relative.x - width * .5f - .2f);
                    float right = Mathf.Max(.8f, relative.x + width * .5f + .2f);
                    float bottom = Mathf.Min(-.8f, relative.y - .25f);
                    float top = Mathf.Max(.8f, relative.y + height);
                    targetOffset = new Vector2((left + right) * .5f, (bottom + top) * .5f);
                    float required = Mathf.Max((top - bottom) * .5f / .86f,
                        (right - left) * .5f / (Mathf.Max(.3f, view.aspect) * .92f));
                    ratio = Mathf.Clamp(required / Mathf.Max(.01f, originalSize), MinimumZoom, 3f);
                }
                else
                {
                    var definition = nearest != null ? nearest.Definition : ColonBossDefinition.Get(nearbyHome.Kind);
                    float height = nearest != null ? nearest.Visual.ReferenceHeight : ReferenceHeight(definition);
                    bool tall = definition.Kind == ColonBossKind.Flamingo && height > 3.9f;
                    targetOffset = new Vector2(Mathf.Clamp(relative.x * .14f, -.65f, .65f),
                        Mathf.Clamp((relative.y + height * .5f) * .46f, -.65f, tall ? 1.9f : 1.4f));
                    float top = Mathf.Max(.8f, relative.y + height);
                    float bottom = Mathf.Min(-.6f, relative.y - .2f);
                    float vertical = Mathf.Max((top - targetOffset.y) / (tall ? .80f : .88f), targetOffset.y - bottom);
                    float horizontal = (Mathf.Abs(relative.x - targetOffset.x) + definition.Size * .5f + .4f) / Mathf.Max(.3f, view.aspect);
                    float required = Mathf.Max(vertical, horizontal);
                    ratio = Mathf.Clamp(required / Mathf.Max(.01f, originalSize), MinimumZoom, tall ? TallBossMaximumZoom : MaximumZoom);
                }
                targetSize = originalSize * Mathf.Lerp(1, ratio, proximity);
                targetOffset *= proximity;
                controlling = true;
            }

            // A live nearby combatant has priority. Once it is gone, keep the
            // reward flight and the full doorway in this same camera controller.
            bool openingCutscene = exit != null && (exit.State == ColonExitSequence.ExitState.Unlocking || exit.State == ColonExitSequence.ExitState.Demo);
            if (playing && (nearest == null && nearestKing == null || openingCutscene) &&
                TryFrameExit(player, exit, out float exitSize, out Vector2 exitOffset))
            {
                targetSize = exitSize; targetOffset = exitOffset;
                FramingFinalBoss = false;
                framing = controlling = true;
            }

            if (!controlling) return;
            // Unscaled time also returns to the normal framing behind result/settings panels.
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float duration = FramingExit && exitPresentation ? .12f : FramingKeyDrop ? .22f : nearestKing != null ? .35f : framing ? .55f : 1.5f;
            view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, targetSize, ref zoomVelocity, duration, Mathf.Infinity, dt);
            offset = Vector2.SmoothDamp(offset, targetOffset, ref offsetVelocity, duration, Mathf.Infinity, dt);
            view.transform.localPosition = originalLocalPosition + (Vector3)offset;
            if (!framing && Mathf.Abs(view.orthographicSize - originalSize) < .001f && offset.sqrMagnitude < .000001f) Restore();
        }

        bool TryFrameExit(Character player, ColonExitSequence exit, out float size, out Vector2 position)
        {
            size = originalSize; position = Vector2.zero;
            if (exit == null || !exit.isActiveAndEnabled || exit.Opening == null ||
                exit.State == ColonExitSequence.ExitState.Cancelled || exit.State == ColonExitSequence.ExitState.Completed) return false;
            var state = exit.State; Vector2 actor = player.Position;
            Bounds envelope;
            float strength = 1;
            if (state == ColonExitSequence.ExitState.Dropping && exit.Key != null && Vector2.Distance(actor, exit.PlantedPoint) < 14)
            {
                if (!followingKeyDrop)
                {
                    var king = level.GetComponent<ConstipationKingDirector>()?.Boss;
                    keyDropOrigin = king != null ? (Vector2)king.transform.position : exit.PlantedPoint-Vector2.right*1.7f;
                    followingKeyDrop = true;
                }
                // Stable flight envelope also survives the boss object disappearing.
                // It includes the crown-height launch, rotating key and planted tip.
                envelope = new Bounds(keyDropOrigin+Vector2.up*3.65f, new Vector3(8.2f,8.3f,0));
                var art = exit.Key.GetComponent<SpriteRenderer>();
                if (art != null) envelope.Encapsulate(art.bounds);
                envelope.Encapsulate(exit.PlantedPoint);
                FramingKeyDrop = true;
            }
            else if ((state == ColonExitSequence.ExitState.Dropped || state == ColonExitSequence.ExitState.PickingUp) &&
                     exit.Key != null && Vector2.Distance(actor,exit.PlantedPoint)<7)
            {
                var art = exit.Key.GetComponent<SpriteRenderer>();
                envelope = art != null ? art.bounds : new Bounds(exit.Key.position,Vector3.one*3);
                envelope.Encapsulate(exit.PlantedPoint); envelope.Expand(new Vector3(.8f,.8f,0));
                strength = state == ColonExitSequence.ExitState.PickingUp ? 1 : Mathf.SmoothStep(0,1,(7-Vector2.Distance(actor,exit.PlantedPoint))/2);
            }
            else
            {
                bool lockedShot = state == ColonExitSequence.ExitState.Unlocking || state == ColonExitSequence.ExitState.Demo;
                float distance = Vector2.Distance(actor,exit.Opening.transform.position);
                if (!lockedShot && distance>=7) return false;
                var opening = exit.Opening; var radii = opening.Radii;
                envelope = new Bounds(opening.Center+opening.Inward*.6f,new Vector3(radii.x*2.8f,radii.y*2.5f,0));
                if (exit.GateVisual != null)
                {
                    envelope.Encapsulate(exit.GateVisual.LockBounds);
                    foreach (var anchor in exit.GateVisual.Anchors) envelope.Encapsulate(anchor);
                }
                var throne = level.GetComponent<ConstipationKingDirector>()?.Throne;
                if (throne != null && throne.gameObject.activeInHierarchy && throne.Toilet != null)
                {
                    float hint = Mathf.SmoothStep(0,1,(5-Vector2.Distance(actor,throne.transform.position))/2);
                    // Show the whole invitation while approaching it, without a
                    // hard zoom threshold between the adjacent throne and seal.
                    var bounds = throne.Toilet.bounds; Vector3 center = envelope.center;
                    envelope.Encapsulate(Vector3.Lerp(center,bounds.min,hint));
                    envelope.Encapsulate(Vector3.Lerp(center,bounds.max,hint));
                }
                if (state == ColonExitSequence.ExitState.Unlocking && exit.Key != null)
                {
                    var art = exit.Key.GetComponent<SpriteRenderer>();
                    if (art != null && art.enabled) envelope.Encapsulate(art.bounds);
                }
                strength = lockedShot ? 1 : Mathf.SmoothStep(0,1,(7-distance)/3);
                FramingExit = true;
            }
            envelope.Encapsulate(actor+new Vector2(-.8f,-.6f));
            envelope.Encapsulate(actor+new Vector2(.8f,1.5f));
            float required = Mathf.Max(envelope.extents.y/.86f, envelope.extents.x/(Mathf.Max(.3f,view.aspect)*.92f));
            size = Mathf.Lerp(originalSize,Mathf.Max(originalSize,required),strength);
            position = ((Vector2)envelope.center-actor)*strength;
            return true;
        }

        static float ReferenceHeight(ColonBossDefinition definition)
        {
            if (referenceHeights.TryGetValue(definition.Kind, out float height)) return height;
            var asset = Resources.Load<TextAsset>("ColonBosses/" + definition.Id + "/BossArt");
            var art = asset != null ? JsonUtility.FromJson<ArtSize>(asset.text) : new ArtSize();
            height = definition.Size * art.referencePixelHeight / Mathf.Max(1, art.referencePixelWidth);
            referenceHeights[definition.Kind] = height;
            return height;
        }

        void Restore()
        {
            if (view != null && controlling && view.GetComponent<StageTravelCamera>() == null)
            {
                view.orthographicSize = originalSize;
                view.transform.localPosition = originalLocalPosition;
            }
            controlling = false;
            FramingFinalBoss = FramingExit = FramingKeyDrop = followingKeyDrop = false;
            offset = offsetVelocity = Vector2.zero;
            zoomVelocity = 0;
        }
        void OnDisable() { Restore(); }
    }
}
