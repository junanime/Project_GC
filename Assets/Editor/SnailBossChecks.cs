using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;
public static class SnailBossChecks
{
    public static int Count {get;private set;}
    public static void Check(bool valid,string message)
    {if(!valid)throw new Exception("SNAIL FAIL: "+message);Count++;Debug.Log("SNAIL PASS: "+message);}
    [MenuItem("Tools/Junhan2/Validate roll cake snail")]
    public static void Run()
    {
        Count=0;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SnailBossSetup.PrefabPath);
        Check(prefab!=null&&prefab.GetComponent<SnailBossRuntime>()!=null,"snail prefab installed");
        var config=Resources.Load<SnailBossSettings>("SnailBossSettings");
        Check(config!=null,"settings available");
        Check(Mathf.Approximately(config.chocolateSlow,config.vanillaSlow*1.2f),"chocolate slow is 1.2x slow amount");
        Check(config.phaseThreshold==.3f,"phase 2 at 30% remaining");
        for(int i=0;i<=4;i++)Check(Mathf.Abs(SnailBossRules.SpawnFraction(i)-(1-.175f*i))<.0001f,"phase1 reduction "+i);
        SnailFieldProgress.Reset();for(int fruit=0;fruit<4;fruit++){SnailFieldProgress.Record(fruit);SnailFieldProgress.Record(fruit);Check(!SnailFieldProgress.Missing(fruit),"two kills keep ingredient");SnailFieldProgress.Record(fruit);Check(SnailFieldProgress.Missing(fruit),"three kills disable ingredient");}
        Check(SnailFieldProgress.Mask==15,"all four removed");SnailFieldProgress.Reset();
        Check(SnailBossRules.BurstCount(false)==2&&SnailBossRules.BurstCount(true)==3,"basic 2/3 bursts");
        Check(SnailBossRules.BombCount(false)==2&&SnailBossRules.BombCount(true)==5,"bomb 2/5 throws");
        Check(SnailBossRules.PlateLaneDegrees(1,12)==0,"middle tablet lane stays straight");
        Check(SnailBossRules.PlateLaneDegrees(0,12)==-SnailBossRules.PlateLaneDegrees(2,12),"outer lanes spread symmetrically");
        Check(SnailBossRules.AbsorbCount(false)==6&&SnailBossRules.AbsorbCount(true)==12,"absorb minion count");
        foreach(string key in new[]{"Shell1","Shell2","Body1","Body2","Groggy1","Groggy2","Puff1","Puff2","DashPuff1","DashPuff2","Spit1","Spit2","Toppings1","Toppings2","Blueberry","Strawberry","Melon","Mango","Ball","Kisses","Tablet","Bar","CreamPool","ChocolatePool","Wrapper"})
        {
            var sprite=SnailBossArt.Get(key);Check(sprite!=null,"art "+key);
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Check(importer.alphaIsTransparency&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"uncompressed alpha "+key);
        }
        foreach(string guid in AssetDatabase.FindAssets("t:LevelBlueprint"))
        {
            var level=AssetDatabase.LoadAssetAtPath<LevelBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
            if(level.finalBoss!=null&&level.finalBoss.bossPrefab!=null)Check(level.finalBoss.bossPrefab==prefab,"spawner wired "+level.name);
        }
        var go=new GameObject("Scale check");
        try
        {
            var visual=go.AddComponent<SnailBossVisual>();visual.Configure(config.bodyWidth);
            foreach(bool phase in new[]{false,true})
            {
                visual.SetPhase(phase);float shell=visual.Shell.transform.localScale.x;var sprite=visual.Shell.sprite;
                foreach(SnailAction action in Enum.GetValues(typeof(SnailAction)))
                {visual.Pose(action);Check(visual.Shell.sprite==sprite&&visual.Shell.transform.localScale.x==shell,"immutable shell "+phase+" "+action);}
            }
        }finally{UnityEngine.Object.DestroyImmediate(go);}
        Debug.Log("SNAIL_CHECKS_PASS count="+Count);
    }
}
