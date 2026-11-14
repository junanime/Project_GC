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
                FramingFinalBoss = false;
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
            bool playing = player != null && player.IsAlive && !level.IsLevelEnded && !level.IsRunFlowPaused;
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

            if (!controlling) return;
            // Unscaled time also returns to the normal framing behind result/settings panels.
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float duration = nearestKing != null ? .35f : framing ? .55f : 1.5f;
            view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, targetSize, ref zoomVelocity, duration, Mathf.Infinity, dt);
            offset = Vector2.SmoothDamp(offset, targetOffset, ref offsetVelocity, duration, Mathf.Infinity, dt);
            view.transform.localPosition = originalLocalPosition + (Vector3)offset;
            if (!framing && Mathf.Abs(view.orthographicSize - originalSize) < .001f && offset.sqrMagnitude < .000001f) Restore();
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
            FramingFinalBoss = false;
            offset = offsetVelocity = Vector2.zero;
            zoomVelocity = 0;
        }
        void OnDisable() { Restore(); }
    }
}
