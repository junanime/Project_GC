using System.Collections;
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
        string altarSettings;Sprite altarSprite;Material altarMaterial;Vector3 altarScale;int altarLayer,altarOrder;
        public static StageProgression Ensure(LevelManager owner){var p=owner.GetComponent<StageProgression>()??owner.gameObject.AddComponent<StageProgression>();p.level=owner;return p;}
        void Awake()
        {
            level=GetComponent<LevelManager>();definition=Resources.Load<StageExitDefinition>("StageExitDefinition");
            var altar=FindObjectOfType<FinalBossSummonInteractable>();
            if(altar!=null)
            {
                altarSettings=JsonUtility.ToJson(altar);altarScale=altar.transform.localScale;
                var sr=altar.GetComponent<SpriteRenderer>();if(sr!=null){altarSprite=sr.sprite;altarMaterial=sr.sharedMaterial;altarLayer=sr.sortingLayerID;altarOrder=sr.sortingOrder;}
            }
        }
        void RestoreBossAltar(FiniteStomachMap map)
        {
            if(FindObjectOfType<FinalBossSummonInteractable>()!=null)return;
            if(altarSettings==null)throw new System.InvalidOperationException("Missing original boss altar settings");
            var go=new GameObject("대장 보스 소환 제단");go.transform.position=FiniteStomachMap.SafeSpawn(map.UVToWorld(new Vector2(.73f,.3f)),2);go.transform.localScale=altarScale;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=altarSprite;sr.sharedMaterial=altarMaterial;sr.sortingLayerID=altarLayer;sr.sortingOrder=altarOrder;
            go.AddComponent<CircleCollider2D>();var altar=go.AddComponent<FinalBossSummonInteractable>();JsonUtility.FromJsonOverwrite(altarSettings,altar);altar.ResetForNextStage();
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
        IEnumerator EnterColon(Character player,StomachOpening outlet)
        {
            var travel=BloodClotTravel.Ensure(player);Transform anchor;
            TransitionPhase="loading";var loading=StageEntryLoading.CreateStageTransfer(player.Blueprint);yield return null;if(iris!=null)iris.SetVisible(false);yield return null;
            if(Capsule!=null){Destroy(Capsule.gameObject);Capsule=null;}
            foreach(var chest in FindObjectsOfType<Chest>())level.EntityManager.DespawnChest(chest);
            foreach(var p in FindObjectsOfType<Collectable>())p.DissolveInAcidLake();
            foreach(var p in FindObjectsOfType<SupplyRelicOrb>())Destroy(p.gameObject);
            foreach(var p in FindObjectsOfType<SupplyCapsule>())Destroy(p.gameObject);
            if(Portal!=null){Portal.gameObject.SetActive(false);Destroy(Portal.gameObject);}Portal=null;escapeHole=null;
            StageNumber=2;FirstBossDefeated=false;CapsuleBoarded=false;
            var map=FiniteStomachMap.Instance;var nextMap=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");
            if(nextMap==null)throw new System.InvalidOperationException("ColonMap/Stage2 is missing");
            map.ChangeDefinition(nextMap);loading.SetProgress(.4f);yield return null;
            RestoreBossAltar(map);
            stageTwo=ColonMonsterRoster.Create(level);
            level.BeginNextStage(stageTwo);level.SetRunFlowPaused(true);level.EntityManager.SetFieldMonsterRuntimeSuspended(true);level.GetComponent<SupplyCapsuleDirector>()?.RestartForStage();
            foreach(var altar in FindObjectsOfType<FinalBossSummonInteractable>())altar.transform.position=FiniteStomachMap.SafeSpawn(altar.transform.position,2);
            var inlet=StomachOpening.Find(true);inlet.SetOpened(0);var landing=map.ClampToField(inlet.FrontLip+Vector2.down*2,map.WallWidth+.6f);
            travel.SwimPose(landing,0);travel.Visual.color=Color.clear;loading.SetProgress(.7f);
            yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            if(!level.InitialFieldObjectsReady)throw new System.InvalidOperationException("Stage 2 field placement failed");
            for(float t=0;t<.7f;t+=Time.unscaledDeltaTime){loading.SetProgress(Mathf.Lerp(.7f,1,t/.7f));yield return null;}
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
        }
        void RestorePassenger(){if(hiddenPassenger==null)return;for(int i=0;i<hiddenPassenger.Length;i++)if(hiddenPassenger[i]!=null)hiddenPassenger[i].forceRenderingOff=hiddenStates[i];hiddenPassenger=null;}
        void OnDestroy(){RestorePassenger();if(stageTwo!=null)Destroy(stageTwo);if(Capsule!=null)Destroy(Capsule.gameObject);if(iris!=null)Destroy(iris.gameObject);}
    }
}
