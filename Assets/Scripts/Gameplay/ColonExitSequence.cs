using System;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    // The final boss only releases the key. Success belongs to the explicit exit confirmation.
    [DisallowMultipleComponent]
    public sealed class ColonExitSequence : MonoBehaviour
    {
        public enum ExitState { Locked, Dropping, Dropped, Carrying, Unlocking, Open, Demo, Completed }
        public ExitState State { get; private set; }
        public static ColonExitSequence Active { get; private set; }
        public bool ProtectsPlayer => isActiveAndEnabled && (State == ExitState.Unlocking || State == ExitState.Demo);
        public bool Protects(Character player) => ProtectsPlayer && level != null && level.PlayerCharacter == player;
        public bool CompletionAuthorized => State == ExitState.Completed;
        public Transform Key => key != null ? key.transform : null;
        public ColonKeyPickup Pickup { get; private set; }
        public DemoCompletionDialogue Dialogue { get; private set; }
        public bool CanInteract => FieldReady && (State == ExitState.Locked || State == ExitState.Dropped || State == ExitState.Carrying || State == ExitState.Open);
        public bool CanPickup => FieldReady && State == ExitState.Dropped;
        public StomachOpening Opening => opening;
        LevelManager level;
        StomachOpening opening;
        Transform gate, carryHands;
        readonly LineRenderer[] arms = new LineRenderer[2];
        SpriteRenderer seal, key;
        LineRenderer[] chains;
        readonly SpriteRenderer[] fragments = new SpriteRenderer[2];
        Character carrier;
        SpriteRenderer carrierBody;
        Material lineMaterial;
        bool defeatFinished, ownsPause;
        float clock, previousScale = 1;
        bool FieldReady => level != null && !level.IsLevelEnded && !level.IsRunFlowPaused && Time.timeScale > 0 &&
            !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive &&
            !level.PlayerCharacter.IsDashing && !level.PlayerCharacter.IsPortalTravelling && !level.PlayerCharacter.IsPortalPoseHeld && level.PlayerCharacter.Skills?.IsCutin != true &&
            level.GetComponent<StageProgression>()?.StageNumber == 2 && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldActive;

        public static ColonExitSequence Ensure(LevelManager owner)
        {
            var result = owner.GetComponent<ColonExitSequence>() ?? owner.gameObject.AddComponent<ColonExitSequence>();
            result.level = owner;
            Active = result;
            result.InstallGate();
            return result;
        }
        void InstallGate()
        {
            if (gate != null || level == null || level.GetComponent<StageProgression>()?.StageNumber != 2 || FiniteStomachMap.Instance == null || !FiniteStomachMap.Instance.IsColon) return;
            opening = StomachOpening.Find(false);
            if (opening == null) return;
            gate = new GameObject("출구 봉인 · 금 체인과 왕의 자물쇠").transform;
            gate.SetParent(opening.transform, false); gate.position = opening.Center;
            seal = MakeSprite(gate, "SealLock", Mathf.Max(3.2f, opening.Radii.y * .72f), 490);
            seal.transform.localPosition = new Vector3(0, .15f, 0);
            float span = Mathf.Max(1.8f, opening.Radii.x * .9f);
            int linkCount = Mathf.CeilToInt(span * 2 * 1.146f / .34f) + 1;
            chains = new LineRenderer[linkCount * 2];
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < linkCount; i++)
                {
                    var link = MakeLine(gate, "Solid gold chain link", 488, .09f);
                    link.loop = true; link.positionCount = 17;
                    // Lower anchors sit on the lower wall shoulder, not beyond the map silhouette.
                    float x = Mathf.Lerp(side == 0 ? -span * .55f : -span, side == 0 ? span : span * .55f, i / (float)(linkCount - 1));
                    float y = (side == 0 ? 1 : -1) * x * .56f + .15f;
                    for (int j = 0; j < 17; j++)
                    {
                        float a = j * Mathf.PI * 2 / 17;
                        var p = new Vector2(Mathf.Cos(a) * .25f, Mathf.Sin(a) * (i % 2 == 0 ? .15f : .075f));
                        p = Quaternion.Euler(0, 0, side == 0 ? 29 : -29) * p;
                        link.SetPosition(j, new Vector3(x + p.x, y + p.y));
                    }
                    chains[side * linkCount + i] = link;
                }
            opening.SetOpened(0);
        }
        SpriteRenderer MakeSprite(Transform parent, string resource, float height, int order)
        {
            var sr = new GameObject(resource).AddComponent<SpriteRenderer>(); sr.transform.SetParent(parent, false);
            sr.sprite = Resources.Load<Sprite>("ColonEnding/" + resource);
            if (sr.sprite == null) Debug.LogError("Missing colon ending art: " + resource, this);
            else sr.transform.localScale = Vector3.one * height / Mathf.Max(.01f, sr.sprite.bounds.size.y);
            sr.sortingLayerName = "Monster Full"; sr.sortingOrder = order;
            return sr;
        }
        LineRenderer MakeLine(Transform parent, string label, int order, float width)
        {
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            var line = new GameObject(label).AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.useWorldSpace = false; line.sharedMaterial = lineMaterial; line.widthMultiplier = width;
            line.numCapVertices = 3; line.numCornerVertices = 3;
            line.startColor = new Color(1, .82f, .32f); line.endColor = new Color(.84f, .51f, .13f);
            line.sortingLayerName = "Monster Full"; line.sortingOrder = order; return line;
        }
        void Update()
        {
            if (level == null) return;
            if (level.GetComponent<StageProgression>()?.StageNumber != 2)
            { Destroy(this); return; }
            InstallGate();
            bool visible = !MiniStageRuntimeState.IsInsideMiniStage && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldVisible;
            if (gate != null) gate.gameObject.SetActive(visible);
            if (key != null) key.gameObject.SetActive(visible && State != ExitState.Open && State != ExitState.Demo && State != ExitState.Completed);
            if (carryHands != null) carryHands.gameObject.SetActive(visible && State == ExitState.Carrying);
            if (FieldReady) clock += Time.deltaTime;
        }
        void LateUpdate()
        {
            if (State != ExitState.Carrying || carrier == null || key == null) return;
            var head = (Vector2)carrier.transform.position + Vector2.up * 1.1f;
            Bounds visibleBody = new Bounds(carrier.transform.position, new Vector3(.5f, .7f, 0));
            if (carrierBody != null && carrierBody.sprite != null)
            {
                visibleBody = PromptSpriteGeometry.WorldBounds(carrierBody);
                head = new Vector2(visibleBody.center.x, visibleBody.max.y + .025f);
            }
            head.y += Mathf.Sin(clock * 5) * .035f;
            key.transform.position = head + Vector2.up * .28f;
            key.transform.rotation = Quaternion.Euler(0, 0, -90);
            if (carryHands != null) carryHands.position = head;
            if (carryHands != null)
            {
                float shoulder = Mathf.Clamp(visibleBody.size.x * .27f, .1f, .3f);
                float reach = Mathf.Clamp(visibleBody.size.y * .35f, .14f, .34f);
                float grip = Mathf.Clamp(visibleBody.size.x * .65f, .22f, .4f);
                for (int i = 0; i < arms.Length; i++)
                {
                    if (arms[i] == null) continue;
                    float side = i == 0 ? -1 : 1;
                    arms[i].widthMultiplier = Mathf.Clamp(visibleBody.size.x * .12f, .045f, .085f);
                    arms[i].SetPosition(0, new Vector3(side * shoulder, -reach));
                    arms[i].SetPosition(1, new Vector3(side * (grip + .045f), .06f));
                    arms[i].SetPosition(2, new Vector3(side * grip, .27f));
                }
            }
        }
        public void DropKey(Vector3 hand, Vector3 ground)
        {
            if (State != ExitState.Locked) return;
            InstallGate(); State = ExitState.Dropping;
            key = MakeSprite(transform, "KeyScepter", 2.35f, 520);
            key.transform.position = hand;
            var destination = FiniteStomachMap.Instance.ClampToFloor((Vector2)ground + Vector2.right * 1.5f, .8f);
            StartCoroutine(Drop(hand, destination));
        }
        IEnumerator Drop(Vector3 from, Vector3 to)
        {
            yield return Animate(.85f, p =>
            {
                key.transform.position = Vector3.Lerp(from, to, p) + Vector3.up * Mathf.Sin(p * Mathf.PI) * 1.2f;
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -115, p));
            });
            while (!defeatFinished) { if (!AliveInColon()) yield break; yield return null; }
            State = ExitState.Dropped;
            var trigger = new GameObject("지팡이 열쇠 줍기"); trigger.transform.SetParent(transform, false); trigger.transform.position = to;
            trigger.AddComponent<CircleCollider2D>().radius = 1.65f;
            Pickup = trigger.AddComponent<ColonKeyPickup>(); Pickup.Configure(this, key);
            trigger.AddComponent<MapMarker>().Configure(MapMarkerKind.Event, "변비왕의 지팡이 열쇠");
            ColonBossDirector.ShowNotice("봉인을 풀 열쇠", "지팡이 열쇠를 주워 좌측 하단 자물쇠로 가져가세요");
        }
        public void FinishDefeat() { defeatFinished = true; }
        bool AliveInColon() => level != null && !level.IsLevelEnded && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive && level.GetComponent<StageProgression>()?.StageNumber == 2;
        public bool TakeKey(Character player)
        {
            if (!CanPickup || player != level.PlayerCharacter || Pickup == null || Vector2.Distance(player.transform.position, Pickup.transform.position) > 2.4f) return false;
            State = ExitState.Carrying; carrier = player;
            var animator = player.GetComponentInChildren<SpriteAnimator>(); carrierBody = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            Pickup.gameObject.SetActive(false);
            carryHands = new GameObject("두 손으로 지팡이 열쇠 번쩍 들기").transform; carryHands.SetParent(transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = MakeLine(carryHands, "Raised arm and glove", 521, .115f);
                arms[side == -1 ? 0 : 1] = arm;
                arm.startColor = new Color(.98f, .81f, .65f); arm.endColor = new Color(1, .94f, .81f);
                arm.positionCount = 3;
                arm.SetPosition(0, new Vector3(side * .26f, -.45f)); arm.SetPosition(1, new Vector3(side * .43f, -.08f)); arm.SetPosition(2, new Vector3(side * .38f, .27f));
            }
            ColonBossDirector.ShowNotice("열쇠를 얻었습니다", "좌측 하단 자물쇠 앞에서 상호작용하세요");
            LateUpdate(); return true;
        }
        public bool InteractAtGate(Character player, StomachOpening target)
        {
            if (!CanInteract || player != level.PlayerCharacter || target != opening || Vector2.Distance(player.transform.position, opening.transform.position) > 2.6f) return false;
            if (State == ExitState.Open) { ShowDemo(); return true; }
            if (State != ExitState.Carrying)
            { ColonBossDirector.ShowNotice("왕의 봉인", defeatFinished ? "떨어진 지팡이 열쇠를 먼저 주워주세요" : "변비왕이 가진 지팡이 열쇠가 필요합니다"); return true; }
            State = ExitState.Unlocking; ownsPause = true; level.SetRunFlowPaused(true); level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            StartCoroutine(Unlock()); return true;
        }
        IEnumerator Animate(float seconds, Action<float> sample)
        {
            for (float age = 0; age < seconds;)
            {
                if (!AliveInColon()) yield break;
                if (Time.timeScale > 0 && !MiniStageRuntimeState.IsInsideMiniStage && !StageEntryLoading.IsLoading && (ownsPause || !level.IsRunFlowPaused))
                { age += Time.deltaTime; sample(Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / seconds))); }
                yield return null;
            }
        }
        IEnumerator Unlock()
        {
            if (carryHands != null) carryHands.gameObject.SetActive(false);
            Vector3 start = key.transform.position, end = seal.transform.position;
            Vector3 scale = key.transform.localScale;
            Vector3 teeth = key.sprite != null ? new Vector3(0, key.sprite.bounds.min.y + key.sprite.bounds.size.y * .12f, 0) : Vector3.down;
            Vector3 socket = end + Vector3.down * .2f;
            yield return Animate(.65f, p =>
            {
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(-90, -25, p));
                Vector3 inserted = socket - key.transform.rotation * Vector3.Scale(teeth, scale);
                key.transform.position = Vector3.Lerp(start, inserted, p);
            });
            if (!AliveInColon()) yield break;
            yield return Animate(.4f, p =>
            {
                key.transform.localScale = scale * Mathf.Lerp(1, .75f, p);
                key.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(-25, 65, p));
                key.transform.position = socket - key.transform.rotation * Vector3.Scale(teeth, key.transform.localScale);
            });
            if (!AliveInColon()) yield break;
            key.enabled = false;
            // Two sprite fragments split at the seal's own pivot. The unlocked shackle and chain links spring apart.
            var bits = fragments;
            var source = seal.sprite;
            if (source != null)
                for (int i = 0; i < 2; i++)
                {
                    Rect r = source.rect; float half = r.width * .5f; r.x += half * i; r.width = half;
                    var sprite = Sprite.Create(source.texture, r, new Vector2(i == 0 ? 1 : 0, .5f), source.pixelsPerUnit);
                    bits[i] = new GameObject("깨지는 자물쇠 조각").AddComponent<SpriteRenderer>(); bits[i].transform.SetParent(gate, false);
                    bits[i].sprite = sprite; bits[i].transform.position = end; bits[i].transform.localScale = seal.transform.localScale;
                    bits[i].sortingLayerName = seal.sortingLayerName; bits[i].sortingOrder = seal.sortingOrder + 1;
                }
            seal.enabled = false;
            yield return Animate(.8f, p =>
            {
                for (int i = 0; i < bits.Length; i++) if (bits[i] != null)
                { bits[i].transform.position = end + new Vector3((i == 0 ? -1 : 1) * p * 1.7f, Mathf.Sin(p * Mathf.PI) * .6f - p * 1.3f); bits[i].transform.rotation = Quaternion.Euler(0, 0, (i == 0 ? 1 : -1) * p * 80); bits[i].color = new Color(1, 1, 1, 1 - p); }
                foreach (var chain in chains) { chain.transform.localPosition = Vector3.down * p * p * 1.8f; chain.startColor = chain.endColor = new Color(1, .74f, .23f, 1 - p); }
            });
            ClearFragments();
            if (!AliveInColon()) yield break;
            yield return Animate(1.1f, p => opening.SetOpened(p));
            if (!AliveInColon()) yield break;
            State = ExitState.Open; ReleasePause();
            ColonBossDirector.ShowNotice("출구가 열렸습니다", "구멍 앞에서 상호작용하면 데모 탐험을 마칩니다");
        }
        void ShowDemo()
        {
            State = ExitState.Demo; ownsPause = true; level.SetRunFlowPaused(true); level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            previousScale = Time.timeScale; Time.timeScale = 0;
            Dialogue = ApothecaryUI.Instance.BuildColonDemoCompletionDialogue();
            StartCoroutine(WaitForConfirmation());
        }
        IEnumerator WaitForConfirmation()
        {
            while (Dialogue != null && !Dialogue.Confirmed) yield return null;
            if (Dialogue == null || !AliveInColon()) yield break;
            State = ExitState.Completed;
            level.LevelPassed(null);
        }
        void ReleasePause()
        {
            if (!ownsPause || level == null) return;
            ownsPause = false; level.SetRunFlowPaused(false); level.EntityManager.SetFieldMonsterRuntimeSuspended(false);
        }
        void OnDestroy()
        {
            if (Active == this) Active = null;
            ClearFragments();
            if (State != ExitState.Completed) { ReleasePause(); if (State == ExitState.Demo) Time.timeScale = previousScale; }
            if (gate != null) Destroy(gate.gameObject);
            if (key != null) Destroy(key.gameObject);
            if (Pickup != null) Destroy(Pickup.gameObject);
            if (carryHands != null) Destroy(carryHands.gameObject);
            if (Dialogue != null) Destroy(Dialogue.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
        void ClearFragments()
        {
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] != null) { Destroy(fragments[i].sprite); Destroy(fragments[i].gameObject); }
                fragments[i] = null;
            }
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
