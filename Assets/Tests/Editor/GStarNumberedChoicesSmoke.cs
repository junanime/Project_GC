using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Parent = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class GStarNumberedChoicesSmoke
    {
        const string KeyName = "GStarNumberedChoicesQA";
        const string Output = "Library/GStarUIQA/numbered-choices";
        static Keyboard keyboard;
        static double deadline;
        static bool started;

        static GStarNumberedChoicesSmoke() { if (SessionState.GetBool(KeyName,false)) Attach(); }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/report.txt","");
            SessionState.SetBool(KeyName,true);
            SessionState.SetString(KeyName+"result","");
            SessionState.SetString(KeyName+"data",Path.GetFullPath(Output+"/isolated-"+Guid.NewGuid().ToString("N")));
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0,0,1280,750);
            Attach();
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            GStarDemo.TestOverride=true;
            GStarDemo.TestDataDirectory=SessionState.GetString(KeyName+"data","");
            GStarSession.TestDirectory=GStarLeaderboard.TestDirectory=GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests(); GStarLeaderboard.ReloadForTests();
        }

        static void Attach()
        {
            started=false;
            deadline=EditorApplication.timeSinceStartup+300;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }

        static void Log(string message,string trace,LogType type)
        {
            if (!SessionState.GetBool(KeyName,false) || (type!=LogType.Error && type!=LogType.Assert && type!=LogType.Exception)) return;
            File.AppendAllText(Output+"/report.txt",message+"\n"+trace+"\n");
            SessionState.SetString(KeyName+"result","FAIL");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(KeyName,false)) return;
            if (EditorApplication.timeSinceStartup>deadline) SessionState.SetString(KeyName+"result","TIMEOUT");
            string result=SessionState.GetString(KeyName+"result","");
            if (result.Length>0)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(KeyName,false); File.WriteAllText(Output+"/result.txt",result);
                EditorApplication.Exit(result=="PASS" ? 0 : 1); return;
            }
            if (!EditorApplication.isPlaying || started || ApothecaryUI.Instance?.Page!="main") return;
            started=true;
            var host=new GameObject("Number keys playthrough").AddComponent<GStarUITestHost>();
            Object.DontDestroyOnLoad(host);
            host.StartCoroutine(CheckField());
        }

        static void Check(bool passed,string description)
        {
            if (!passed) throw new Exception("Numbered choices QA: "+description);
            File.AppendAllText(Output+"/report.txt","PASS "+description+"\n");
        }

        static IEnumerator Wait(Func<bool> condition,string description,int seconds=20)
        {
            var end=Time.realtimeSinceStartup+seconds;
            while (!condition() && Time.realtimeSinceStartup<end) yield return null;
            Check(condition(),description);
        }

        static IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            yield return null;
            yield return null;
        }

        // Optional evidence recorded through the same production camera, canvases and
        // input handling as the assertions below. Other test runs keep their normal pacing.
        sealed class ChoiceFilm
        {
            readonly string destination;
            readonly string temporary="numbered-choices/film-"+Guid.NewGuid().ToString("N");
            int sequence;
            ChoiceFilm(string path)
            {
                destination=Path.GetFullPath(path);
                Directory.CreateDirectory(Path.Combine(destination,"frames"));
                Directory.CreateDirectory(Path.Combine("Library/GStarUIQA",temporary));
                Time.captureFramerate=60;
            }
            public static ChoiceFilm Start()
            {
                var args=Environment.GetCommandLineArgs();
                int i=Array.IndexOf(args,"-qaChoiceFilm");
                return i>=0 && i+1<args.Length ? new ChoiceFilm(args[i+1]) : null;
            }
            public IEnumerator Record(int count)
            {
                for(int i=0;i<count;i++)
                {
                    string name=(sequence++).ToString("D4");
                    GStarUISmoke.Capture(temporary+"/"+name,960,540);
                    File.Copy(Path.Combine("Library/GStarUIQA",temporary,name+".png"),Path.Combine(destination,"frames",name+".png"),true);
                    // Input and animation are simulated at 60 Hz; save every fifth game frame.
                    for(int tick=0;tick<5;tick++)yield return null;
                }
            }
            public void Finish()
            {
                Time.captureFramerate=0;
                File.WriteAllText(Path.Combine(destination,"film-checks.txt"),
                    "PASS\nframes="+sequence+"\nfps=12\nwidth=960\nheight=540\nselected_columns=2,3\n");
            }
        }

        static IEnumerator CheckField()
        {
            var prefs=GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss=false; prefs.keyboard=KeyboardBindingValues.Defaults(); GamePreferences.Apply(prefs,false);
            var booth=ApothecaryUI.Instance;
            booth.Show("prepare");
            yield return null;
            var roster=booth.Config.characters;
            int indexShini=Array.FindIndex(roster,c=>c!=null && OctoberArt.CharacterKey(c)=="Shini");
            booth.GetComponentsInChildren<Button>().First(b=>b.name=="Choice 0 "+indexShini).onClick.Invoke();
            var selected=roster[indexShini];
            var fireBlueprint=booth.Config.weapons.First(w=>w!=null && w.Type==Parent.FireNeedle);
            StartingNeedleSelection.SetEnabled(fireBlueprint,true,booth.Config);
            StartingNeedleSelection.Set(fireBlueprint,selected);
            CrossSceneData.CharacterBlueprint=selected;
            Check(StageEntryLoading.Begin(selected,null,0),"ordinary stage-zero departure");
            yield return Wait(()=>GStarSession.AttemptStarted && !StageEntryLoading.IsLoading,"field loaded",90);
            booth=ApothecaryUI.Instance;
            var level=Object.FindObjectOfType<LevelManager>();
            var player=level.PlayerCharacter;
            var dialog=level.EntityManager.AbilitySelectionDialog;
            var manager=Object.FindObjectOfType<AbilityManager>();
            var stats=Object.FindObjectOfType<StatsManager>();
            var itemRuntime=OctoberItemRuntime.Get(player);
            keyboard=InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();

            for (int index=0;index<Ver4AugmentCatalog.OriginalNames.Length;index++)
            {
                for (int count=0;count<3;count++)
                {
                    var copy=AugmentBrief.Original(index,count);
                    if (!copy.Any(char.IsDigit) || copy.Length>52) throw new Exception("Invalid original "+index+" rank "+count+": "+copy);
                }
                if (!AugmentBrief.Original(index,3).Contains("최대")) throw new Exception("Missing level cap "+index);
            }
            Check(AugmentBrief.Original(23,1).Contains("20% → 31%") && AugmentBrief.Original(56,1).Contains("1.5초 → 2초"),
                "all 189 card ranks include numbers; signature formation and ice show actual next values");

            manager.OriginalRewardContext = true;
            dialog.RequestLevelReward(2);
            dialog.RequestLevelReward(3);
            yield return new WaitForSecondsRealtime(.68f);
            var cards=dialog.GetComponentsInChildren<AbilityCard>();
            Check(cards.Length==3 && cards.All(c=>c.transform.Find("Choice keycap")!=null),"three visible augment keycaps");
            // The first expensive field frame can span the entire stagger timer.
            // Capture the settled visual, after every actual card has finished appearing.
            var appear = typeof(AbilityCard).GetField("appearCoroutine",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            yield return Wait(()=>cards.All(c=>c.transform.localScale.sqrMagnitude>.01f && appear.GetValue(c)==null),
                "all three augment cards have fully appeared");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var fieldKey = Resources.Load<Sprite>("Interaction/KeycapE_Pixel");
            for (int i = 0; i < cards.Length; i++)
            {
                var cap = cards[i].transform.Find("Choice keycap").GetComponent<InteractionKeycapGraphic>();
                Check(cap != null && cap.Legend.text == (i + 1).ToString() && cap.mainTexture == fieldKey.texture &&
                    cap.rectTransform.anchorMin == Vector2.up && cap.rectTransform.anchorMax == Vector2.up &&
                    cap.rectTransform.rect.width >= 50 && cap.rectTransform.rect.height >= 50 &&
                    !cap.raycastTarget && !cap.Legend.raycastTarget,
                    "large field-style keyboard key at top left of augment " + (i + 1));
            }
            GStarUISmoke.Capture("numbered-choices/augment",960,540);
            var clicks=new int[3];
            for (int i=0;i<3;i++)
            {
                int slot=i;
                cards[i].transform.Find("Background Button").GetComponent<Button>().onClick.AddListener(()=>clicks[slot]++);
            }
            ApothecaryUI.Instance.OpenRunBook();
            yield return null;
            Check(ApothecaryUI.Instance.Page=="run","journal covers augment cards");
            yield return Press(Key.Digit1);
            Check(clicks.Sum()==0 && dialog.MenuOpen,"covered reward ignores number keys");
            yield return Press();
            ApothecaryUI.Instance.CloseRunBook();
            yield return null;
            var film=ChoiceFilm.Start();
            if(film!=null)yield return film.Record(8);
            yield return Press(Key.Digit2);
            Check(clicks.SequenceEqual(new[]{0,1,0}) && dialog.SelectionAnimating,
                $"2 selects the middle card once (clicks {string.Join(",",clicks)}, page {ApothecaryUI.Instance.Page})");
            if(film!=null)yield return film.Record(13);
            yield return Wait(()=>!dialog.SelectionAnimating,"selection animation completes with the key held");
            yield return new WaitForSecondsRealtime(.3f);
            Check(clicks.SequenceEqual(new[]{0,1,0}) && dialog.MenuOpen,"holding 2 through next offer selects only middle card");
            yield return Press();
            yield return new WaitForSecondsRealtime(.55f);
            if(film!=null)yield return film.Record(4);
            yield return Press(Key.Numpad3);
            if(film!=null)yield return film.Record(11);
            yield return Wait(()=>!dialog.MenuOpen,"numpad 3 completes the remaining offer");
            Check(clicks.SequenceEqual(new[]{0,1,1}),"right card receives its real button click");
            film?.Finish();
            yield return Press();

            dialog.RequestLevelReward(4);
            dialog.RequestLevelReward(5);
            yield return new WaitForSecondsRealtime(.6f);
            var skip=dialog.GetComponentsInChildren<Button>().First(b=>b.name=="Skip augment");
            var skipKey=skip.GetComponentInChildren<InteractionKeycapGraphic>();
            Check(skipKey.Legend.text=="Esc" && !skipKey.raycastTarget,"skip uses the field Esc keycap");
            int skipped=0;
            skip.onClick.AddListener(()=>skipped++);
            yield return Press(Key.Escape);
            Check(dialog.MenuOpen && skipped==1 && booth.Page=="hud","Esc skips exactly one queued offer and keeps settings closed");
            yield return new WaitForSecondsRealtime(.2f);
            Check(skipped==1 && dialog.MenuOpen,"holding Esc preserves the next offer");
            yield return Press();
            yield return Press(Key.Escape);
            Check(!dialog.MenuOpen && skipped==2 && booth.Page=="hud","second discrete Esc closes last offer");
            yield return Press();

            var npc=Object.FindObjectOfType<MerchantNPC>();
            if (npc==null)
                npc=new GameObject("QA merchant",typeof(CircleCollider2D),typeof(MerchantNPC)).GetComponent<MerchantNPC>();
            var offers=Resources.LoadAll<MerchantItemBlueprint>("OctoberContent/Catalog")
                .Where(i=>i.octoberId>0 && ItemOfferPolicy.CanOffer(i) && !itemRuntime.Has(i.octoberId) && i.cost>0)
                .OrderBy(i=>i.octoberId).Take(3).ToList();
            Check(offers.Count==3,"three purchasable catalog items");
            npc.SetShopItems(offers);
            stats.IncreaseCoinsGained(-stats.CoinsGained);
            MerchantUIManager.Instance.OpenShop(npc);
            yield return new WaitForSecondsRealtime(.2f);
            var inactiveReroll=booth.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Button 새로고침"));
            var shopKey=inactiveReroll.GetComponentInChildren<InteractionKeycapGraphic>();
            Check(!inactiveReroll.IsInteractable() && shopKey.Legend.text=="R" && !shopKey.raycastTarget,
                "unaffordable refresh has a visible R keycap");
            yield return Press(Key.R);
            Check(npc.GetShopItems().SequenceEqual(offers) && stats.CoinsGained==0,"unaffordable R leaves offers and wallet unchanged");
            yield return Press();
            yield return Press(Key.Numpad2);
            Check(ApothecaryUI.Instance.GetComponentsInChildren<NumberedCardChoice>().Any(c=>c.ShowingUnavailable),"unaffordable card responds with a local keycap pulse");
            Check(ApothecaryUI.Instance.Page=="merchant" && stats.CoinsGained==0 && !itemRuntime.Has(offers[1].octoberId),
                "unaffordable numeric purchase keeps shop and inventory intact");
            yield return Press();
            stats.IncreaseCoinsGained(1000);
            ApothecaryUI.Instance.Show("merchant");
            yield return new WaitForSecondsRealtime(.22f);
            Check(ApothecaryUI.Instance.GetComponentsInChildren<NumberedCardChoice>().Length==3,"three visible purchase keycaps");
            GStarUISmoke.Capture("numbered-choices/merchant",960,540);
            int gold=stats.CoinsGained, price=RelicRuntime.Price(player,offers[0].cost);
            yield return Press(Key.Numpad1);
            yield return Wait(()=>ApothecaryUI.Instance.Page=="hud","successful numeric purchase closes merchant");
            Check(stats.CoinsGained==gold-price && itemRuntime.Has(offers[0].octoberId),"numpad targets left item and charges once");
            yield return new WaitForSecondsRealtime(.2f);
            Check(stats.CoinsGained==gold-price,"held key cannot repeat the transaction");
            yield return Press();
            var nextNpc=new GameObject("QA reroll merchant",typeof(CircleCollider2D),typeof(MerchantNPC)).GetComponent<MerchantNPC>();
            nextNpc.SetShopItems(offers);
            MerchantUIManager.Instance.OpenShop(nextNpc);
            yield return null;
            var reroll=booth.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Button 새로고침"));
            int rerollCost=int.Parse(System.Text.RegularExpressions.Regex.Match(reroll.name,@"\d+").Value);
            int wallet=stats.CoinsGained;
            yield return Press(Key.R);
            Check(booth.Page=="merchant" && stats.CoinsGained==wallet-rerollCost,"R rerolls offers and charges the displayed cost once");
            yield return new WaitForSecondsRealtime(.2f);
            Check(stats.CoinsGained==wallet-rerollCost,"holding R does not pay again on the regenerated page");
            yield return Press();
            var selectedItem=nextNpc.GetShopItems().First(i=>i.octoberId>0 && !itemRuntime.Has(i.octoberId));
            var cardRect=booth.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Merchant "+selectedItem.itemName);
            var purchase=cardRect.GetComponentInChildren<Button>();
            var events=EventSystem.current;
            var canvas=cardRect.GetComponentInParent<Canvas>().rootCanvas;
            var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var hits=new System.Collections.Generic.List<RaycastResult>();
            var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left};
            foreach (float y in new[]{.09f,.35f,.76f})
            {
                pointer.position=RectTransformUtility.WorldToScreenPoint(cam,cardRect.TransformPoint(cardRect.rect.center+Vector2.up*(y-.5f)*cardRect.rect.height));
                hits.Clear();
                events.RaycastAll(pointer,hits);
                Check(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==purchase.gameObject,
                    "price, description and artwork share the same purchase hit target at "+y);
            }
            wallet=stats.CoinsGained;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Check(booth.Page=="hud" && itemRuntime.Has(selectedItem.octoberId) &&
                stats.CoinsGained==wallet-RelicRuntime.Price(player,selectedItem.cost),"clicking card artwork buys once");
            yield return Wait(()=>Object.FindObjectsOfType<Monster>().Any(m=>m.HP>0),"living field target",60);
            Time.timeScale=0;
            var victim=Object.FindObjectsOfType<Monster>().First(m=>m.HP>0 && !MonsterNeedleBarrier.Blocks(m));
            var fire=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First(p=>p.Type==Parent.FireNeedle && p.Owned);
            int option=manager.Ver4.Progress.CanSelect(Parent.FireNeedle.ToString(),1) ? 1 : 2;
            var sample=new GameObject("QA fire upgrade").AddComponent<Ver4AugmentOffer>();
            sample.Configure(manager.Ver4,Ver4RewardKind.Original,AugmentUpgradeGrade.Original,fire,option,0,fire.Name,"");
            var earlier=manager.Ver4.Combat;
            int rank=manager.Ver4.Progress.Count(Parent.FireNeedle.ToString(),option);
            Check(rank>=1 && sample.OwnedOriginalCount==rank && manager.Ver4.IsValid(sample),
                $"signature offer corresponds to live rank {rank}, cached {sample.OwnedOriginalCount}, owner {player.Blueprint.name}, enabled {StartingNeedleSelection.Enabled(fire)}");
            Check(manager.Ver4.Apply(sample) && manager.Ver4.Progress.Count(Parent.FireNeedle.ToString(),option)==rank+1,
                "signature progresses exactly one rank");
            var current=manager.Ver4.Combat;
            var intermediate = current;
            if (rank == 1)
            {
                var next = new GameObject("QA same upgrade again").AddComponent<Ver4AugmentOffer>();
                next.Configure(manager.Ver4,Ver4RewardKind.Original,AugmentUpgradeGrade.Original,fire,option,0,fire.Name,"");
                Check(manager.Ver4.Apply(next), "second unspent selection reaches the rank cap");
                current = manager.Ver4.Combat;
                Object.Destroy(next.gameObject);
            }
            var burn=victim.GetComponent<Ver4NeedleStatus>() ?? victim.gameObject.AddComponent<Ver4NeedleStatus>();
            int SparkCount()=>Object.FindObjectsOfType<SyringeAugmentVfx>().Count(v=>v.name.StartsWith("CursorControlHit"));
            int sparks=SparkCount();
            burn.ApplyFire(new SyringeSpecialRuntime{ver4=earlier,ver4HitDamage=10},player,true);
            Check(SparkCount()==sparks,"old projectile cannot consume new original feedback");
            if (rank == 1)
            {
                burn.ApplyFire(new SyringeSpecialRuntime{ver4=intermediate,ver4HitDamage=10},player,true);
                Check(SparkCount()==sparks, "previous unspent rank does not spend the latest cue");
            }
            var currentFire = new SyringeSpecialRuntime{ver4=current,ver4HitDamage=10};
            var shield = victim.GetComponent<MonsterNeedleBarrier>() ?? victim.gameObject.AddComponent<MonsterNeedleBarrier>();
            shield.Arm();
            Check(shield.IsProtected, "field target carries a real needle barrier");
            int preShieldStacks = burn.BurnStacks;
            burn.ApplyFire(currentFire,player,true);
            Check(SparkCount()==sparks && burn.BurnStacks==preShieldStacks,"shielded fire cannot consume the upgrade cue");
            shield.ResetBarrier();
            if (option==2)
                for (int stack=burn.BurnStacks; stack < 3 + current.Count(Parent.FireNeedle,2) - 1; stack++)
                    burn.ApplyFire(currentFire,player,true);
            burn.ApplyFire(currentFire,player,true);
            Check(SparkCount()==sparks+1,"actual fire application emits a single local cue");
            yield return null;
            burn.ApplyFire(new SyringeSpecialRuntime{ver4=current,ver4HitDamage=10},player,true);
            Check(SparkCount()==sparks+1,"repeated proc does not replay selection cue");
            Object.Destroy(sample.gameObject);
            Time.timeScale=1;

            InputSystem.RemoveDevice(keyboard);
            SessionState.SetString(KeyName+"result","PASS");
        }
    }
}
