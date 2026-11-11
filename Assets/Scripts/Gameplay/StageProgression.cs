using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class StageProgression:MonoBehaviour
    {
        public static bool StageTwoEnabled
        {
            get
            {
#if PROJECT_GC_STAGE_ONE_ONLY
                return false;
#elif UNITY_EDITOR
                return !PreviewWebDemoEnding;
#else
                return true;
#endif
            }
        }
#if UNITY_EDITOR
        public static bool PreviewWebDemoEnding;
#endif
        public int StageNumber {get;private set;}=1;
        public static bool StageZeroEnabled => StageTwoEnabled;
        StomachMapDefinition oralDefinition;
        LevelBlueprint oralBlueprint;
        LevelBlueprint preOralBlueprint;
        readonly Dictionary<Behaviour,bool> preOralDirectors=new Dictionary<Behaviour,bool>();
        public bool BeginDirectOral()
        {
            if(!StageZeroEnabled||level==null||!level.InitialFieldObjectsReady||StageNumber!=1||Travelling)return false;
            if(FiniteStomachMap.Instance==null||Resources.Load<StomachMapDefinition>("StomachMap/Stage1")==null||level.CurrentLevelBlueprint==null||level.GetComponent<FieldObjectLayout>()==null)return false;
            Travelling=true;level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            TutorialGuide.Instance?.Close();
            RunScore.Exclude("스테이지 0 구강 직접 입장");
            StartCoroutine(EnterOral());return true;
        }
        IEnumerator EnterOral()
        {
            var loading=StageEntryLoading.CreateOralTransfer(level.PlayerCharacter.Blueprint);
            TransitionPhase="oral-loading";yield return null;
            var map=FiniteStomachMap.Instance;
            var source=Resources.Load<StomachMapDefinition>("StomachMap/Stage1");
            oralDefinition=Instantiate(source);oralDefinition.name="Stage0 Oral Runtime";
            oralDefinition.oral=true;oralDefinition.colon=false;
            oralDefinition.worldWidth=source.worldWidth/Mathf.Sqrt(6);oralDefinition.wallWidth=0;oralDefinition.climbableWidth=.5f;
            oralDefinition.startUV=new Vector2(.5f,.44f);
            StageNumber=0;
            map.ChangeDefinition(oralDefinition);
            var landing=OralMapGeometry.TongueCenter(map);
            level.PlayerCharacter.transform.position=landing;
            var rb=level.PlayerCharacter.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=landing;rb.velocity=Vector2.zero;}
            foreach(var collectable in FindObjectsOfType<Collectable>())collectable.DissolveInAcidLake();
            foreach(var altar in FindObjectsOfType<FinalBossSummonInteractable>()){altar.gameObject.SetActive(false);Destroy(altar.gameObject);}
            preOralBlueprint=level.CurrentLevelBlueprint;
            try { oralBlueprint=OralMonsterRoster.Create(preOralBlueprint); }
            catch(System.Exception error)
            {Debug.LogException(error);loading.ReturnToMenu("구강 몬스터 준비에 실패했습니다. 메인 화면으로 돌아갑니다.");}
            if(oralBlueprint==null)yield break;
            oralBlueprint.name="Stage0 Oral Field";
            oralBlueprint.levelTime=OralSpecialSpawnProfile.Duration;oralBlueprint.normalSpawnPopulationLimit=90;
            level.BeginNextStage(oralBlueprint);
            preOralDirectors.Clear();
            foreach(var director in FindObjectsOfType<StageEventDirector>())SuspendOralDirector(director);


            SuspendOralDirector(level.GetComponent<SupplyCapsuleDirector>());
            // Blood clots, elite terminals and stage-one special encounters remain active.
            // Only stomach-specific environmental events are suspended.

            level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            loading.SetProgress(.55f);yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            if(!level.InitialFieldObjectsReady)
            {Debug.LogError("[OralStage] Field placement failed; returning to the menu.");loading.ReturnToMenu("구강 필드 준비에 실패했습니다. 메인 화면으로 돌아갑니다.");yield break;}
            OralStageRuntime runtime=null;
            try { runtime=OralStageRuntime.Install(level); }
            catch(System.Exception error)
            {Debug.LogException(error);loading.ReturnToMenu("구강 이벤트 준비에 실패했습니다. 메인 화면으로 돌아갑니다.");}
            if(runtime==null)yield break;
            runtime.TeleportPlayer(landing,OralZone.Tongue);
            loading.SetProgress(1);yield return null;
            Travelling=false;TransitionPhase="field";
            level.EntityManager.SetFieldMonsterRuntimeSuspended(false);level.SetRunFlowPaused(false);
            loading.CompleteStageTransfer();
        }
        public bool BeginOralToStomach()
        {
            if(StageNumber!=0||Travelling||level==null||level.IsLevelEnded||level.IsRunFlowPaused||preOralBlueprint==null)return false;
            if(OralStageRuntime.Instance!=null&&(OralStageRuntime.Instance.OnCeiling||OralStageRuntime.Instance.IsAirborne))return false;
            Travelling=true;level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            StartCoroutine(ReturnFromOral());return true;
        }
        IEnumerator ReturnFromOral()
        {
            var loading=StageEntryLoading.CreateStomachTransfer(level.PlayerCharacter.Blueprint);
            TransitionPhase="stomach-loading";yield return null;
            var oral=GetComponent<OralStageRuntime>();var events=GetComponent<OralFieldEvents>();var visual=GetComponent<OralMapVisual>();
            if(events!=null){events.enabled=false;Destroy(events);}
            if(visual!=null){visual.enabled=false;Destroy(visual);}
            if(oral!=null){oral.enabled=false;Destroy(oral);}
            yield return null;
            foreach(var collectable in FindObjectsOfType<Collectable>())collectable.DissolveInAcidLake();
            StageNumber=1;FirstBossDefeated=false;
            var map=FiniteStomachMap.Instance;var definition=Resources.Load<StomachMapDefinition>("StomachMap/Stage1");
            map.ChangeDefinition(definition);var landing=map.UVToWorld(definition.startUV);
            level.PlayerCharacter.transform.position=landing;var rb=level.PlayerCharacter.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=landing;rb.velocity=Vector2.zero;}
            level.BeginNextStage(preOralBlueprint);
            foreach(var prior in preOralDirectors)if(prior.Key!=null)prior.Key.enabled=prior.Value;
            preOralDirectors.Clear();
            var supplies=level.GetComponent<SupplyCapsuleDirector>();if(supplies!=null&&supplies.enabled)supplies.RestartForStage();
            level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            if(!level.InitialFieldObjectsReady)
            {Debug.LogError("[OralStage] Stomach placement failed; returning to the menu.");loading.ReturnToMenu("위장 필드 준비에 실패했습니다. 메인 화면으로 돌아갑니다.");yield break;}
            OralMonsterRoster.Release(oralBlueprint);oralBlueprint=null;
            if(oralDefinition!=null)Destroy(oralDefinition);oralDefinition=null;
            loading.SetProgress(1);yield return null;
            Travelling=false;TransitionPhase="field";level.EntityManager.SetFieldMonsterRuntimeSuspended(false);level.SetRunFlowPaused(false);
            loading.CompleteStageTransfer();
        }
        void SuspendOralDirector(Behaviour director)
        {
            if(director==null)return;
            preOralDirectors[director]=director.enabled;
            director.enabled=false;
        }
        public bool IsDirectColonStart {get;private set;}
        public bool FirstBossDefeated {get;private set;}
        public bool Travelling {get;private set;}
        public bool CapsuleBoarded {get;private set;}
        public bool SuitEquipped=>false; // Retired remnant API, kept for old editor tooling.
        public bool AtOutlet {get;private set;}
        public StageExitPortal Portal {get;private set;}
        public BossRemnantPickup Remnant=>null;
        public AmphibiousCapsule Capsule {get;private set;}
        public FoodBossKind DefeatedKind {get;private set;}
        public string TransitionPhase {get;private set;}="field";
        LevelManager level;StageExitDefinition definition;Transform escapeHole;
        bool entering;LevelBlueprint stageTwo;SpiralStageTransition iris;SpriteRenderer[] hiddenPassenger;bool[] hiddenStates;
        public static StageProgression Ensure(LevelManager owner){var p=owner.GetComponent<StageProgression>()??owner.gameObject.AddComponent<StageProgression>();p.level=owner;return p;}
        void Awake()
        {
            level=GetComponent<LevelManager>();definition=Resources.Load<StageExitDefinition>("StageExitDefinition");
        }
        public Transform BeginEscape(FoodBossKind kind,Vector3 position)
        {
            DefeatedKind=kind;if(escapeHole!=null)return escapeHole;
            var go=new GameObject("보스가 뚫은 추격 구멍");go.transform.position=position;escapeHole=go.transform;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=BossTravelArt.Hole??definition.crater;
            go.transform.localScale=Vector3.one*(4.8f/sr.sprite.bounds.size.x)*.001f;
            GroundVisualSorting.ApplyBackground(sr,-10);return escapeHole;
        }
        public bool FinalBossDefeated(Vector3 position,float measuredDps,FoodBossKind kind=FoodBossKind.Rolpangi)
        {
            // This callback belongs to the five stage-one food bosses. Optional colon bosses
            // never open an exit; Byunbi King is the mandatory stage-two final boss (separate work).
            if(StageNumber!=1)return true;
            if(FirstBossDefeated||level==null||level.IsLevelEnded)return true;
            if(definition==null||definition.crater==null)return false;
            FirstBossDefeated=true;DefeatedKind=kind;level.NotifyExternalFinalBossSpawned();
            for(int i=0;i<3;i++){float a=(210+i*60)*Mathf.Deg2Rad;var p=FiniteStomachMap.SafeSpawn((Vector2)position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*3.2f,.8f);level.EntityManager.SpawnChest(definition.rewardChest??level.CurrentLevelBlueprint.chestBlueprint,p);}
            var hole=escapeHole??BeginEscape(kind,position);var sr=hole.GetComponent<SpriteRenderer>();hole.localScale=Vector3.one*(4.8f/sr.sprite.bounds.size.x);
            hole.gameObject.AddComponent<CircleCollider2D>().radius=.9f;Portal=hole.gameObject.AddComponent<StageExitPortal>();Portal.Configure(this);
            hole.gameObject.AddComponent<MapMarker>().Configure(MapMarkerKind.Event,StageNumber==1?"보스 추격 구멍 · 스페셜 스테이지 준비 중":"탈출구 · 탐험 완료");
            if(StageNumber==1)StartCoroutine(DeliverCapsule());
            GameAudioManager.FinishStageBossAudio();return true;
        }
        public bool CanEnter(Character p)=>FirstBossDefeated&&!Travelling&&level!=null&&!level.IsLevelEnded&&!level.IsRunFlowPaused&&!MiniStageRuntimeState.IsInsideMiniStage&&BloodClotTravel.CanTravel(p)&&Time.timeScale>0;
        public bool TryEnter(Character player,StageExitPortal portal)
        {
            if(StageNumber==1||portal==null||portal!=Portal||AcidLakeEvent.Contains(portal.transform.position)||!CanEnter(player))return false;
            Travelling=true;level.LevelPassed(null);return true;
        }
        // Old remnants never spawn or initiate travel anymore.
        public bool TryEquip(Character player,BossRemnantPickup pickup)=>false;
        public bool CanUseOutlet(StomachOpening o)=>false;
        public bool TryUseOutlet(Character player,StomachOpening o)=>false;
        IEnumerator DeliverCapsule()
        {
            while(!BloodClotTravel.CanTravel(level.PlayerCharacter)||Time.timeScale<=0||level.IsRunFlowPaused)yield return null;
            var player=level.PlayerCharacter;Travelling=true;level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            var travel=BloodClotTravel.Ensure(player);travel.Begin();travel.CapsulePose(player.transform.position,1);
            var point=FiniteStomachMap.SafeSpawn((Vector2)player.transform.position+new Vector2(-2.4f,.35f),2f);
            var lake=AcidLakeEvent.Instance;if(lake!=null&&lake.Flooding)point=lake.NearestDryLand(point,1.8f);
            Capsule=AmphibiousCapsule.Create(this,point);TransitionPhase="capsule-descent";yield return Capsule.Descend();
            if(!CapsuleDoctorDialogue.Seen)
            {
                TransitionPhase="doctor";var dialogue=CapsuleDoctorDialogue.Show(player);
                while(dialogue!=null&&!dialogue.Finished)yield return null;
                if(dialogue!=null)Destroy(dialogue.gameObject);
            }
            travel.Cancel();Travelling=false;TransitionPhase="capsule-ready";Capsule.Ready=true;
            level.EntityManager.SetFieldMonsterRuntimeSuspended(false);level.SetRunFlowPaused(false);
        }
        public bool TryBoard(Character player,AmphibiousCapsule capsule)
        {
            if(capsule!=Capsule||!capsule.Ready||StageNumber!=1||!CanEnter(player))return false;
            capsule.Ready=false;Travelling=true;CapsuleBoarded=true;level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);StartCoroutine(BoardAndTravel(player));return true;
        }
        IEnumerator BoardAndTravel(Character player)
        {
            var travel=BloodClotTravel.Ensure(player);travel.Begin();Vector3 start=player.transform.position;
            TransitionPhase="hatch-open";for(float t=0;t<.7f;t+=Time.deltaTime){Capsule.SetHatch(Mathf.SmoothStep(0,1,t/.7f));travel.CapsulePose(start,1);yield return null;}Capsule.SetHatch(1);
            TransitionPhase="boarding";Vector3 door=Capsule.BoardingPoint-Vector3.up*travel.ReferenceHeight*.5f;
            for(float t=0;t<1.1f;t+=Time.deltaTime){float p=Mathf.SmoothStep(0,1,t/1.1f);travel.CapsulePose(Vector3.Lerp(start,door,p),1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,p)));yield return null;}
            hiddenPassenger=player.GetComponentsInChildren<SpriteRenderer>(true);hiddenStates=new bool[hiddenPassenger.Length];for(int i=0;i<hiddenPassenger.Length;i++){hiddenStates[i]=hiddenPassenger[i].forceRenderingOff;hiddenPassenger[i].forceRenderingOff=true;}
            TransitionPhase="hatch-close";for(float t=0;t<.65f;t+=Time.deltaTime){Capsule.SetHatch(1-Mathf.SmoothStep(0,1,t/.65f));yield return null;}Capsule.SetHatch(0);
            var outlet=StomachOpening.Find(false);outlet.SetOpened(0);
            Vector3 from=Capsule.transform.position,to=(Vector3)(outlet.FrontLip+outlet.Inward*1.25f);
            TransitionPhase="capsule-travel";
            for(float t=0;t<8;t+=Time.deltaTime){Capsule.transform.position=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t/8));travel.CapsulePose(Capsule.transform.position,0);yield return null;}
            Capsule.transform.position=to;travel.CapsulePose(to,0);AtOutlet=true;TransitionPhase="outlet-open";
            CapsuleOutletReveal.Create(outlet);var exitShot=StageTravelCamera.Begin(outlet,to);
            yield return new WaitForSeconds(.35f);
            for(float t=0;t<1.25f;t+=Time.deltaTime){outlet.SetOpened(Mathf.SmoothStep(0,1,t/1.25f));yield return null;}outlet.SetOpened(1);
            if(!StageTwoEnabled)
            {
                TransitionPhase="demo-complete";
                level.CompleteWebDemo();
                var dialogue=ApothecaryUI.Instance.BuildDemoCompletionDialogue();
                while(dialogue!=null&&!dialogue.Confirmed)yield return null;
                if(dialogue!=null)Destroy(dialogue.gameObject);
                iris=SpiralStageTransition.Create();TransitionPhase="iris-close";yield return iris.Animate(true);
                if(exitShot!=null)Destroy(exitShot);
                Time.timeScale=1;
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                yield break;
            }
            iris=SpiralStageTransition.Create();TransitionPhase="iris-close";yield return iris.Animate(true);if(exitShot!=null)Destroy(exitShot);
            AtOutlet=false;entering=true;yield return EnterColon(player,outlet);
        }
        public bool BeginDirectColon(StageEntryLoading departureLoading=null)
        {
            if(!StageTwoEnabled||level==null||!level.InitialFieldObjectsReady||StageNumber!=1||Travelling)return false;
            IsDirectColonStart=true;
            Travelling=true;entering=true;level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            TutorialGuide.Instance?.Close();
            iris=SpiralStageTransition.Create();iris.SetClosed(1);
            if(departureLoading!=null)departureLoading.ContinueAsColonTransfer();
            StartCoroutine(EnterColon(level.PlayerCharacter,null,departureLoading));return true;
        }
        IEnumerator EnterColon(Character player,StomachOpening outlet,StageEntryLoading departureLoading=null)
        {
            var travel=BloodClotTravel.Ensure(player);Transform anchor;
            TransitionPhase="loading";var loading=departureLoading!=null?departureLoading:StageEntryLoading.CreateStageTransfer(player.Blueprint);yield return null;if(iris!=null)iris.SetVisible(false);yield return null;
            if(Capsule!=null){Destroy(Capsule.gameObject);Capsule=null;}
            foreach(var chest in FindObjectsOfType<Chest>())level.EntityManager.DespawnChest(chest);
            foreach(var p in FindObjectsOfType<Collectable>())p.DissolveInAcidLake();
            foreach(var p in FindObjectsOfType<SupplyRelicOrb>())Destroy(p.gameObject);
            foreach(var p in FindObjectsOfType<SupplyCapsule>())Destroy(p.gameObject);
            if(Portal!=null){Portal.gameObject.SetActive(false);Destroy(Portal.gameObject);}Portal=null;escapeHole=null;
            StageNumber=2;FirstBossDefeated=false;CapsuleBoarded=false;
            var map=FiniteStomachMap.Instance;var nextMap=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");
            if(nextMap==null)throw new System.InvalidOperationException("ColonMap/Stage2 is missing");
            map.ChangeDefinition(nextMap);loading.SetTransferProgress(.4f);yield return null;
            foreach(var altar in FindObjectsOfType<FinalBossSummonInteractable>())
            {
                altar.gameObject.SetActive(false);Destroy(altar.gameObject);
            }
            stageTwo=ColonMonsterRoster.Create(level);
            level.BeginNextStage(stageTwo);level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);level.GetComponent<SupplyCapsuleDirector>()?.RestartForStage();
            var inlet=StomachOpening.Find(true);inlet.SetOpened(0);var landing=map.ClampToField(inlet.FrontLip+Vector2.down*2,map.WallWidth+.6f);
            travel.SwimPose(landing,0);travel.Visual.color=Color.clear;loading.SetTransferProgress(.7f);
            yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            if(!level.InitialFieldObjectsReady)throw new System.InvalidOperationException("Stage 2 field placement failed");
            for(float t=0;t<.7f;t+=Time.unscaledDeltaTime){loading.SetTransferProgress(Mathf.Lerp(.7f,1,t/.7f));yield return null;}
            loading.SetTransferProgress(1);
            var shot=StageTravelCamera.Begin(inlet,landing);
            if(iris!=null){iris.SetClosed(1);iris.SetVisible(true);}
            loading.CompleteStageTransfer();yield return null;TransitionPhase="iris-open";
            if(iris!=null){yield return iris.Animate(false);Destroy(iris.gameObject);iris=null;}
            TransitionPhase="opening";
            for(float t=0;t<1.2f;t+=Time.deltaTime){inlet.SetOpened(t/1.2f);yield return null;}inlet.SetOpened(1);
            RestorePassenger();
            anchor=new GameObject("Colon inlet ejection anchor").transform;anchor.position=inlet.Center;
            TransitionPhase="eject";yield return travel.Eject(anchor,landing);Destroy(anchor.gameObject);
            travel.HoldForInteraction();if(shot!=null)shot.Release();TransitionPhase="settle";yield return new WaitForSeconds(1);
            travel.ReleasePose();Travelling=false;entering=false;TransitionPhase="field";level.EntityManager.SetFieldMonsterRuntimeSuspended(false);level.SetRunFlowPaused(false);GameAudioManager.ExitMiniStageAudio();
            ColonBossDirector.ShowNotice("대장 탐험 시작","거주지를 깨워 선택 미니보스에 도전하세요 · 전부 처치는 선택사항");
        }
        void RestorePassenger(){if(hiddenPassenger==null)return;for(int i=0;i<hiddenPassenger.Length;i++)if(hiddenPassenger[i]!=null)hiddenPassenger[i].forceRenderingOff=hiddenStates[i];hiddenPassenger=null;}
        void OnDestroy(){RestorePassenger();if(stageTwo!=null)Destroy(stageTwo);if(oralDefinition!=null)Destroy(oralDefinition);if(oralBlueprint!=null)OralMonsterRoster.Release(oralBlueprint);if(Capsule!=null)Destroy(Capsule.gameObject);if(iris!=null)Destroy(iris.gameObject);}
    }
}
