using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class LeaderboardSmoke
    {
        const string Key="LeaderboardSmoke";
        static bool launched; static double deadline; static int checks;
        static LeaderboardSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);Debug.Log("[LeaderboardSmoke] FINISHED checks="+checks+" failed="+SessionState.GetBool(Key+"Failed",false));
                EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            if(!launched&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null){launched=true;Checks();}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Debug.Log("[LeaderboardSmoke] PASS "+label);}
        static Task<QaLeaderboard.Reply> Call(string action,string json)=>CloudCodeService.Instance.CallEndpointAsync<QaLeaderboard.Reply>("QaRun",new Dictionary<string,object>{{"action",action},{"payload",json}});
        static string Payload(string id,RunScoreData data)=>JsonUtility.ToJson(data).TrimEnd('}')+",\"runId\":\""+id+"\"}";
        static void Capture(string file)=>typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{file});
        static async void Checks()
        {
            try
            {
                var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                // Dedicated anonymous profiles and Validation_Only board never pollute QA participant records.
                await UnityServices.InitializeAsync();
                AuthenticationService.Instance.SwitchProfile("validationA");
                await QaLeaderboard.Ready();
                await AuthenticationService.Instance.UpdatePlayerNameAsync("검증전용A");
                string first=AuthenticationService.Instance.PlayerId;
                string id=Guid.NewGuid().ToString("N");
                var begin=await Call("begin-test","{\"runId\":\""+id+"\"}");Check(begin.runId==id,"server creates run ticket");
                Check((await Call("begin-test","{\"runId\":\""+id+"\"}")).runId==id,"begin retry is idempotent");
                var data=new RunScoreData{normal=10,special=2,elite=1,entered=1,cleared=1,overcharge=1,seconds=1};
                Check(data.Total==1100,"normal 30 special 50 and event weights");
                Check((await Call("submit",Payload(id,data))).score==1100,"server independently computes score");
                data.normal=99;
                Check((await Call("submit",Payload(id,data))).score==1100,"retry cannot inflate finalized score");
                var score=await LeaderboardsService.Instance.GetPlayerScoreAsync("Validation_Only");Check(score.Score>=1100,"cloud score persisted");
                id=Guid.NewGuid().ToString("N");await Call("begin-test","{\"runId\":\""+id+"\"}");
                Check((await Call("submit",Payload(id,new RunScoreData{normal=1,seconds=1}))).score==30,"second run calculates only its own score");
                Check((await LeaderboardsService.Instance.GetPlayerScoreAsync("Validation_Only")).Score==score.Score,"lower run keeps personal best");
                bool denied=false;try{await LeaderboardsService.Instance.AddPlayerScoreAsync("Validation_Only",9999999);}catch(Exception){denied=true;}
                Check(denied,"direct client score writes blocked");
                id=Guid.NewGuid().ToString("N");await Call("begin-test","{\"runId\":\""+id+"\"}");
                Check(!string.IsNullOrEmpty((await Call("submit",Payload(id,new RunScoreData{normal=-1}))).rejected),"negative counter rejected");
                AuthenticationService.Instance.SignOut();AuthenticationService.Instance.SwitchProfile("validationB");await QaLeaderboard.Ready();
                await AuthenticationService.Instance.UpdatePlayerNameAsync("검증전용B");
                Check(AuthenticationService.Instance.PlayerId!=first,"separate players have separate identities");
                id=Guid.NewGuid().ToString("N");await Call("begin-test","{\"runId\":\""+id+"\"}");
                await Call("submit",Payload(id,new RunScoreData{normal=2,seconds=1}));
                var top=await LeaderboardsService.Instance.GetScoresAsync("Validation_Only",new GetScoresOptions{Limit=10});
                Check(top.Results.Any(r=>r.PlayerId==first)&&top.Results.Any(r=>r.PlayerId==AuthenticationService.Instance.PlayerId),"two players see shared leaderboard");
                var snapshot=new QaLeaderboard.Snapshot();
                for(int i=1;i<=12;i++)snapshot.rows.Add(new QaLeaderboard.Row{rank=i,playerId="p"+i,name="이름"+i,score=1300-i});
                snapshot.mine=snapshot.rows[11];
                var rows=QaLeaderboard.VisibleRows(snapshot,"p12");Check(rows.Count==10&&rows[9].rank==12,"top nine plus own rank");
                snapshot.mine=snapshot.rows[2];rows=QaLeaderboard.VisibleRows(snapshot,"p3");Check(rows.Count==10&&rows.Select(r=>r.playerId).Distinct().Count()==10,"self in top nine shows ten unique players");
                rows=QaLeaderboard.VisibleRows(new QaLeaderboard.Snapshot(),"new");Check(rows.Count==10&&rows.All(r=>r==null),"empty board has ten empty slots");
                const string cacheKey="Leaderboard.qa_s1_v1";
                string previousCache=PlayerPrefs.GetString(cacheKey,"");
                PlayerPrefs.SetString(cacheKey,JsonUtility.ToJson(new QaLeaderboard.Snapshot{updated="empty test"}));
                typeof(QaLeaderboard).GetField("cacheLoaded",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
                QaLeaderboard.LoadCache();Check(QaLeaderboard.Cache.mine==null,"empty cached board never creates rank zero");
                PlayerPrefs.SetString(cacheKey,previousCache);
                var movie=UnityEngine.Object.FindObjectOfType<IntroMovie>();if(movie!=null){movie.RequestSkip();movie.RequestSkip();movie.ConfirmSkip();}
                ApothecaryUI.Instance.Show("main");QaLeaderboard.Refresh(true);await Task.Delay(1800);
                Check(GameObject.Find("QA leaderboard")!=null,"main menu ranking panel visible");
                Capture("qa-ranking-main.png");
                var verified=new QaLeaderboard.Snapshot{owner=AuthenticationService.Instance.PlayerId,updated="검증 전용"};
                verified.rows=top.Results.Select(r=>new QaLeaderboard.Row{rank=r.Rank+1,playerId=r.PlayerId,name=QaLeaderboard.DisplayName(r.PlayerName),score=r.Score}).ToList();
                verified.mine=verified.rows.First(r=>r.playerId==verified.owner);
                typeof(QaLeaderboard).GetProperty("Cache").SetValue(null,verified);
                typeof(QaLeaderboard).GetProperty("Status").SetValue(null,"검증 전용 순위표 · 실제 서버 기록");
                ApothecaryUI.Instance.Show("main");await Task.Delay(150);Capture("qa-ranking-live-test.png");
                ApothecaryUI.Instance.Show("rankingName");await Task.Delay(150);Capture("qa-ranking-name.png");
                Check(UnityEngine.Object.FindObjectOfType<TMPro.TMP_InputField>()!=null,"nickname input available");
                var input=UnityEngine.Object.FindObjectOfType<TMPro.TMP_InputField>();input.text="검증전용B";
                UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(b=>b.name=="Button 저장").onClick.Invoke();
                for(int wait=0;wait<100&&ApothecaryUI.Instance.Page!="main";wait++)await Task.Delay(100);
                Check(ApothecaryUI.Instance.Page=="main"&&QaLeaderboard.Nickname=="검증전용B","nickname save button updates cloud and returns to main");
                ApothecaryUI.Instance.Show("rankingInfo");await Task.Delay(150);Capture("qa-ranking-rules.png");
                var load=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Scenes/Game/Level 1.unity");
                while(!load.isDone)await Task.Delay(100);
                while(RunScore.Current==null||StageEntryLoading.IsLoading)await Task.Delay(100);
                RunScore.Exclude("Leaderboard smoke test");
                var level=UnityEngine.Object.FindObjectOfType<LevelManager>();level.SetRunFlowPaused(true);
                var run=RunScore.Current;
                var marker=new GameObject("Unique scoring test");
                run.Enter(marker);run.Enter(marker);run.Overcharge(marker);run.Overcharge(marker);
                Check(run.Data.entered==1&&run.Data.overcharge==1,"same portal and overcharge reward only once");
                var dataBlueprint=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
                int before=run.Data.normal;
                var monster=level.EntityManager.SpawnMonster(0,(Vector2)level.PlayerCharacter.transform.position+Vector2.right*15,dataBlueprint,500);
                monster.StartCoroutine(monster.Killed());monster.RecordRankingKill();
                Check(run.Data.normal==before+1,"actual normal death scores once before despawn");
                monster=level.EntityManager.SpawnMonster(0,(Vector2)level.PlayerCharacter.transform.position+Vector2.left*15,dataBlueprint,500);
                monster.DissolveInAcidLake();Check(run.Data.normal==before+1,"acid dissolution awards no player score");
                int specialIndex=Array.FindIndex(level.CurrentLevelBlueprint.monsters,m=>RunScore.IsSpecial(m.monstersPrefab.GetComponent<Monster>()));
                Check(specialIndex>=0,"special monster classification exists in live spawn pool");
                var special=level.EntityManager.SpawnMonster(specialIndex,(Vector2)level.PlayerCharacter.transform.position+Vector2.up*15,level.CurrentLevelBlueprint.monsters[specialIndex].monsterBlueprints[0],500);
                int specialBefore=run.Data.special;special.StartCoroutine(special.Killed());
                Check(run.Data.special==specialBefore+1&&run.Data.normal==before+1,"special death scores 50 separately from normal");
                run.Finish(false);int finalTotal=run.Data.Total;run.SpecialKill();
                Check(run.Data.Total==finalTotal&&run.Exclusion!=null,"finished debug run cannot change score or submit");
                Check(ApothecaryUI.TryShowResult(false),"actual game result panel opens");await Task.Delay(200);Capture("qa-ranking-result.png");
                Check(UnityEngine.Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(t=>t.text.Contains("이번 탐험")&&t.text.Contains("점")),"result shows total score");
                SessionState.SetBool(Key+"Done",true);
            }
            catch(Exception e){Debug.LogError("[LeaderboardSmoke] "+e);SessionState.SetBool(Key+"Failed",true);}
        }
    }
}
