using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static partial class FoodBossSmoke
    {
        const string Key="FoodBossSmoke",Output="Library/FoodBossProof";
        static bool started;static double deadline;
        [Serializable] class Entry{public string id;}[Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}[Serializable] class Backup{public Saved[] values;}
        static FoodBossSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()=>Start(false);
        public static void RunFlowReview(){SessionState.SetBool(Key+"Flow",true);Start(false);}
        public static void RunTangdengiWalk(){SessionState.SetBool(Key+"TangWalk",true);Start(false);}
        public static void RunMotionUpdate(){SessionState.SetBool(Key+"MotionUpdate",true);Start(false);}
        public static void RunFullMotionUpdate(){SessionState.SetBool(Key+"AfterMotion",true);Start(false);}
        public static void RunBasicReview(){SessionState.SetBool(Key+"Basic",true);Start(false);}
        public static void RunFinalReview()=>Start(true);
        public static void RunVisualProof(){SessionState.SetBool(Key+"Visual",true);Start(true);}
        public static void RunPhaseProof(){SessionState.SetBool(Key+"Phase",true);Start(true);}
        static void Start(bool review)
        {
            SessionState.SetBool(Key+"Review",review);
            Directory.CreateDirectory(Output);Restore();File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct();
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var view=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(view).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1200;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline reached\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;Restore();SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Visual",false);SessionState.SetBool(Key+"Phase",false);bool failed=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");EditorApplication.Exit(failed?1:0);return;}
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null){started=true;var go=new GameObject("Food boss QA");Object.DontDestroyOnLoad(go);go.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks());}
        }
        static void Restore(){string p=Output+"/prefs-backup.json";if(!File.Exists(p))return;foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(p)).values)if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);PlayerPrefs.Save();File.Move(p,Output+"/restored-"+DateTime.UtcNow.Ticks+".json");}
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Check(bool ok,string text){if(!ok)throw new Exception("FOOD_BOSS_FAIL "+text);Debug.Log("FOOD_BOSS_PASS "+text);File.AppendAllText(Output+"/checks.txt",text+"\n");}
        static void Position(Character p,Vector2 at){p.transform.position=at;p.GetComponent<Rigidbody2D>().position=at;p.GetComponent<Rigidbody2D>().velocity=Vector2.zero;}
        static void Capture(string name)
        {
            var camera=Camera.main;var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();
            var orders=overlays.Select(c=>c.sortingOrder).ToArray();var overrideSort=overlays.Select(c=>c.overrideSorting).ToArray();
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try{camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=overrideSort[i];}camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();}
        }
        static IEnumerator Checks()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");
            yield return null;Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"actual Level1 loading starts");while(StageEntryLoading.IsLoading)yield return null;
            var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;var origin=(Vector2)player.transform.position;
            TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            Set(player,"isInvincible",true);Time.timeScale=1;
            RunScore.Exclude("보스 자동 검증");
            if(SessionState.GetBool(Key+"MotionUpdate",false)){SessionState.SetBool(Key+"MotionUpdate",false);yield return MotionUpdateReview(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"TangWalk",false)){SessionState.SetBool(Key+"TangWalk",false);yield return TangdengiWalkReview(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"Flow",false)){SessionState.SetBool(Key+"Flow",false);yield return FlowReview(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"Basic",false)){SessionState.SetBool(Key+"Basic",false);yield return BasicReview(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"Phase",false)){yield return PhaseProof(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"Visual",false)){yield return VisualProof(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            if(SessionState.GetBool(Key+"Review",false)){yield return FinalReview(player,origin);yield return Integration(player,origin);yield return Presentation(player,origin);SessionState.SetBool(Key+"Done",true);yield break;}
            var altar=Object.FindObjectOfType<FinalBossSummonInteractable>();
            Check(altar.OpenBossChoice(player),"altar opens temporary choice panel");Check(Time.timeScale==0&&Object.FindObjectOfType<FoodBossRuntime>()==null,"choice pauses and does not spawn early");Capture("00-boss-choice");ApothecaryUI.Instance.CloseBossChoice();Check(Time.timeScale==1&&!FinalBossSummonInteractable.IsSummoning,"cancel resumes and releases summon lock");
            Check(altar.OpenBossChoice(player),"cancelled altar reusable");ApothecaryUI.Instance.CloseBossChoice(FoodBossKind.Denikani);
            float until=Time.time+12;while(Object.FindObjectOfType<FoodBossRuntime>()==null&&Time.time<until)yield return null;
            var selected=Object.FindObjectOfType<FoodBossRuntime>();Check(selected!=null&&selected.Kind==FoodBossKind.Denikani,"selected boss passes through real altar descent");Object.Destroy(selected.gameObject);yield return null;
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                Position(player,origin);var boss=level.EntityManager.SpawnFinalBoss(level.CurrentLevelBlueprint,origin+Vector2.up*3.5f,0,kind).GetComponent<FoodBossRuntime>();boss.AutoPatterns=false;
                Check(boss!=null&&boss.Health==boss.MaxHealth,"full-health selected roster spawn "+kind);Check(boss.gameObject.layer==LayerMask.NameToLayer("Monster Full"),"player attacks can collide "+kind);Check(boss.Audio.HasAllClips,"all eleven concept-specific sound clips loaded "+kind);
                for(int phase=0;phase<2;phase++)
                {
                    if(phase==1){boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;Check(boss.PhaseTwo&&!boss.Play(FoodBossAttack.Recovery),"P2 rejects recovery "+kind);}
                    Position(player,(Vector2)boss.transform.position+Vector2.right*3.2f+Vector2.up*1.2f);yield return new WaitForSeconds(.6f);Capture($"{(int)kind+1}-p{phase+1}-idle");
                    foreach(FoodBossAttack attack in new[]{FoodBossAttack.Basic,FoodBossAttack.Radial,FoodBossAttack.Dash,FoodBossAttack.UniqueOne,FoodBossAttack.UniqueTwo})
                    {
                        Position(player,(Vector2)boss.transform.position+Vector2.right*3.2f+Vector2.up*1.2f);Check(boss.Play(attack),"start "+kind+" p"+(phase+1)+" "+attack);float start=Time.time;int shots=0,frame=0;var ids=new System.Collections.Generic.HashSet<int>();bool sawAir=false,snapped=false;
                        while(boss.Busy&&Time.time-start<22)
                        {
                            foreach(var bullet in boss.Effects.GetComponentsInChildren<FoodBossBullet>())if(ids.Add(bullet.GetInstanceID()))shots++;
                            sawAir|=!boss.Visual.Body.enabled;
                            if(attack==FoodBossAttack.Dash&&phase==1&&Time.time-start>=frame*.1f&&frame<60){Capture($"motion-{(int)kind}-{frame:D3}");frame++;}
                            if(!snapped&&Time.time-start>(attack==FoodBossAttack.Dash?.95f:1.2f)){Capture($"{(int)kind+1}-p{phase+1}-{attack}");snapped=true;}
                            yield return null;
                        }
                        Check(!boss.Busy,"pattern completes "+kind+" p"+(phase+1)+" "+attack);
                        if(attack==FoodBossAttack.Basic)Check(shots==(phase==0?3:5),"actual basic projectile count "+shots);
                        if(attack==FoodBossAttack.Radial)Check(shots==FoodBossRuntime.RingCount*(phase+1),"actual radial projectile count "+shots);
                        if(attack==FoodBossAttack.Dash&&(kind==FoodBossKind.Maribi||kind==FoodBossKind.Hurumi))Check(sawAir&&boss.Visual.Body.enabled,"jump hides body and restores after landing");
                        Call(boss,"ClearEffects");yield return null;
                    }
                    if(phase==0)
                    {
                        Check(boss.Play(FoodBossAttack.Recovery),"P1 recovery starts "+kind);yield return new WaitForSeconds(.3f);Capture($"{(int)kind+1}-p1-Recovery");
                        if(kind==FoodBossKind.Denikani)
                        {
                            for(int n=0;n<5;n++){var dir=(Vector2)Field(boss,"recoveryDirection").GetValue(boss);Position(player,(Vector2)boss.transform.position+dir*2);boss.TakeDamage(1,-dir);Check(boss.Reversals==n+1,"directional shell reversal "+(n+1));yield return new WaitForSeconds(.7f);}
                        }
                        else
                        {
                            var nodes=boss.Effects.GetComponentsInChildren<FoodBossWeakpoint>();Check(nodes.Length==(kind==FoodBossKind.Maribi?1:kind==FoodBossKind.Rolpangi?4:3),"correct recovery target count "+kind);
                            if(kind==FoodBossKind.Maribi||kind==FoodBossKind.Tangdengi){float hp=boss.Health;boss.TakeDamage(9999);Check(hp==boss.Health&&!boss.GetComponent<Collider2D>().enabled,"recovery shell invulnerable; only weakpoints targetable");}
                            foreach(var node in nodes){Check(NeedleTargeting.IsValidBossTarget(node,~0),"recovery object auto aim valid");node.TakeDamage(node.Health+1);}
                        }
                        until=Time.time+3;while(boss.Busy&&Time.time<until)yield return null;Check(!boss.Busy&&!boss.Recovering,"objective terminates recovery "+kind);
                    }
                }
                Check(boss.Audio.PlayedCount>10,"attacks emitted audio events "+kind);Object.Destroy(boss.gameObject);yield return null;Check(Object.FindObjectsOfType<FoodBossBullet>().Length==0&&Object.FindObjectsOfType<FoodBossWeakpoint>().Length==0,"boss destruction cleans all attacks and recovery objects");
            }
            yield return Avoidance(player,origin);
            yield return Fanta(player,origin);
            if(SessionState.GetBool(Key+"AfterMotion",false)){SessionState.SetBool(Key+"AfterMotion",false);yield return MotionUpdateReview(player,origin);}
            yield return Integration(player,origin);yield return Presentation(player,origin);
            Position(player,origin);SessionState.SetBool(Key+"Done",true);
        }

        static IEnumerator PhaseProof(Character player,Vector2 origin)
        {
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
            Time.captureDeltaTime=.04f;
            var arguments=Environment.GetCommandLineArgs();int selected=-1;
            int selector=Array.IndexOf(arguments,"-phaseBoss");
            if(selector>=0&&selector+1<arguments.Length)int.TryParse(arguments[selector+1],out selected);
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                if(selected>=0&&(int)kind!=selected)continue;
                Position(player,origin+new Vector2(3.2f,1.75f));
                var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
                yield return new WaitForSeconds(.4f);
                boss.TakeDamage(boss.MaxHealth*.35f);
                for(int f=0;f<185;f++)
                {
                    if(f==18)boss.SetPhaseTwoForTest();
                                        if(boss.Transitioning){float hp=boss.Health;boss.TakeDamage(boss.MaxHealth*2);boss.TakePeriodicDamage(boss.MaxHealth*2,Vector2.zero);if(boss.Health!=hp)throw new Exception("Transition invulnerability failed "+kind);}
                    // Actual Level1 render, actual update/animation clock, no composited sprites.
                    yield return null;
                    Capture($"phase-{(int)kind}-{f:D3}");
                }
                Check(boss.PhaseTwo&&!boss.Transitioning&&!boss.Busy,"phase finishes and releases action "+kind);
                Check(Mathf.Abs(boss.Health/boss.MaxHealth-.49f)<.001f,"transition preserves remaining health "+kind);
                Check(boss.Visual.Body.enabled,"original phase-two art restored "+kind);
                Check(boss.PhaseVisual.EmittedCues==FoodBossPhaseCues.For(kind).Length,"all timed transition audio cues played "+kind);
                Check(FoodBossPhaseVisual.Duration(kind)<=5.5f,"transition within 5.5 seconds "+kind);
                Check(!boss.Play(FoodBossAttack.Recovery),"phase two recovery remains disabled "+kind);
                // Interruption and pause should never leave the replacement renderer on screen.
                Object.Destroy(boss.gameObject);yield return null;
            }
            var pausedBoss=FoodBossRuntime.Spawn(FoodBossKind.Maribi,player,origin);pausedBoss.AutoPatterns=false;
            pausedBoss.SetPhaseTwoForTest();yield return new WaitForSeconds(.3f);
            Time.timeScale=0;float progress=pausedBoss.PhaseVisual.Progress;
            yield return new WaitForSecondsRealtime(.15f);
            Check(Mathf.Approximately(progress,pausedBoss.PhaseVisual.Progress),"phase animation respects pause");
            Time.timeScale=1;Call(pausedBoss,"Cancel");yield return null;
            Check(!pausedBoss.Busy&&pausedBoss.Visual.Body.enabled&&!pausedBoss.Transitioning,"interruption restores original rig");
            Object.Destroy(pausedBoss.gameObject);Time.captureDeltaTime=0;
        }

        static IEnumerator Presentation(Character player,Vector2 origin)
        {
            Position(player,origin+Vector2.right*3.4f);
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Maribi,player,origin);boss.AutoPatterns=false;yield return null;
            boss.Play(FoodBossAttack.Recovery);yield return new WaitForSeconds(.2f);
            var tongue=boss.Effects.GetComponentInChildren<FoodBossWeakpoint>();Vector2 anchored=tongue.transform.position;
            Position(player,origin+Vector2.left*3.4f);yield return new WaitForSeconds(.1f);
            Check(Vector2.Distance(anchored,tongue.transform.position)<.01f,"tongue hitbox stays with closed shell when player crosses behind");
            Check(Vector2.Distance(tongue.transform.position,boss.Visual.TongueSocket)<.01f,"tongue hitbox matches artwork socket");Object.Destroy(boss.gameObject);yield return null;
            Position(player,origin+Vector2.right*3.4f);boss=FoodBossRuntime.Spawn(FoodBossKind.Tangdengi,player,origin);boss.AutoPatterns=false;yield return null;
            Check(boss.Play(FoodBossAttack.UniqueOne),"horn charge starts");
            FoodBossEnergyBeam beam=null;float beamDeadline=Time.time+6f;
            while(beam==null&&boss.Busy&&Time.time<beamDeadline){yield return null;beam=boss.Effects.GetComponentInChildren<FoodBossEnergyBeam>();}
            Check(beam!=null,"continuous horn beam exists after charge");
            Check(Vector2.Distance((Vector2)Field(beam,"origin").GetValue(beam),boss.Visual.HornTip)<.01f,"beam starts between horn tips");
            boss.SetPhaseTwoForTest();yield return null;Check(boss.Effects.GetComponentsInChildren<FoodBossEnergyBeam>().Length==0,"phase change removes active beam");Object.Destroy(boss.gameObject);yield return null;
            boss=FoodBossRuntime.Spawn(FoodBossKind.Hurumi,player,origin);boss.AutoPatterns=false;yield return null;
            boss.Play(FoodBossAttack.UniqueTwo);yield return new WaitForSeconds(.4f);
            Check(boss.Visual.Whipping&&boss.Visual.Body.sharedMaterial.shader.name=="FoodBosses/ArticulatedBody","only raised noodle switches to articulated rig");
            var noodle=boss.Effects.GetComponentInChildren<FoodBossNoodle>();Check(noodle!=null&&Vector2.Distance(noodle.Points[0],boss.Visual.NoodleTip)<.01f,"whip root stays on original noodle socket");
            boss.SetPhaseTwoForTest();yield return null;
            Check(!boss.Visual.Whipping&&boss.Visual.Body.sharedMaterial.shader.name!="FoodBosses/ArticulatedBody","interrupted whip restores original body art");
            Check(boss.Effects.GetComponentsInChildren<FoodBossNoodle>().Length==0,"interrupted whip removes extended noodle");Object.Destroy(boss.gameObject);yield return null;
            boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin);boss.AutoPatterns=false;boss.TakeDamage(boss.MaxHealth*.3f);yield return null;
            boss.Play(FoodBossAttack.Recovery);yield return new WaitForSeconds(.2f);
            var mini=boss.Effects.GetComponentsInChildren<FoodBossWeakpoint>()[0];Set(mini,"routeIndex",int.MaxValue);mini.transform.position=boss.Mouth+Vector2.right*.38f;
            float hp=boss.Health;yield return new WaitForSeconds(.4f);
            Check(mini==null&&Mathf.Approximately(boss.Health,hp+boss.MaxHealth*.02f),"mouth absorption consumes once and restores exactly two percent");Object.Destroy(boss.gameObject);yield return null;
        }

        static IEnumerator Integration(Character player,Vector2 origin)
        {
            Position(player,origin);var boss=FoodBossRuntime.Spawn(FoodBossKind.Maribi,player,origin+Vector2.right*2.8f);boss.AutoPatterns=false;yield return null;Physics2D.SyncTransforms();
            var needle=Object.FindObjectOfType<SyringeDartAbility>();Check(needle!=null,"live player needle ability present");
            var direction=(Vector2)Call(needle,"GetAutomaticNeedleDirection");Check(direction.x>.5f,"automatic basic aim selects new boss collider");
            float before=boss.Health;Check((bool)Call(needle,"LaunchAimedSyringe",direction,false,1f,0),"real player needle launches");
            float until=Time.time+3;while(boss.Health==before&&Time.time<until)yield return null;Check(boss.Health<before,"real player needle collision damages boss");
            var runtime=new SyringeSpecialRuntime{poisonEnabled=true,poisonDuration=1,poisonTickInterval=.1f,poisonTickDamage=7,corrosionEnabled=true,corrosionDuration=3,corrosionMaxStacks=3,corrosionBossDamageTakenBonusPerStack=.05f,markEnabled=true,markDuration=3};
            Ver4HitEffects.AfterHit(boss,runtime,player,~0,false);before=boss.Health;yield return new WaitForSeconds(.25f);
            Check(boss.Health<before,"poison ticks damage new boss");Check(boss.GetComponent<CorrosionStatus>().CurrentStacks==1,"corrosion applies to new boss");Check(boss.GetComponent<NeedleMarkStatus>().TryConsume(),"needle mark applies and consumes on new boss");
            IceSkillRules.Freeze(boss);Check(Mathf.Approximately(boss.GetComponent<FoodBossCombatStatus>().SpeedMultiplier,.8f),"ice retains boss resistance with 20 percent slow");
            boss.Audio.Play(FoodBossSound.Dash);yield return new WaitForSeconds(.1f);Time.timeScale=0;var voice=boss.GetComponents<AudioSource>().First(s=>s.isPlaying);yield return null;float pausedAt=voice.time;yield return new WaitForSecondsRealtime(.15f);Check(Mathf.Abs(voice.time-pausedAt)<.02f,"boss sound pauses with game");Time.timeScale=1;yield return null;
            Check(boss.Play(FoodBossAttack.Dash),"jump starts for phase-interruption test");
            float jumpDeadline=Time.time+3;while(boss.Visual.Body.enabled&&boss.Busy&&Time.time<jumpDeadline)yield return null;
            Check(!boss.Visual.Body.enabled,"jump body off screen before interruption");boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;Check(boss.Visual.Body.enabled&&boss.GetComponent<Collider2D>().enabled,"phase transition restores interrupted airborne boss");
            int score=RunScore.Current.Data.boss;boss.TakeDamage(boss.MaxHealth*2);Check(boss.Dead&&RunScore.Current.Data.boss==score+1,"boss death counted once in ranking");boss.TakeDamage(1);Check(RunScore.Current.Data.boss==score+1,"duplicate death hit cannot score twice");yield return new WaitForSeconds(1.2f);
        }
        static IEnumerator VisualProof(Character player,Vector2 origin)
        {
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
            var altar=Object.FindObjectOfType<FinalBossSummonInteractable>();altar.OpenBossChoice(player);Capture("final-boss-choice");ApothecaryUI.Instance.CloseBossChoice();
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
                Position(player,origin+new Vector2(3.2f,1.4f));yield return new WaitForSeconds(.6f);Capture($"final-{(int)kind}-p1");
                boss.Play(FoodBossAttack.Recovery);yield return new WaitForSeconds(.3f);Capture($"final-{(int)kind}-recovery");Call(boss,"Cancel");Call(boss,"ClearEffects");
                boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;yield return new WaitForSeconds(.2f);Capture($"final-{(int)kind}-p2");
                boss.Play(FoodBossAttack.Dash);float start=Time.time;int frame=0;
                while(boss.Busy){if(Time.time-start>=frame*.1f&&frame<60){Capture($"final-motion-{(int)kind}-{frame:D3}");File.AppendAllText(Output+$"/final-motion-{(int)kind}-timing.csv",frame+","+(Time.time-start).ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n");frame++;}yield return null;}
                if(kind==FoodBossKind.Denikani){Position(player,(Vector2)boss.transform.position+new Vector2(3.2f,1.4f));yield return new WaitForSeconds(.5f);boss.Play(FoodBossAttack.UniqueOne);yield return new WaitForSeconds(2.1f);Capture("final-crab-grab");while(boss.Busy)yield return null;}
                Object.Destroy(boss.gameObject);yield return null;
            }
        }
        static IEnumerator FinalReview(Character player,Vector2 origin)
        {
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Denikani,player,origin);boss.AutoPatterns=false;
            for(int phase=0;phase<2;phase++)
            {
                if(phase==1){boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;}
                Position(player,(Vector2)boss.transform.position+new Vector2(3.2f,1.2f));yield return new WaitForSeconds(.5f);
                Check(boss.Play(FoodBossAttack.UniqueOne),"articulated crab grab starts");float start=Time.time;int frame=0;
                while(boss.Busy){if(Time.time-start>=frame*.1f&&frame<60){Capture($"crab-grab-p{phase+1}-{frame:D3}");frame++;}yield return null;}
                Check(boss.Visual.Body.enabled,"crab grab ends with visible body");
            }
            Object.Destroy(boss.gameObject);yield return null;
            boss=FoodBossRuntime.Spawn(FoodBossKind.Denikani,player,origin);boss.AutoPatterns=false;boss.Play(FoodBossAttack.Recovery);yield return new WaitForSeconds(.2f);
            var dir=(Vector2)Field(boss,"recoveryDirection").GetValue(boss);Position(player,(Vector2)boss.transform.position+dir*2);boss.TakePeriodicDamage(10,-dir);Check(boss.Reversals==0,"poison or periodic damage cannot reverse shell");boss.TakeDamage(10,-dir);Check(boss.Reversals==1,"direct opposing hit reverses shell");Object.Destroy(boss.gameObject);yield return null;
            boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin);boss.AutoPatterns=false;Position(player,origin+new Vector2(2.8f,1));yield return null;
            var needle=Object.FindObjectOfType<SyringeDartAbility>();var projectile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Prefab Syringe").First(id=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)).GetComponent<SyringeProjectile>()!=null)))).GetComponent<SyringeProjectile>();
            projectile.Setup(0,origin+Vector2.right,10,0,4,~0);Set(projectile,"playerCharacter",player);Set(projectile,"specials",new SyringeSpecialRuntime{explosionRadius=4,explosionDamage=25});var other=new GameObject("Explosion original target");float hp=boss.Health;Call(projectile,"ApplyExplosion",other);Check(boss.Health<hp,"explosion secondary damage includes new boss");Object.Destroy(projectile.gameObject);Object.Destroy(other);
            var acid=new SyringeSpecialRuntime{digestiveAcidSacEnabled=true,digestiveAcidPuddleLifetime=3,digestiveAcidPuddleRadius=1.2f,digestiveAcidPuddleDamagePerSecond=5,digestiveAcidPuddleTickInterval=.5f,digestiveAcidPuddleColor=Color.green};
            FoodBossCombatStatus.Apply(boss,acid,player,false);FoodBossCombatStatus.Apply(boss,default,player,false);boss.GetComponent<FoodBossCombatStatus>().Defeated();var puddle=Object.FindObjectOfType<DigestiveAcidPuddle>();Check(puddle!=null&&Mathf.Approximately((float)Field(puddle,"radius").GetValue(puddle),1.2f),"different needle hit preserves stored acid death effect");Object.Destroy(puddle.gameObject);Object.Destroy(boss.gameObject);yield return null;
        }
        static IEnumerator Avoidance(Character player,Vector2 origin)
        {
            // Actual collision routine: straight movement at the slow baseline, no dash/invulnerability.
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin+Vector2.up*5);boss.AutoPatterns=false;
            for(int distance=1;distance<=2;distance++)
            {
                Position(player,origin);var previous=player.CurrentHealth;Set(player,"isInvincible",false);float age=0,next=0;int count=0;
                while(age<3)
                {
                    Position(player,origin+Vector2.right*(age*1.15f));
                    if(count<3&&age>=next){var start=origin+Vector2.up*distance;FoodBossBullet.Fire(boss,start,((Vector2)player.transform.position-start).normalized,FoodBossRuntime.BasicSpeed,1);next+=FoodBossRuntime.BasicGap;count++;}
                    age+=Time.deltaTime;yield return null;
                }
                Check(Mathf.Approximately(previous,player.CurrentHealth),"three aimed shots avoided by 1.15 walking speed at distance "+distance);Set(player,"isInvincible",true);Call(boss,"ClearEffects");yield return null;
            }
            Object.Destroy(boss.gameObject);yield return null;
        }
        static IEnumerator Fanta(Character player,Vector2 origin)
        {
            Position(player,origin);yield return new WaitForSeconds(.3f);var camera=Camera.main;var host=new GameObject("Fanta QA").AddComponent<StageEntryLoadingTestHost>();
            host.StartCoroutine(AcidToadEvent.Emit(host.transform,new Rect(origin.x-8,origin.y-4,16,8),true,1.5f,2,1.5f,0,.5f,player));
            yield return new WaitForSeconds(.2f);var lane=Object.FindObjectsOfType<LineRenderer>().First(r=>r.name=="Fluid contour"&&r.positionCount==2);Vector2 locked=lane.GetPosition(0);
            Position(player,origin+Vector2.up*3);yield return new WaitForSeconds(1.45f);var wave=Object.FindObjectOfType<ChameleonSodaWave>();Check(wave!=null,"Fanta wave launched");var from=(Vector2)Field(wave,"from").GetValue(wave);Check(Vector2.Distance(from,locked)<.001f,"Fanta world launch remains exactly at telegraphed origin after camera movement");Capture("06-fanta-fixed-path");Object.Destroy(host.gameObject);Object.Destroy(wave.gameObject);
        }
    }
}
