using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class Ver4PlaySmoke
    {
        const string Key="Ver4PlaySmoke";
        static bool started, transferring;
        static int previousPlayerId;
        static float expectedDamage,expectedHealth;
        static List<string> expectedAcquisitions;
        static double deadline;
        static Ver4PlaySmoke() { if(SessionState.GetBool(Key,false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Done",false); SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) SessionState.SetBool(Key+"Failed",true); }
        static void Check(bool valid,string message)
        { if(!valid) throw new Exception(message); Debug.Log("[Ver4Smoke] PASS "+message); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode)
                { bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("[Ver4Smoke] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0); }
                return;
            }
            if(EditorApplication.timeSinceStartup>deadline || SessionState.GetBool(Key+"Failed",false))
            { Debug.LogWarning("[Ver4Smoke] Stopped: timeout or error");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);return; }
            if(!EditorApplication.isPlaying) return;
            var level=UnityEngine.Object.FindObjectOfType<LevelManager>();
            if(transferring && level!=null && level.PlayerCharacter!=null && level.PlayerCharacter.GetInstanceID()!=previousPlayerId && level.CurrentLevelTime>1)
            {
                try
                {
                    var manager=UnityEngine.Object.FindObjectOfType<AbilityManager>();
                    Check(!CrossSceneData.HasPendingRunSceneTransfer,"Real scene transfer consumed snapshot");
                    Check(manager.Ver4.CaptureRunSceneConditionalAugmentIds().SequenceEqual(expectedAcquisitions),"All 189 originals and numeric acquisitions survive scene reload");
                    Check(Mathf.Abs(level.PlayerCharacter.DamageMultiplier-expectedDamage)<.001f && Mathf.Abs(level.PlayerCharacter.MaxHealth-expectedHealth)<.001f,"Final character stats restored once, without duplication");
                    Check(manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Count(a=>a.Owned)==21,"All 21 parent states restored");
                    Check(!SyringeAbilityResolver.FindOwnedOrFirst(manager).HasCursorControlLegendary(),"Unselected CursorControl remains disabled after reload");
                }
                catch(Exception e){Debug.LogException(e);}
                transferring=false;SessionState.SetBool(Key+"Done",true);return;
            }
            if(!started && level!=null && level.PlayerCharacter!=null && level.CurrentLevelTime>1)
            { started=true;level.StartCoroutine(Checks(level)); }
        }
        static Ver4AugmentOffer Offer(Ver4AugmentRuntime runtime,Ability source,Ver4RewardKind kind,int option=0,float amount=0,AugmentUpgradeGrade grade=AugmentUpgradeGrade.Original)
        {
            var offer=new GameObject("Disposable test offer").AddComponent<Ver4AugmentOffer>();
            offer.Configure(runtime,kind,grade,source,option,amount,"Test","Test");return offer;
        }
        static object Get(object obj,string name)
        {
            for(var type=obj.GetType();type!=null;type=type.BaseType)
            { var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); if(field!=null)return field.GetValue(obj); }
            throw new MissingFieldException(name);
        }
        static void Set(object obj,string name,object value)
        {
            for(var type=obj.GetType();type!=null;type=type.BaseType)
            { var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); if(field!=null){field.SetValue(obj,value);return;} }
            throw new MissingFieldException(name);
        }
        // BatchMode has no presenting GameView, so ScreenCapture can silently
        // skip its write. Render the actual scene/UI into an offscreen target.
        // All temporary Canvas/Camera settings are restored before continuing.
        static void CaptureView(string filename)
        {
            var camera=Camera.main;
            if(camera==null) { Debug.LogWarning("[Ver4Smoke] No camera for capture"); return; }
            int width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
            var overlays=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras=overlays.Select(c=>c.worldCamera).ToArray();
            var oldDistances=overlays.Select(c=>c.planeDistance).ToArray();
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;
                foreach(var canvas in overlays)
                { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f; }
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("../work/"+filename),pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++)
                { overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=oldCameras[i];overlays[i].planeDistance=oldDistances[i]; }
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);
                if(pixels!=null)UnityEngine.Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }

        static Rect ScreenRect(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
            var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }

        static IEnumerator Checks(LevelManager level)
        {
            // StartCoroutine is dispatched by EditorApplication.update; resume
            // inside the player loop before checking frame-based input buffers.
            yield return null;
            var player=level.PlayerCharacter; var manager=UnityEngine.Object.FindObjectOfType<AbilityManager>();
            var runtime=manager.Ver4; var syringe=SyringeAbilityResolver.FindOwnedOrFirst(manager);
            Check(runtime!=null,"Scene initializes Ver4 reward runtime");
            var previousKeyboard=UnityEngine.InputSystem.Keyboard.current;
            var testKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                testKeyboard.MakeCurrent();
                UnityEngine.InputSystem.LowLevel.InputState.Change(testKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(
                    UnityEngine.InputSystem.Key.E,UnityEngine.InputSystem.Key.UpArrow,UnityEngine.InputSystem.Key.Digit1),UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic);
                Debug.Log($"[Ver4Smoke] Keyboard diagnostics: update={UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType}, E={testKeyboard.eKey.isPressed}, pressedThisFrame={testKeyboard.eKey.wasPressedThisFrame}");
                Check(GameInput.GetKeyDown(KeyCode.E)&&GameInput.GetKeyDown(KeyCode.UpArrow)&&GameInput.GetKeyDown(KeyCode.Alpha1)&&!GameInput.GetKeyDown(KeyCode.F),"Keyboard presses retain E/arrow/digit bindings in player input state");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(testKeyboard);
                if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            }
            Set(player,"isInvincible",true);level.SetRunFlowPaused(true);
            // Exercise actual mobile controls in the desktop PlayMode without modifying the player prefab.
            var mobile=player.gameObject.AddComponent<MobileGameplayControls>();Set(mobile,"simulateInEditor",true);
            yield return null;
            Check(MobileGameplayInput.Active && EventSystem.current!=null,"Mobile UI created with scene event system");
            Check(EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()!=null && EventSystem.current.GetComponent<StandaloneInputModule>()==null,"Scene UI migrated to the single Input System backend");
            var controls=UnityEngine.Object.FindObjectsOfType<MobileTouchControl>();
            var move=controls.Single(c=>c.name=="MOVE"); var aim=controls.Single(c=>c.name=="AIM / CHARGE");
            Canvas.ForceUpdateCanvases();
            var hudRects=UnityEngine.Object.FindObjectsOfType<RectTransform>();
            Check(!ScreenRect(hudRects.Single(r=>r.name=="MiniMapRoot")).Overlaps(ScreenRect((RectTransform)move.transform)),"Mobile minimap does not overlap movement stick");
            Check(!ScreenRect(hudRects.Single(r=>r.name=="Inventory Buttons")).Overlaps(ScreenRect((RectTransform)aim.transform)),"Mobile inventory does not overlap aim stick");
            var left=new PointerEventData(EventSystem.current){pointerId=11,position=RectTransformUtility.WorldToScreenPoint(null,move.transform.position)+Vector2.right*75};
            var right=new PointerEventData(EventSystem.current){pointerId=12,position=RectTransformUtility.WorldToScreenPoint(null,aim.transform.position)+Vector2.up*75};
            move.OnPointerDown(left);aim.OnPointerDown(right);
            Check(player.EffectiveMoveDirection.x>0 && MobileGameplayInput.ChargeHeld,"Movement and charge work with two different fingers");
            move.OnPointerUp(right);Check(player.EffectiveMoveDirection.x>0,"Other finger release does not cancel movement");
            Time.timeScale=0; yield return null;
            Check(player.EffectiveMoveDirection==Vector2.zero&&!MobileGameplayInput.ChargeHeld,"Paused UI clears held move and charge");
            Time.timeScale=1;yield return null;
            MobileGameplayInput.RequestInteraction();Check(MobileGameplayInput.ConsumeInteraction()&&!MobileGameplayInput.ConsumeInteraction(),"Interaction consumed once");
            for(int i=0;i<4;i++){MobileGameplayInput.RequestArrow(i);Check(MobileGameplayInput.ConsumeArrow(out int value)&&value==i,"Touch escape arrow "+i);}
            Check(player.TryDash(),"Touch-callable dash starts");yield return new WaitForSeconds(.6f);
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                CaptureView("ver4-mobile-ui.png");
                yield return new WaitForSecondsRealtime(.5f);
            }
            UnityEngine.Object.Destroy(mobile);yield return null;

            var initial=manager.SelectAbilities();
            Check(initial.Count==3&&initial.Cast<Ver4AugmentOffer>().All(x=>x.Kind==Ver4RewardKind.NewSpecial),"First reward offers three acquisitions, no parentless original");
            manager.ReturnAbilities(initial);
            var parents=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).OrderBy(p=>(int)p.Type).ToArray();
            Check(parents.Length==21,"Exactly 21 special sources, actual="+parents.Length+": "+string.Join(",",parents.Select(p=>p.Type.ToString())));
            foreach(var parent in parents) Check(manager.AcquireVer4Ability(parent),"Acquire parent "+parent.Type);
            var refresh=manager.SelectAbilities();manager.ReturnAbilities(refresh);
            Check(!syringe.HasCursorControlLegendary(),"Acquiring all specials does not enable CursorControl");
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var dialog=UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>(true);
                dialog.Open(false);yield return new WaitForSecondsRealtime(1.5f);
                CaptureView("ver4-reward-ui.png");
                yield return new WaitForSecondsRealtime(.5f);dialog.Close();
            }
            bool mixed=false;
            for(int roll=0;roll<100;roll++)
            {
                var offers=manager.SelectAbilities();var cards=offers.Cast<Ver4AugmentOffer>().ToArray();
                Check(cards.Length==3&&cards.All(c=>c.Kind!=Ver4RewardKind.LegendaryAbility),"Three normal cards exclude legendary abilities #"+roll);
                if(cards.Select(c=>c.Grade).Distinct().Count()>1)mixed=true;
                manager.ReturnAbilities(offers);
            }
            Check(mixed,"Independent panel rolls produce mixed grades");
            var legendary=runtime.CreateOffers(true,3);
            Check(legendary.Count==3&&legendary.Cast<Ver4AugmentOffer>().All(c=>c.Kind==Ver4RewardKind.LegendaryAbility),"Legendary chest uses separate pool");manager.ReturnAbilities(legendary);
            foreach(var parent in parents)
                for(int option=0;option<3;option++)
                    for(int count=0;count<3;count++)
                    {
                        var offer=Offer(runtime,parent,Ver4RewardKind.Original,option);
                        Check(runtime.Apply(offer),$"Original {parent.Type}/{option} count {count+1}");UnityEngine.Object.Destroy(offer.gameObject);
                    }
            foreach(var parent in parents)
            {
                Check(runtime.Progress.Level(parent.Type.ToString())==9,"Parent reaches Lv.9: "+parent.Type);
                var invalid=Offer(runtime,parent,Ver4RewardKind.Original);Check(!runtime.Apply(invalid),"Fourth original blocked: "+parent.Type);UnityEngine.Object.Destroy(invalid.gameObject);
            }
            for(int stat=0;stat<9;stat++)
            {
                var numeric=Offer(runtime,parents[0],Ver4RewardKind.Numeric,stat,runtime.Balance.NumericValue(AugmentUpgradeGrade.Epic,stat),AugmentUpgradeGrade.Epic);
                Check(runtime.Apply(numeric),"Numeric shared stat "+stat);UnityEngine.Object.Destroy(numeric.gameObject);
            }
            var capped=runtime.CreateOffers(false,3);
            Check(capped.Cast<Ver4AugmentOffer>().All(c=>c.Kind==Ver4RewardKind.Numeric&&c.Grade==AugmentUpgradeGrade.Supreme),"All capped parents offer Supreme numeric upgrades only");manager.ReturnAbilities(capped);
            var saved=runtime.CaptureRunSceneConditionalAugmentIds();float damage=player.DamageMultiplier;
            Check(runtime.RestoreRunSceneConditionalAugments(saved)&&player.DamageMultiplier==damage,"Same-run restore is idempotent");
            // Real target tests use isolated snapshots so collateral effects cannot hide failures.
            syringe.enabled=false;
            var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
            var entity=level.EntityManager;
            var bossObject=new GameObject("Disposable boss chill test");bossObject.SetActive(false);
            var bossController=bossObject.AddComponent<BossController>();
            float normalBossSpeed=bossController.GetModifiedMovementSpeed(10);
            bossController.ApplyVer4IceChill(.1f);
            Check(Mathf.Abs(bossController.GetModifiedMovementSpeed(10)-normalBossSpeed*.8f)<.001f,"Boss body applies 20 percent ice slow without freeze");
            yield return new WaitForSeconds(.15f);
            Check(Mathf.Abs(bossController.GetModifiedMovementSpeed(10)-normalBossSpeed)<.001f,"Boss ice slow expires without changing phase multipliers");
            UnityEngine.Object.Destroy(bossObject);
            int miniIndex=Array.FindIndex(level.CurrentLevelBlueprint.monsters,m=>m.monstersPrefab.GetComponent<MiniBossMonster>()!=null);
            Check(miniIndex>=0,"MiniBoss pool exists for dedicated reward test");
            var miniData=level.CurrentLevelBlueprint.monsters[miniIndex].monsterBlueprints[0];
            var miniBoss=entity.SpawnMonster(miniIndex,(Vector2)player.transform.position+Vector2.up*20,miniData,0);
            miniBoss.TakeDamage(10000000);
            var reward=UnityEngine.Object.FindObjectsOfType<Chest>().FirstOrDefault(c=>((ChestBlueprint)Get(c,"chestBlueprint")).legendaryAugmentChest);
            Check(reward!=null,"Actual mini-boss death spawns legendary chest");
            reward.OpenChest();yield return new WaitForSecondsRealtime(.4f);
            var rewardDialog=UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>(true);
            Check(rewardDialog.MenuOpen&&manager.LegendaryRewardContext,"Actual legendary chest opens its modal context");
            Check(((List<Ability>)Get(rewardDialog,"displayedAbilities")).Cast<Ver4AugmentOffer>().All(c=>c.Kind==Ver4RewardKind.LegendaryAbility),"Chest modal displays only legendary ability cards");
            rewardDialog.Close();yield return new WaitForSeconds(.8f);
            int rewardsBefore=UnityEngine.Object.FindObjectsOfType<Chest>().Length;
            miniBoss=entity.SpawnMonster(miniIndex,(Vector2)player.transform.position+Vector2.up*20,miniData,0);
            miniBoss.StartCoroutine(miniBoss.Killed(false));
            Check(UnityEngine.Object.FindObjectsOfType<Chest>().Length==rewardsBefore,"Cleanup/non-player mini-boss removal does not grant a legendary chest");
            var target=entity.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*15,data,500);
            var nearby=entity.SpawnMonster(0,(Vector2)target.transform.position+Vector2.right*.6f,data,500);
            target.enabled=false;nearby.enabled=false;yield return null;
            var prog=new OriginalAugmentProgress();prog.RegisterOwnedParent("WoodNeedle");
            var combat=new SyringeSpecialRuntime {ver4=new Ver4CombatSnapshot(prog),ver4HitDamage=100,ver4Knockback=1};
            float before=nearby.HP;
            for(int hit=0;hit<3;hit++)Ver4HitEffects.AfterHit(target,combat,player,~0,false);
            Check(nearby.HP<before,"Third wood hit launches actual branch damage");
            prog=new OriginalAugmentProgress();prog.RegisterOwnedParent("VibrationNeedle");combat.ver4=new Ver4CombatSnapshot(prog);
            before=nearby.HP;Ver4HitEffects.AfterHit(target,combat,player,~0,false);
            Check(nearby.HP<before,"Vibration shockwave damages nearby target");
            before=nearby.HP;Ver4HitEffects.AfterHit(target,combat,player,~0,false);
            Check(nearby.HP==before,"Vibration cooldown prevents same-frame duplicate wave");
            prog=new OriginalAugmentProgress();prog.RegisterOwnedParent("IceNeedle");prog.TrySelect("IceNeedle",1);prog.TrySelect("IceNeedle",2);combat.ver4=new Ver4CombatSnapshot(prog);
            for(int attempt=0;attempt<100;attempt++)
            {Ver4HitEffects.AfterHit(target,combat,player,~0,false);var ice=target.GetComponent<NeuralBlockedMonsterStatus>();if(ice!=null&&ice.IceFrozen)break;}
            Check(target.GetComponent<NeuralBlockedMonsterStatus>().IceFrozen,"Ice proc freezes actual target");
            Check(Mathf.Abs(Ver4HitEffects.BeforeHit(target,combat)-1.2f)<.001f,"Next needle consumes freeze and gains 20 percent");
            before=nearby.HP;Ver4HitEffects.AfterHit(target,combat,player,~0,false);
            Check(nearby.HP<before&&UnityEngine.Object.FindObjectsOfType<Ver4GroundEffect>().Length>0,"Shatter damage and cold ground resolve");
            prog=new OriginalAugmentProgress();prog.RegisterOwnedParent("FireNeedle");combat.ver4=new Ver4CombatSnapshot(prog);
            target=entity.SpawnMonster(0,(Vector2)player.transform.position+Vector2.left*25,data,500);
            target.enabled=false;
            for(int hit=0;hit<30;hit++)Ver4HitEffects.AfterHit(target,combat,player,~0,false);
            Check(((IList)Get(target.GetComponent<Ver4NeedleStatus>(),"burns")).Count>0,"Fire proc creates burn state independently of cold ground");
            before=target.HP;yield return new WaitForSeconds(1.15f);
            Check(target.HP<before,"Fire stacks deal timed damage in PlayMode");
            target.gameObject.SetActive(false);
            Check(((IList)Get(target.GetComponent<Ver4NeedleStatus>(),"burns")).Count==0,"Pooled target clears burn stacks");
            expectedDamage=player.DamageMultiplier;expectedHealth=player.MaxHealth;expectedAcquisitions=saved;previousPlayerId=player.GetInstanceID();
            transferring=true;Check(RunSceneTransferTestService.CaptureCurrentRunAndLoad("Level 1"),"Scene reload starts with Ver4 state");
        }
    }
}
