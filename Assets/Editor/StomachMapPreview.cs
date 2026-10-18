using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    // Terrain, slope/spawn/mini-stage integration checks and real Level 1 captures.
    [InitializeOnLoad]
    public static class StomachMapPreview
    {
        const string Key="StomachMapPreview",Output="Library/StomachMapProof";
        const string DefinitionPath="Assets/Resources/StomachMap/Stage1.asset";
        static bool started; static double deadline;
        [Serializable] class Entry{public string id;}
        [Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static StomachMapPreview(){if(SessionState.GetBool(Key,false))Attach();}

        [MenuItem("Tools/Stomach Map/Install and capture Ashi preview")]
        public static void InstallAndRun()
        {
            Directory.CreateDirectory(Output);
            if(File.Exists(Output+"/errors.txt"))File.Delete(Output+"/errors.txt");
            if(File.Exists(Output+"/result.txt"))File.Delete(Output+"/result.txt");
            ImportTexture("Assets/Art/StomachMap/StomachLayout.png",TextureWrapMode.Clamp);
            ImportTexture("Assets/Art/StomachMap/FineFloor.png",TextureWrapMode.Mirror);
            ImportTexture("Assets/Art/StomachMap/FoldedWall.png",TextureWrapMode.Clamp);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/StomachMap/Terrain.shader");
            if(shader==null || UnityEditor.ShaderUtil.ShaderHasError(shader))throw new Exception("Terrain shader did not compile");
            const string materialPath="Assets/Resources/StomachMap/Terrain.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,materialPath);}
            mat.shader=shader;
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/StomachMap/StomachLayout.png");
            mat.SetTexture("_DetailTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/StomachMap/FineFloor.png"));
            mat.SetTexture("_WallTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/StomachMap/FoldedWall.png"));
            mat.SetFloat("_DetailStrength",1f); mat.SetFloat("_LayoutLod",1.25f);
            mat.SetFloat("_PixelSize",StomachMapDefinition.ReferencePixelSize);
            EditorUtility.SetDirty(mat);
            var definition=AssetDatabase.LoadAssetAtPath<StomachMapDefinition>(DefinitionPath);
            if(definition==null){definition=ScriptableObject.CreateInstance<StomachMapDefinition>();AssetDatabase.CreateAsset(definition,DefinitionPath);}
            definition.terrainMaterial=mat; definition.pixelWorldSize=StomachMapDefinition.ReferencePixelSize; EditorUtility.SetDirty(definition);
            // Add only our serialized reference. Saving the entire legacy scene also migrates
            // unrelated event fields; avoid changing those in a terrain-only update.
            const string scenePath="Assets/Scenes/Game/Level 1.unity";
            string source=File.ReadAllText(scenePath);
            if(!source.Contains("  stomachMap:"))
            {
                string scriptGuid=AssetDatabase.AssetPathToGUID("Assets/Scripts/Gameplay/LevelManager.cs");
                int component=source.IndexOf("guid: "+scriptGuid,StringComparison.Ordinal);
                int field=source.IndexOf("  infiniteBackground:",component,StringComparison.Ordinal);
                int lineEnd=source.IndexOf('\n',field)+1;
                if(component<0 || field<0 || lineEnd<1)throw new Exception("LevelManager terrain insertion anchor missing");
                source=source.Insert(lineEnd,"  stomachMap: {fileID: 11400000, guid: "+AssetDatabase.AssetPathToGUID(DefinitionPath)+", type: 2}\n");
                File.WriteAllText(scenePath,source,new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(scenePath,ImportAssetOptions.ForceSynchronousImport);
            }
            EditorSceneManager.OpenScene(scenePath);
            AssetDatabase.SaveAssets();
            Run();
        }
        static void ImportTexture(string path,TextureWrapMode wrap)
        {
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Default;imp.npotScale=TextureImporterNPOTScale.None;
            imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=4096;
            imp.mipmapEnabled=true;imp.filterMode=FilterMode.Trilinear;imp.wrapMode=wrap;
            imp.sRGBTexture=true;imp.alphaSource=TextureImporterAlphaSource.None;imp.isReadable=false;
            imp.SaveAndReimport();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(File.Exists(Output+"/prefs.json"))Restore();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1600,941);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Restore()
        {
            if(!File.Exists(Output+"/prefs.json"))return;
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs.json")).values)
            {if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}
            PlayerPrefs.Save();File.Delete(Output+"/prefs.json");
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+300;started=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");
            SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Preview timeout");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");Debug.Log("[StomachMap] FINISHED failed="+failed);
                EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
                if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            // Focus-loss pause can hold levelTime at zero in a hidden batch editor. Initialization
            // is complete once the finite map exists; CapturePreview explicitly resumes the test.
            if(!started && EditorApplication.isPlaying && level!=null && level.PlayerCharacter!=null && level.GetComponent<FiniteStomachMap>()!=null)
            {started=true;level.StartCoroutine(CapturePreview(level));}
        }
        static void Check(bool value,string message){if(!value)throw new Exception("[StomachMap] "+message);Debug.Log("[StomachMap] PASS "+message);}
        static IEnumerator Screenshot(string name)
        {yield return null;ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return null;yield return null;}
        static void CameraScreenshot(Camera camera,string name,int width,int height)
        {
            var target=new RenderTexture(width,height,24); var previous=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        static IEnumerator CapturePreview(LevelManager level)
        {
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=1f/60;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var map=level.GetComponent<FiniteStomachMap>();
            Check(map!=null && map.ChunkCount>1,"finite terrain installed in actual Level 1");
            Check(map.Contains(level.PlayerCharacter.transform.position),"Ashi starts inside stomach");
            Check(!Object.FindObjectOfType<InfiniteBackground>().GetComponent<Renderer>().enabled,"infinite field is hidden");
            var definition=map.Definition;
            Check(Mathf.Abs(definition.worldWidth-203.2381f)<.01f,"map ratio follows half-size 7px reference marker");
            Check(Mathf.Abs(definition.pixelWorldSize-StomachMapDefinition.ReferencePixelSize)<.00001f,"floor scale follows latest yellow-box reference");
            var player=level.PlayerCharacter;
            var animator=player.GetComponentInChildren<SpriteAnimator>();var ash=animator.GetComponent<SpriteRenderer>();
            yield return new WaitForSeconds(1.2f);
            yield return Screenshot("01-ashi-gameplay");
            var needle=Object.FindObjectsOfType<SyringeProjectile>().FirstOrDefault(n=>n.IsFlying);
            string needleInfo="no live needle at sample frame";
            if(needle!=null)
            {
                var sr=needle.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.sprite!=null && r.sprite.texture.name=="basic_niddle2");
                if(sr!=null)
                {
                    float actualPixel=12f*sr.transform.lossyScale.x/sr.sprite.pixelsPerUnit;
                    needleInfo=$"needle renderer scale={sr.transform.lossyScale} ppu={sr.sprite.pixelsPerUnit} painted pixel={actualPixel:0.000000}";
                    // Needle size is recorded only; the latest art direction supersedes it.
                }
            }
            File.WriteAllText(Output+"/measurements.txt",$"Map world size: {map.WorldRect.size}\nChunks: {map.ChunkCount}\nAshi sprite: {ash.sprite.name} rect={ash.sprite.rect} ppu={ash.sprite.pixelsPerUnit} scale={ash.transform.lossyScale}\nAshi visible source width: 330px (alpha > 128)\nMap ratio: 1164 / 3.5 = {1164f/3.5f:0.000} visible Ashi widths\nTerrain pixel: {definition.pixelWorldSize:0.000000} world units\n{needleInfo}\nScreen: {Screen.width} x {Screen.height}\nCamera ortho: {Camera.main.orthographicSize}\n");
            // Freeze only this temporary preview's combat so scale views remain readable.
            // Production spawning and all mini-stage behavior are left unchanged.
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner || b is SupplyCapsuleDirector){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.gameObject.SetActive(false);
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.gameObject.SetActive(false);
            var ability=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());
            if(ability!=null){ability.enabled=false;ability.StopAllCoroutines();}
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.gameObject.SetActive(false);
            yield return StomachMapChecks.Run(level,map);
            var original=player.transform.position;
            var body=player.GetComponent<Rigidbody2D>();body.velocity=Vector2.zero;
            var camGO=new GameObject("Map overview capture camera");var overview=camGO.AddComponent<Camera>();overview.CopyFrom(Camera.main);overview.enabled=false;
            overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=new Color(.20f,.045f,.07f);
            overview.aspect=1.5f;overview.orthographicSize=map.WorldRect.height*.515f;
            overview.transform.position=new Vector3(map.WorldRect.center.x,map.WorldRect.center.y,Camera.main.transform.position.z);
            // Overview uses the same scene and real player; only hide unrelated actors for clarity.
            var isolated=player.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject)
                .Concat(map.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Stomach terrain [")).Select(r=>r.gameObject)).Distinct().ToArray();
            var oldLayers=isolated.Select(g=>g.layer).ToArray();
            foreach(var g in isolated)g.layer=31;
            overview.cullingMask=1<<31;
            CameraScreenshot(overview,"02-full-map-real-ashi",2304,1536);
            for(int i=0;i<isolated.Length;i++)isolated[i].layer=oldLayers[i];
            Object.Destroy(camGO);
            Vector2 edge=map.UVToWorld(new Vector2(.245f,1));
            Vector2 foot=map.ClampToField(edge,map.WallWidth+.65f);
            player.Move(Vector2.zero);player.transform.position=new Vector3(foot.x,foot.y,original.z);body.position=foot;
            yield return new WaitForSeconds(1.5f);yield return Screenshot("06-wall-ground-view");
            Vector2 wall=map.ClampToField(edge,map.ClimbLimit+.3f);
            map.DistanceToRim(wall,out var wallInward);player.Move(-wallInward);
            player.transform.position=new Vector3(wall.x,wall.y,original.z);body.position=wall;
            yield return new WaitForSeconds(1.5f);yield return Screenshot("03-wall-slope-ashi");
            player.Move(Vector2.zero);
            // Inspect a mesh join at normal gameplay zoom, with Ashi at its exact corner.
            Vector2 join=map.WorldRect.min+Vector2.one*definition.chunkSize*5;
            Check(map.Contains(join),"chunk seam sample inside playfield");
            player.transform.position=new Vector3(join.x,join.y,original.z);body.position=join;
            yield return new WaitForSeconds(1.5f);yield return Screenshot("04-chunk-seam-ashi");
            player.transform.position=original;body.position=original;
            yield return new WaitForSeconds(.8f);
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
