using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vampire;
[InitializeOnLoad]
public static class SnailBossPlaySmoke
{
    const string Key="SnailPlaySmoke";static bool started;static double deadline;
    static SnailBossPlaySmoke(){if(SessionState.GetBool(Key,false))Attach();}
    public static void Run()
    {
        SnailBossSetup.Install();SnailBossChecks.Run();
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
        EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline=EditorApplication.timeSinceStartup+240;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(SessionState.GetBool(Key+"Done",false))
        {
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            else if(!EditorApplication.isPlayingOrWillChangePlaymode)
            {bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("SNAIL_SMOKE_DONE failed="+failed);EditorApplication.Exit(failed?1:0);}
            return;
        }
        if(EditorApplication.timeSinceStartup>deadline||SessionState.GetBool(Key+"Failed",false))
        {Debug.Log("SNAIL_SMOKE_ABORT deadline or runtime error");SessionState.SetBool(Key+"Done",true);SessionState.SetBool(Key+"Failed",true);return;}
        if(!EditorApplication.isPlaying)return;
        var level=UnityEngine.Object.FindObjectOfType<LevelManager>();
        if(!started&&level!=null&&level.PlayerCharacter!=null&&level.CurrentLevelTime>1)
        {started=true;level.StartCoroutine(Checks(level));}
        var dialog=UnityEngine.Object.FindObjectOfType<AbilitySelectionDialog>();if(dialog!=null&&dialog.MenuOpen)dialog.Close();
        Time.timeScale=1;
    }
    static void Check(bool v,string msg)=>SnailBossChecks.Check(v,msg);
    static IEnumerator Checks(LevelManager level)
    {
        var player=level.PlayerCharacter;
        typeof(Character).GetField("isInvincible",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(player,true);
        Check(SnailBossDebugSpawn.SpawnFieldMinis(level)==3,"field wave creates three random topping minis");
        var fieldMinis=UnityEngine.Object.FindObjectsOfType<AcidLeechMonster>();
        Check(fieldMinis.Length>=3,"field mini snail actors exist");
        var fieldRig=fieldMinis[0].GetComponentInChildren<SnailMiniVisual>();
        Check(fieldRig!=null&&fieldRig.Body.sprite.name=="Body1","field uses attached vanilla snail body");
        float fieldPhase=fieldRig.WalkPhase;
        yield return new WaitForSeconds(.23f);
        Check(!Mathf.Approximately(fieldPhase,fieldRig.WalkPhase),"field curved motion drives crawl cycle");
        MiniStageRuntimeState.EnterMiniStage(null);yield return null;yield return null;
        fieldPhase=fieldRig.WalkPhase;
        yield return new WaitForSeconds(.15f);
        Check(fieldRig.WalkPhase==fieldPhase,"field snail animation freezes in mini-stage");
        MiniStageRuntimeState.ExitMiniStage(null);yield return null;
        for(int i=0;i<4;i++)for(int n=0;n<3;n++)SnailFieldProgress.Record(i);
        var allMissing=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SnailBossSetup.PrefabPath),player.transform.position+Vector3.right*4,Quaternion.identity).GetComponent<SnailBossRuntime>();
        allMissing.Initialize(player);allMissing.AutoPatterns=false;
        Check(allMissing.MissingMask==15&&Mathf.Abs(allMissing.Health-allMissing.settings.maxHealth*.3f)<.01f,"all fruit clears remove only phase1 health");
        Capture(allMissing,"all-fruit-missing");
        yield return new WaitForSeconds(2);
        Check(allMissing.Chocolate,"all fruit clears enter phase2 automatically");
        UnityEngine.Object.Destroy(allMissing.gameObject);SnailFieldProgress.Reset();yield return null;
        Check(SnailBossDebugSpawn.TrySpawn(),"Insert command accepted through existing terminal");
        float deadline=Time.time+12;
        while(UnityEngine.Object.FindObjectOfType<SnailBossRuntime>()==null&&Time.time<deadline)yield return null;
        var boss=UnityEngine.Object.FindObjectOfType<SnailBossRuntime>();Check(boss!=null,"terminal arrival yields snail");
        Check(!SnailBossDebugSpawn.TrySpawn(),"duplicate Insert rejected");
        boss.AutoPatterns=false; // Explicitly exercise each production pattern, retaining health/pose updates.
        boss.transform.position=player.transform.position+Vector3.right*4;
        yield return null;
        Capture(boss,"idle-phase1");
        yield return CheckMiniVisuals(boss);
        for(int frame=0;frame<8;frame++){yield return new WaitForSeconds(.15f);CaptureRig(boss,"idle1-"+frame);}
        foreach(var p in new[]{SnailPattern.Basic,SnailPattern.Bomb,SnailPattern.Fan,SnailPattern.Homing,SnailPattern.Summon,SnailPattern.Absorb,SnailPattern.Dash})
        {
            Check(boss.UsePattern(p),"phase1 start "+p);yield return new WaitForSeconds(.55f);
            Capture(boss,"phase1-"+p);
            while(boss.Busy||boss.Groggy)yield return null;
        }
        var slow=player.GetComponent<SnailSlowStatus>()??player.gameObject.AddComponent<SnailSlowStatus>();
        slow.Clear();slow.Enter(101,.25f);Check(Mathf.Approximately(slow.Multiplier,.75f),"cream slow");
        slow.Enter(102,.3f);Check(Mathf.Approximately(slow.Multiplier,.7f),"overlap chooses strongest");
        slow.Exit(102,0);Check(Mathf.Approximately(slow.Multiplier,.75f),"leaving chocolate keeps cream");
        slow.Exit(101,0);Check(slow.Multiplier==1,"dash exit immediate restore");
        slow.Enter(103,.3f);yield return new WaitForSeconds(2.2f);Check(Mathf.Approximately(slow.Multiplier,.7f),"standing in puddle never expires");
        slow.Exit(103,2);yield return new WaitForSeconds(1);Check(slow.Multiplier<1,"Kisses exit tail persists");
        yield return new WaitForSeconds(1.1f);Check(slow.Multiplier==1,"Kisses exit tail expires");
        boss.TakeDamage(boss.settings.maxHealth*.71f);while(boss.Transitioning)yield return null;
        Check(boss.Chocolate,"30% threshold transitions to chocolate");
        boss.HealFraction(.5f);Check(boss.Chocolate,"healing never reverts phase");
        yield return new WaitForSeconds(.25f);Capture(boss,"idle-phase2");
        yield return CheckMiniVisuals(boss);
        for(int frame=0;frame<8;frame++){yield return new WaitForSeconds(.15f);CaptureRig(boss,"idle2-"+frame);}
        foreach(var p in new[]{SnailPattern.Basic,SnailPattern.Bomb,SnailPattern.Fan,SnailPattern.Homing,SnailPattern.Summon,SnailPattern.Absorb,SnailPattern.Dash})
        {
            Check(boss.UsePattern(p),"phase2 start "+p);yield return new WaitForSeconds(.55f);Capture(boss,"phase2-"+p);
            while(boss.Busy||boss.Groggy)yield return null;
        }
        Check(boss.UsePattern(SnailPattern.Dash),"trap dash starts");while(!boss.IsDashing)yield return null;
        float before=boss.Health;boss.TriggerTrap();Check(boss.Groggy&&boss.Health<before&&!boss.Busy,"trap interrupts dash and groggies");
        float damaged=boss.Health;boss.TakeDamage(10);Check(Mathf.Abs(damaged-boss.Health-15)<.01f,"groggy multiplier 1.5 without core");
        Capture(boss,"groggy");
        boss.AutoPatterns=false;
        yield return new WaitForSeconds(boss.settings.groggyDuration+.1f);
        Check(!boss.Groggy,"groggy ends");
        Check(boss.UsePattern(SnailPattern.Basic),"pattern before mini-stage pause");yield return new WaitForSeconds(.6f);
        MiniStageRuntimeState.EnterMiniStage(null);yield return null;yield return null;
        Check(!boss.Busy&&boss.EffectsRoot.childCount==0,"mini-stage cancels boss attacks and effects");
        Vector3 frozen=boss.transform.position;yield return new WaitForSeconds(.3f);
        Check(boss.transform.position==frozen,"mini-stage cannot drag boss into room");
        MiniStageRuntimeState.ExitMiniStage(null);yield return null;
        boss.TakeDamage(999999);Check(boss.Dead,"lethal damage enters death");
        yield return new WaitForSeconds(1.7f);Check(level.IsLevelEnded,"boss death completes level");
        Debug.Log("SNAIL_PLAY_ALL_PASS");
        SessionState.SetBool(Key+"Done",true);
    }
    static IEnumerator CheckMiniVisuals(SnailBossRuntime boss)
    {
        int fieldMask=SnailFieldProgress.Mask;
        for(int i=0;i<4;i++)
        {
            foreach(bool absorbing in new[]{false,true})
            {
                Vector2 position=(Vector2)(absorbing?boss.transform.position:boss.Player.transform.position)+new Vector2(2+i*.2f,2);
                var mini=SnailBossMinion.Spawn(boss,position,i,absorbing);
                var rig=mini.GetComponentInChildren<SnailMiniVisual>();
                Check(rig!=null&&rig.Chocolate==boss.Chocolate&&rig.Ingredient==i,
                    "mini role/phase/kind "+absorbing+" "+boss.Chocolate+" "+i);
                var shell=rig.Shell.sprite;var scale=rig.Shell.transform.localScale;
                float phase=rig.WalkPhase;Vector3 before=mini.transform.position;
                yield return new WaitForSeconds(.19f);
                Check(mini!=null&&mini.transform.position!=before&&rig.WalkPhase!=phase,
                    "mini movement animates "+absorbing+" "+boss.Chocolate+" "+i);
                Check(rig.Shell.sprite==shell&&rig.Shell.transform.localScale==scale,
                    "mini rigid shell "+absorbing+" "+boss.Chocolate+" "+i);
                Check(Mathf.Approximately(mini.Health,boss.settings.summonHealth*(boss.Chocolate?1.3f:1)),
                    "mini unchanged health "+absorbing+" "+boss.Chocolate+" "+i);
                mini.TakeDamage(99999);
                yield return null;
                Check(SnailFieldProgress.Mask==fieldMask,"mini kill does not alter field progress "+absorbing+" "+boss.Chocolate+" "+i);
            }
        }
    }
    static void CaptureRig(SnailBossRuntime boss,string label)
    {
        var renderers=boss.GetComponentsInChildren<SpriteRenderer>();var layers=new int[renderers.Length];
        for(int i=0;i<renderers.Length;i++){layers[i]=renderers[i].gameObject.layer;renderers[i].gameObject.layer=30;}
        boss.Visual.Flash(0);
        try{Capture(boss,label,true);}finally{for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];}
    }
    static void Capture(SnailBossRuntime boss,string label,bool isolated=false)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var go=new GameObject("QA camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.8f;
        if(isolated){camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(1,.98f,.95f);camera.orthographicSize=2.65f;}
        camera.transform.position=boss.transform.position+new Vector3(0,1.7f,-30);
        var target=new RenderTexture(1024,768,24);camera.targetTexture=target;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1024,768,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1024,768),0,0);tex.Apply();
        System.IO.Directory.CreateDirectory("Logs/SnailQA");System.IO.File.WriteAllBytes("Logs/SnailQA/"+label+".png",tex.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(go);
    }
}
