using System.Collections;
using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class ConstipationKingDirector : MonoBehaviour
    {
        public const float SummonDuration = 3.8f;
        public static ConstipationKingDirector Active { get; private set; }
        public ConstipationKingRuntime Boss { get; private set; }
        public KingSummonThrone Throne { get; private set; }
        public bool Spawned { get; private set; }
        public bool Defeated { get; private set; }
        public bool Summoning { get; private set; }
        public bool ProtectsPlayer => Summoning;
        public bool Protects(Character player) => Summoning && level != null && level.PlayerCharacter == player;
        public Vector2 SummonAnchor => Throne != null ? (Vector2)Throne.transform.position : Vector2.zero;
        public bool CanSummon => !Spawned && !Summoning && level != null && level.EntityManager != null && !level.IsLevelEnded && !level.IsRunFlowPaused &&
            !StageEntryLoading.IsLoading && Time.timeScale > 0 && !MiniStageRuntimeState.IsInsideMiniStage &&
            level.PlayerCharacter != null && level.PlayerCharacter.IsAlive && !level.PlayerCharacter.IsDashing &&
            !level.PlayerCharacter.IsPortalTravelling && !level.PlayerCharacter.IsPortalPoseHeld &&
            level.PlayerCharacter.Skills?.IsCutin != true && FiniteStomachMap.Instance != null &&
            FiniteStomachMap.Instance.IsColon && FiniteStomachMap.Instance.FieldActive && FiniteStomachMap.Instance.FieldVisible &&
            StageProgression.Ensure(level).StageNumber == 2;
        LevelManager level; bool inColon, ownsPause;
        Vector2 playerSummonFrom, playerSummonTo;
        public static ConstipationKingDirector Ensure(LevelManager owner)
        {
            var d = owner.GetComponent<ConstipationKingDirector>() ?? owner.gameObject.AddComponent<ConstipationKingDirector>();
            d.level = owner; Active = d; d.InstallThrone(); return d;
        }
        void Update()
        {
            if (level == null) return;
            bool colon = StageProgression.Ensure(level).StageNumber == 2 && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.IsColon;
            if (!colon) { if (inColon) ResetEncounter(); inColon = false; return; }
            inColon = true; Active = this; ColonExitSequence.Ensure(level); InstallThrone();
            // The empty throne is an invitation. Passing time cannot start combat.
        }
        void InstallThrone()
        {
            var map = FiniteStomachMap.Instance;
            if (Throne != null || Spawned || level == null || map == null || !map.IsColon || StageProgression.Ensure(level).StageNumber != 2) return;
            var opening = StomachOpening.Find(false); if (opening == null) return;
            var at = ColonOpeningGeometry.ThroneCenter(map);
            Throne = KingSummonThrone.Create(this, at);
        }
        public bool TrySpawn() => TrySummon(level != null ? level.PlayerCharacter : null);
        public bool TrySummon(Character player)
        {
            InstallThrone();
            if (!CanSummon || player != level.PlayerCharacter || Throne == null ||
                Vector2.Distance(player.transform.position, Throne.transform.position) > 2.65f) return false;
            Spawned = Summoning = true; ownsPause = true;
            playerSummonFrom = player.transform.position;
            playerSummonTo = FindSummonViewingPoint(playerSummonFrom);
            player.Move(Vector2.zero);
            level.SetRunFlowPaused(true); level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            Throne.BeginSummoning();
            Boss = ConstipationKingRuntime.Spawn(level, Throne.transform.position, this);
            if (Boss == null) { Spawned = Summoning = false; ReleasePause(); Throne.RestoreInvitation(); return false; }
            StomachOpening.Find(false)?.SetOpened(0);
            level.NotifyExternalFinalBossSpawned(); GameAudioManager.StartBossAudio();
            // The invitation can be used during any field event. Its summon announcement
            // must replace the paused field notice instead of waiting behind its queue.
            Object.FindObjectOfType<StageEventToastUI>()?.ShowImmediateEncounter("왕의 변기가 역류합니다|변기 안에서 변비왕이 깨어나고 있습니다"); return true;
        }
        public IEnumerator PresentSummon(ConstipationKingRuntime boss)
        {
            if (Throne == null) yield break;
            boss.Visual.Face(Vector2.right); boss.Visual.SetPose(KingPose.Idle);
            boss.Visual.SetEmergence(0, Throne.BowlWorldY, 0);
            for (float elapsed = 0; elapsed < SummonDuration;)
            {
                // Spawn starts Arrival synchronously before TrySummon receives its
                // return value. A null Boss here is the in-progress assignment.
                if (!Summoning || (Boss != null && boss != Boss) || Throne == null) yield break;
                if (level == null || level.IsLevelEnded || level.PlayerCharacter == null || !level.PlayerCharacter.IsAlive)
                {
                    Summoning = false; ReleasePause(); boss.Visual.SetVisible(false); Throne.CompleteSummoning(); yield break;
                }
                bool visible = !MiniStageRuntimeState.IsInsideMiniStage && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldVisible;
                if (boss.Visual.Visible != visible) boss.Visual.SetVisible(visible);
                Throne.SetFieldVisible(visible);
                if (Time.timeScale > 0 && visible && !StageEntryLoading.IsLoading)
                {
                    elapsed += Time.deltaTime; float q = Mathf.Clamp01(elapsed / SummonDuration);
                    StepPlayerAside(elapsed);
                    float emerge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.21f, .78f, q));
                    float settling = Mathf.InverseLerp(.79f,1f,q);
                    float settle = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.90f,settling));
                    Throne.Sample(q, Time.deltaTime); boss.Visual.SetEmergence(emerge, Throne.BowlWorldY, settle); boss.Visual.Tick(Time.deltaTime);
                }
                yield return null;
            }
            boss.Visual.SetVisible(true); boss.Visual.SetEmergence(1, Throne.BowlWorldY, 1);
            level.PlayerCharacter.StartIdleAnimation();
            Throne.CompleteSummoning(); Summoning = false; ReleasePause();
            ColonBossDirector.ShowNotice("변비왕 등장", "변비왕의 지팡이 열쇠로 좌측 하단 봉인을 풀어주세요");
        }
        public void NotifyDefeated(ConstipationKingRuntime boss)
        {
            if (Defeated || !Spawned || boss == null || boss != Boss || !boss.Dead || level == null || level.IsLevelEnded || StageProgression.Ensure(level).StageNumber != 2) return;
            Defeated = true; ColonExitSequence.Ensure(level).FinishDefeat(); GameAudioManager.FinishStageBossAudio();
        }
        public void CancelSummon(ConstipationKingRuntime boss, bool restoreInvitation = false)
        {
            if (!Summoning || boss != Boss) return;
            Summoning = false; ReleasePause();
            if (boss != null) boss.Visual.SetVisible(false);
            bool canRetry = restoreInvitation && level != null && !level.IsLevelEnded && level.PlayerCharacter != null && level.PlayerCharacter.IsAlive &&
                FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.IsColon;
            if (canRetry)
            {
                Spawned = false; Boss = null; Throne?.RestoreInvitation();
                if (boss != null) Destroy(boss.gameObject);
            }
            else Throne?.CompleteSummoning();
        }
        public void BeginDefeat(ConstipationKingRuntime boss)
        {
            if (!Spawned || Defeated || boss == null || boss != Boss || !boss.Dead || level == null || level.IsLevelEnded) return;
            Vector3 point = boss.Visual != null ? (Vector3)boss.Visual.KeyDropPoint : boss.transform.position;
            float angle = boss.Visual != null ? boss.Visual.KeyDropAngle : 0;
            float height = boss.Visual != null ? boss.Visual.KeyDropHeight : 2.35f;
            bool flipped = boss.Visual != null && boss.Visual.KeyDropFlipped;
            boss.Visual?.ReleaseKeyScepter(); ColonExitSequence.Ensure(level).DropKey(point, boss.transform.position, angle, height, flipped);
        }
        Vector2 FindSummonViewingPoint(Vector2 from)
        {
            var map = FiniteStomachMap.Instance; Vector2 anchor = Throne.transform.position;
            var direction = from - anchor; if (direction.sqrMagnitude < .04f) direction = Vector2.right;
            direction.Normalize(); Vector2 best = from; float bestScore = float.PositiveInfinity;
            for (int i = 0; i < 16; i++)
            {
                float angle = (i % 2 == 0 ? i / 2 : -(i + 1) / 2) * Mathf.PI / 8;
                Vector2 d = new Vector2(direction.x*Mathf.Cos(angle)-direction.y*Mathf.Sin(angle), direction.x*Mathf.Sin(angle)+direction.y*Mathf.Cos(angle));
                var candidate = map.ClampToFloor(anchor + d * 3.5f, .6f);
                if (Vector2.Distance(candidate, anchor + Vector2.up*1.7f) < 2.8f || !ColonAttackWall.Clear(from,candidate,.3f)) continue;
                float score = Vector2.Distance(from,candidate) + i*.035f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }
        void StepPlayerAside(float elapsed)
        {
            var player = level.PlayerCharacter;
            float q = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.7f));
            Vector2 point = Vector2.Lerp(playerSummonFrom,playerSummonTo,q);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = point; body.velocity = Vector2.zero; }
            player.transform.position = point;
            player.LookDirection = ((Vector2)Throne.transform.position-point).normalized;
            if (q < 1 && (playerSummonFrom-playerSummonTo).sqrMagnitude>.01f) player.StartWalkAnimation(); else player.StartIdleAnimation();
        }
        void ReleasePause()
        {
            if (!ownsPause) return;
            ownsPause = false;
            // A new stage or game-over transition owns its own pause at this point.
            if (level == null || level.IsLevelEnded || level.GetComponent<StageProgression>()?.StageNumber != 2) return;
            if (level.PlayerCharacter != null) level.PlayerCharacter.StartIdleAnimation();
            level.SetRunFlowPaused(false); level.EntityManager.SetFieldMonsterRuntimeSuspended(false);
        }
        void ResetEncounter()
        {
            Summoning = false; ReleasePause();
            if (Boss != null) { Boss.gameObject.SetActive(false); Destroy(Boss.gameObject); }
            if (Throne != null) { Throne.gameObject.SetActive(false); Destroy(Throne.gameObject); }
            Boss = null; Throne = null; Spawned = Defeated = false;
        }
        void OnDisable()
        {
            // Component-only disabling is used by diagnostic tools; the running
            // boss owns its coroutine. Disabling the level object cancels ownership.
            if (!gameObject.activeInHierarchy && Summoning)
            {
                CancelSummon(Boss);
                if (Boss != null) Boss.gameObject.SetActive(false);
            }
        }
        void OnDestroy() { ResetEncounter(); if (Active == this) Active = null; }
    }
}
