using System;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    // The boss releases a physical key. Completion requires the player's final confirmation.
    [DisallowMultipleComponent]
    public sealed class ColonExitSequence : MonoBehaviour
    {
        public enum ExitState { Locked, Dropping, Dropped, PickingUp, Carrying, Unlocking, Open, Demo, Completed, Cancelled }
        public ExitState State { get; private set; }
        public static ColonExitSequence Active { get; private set; }
        public bool ProtectsPlayer => isActiveAndEnabled && (State == ExitState.PickingUp || State == ExitState.Unlocking || State == ExitState.Demo);
        public bool Protects(Character player) => ProtectsPlayer && level != null && level.PlayerCharacter == player;
        public bool CompletionAuthorized => State == ExitState.Completed;
        public Transform Key => key != null ? key.transform : null;
        public ColonKeyPickup Pickup { get; private set; }
        public DemoCompletionDialogue Dialogue { get; private set; }
        public StomachOpening Opening => opening;
        public ColonGateSealVisual GateVisual => gate;
        public float DropProgress { get; private set; }
        public float PickupProgress { get; private set; }
        public float UnlockProgress { get; private set; }
        public Vector2 PlantedPoint { get; private set; }
        public Vector2 KeyTipPosition => key != null ? (Vector2)key.transform.TransformPoint(KeyTip) : PlantedPoint;
        public bool CanInteract => FieldReady && (State == ExitState.Locked || State == ExitState.Dropped || State == ExitState.Carrying || State == ExitState.Open);
        public bool CanPickup => FieldReady && State == ExitState.Dropped;
        LevelManager level;
        StomachOpening opening;
        ColonGateSealVisual gate;
        ColonKeyGroundVisual groundVisual;
        SpriteRenderer key, carrierBody;
        Character carrier;
        bool defeatFinished, ownsPause, ownsTimeScale;
        float clock, previousScale = 1;
        float FloatingAngle => -90 + Mathf.Sin(clock * 1.9f) * 2.5f;
        Vector3 KeyTip => KeyPoint(.437f, .01f);
        Vector3 KeyPoint(float x, float y)
        {
            if (key == null || key.sprite == null) return Vector3.down;
            var bounds = key.sprite.bounds;
            float px = bounds.min.x + bounds.size.x * x;
            return new Vector3(key.flipX ? -px : px, bounds.min.y + bounds.size.y * y);
        }
        bool FieldReady => AliveInColon() && !level.IsRunFlowPaused && Time.timeScale > 0 && !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage &&
            !level.PlayerCharacter.IsDashing && !level.PlayerCharacter.IsPortalTravelling && !level.PlayerCharacter.IsPortalPoseHeld && level.PlayerCharacter.Skills?.IsCutin != true &&
            FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldActive;
        bool CanAdvance => AliveInColon() && Time.timeScale > 0 && !MiniStageRuntimeState.IsInsideMiniStage && !StageEntryLoading.IsLoading && (ownsPause || !level.IsRunFlowPaused);

        public static ColonExitSequence Ensure(LevelManager owner)
        {
            var result = owner.GetComponent<ColonExitSequence>() ?? owner.gameObject.AddComponent<ColonExitSequence>();
            result.level = owner; Active = result; result.InstallGate(); return result;
        }
        void InstallGate()
        {
            var map = FiniteStomachMap.Instance;
            if (gate != null || level == null || level.GetComponent<StageProgression>()?.StageNumber != 2 || map == null || !map.IsColon) return;
            opening = StomachOpening.Find(false); if (opening == null) return;
            gate = ColonGateSealVisual.Create(opening, map); opening.SetOpened(0);
        }
        SpriteRenderer CreateKey(float height)
        {
            var sr = new GameObject("변비왕의 지팡이 열쇠").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false); sr.sprite = Resources.Load<Sprite>("ColonEnding/KeyScepter");
            if (sr.sprite == null) Debug.LogError("Missing ColonEnding/KeyScepter", this);
            else sr.transform.localScale = Vector3.one * height / Mathf.Max(.01f, sr.sprite.bounds.size.y);
            sr.sortingLayerName = "Monster Full"; sr.sortingOrder = 520; return sr;
        }
        void Update()
        {
            if (level == null) return;
            if (level.GetComponent<StageProgression>()?.StageNumber != 2) { Destroy(this); return; }
            // Direct stage loading may install the seal before its player is ready.
            // Missing startup data is not an aborted encounter.
            bool abort = level.IsLevelEnded || (level.PlayerCharacter != null ? !level.PlayerCharacter.IsAlive : State != ExitState.Locked);
            if (State != ExitState.Completed && State != ExitState.Cancelled && abort) Cancel();
            InstallGate();
            bool visible = State != ExitState.Cancelled && !MiniStageRuntimeState.IsInsideMiniStage && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldVisible;
            if (gate != null) gate.gameObject.SetActive(visible);
            if (key != null) key.gameObject.SetActive(visible && State != ExitState.Open && State != ExitState.Demo && State != ExitState.Completed);
            if (groundVisual != null) groundVisual.gameObject.SetActive(visible && (State == ExitState.Dropping || State == ExitState.Dropped || State == ExitState.PickingUp));
            if (CanAdvance)
            {
                clock += Time.deltaTime;
                if (groundVisual != null && groundVisual.gameObject.activeSelf) groundVisual.Tick(Time.deltaTime);
            }
        }
        void LateUpdate()
        {
            if (State != ExitState.Carrying || carrier == null || key == null || !CanAdvance) return;
            key.transform.rotation = Quaternion.Euler(0, 0, FloatingAngle); key.transform.position = FloatingPosition();
        }
        Vector3 FloatingPosition()
        {
            Bounds body = new Bounds(carrier.transform.position, new Vector3(.5f, .9f, 0));
            if (carrierBody != null && carrierBody.sprite != null) body = PromptSpriteGeometry.WorldBounds(carrierBody);
            // Only the key floats above the visible silhouette: no hands or connecting geometry.
            float halfThickness = .25f;
            if (key.sprite != null)
            {
                var size = Vector3.Scale(key.sprite.bounds.size, key.transform.lossyScale);
                float angle = FloatingAngle * Mathf.Deg2Rad;
                halfThickness = (Mathf.Abs(Mathf.Sin(angle) * size.x) + Mathf.Abs(Mathf.Cos(angle) * size.y)) * .5f;
            }
            return new Vector3(body.center.x + Mathf.Sin(clock * 1.35f) * .035f, body.max.y + halfThickness + .16f + Mathf.Sin(clock * 2.2f) * .055f, carrier.transform.position.z);
        }
        public void DropKey(Vector3 hand, Vector3 ground, float startAngle = 0, float height = 2.35f, bool flipped = false)
        {
            if (State != ExitState.Locked || !AliveInColon()) return;
            InstallGate(); State = ExitState.Dropping;
            key = CreateKey(Mathf.Clamp(height, 1.6f, 4.2f)); key.flipX = flipped; key.transform.position = hand; key.transform.rotation = Quaternion.Euler(0, 0, startAngle);
            PlantedPoint = FiniteStomachMap.Instance.ClampToFloor((Vector2)ground + Vector2.right * 1.7f, .8f);
            groundVisual = ColonKeyGroundVisual.Create(transform); StartCoroutine(Drop(hand, ground, startAngle));
        }
        IEnumerator Drop(Vector3 from, Vector3 ground, float angle)
        {
            Vector3 plantedCenter = (Vector3)PlantedPoint - Vector3.Scale(KeyTip, key.transform.localScale) - Vector3.up * .045f;
            yield return Animate(1.5f, p =>
            {
                DropProgress = p;
                key.transform.position = Vector3.Lerp(from, plantedCenter, p) + Vector3.up * (4 * 2.65f * p * (1 - p));
                float turn = 1 - Mathf.Pow(1 - p, 1.6f);
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(angle, 720, turn));
                var floor = Vector2.Lerp(ground, PlantedPoint, p);
                groundVisual.SetAir(floor, Mathf.Max(0, key.transform.position.y - floor.y));
            }, false);
            if (!AliveInColon()) yield break;
            groundVisual.SetAir(PlantedPoint, 0); groundVisual.Land(PlantedPoint);
            yield return Animate(.24f, p =>
            {
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(p * Mathf.PI * 3) * 4.5f * (1 - p));
                key.transform.position = plantedCenter - Vector3.up * Mathf.Sin(p * Mathf.PI) * .065f;
            }, false);
            while (!defeatFinished) { if (!AliveInColon()) yield break; yield return null; }
            if (!AliveInColon()) yield break;
            State = ExitState.Dropped;
            var trigger = new GameObject("지팡이 열쇠 줍기"); trigger.transform.SetParent(transform, false); trigger.transform.position = PlantedPoint;
            trigger.AddComponent<CircleCollider2D>().radius = 1.65f;
            Pickup = trigger.AddComponent<ColonKeyPickup>(); Pickup.Configure(this, key);
            trigger.AddComponent<MapMarker>().Configure(MapMarkerKind.Event, "변비왕의 지팡이 열쇠");
            ColonBossDirector.ShowNotice("봉인을 풀 열쇠", "바닥에 꽂힌 지팡이 열쇠를 주워 좌측 하단 자물쇠로 가져가세요");
        }
        public void FinishDefeat() { defeatFinished = true; }
        bool AliveInColon() => level != null && !level.IsLevelEnded && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive && level.GetComponent<StageProgression>()?.StageNumber == 2;
        public bool TakeKey(Character player)
        {
            if (!CanPickup || player != level.PlayerCharacter || Pickup == null || Vector2.Distance(player.transform.position, PlantedPoint) > 2.4f) return false;
            State = ExitState.PickingUp; carrier = player;
            var animator = player.GetComponentInChildren<SpriteAnimator>(); carrierBody = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            Pickup.gameObject.SetActive(false); AcquirePause(); StartCoroutine(ExtractKey()); return true;
        }
        IEnumerator ExtractKey()
        {
            Vector3 planted = key.transform.position;
            yield return Animate(.34f, p =>
            {
                PickupProgress = p * .4f;
                key.transform.position = planted + Vector3.up * (.62f * p);
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(p * Mathf.PI * 2) * 4 * (1 - p));
                groundVisual.Extract(p);
            });
            if (!AliveInColon()) yield break;
            Vector3 lifted = key.transform.position;
            yield return Animate(.51f, p =>
            {
                PickupProgress = .4f + p * .6f;
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, FloatingAngle, p));
                key.transform.position = Vector3.Lerp(lifted, FloatingPosition(), p) + Vector3.up * Mathf.Sin(p * Mathf.PI) * .28f;
            });
            if (!AliveInColon()) yield break;
            State = ExitState.Carrying; groundVisual.gameObject.SetActive(false); ReleasePause();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.RareItemPickup);
            ColonBossDirector.ShowNotice("열쇠를 얻었습니다", "좌측 하단 자물쇠 앞에서 상호작용하세요"); LateUpdate();
        }
        public bool InteractAtGate(Character player, StomachOpening target)
        {
            if (!CanInteract || player != level.PlayerCharacter || target != opening || Vector2.Distance(player.transform.position, opening.transform.position) > 2.6f) return false;
            if (State == ExitState.Open) { ShowDemo(); return true; }
            if (State != ExitState.Carrying)
            { ColonBossDirector.ShowNotice("왕의 봉인", defeatFinished ? "떨어진 지팡이 열쇠를 먼저 주워주세요" : "변비왕이 가진 지팡이 열쇠가 필요합니다"); return true; }
            State = ExitState.Unlocking; AcquirePause(); StartCoroutine(Unlock()); return true;
        }
        IEnumerator Animate(float seconds, Action<float> sample, bool smooth = true)
        {
            float age = 0;
            while (age < seconds)
            {
                if (!AliveInColon()) yield break;
                if (CanAdvance)
                {
                    age += Time.deltaTime; float p = Mathf.Clamp01(age / seconds);
                    sample(smooth ? Mathf.SmoothStep(0, 1, p) : p);
                }
                yield return null;
            }
        }
        IEnumerator Unlock()
        {
            Vector3 start = key.transform.position, scale = key.transform.localScale, socket = gate.Keyhole;
            Vector3 teeth = KeyPoint(.78f, .13f);
            yield return Animate(.65f, p =>
            {
                UnlockProgress = p * .19f;
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(-90, -25, p));
                Vector3 inserted = socket - key.transform.rotation * Vector3.Scale(teeth, scale);
                key.transform.position = Vector3.Lerp(start, inserted, p) + Vector3.up * Mathf.Sin(p * Mathf.PI) * .22f;
            });
            if (!AliveInColon()) yield break;
            yield return Animate(.4f, p =>
            {
                UnlockProgress = .19f + p * .12f;
                key.transform.localScale = scale * Mathf.Lerp(1, .75f, p);
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(-25, 65, p));
                key.transform.position = socket - key.transform.rotation * Vector3.Scale(teeth, key.transform.localScale);
            });
            if (!AliveInColon()) yield break;
            key.enabled = false; GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.ChestOpen);
            yield return Animate(.35f, p => { UnlockProgress = .31f + p * .10f; gate.LiftShackle(p); });
            if (!AliveInColon()) yield break;
            yield return Animate(.85f, p => { UnlockProgress = .41f + p * .25f; gate.Break(p); });
            if (!AliveInColon()) yield break;
            yield return Animate(1.2f, p => { UnlockProgress = .66f + p * .34f; opening.SetOpened(p); });
            if (!AliveInColon()) yield break;
            State = ExitState.Open; ReleasePause();
            ColonBossDirector.ShowNotice("출구가 열렸습니다", "구멍 앞에서 상호작용하면 데모 탐험을 마칩니다");
        }
        void AcquirePause()
        {
            ownsPause = true; level.SetRunFlowPaused(true); level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            var body = level.PlayerCharacter.GetComponent<Rigidbody2D>(); if (body != null) body.velocity = Vector2.zero;
        }
        void ShowDemo()
        {
            State = ExitState.Demo; AcquirePause(); previousScale = Time.timeScale; ownsTimeScale = true; Time.timeScale = 0;
            Dialogue = ApothecaryUI.Instance.BuildColonDemoCompletionDialogue(); StartCoroutine(WaitForConfirmation());
        }
        IEnumerator WaitForConfirmation()
        {
            while (Dialogue != null && !Dialogue.Confirmed) yield return null;
            if (Dialogue == null || !AliveInColon()) { Cancel(); yield break; }
            State = ExitState.Completed; level.LevelPassed(null);
        }
        void ReleasePause()
        {
            if (!ownsPause) return;
            ownsPause = false;
            // A stage transition or defeat owns its own pause: never clear it during cleanup.
            if (level == null || level.IsLevelEnded || level.GetComponent<StageProgression>()?.StageNumber != 2) return;
            level.SetRunFlowPaused(false); level.EntityManager.SetFieldMonsterRuntimeSuspended(false);
        }
        void Cancel()
        {
            StopAllCoroutines(); State = ExitState.Cancelled;
            if (Pickup != null) Pickup.gameObject.SetActive(false);
            if (key != null) key.gameObject.SetActive(false);
            if (gate != null) gate.gameObject.SetActive(false);
            if (groundVisual != null) groundVisual.gameObject.SetActive(false);
            if (Dialogue != null) Destroy(Dialogue.gameObject);
            ReleasePause(); RestoreTimeScale();
        }
        void RestoreTimeScale()
        {
            if (!ownsTimeScale) return;
            ownsTimeScale = false;
            if (level == null || !level.IsLevelEnded) Time.timeScale = previousScale;
        }
        void OnDisable()
        {
            if (State != ExitState.Completed && State != ExitState.Cancelled) Cancel();
        }
        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (State != ExitState.Completed) { ReleasePause(); RestoreTimeScale(); }
            if (gate != null) Destroy(gate.gameObject);
            if (key != null) Destroy(key.gameObject);
            if (groundVisual != null) Destroy(groundVisual.gameObject);
            if (Pickup != null) Destroy(Pickup.gameObject);
            if (Dialogue != null) Destroy(Dialogue.gameObject);
        }
    }
    public sealed class ColonKeyPickup : InteractableEventObject
    {
        ColonExitSequence sequence;
        SpriteRenderer key;
        public void Configure(ColonExitSequence owner, SpriteRenderer art) { sequence = owner; key = art; }
        protected override bool InteractionAvailable => sequence != null && sequence.CanPickup;
        protected override Component PromptOwner => key != null ? (Component)key : this;
        protected override bool ExecuteInteraction(Character player) => sequence != null && sequence.TakeKey(player);
    }
}
