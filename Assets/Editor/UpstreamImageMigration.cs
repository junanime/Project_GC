using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Generates new mathematical placeholder art; never samples source image pixels.
// Existing GUIDs/sprite IDs are kept solely to preserve serialized game references.
public static class UpstreamImageMigration
{
    const string AuditPath = "Logs/ArtMigration/upstream-image-audit.json";
    const string ManifestPath = "Documentation/UpstreamImageMigration.json";
    const string DestinationRoot = "Assets/Art/Placeholders/LegacyReplacement/";
    [Serializable] public class Audit { public string @base; public Candidate[] candidates; }
    [Serializable] public class Candidate { public string path, guid, blob; public bool unchanged; }
    [Serializable] public class Manifest { public string sourceCommit; public Entry[] entries; }
    [Serializable] public class Entry
    {
        public string originalPath, replacementPath, guid, originalBlob;
        public int width, height;
        public SpriteState[] sprites;
    }
    [Serializable] public class SpriteState
    {
        public string name; public long id; public Rect rect; public Vector2 pivot;
        public Vector4 border; public float pixelsPerUnit;
    }

    public static void Run()
    {
        try
        {
            if (File.Exists(ManifestPath)) throw new Exception("Migration manifest already exists. Use Verify, not Run.");
            var audit = JsonUtility.FromJson<Audit>(File.ReadAllText(AuditPath));
            if (audit.candidates == null || audit.candidates.Length != 58)
                throw new Exception("Expected the reviewed 58-image upstream inventory.");
            var entries = new List<Entry>();
            // Validate the entire deletion/replacement scope before the first mutation.
            foreach (var c in audit.candidates)
            {
                if (!c.unchanged || !c.path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    c.path.Contains("..") || Blob(c.path) != c.blob || AssetDatabase.AssetPathToGUID(c.path) != c.guid)
                    throw new Exception("Source changed or unsafe candidate: " + c.path);
                var importer = AssetImporter.GetAtPath(c.path) as TextureImporter;
                if (importer == null) throw new Exception("Not an imported texture: " + c.path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                var newPath = DestinationRoot + c.path.Substring(7).Replace("Kenney/", "GenericAtlas/");
                newPath = Path.ChangeExtension(newPath, ".png");
                if (File.Exists(newPath)) throw new Exception("Destination exists: " + newPath);
                entries.Add(new Entry { originalPath=c.path, replacementPath=newPath, guid=c.guid,
                    originalBlob=c.blob, width=width, height=height, sprites=CaptureSprites(c.path) });
            }
            var manifest = new Manifest { sourceCommit=audit.@base, entries=entries.ToArray() };
            Directory.CreateDirectory("Documentation");
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            foreach (var e in entries)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(e.originalPath);
#pragma warning disable 618
                var slices = importer.spritesheet;
#pragma warning restore 618
                var data = Generate(e, slices);
                EnsureAssetFolder(Path.GetDirectoryName(e.replacementPath).Replace('\\','/'));
                var error = AssetDatabase.MoveAsset(e.originalPath, e.replacementPath);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                File.WriteAllBytes(e.replacementPath, data);
                AssetDatabase.ImportAsset(e.replacementPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Debug.Log("[ArtMigration] Replaced " + e.originalPath);
            }
            AssetDatabase.SaveAssets();
            VerifyCore(manifest);
            SnailBossChecks.Run();
            Debug.Log("ART_MIGRATION_PASS images=" + entries.Count);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void Verify()
    {
        try { VerifyCore(JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath))); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void VerifyAndBuild()
    {
        try
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            VerifyCore(manifest);
            var ids=manifest.entries.ToDictionary(e=>e.guid,e=>new HashSet<long>(
                AssetDatabase.LoadAllAssetsAtPath(e.replacementPath).Where(o=>o!=null).Select(o=>{
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id);return id;
                })));
            var pattern=new Regex(@"\{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}");
            var extensions=new HashSet<string>{".unity",".prefab",".asset",".mat",".anim",".controller",".overrideController",".spriteatlas"};
            int checkedReferences=0;
            foreach(var path in Directory.EnumerateFiles("Assets","*",SearchOption.AllDirectories))
            {
                if(!extensions.Contains(Path.GetExtension(path)))continue;
                foreach(Match match in pattern.Matches(File.ReadAllText(path)))
                {
                    if(!ids.TryGetValue(match.Groups[2].Value,out var known))continue;
                    long id=long.Parse(match.Groups[1].Value);
                    if(!known.Contains(id))throw new Exception("Unresolved migrated reference: "+path+" "+match.Value);
                    checkedReferences++;
                }
            }
            Debug.Log("ART_REFERENCE_CHECK_PASS references="+checkedReferences);
            // The old CharacterDesignTests also requires identical starting skills;
            // that predates the current character-specific abilities. Test art only.
            var catalog=Resources.Load<Vampire.ApothecaryUIConfig>("ApothecaryUIConfig");
            if(catalog==null || catalog.characters.Length!=4)throw new Exception("Character catalog missing");
            int characterFrames=0;
            foreach(var character in catalog.characters)
            {
                if(character.profileSprite==null)throw new Exception("Missing portrait: "+character.name);
                foreach(var sequence in new[]{character.walkSpriteSequence,character.idleSpriteSequence,
                    character.dashSpriteSequence,character.capturedSpriteSequence})
                {
                    if(sequence==null || sequence.Length==0)throw new Exception("Missing animation: "+character.name);
                    foreach(var sprite in sequence)
                    {
                        if(sprite==null || sprite.pixelsPerUnit<=0)throw new Exception("Missing animation frame: "+character.name);
                        characterFrames++;
                    }
                }
            }
            Debug.Log("ART_CHARACTER_CHECK_PASS characters=4 frames="+characterFrames);
            SnailBossChecks.Run();
            Vampire.Editor.WindowsDesktopBuild.Run();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }

    static void VerifyCore(Manifest manifest)
    {
        int sprites=0;
        foreach(var e in manifest.entries)
        {
            if(File.Exists(e.originalPath) || File.Exists(e.originalPath + ".meta"))
                throw new Exception("Original image still exists: " + e.originalPath);
            if(AssetDatabase.AssetPathToGUID(e.replacementPath)!=e.guid || Blob(e.replacementPath)==e.originalBlob)
                throw new Exception("Invalid replacement: " + e.replacementPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(e.replacementPath);
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            if(width!=e.width || height!=e.height) throw new Exception("Dimensions changed: " + e.replacementPath);
            var current=CaptureSprites(e.replacementPath);
            if(current.Length!=e.sprites.Length) throw new Exception("Sprite count changed: " + e.replacementPath);
            foreach(var previous in e.sprites)
            {
                var now=current.SingleOrDefault(s=>s.id==previous.id);
                if(now==null || now.rect!=previous.rect || now.pivot!=previous.pivot || now.border!=previous.border ||
                   !Mathf.Approximately(now.pixelsPerUnit,previous.pixelsPerUnit))
                    throw new Exception("Sprite reference/layout changed: " + e.replacementPath + " " + previous.name);
                sprites++;
            }
        }
        Debug.Log("ART_MIGRATION_VERIFY_PASS images="+manifest.entries.Length+" spriteIDs="+sprites);
    }

    static SpriteState[] CaptureSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Select(sprite=>{
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long id);
            return new SpriteState {name=sprite.name,id=id,rect=sprite.rect,pivot=sprite.pivot,
                border=sprite.border,pixelsPerUnit=sprite.pixelsPerUnit};
        }).OrderBy(s=>s.id).ToArray();
    }
    static void EnsureAssetFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        var parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static string Blob(string path)
    {
        byte[] data=File.ReadAllBytes(path), header=Encoding.UTF8.GetBytes("blob "+data.Length+"\0");
        using(var sha=SHA1.Create()) { sha.TransformBlock(header,0,header.Length,null,0); sha.TransformFinalBlock(data,0,data.Length);
            return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant(); }
    }

    static byte[] Generate(Entry e, SpriteMetaData[] slices)
    {
        var pixels=new Color32[e.width*e.height];
        string name=Path.GetFileNameWithoutExtension(e.originalPath);
        if(e.originalPath.Contains("/Textures/")) PaintTexture(e,pixels);
        else if(slices!=null && slices.Length>0)
        { for(int i=0;i<slices.Length;i++) PaintSprite(e,pixels,slices[i].rect,name,i); }
        else PaintSprite(e,pixels,new Rect(0,0,e.width,e.height),name,0);
        var texture=new Texture2D(e.width,e.height,TextureFormat.RGBA32,false);
        try { texture.SetPixels32(pixels); texture.Apply(); return texture.EncodeToPNG(); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void PaintTexture(Entry e,Color32[] p)
    {
        // Procedural technical data: neutral X / upward Y flow; deterministic noise;
        // muted, opaque floor checker. No upstream raster content is reused.
        var random=new System.Random(240929);
        for(int y=0;y<e.height;y++)for(int x=0;x<e.width;x++)
        {
            Color color;
            if(e.originalPath.Contains("Flow")) color=new Color(.5f, .85f+.1f*Mathf.Sin(2*Mathf.PI*x/e.width),0,1);
            else if(e.originalPath.Contains("Noise"))
            { float n=e.originalPath.Contains("White")?(float)random.NextDouble():
                .5f+.20f*Mathf.Sin(2*Mathf.PI*x/e.width)*Mathf.Cos(2*Mathf.PI*y/e.height)+.1f*Mathf.Sin(8*Mathf.PI*x/e.width);
                color=new Color(n,n,n,1); }
            else
            { float n=((x/16+y/16)%2==0)? .82f:.9f;
                color=e.originalPath.Contains("White")?new Color(n,n,n,1):
                    e.originalPath.Contains("Reddish")?new Color(n,n*.73f,n*.64f,1):new Color(n*.72f,n*.77f,n*.68f,1); }
            p[y*e.width+x]=color;
        }
    }
    static void PaintSprite(Entry e,Color32[] p,Rect r,string name,int frame)
    {
        bool tile=name.Contains("Tilemap"), actor=e.originalPath.Contains("Characters/")||e.originalPath.Contains("Monsters/");
        bool white=name.Contains("Circle")||name.Contains("Square");
        Color tint=white?Color.white:Color.HSVToRGB(((StableSeed(name)%100)+frame%4*3)/100f,.5f,.95f);
        if(name.Contains("Coin"))tint=new Color(1,.78f,.24f);
        if(name.Contains("Gem"))tint=new Color(.25f,.85f,1);
        int left=Mathf.RoundToInt(r.x),bottom=Mathf.RoundToInt(r.y),w=Mathf.RoundToInt(r.width),h=Mathf.RoundToInt(r.height);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=(x+.5f)/w*2-1,v=(y+.5f)/h*2-1;
            float d=u*u+v*v; bool fill;
            if(tile) fill=true;
            else if(name.Contains("Square"))fill=true;
            else if(name.Contains("CircleOutline"))fill=d<.98f&&d>.72f;
            else if(name.Contains("Circle"))fill=d<.99f;
            else if(name.Contains("Pause"))fill=Mathf.Abs(v)<.75f&&Mathf.Abs(u)>.15f&&Mathf.Abs(u)<.6f;
            else if(name.Contains("Play"))fill=u>-.6f&&u<.8f&&Mathf.Abs(v)<(.8f-u)*.6f;
            else if(name.Contains("Gem")||name.Contains("Shuriken"))fill=Mathf.Abs(u)+Mathf.Abs(v)<.88f;
            else if(name.Contains("Sword")||name.Contains("saber")||name.Contains("Dagger")||name.Contains("Machete")||name=="Bat")
                fill=(Mathf.Abs(u)<.20f&&Mathf.Abs(v)<.88f)||(Mathf.Abs(u)<.60f&&v>-.55f&&v<-.30f);
            else if(name.Contains("gun")||name.Contains("Gun")||name.Contains("Bazooka"))fill=Mathf.Abs(u)<.85f&&Mathf.Abs(v)<.45f;
            else if(name.Contains("Magnet"))fill=d<.8f&&d>.30f&&v<.35f;
            else if(name.Contains("Teeth"))fill=Mathf.Abs(u)<.8f&&v<.55f&&v>-.6f+Mathf.Abs(u)*.8f;
            else if(actor) { float bob=(frame%2)*.08f; fill=u*u/.64f+(v+bob)*(v+bob)/.75f<1; }
            else fill=d<.78f;
            if(!fill)continue;
            Color c=tint;
            if(tile)c=((x/4+y/4)%2==0)?tint*.80f:tint;
            else if(!white && (actor||name.Contains("Emoji")))
            { if(Mathf.Abs(Mathf.Abs(u)-.28f)<.12f&&Mathf.Abs(v-.18f)<.12f)c=new Color(.1f,.15f,.2f); }
            else if(!white && d<.16f)c=Color.Lerp(tint,Color.white,.65f);
            c.a=1;
            int px=left+x,py=bottom+y;
            if(px>=0&&py>=0&&px<e.width&&py<e.height)p[py*e.width+px]=c;
        }
    }
    static int StableSeed(string value) { int h=0; foreach(char c in value)h=(h*31+c)&0x7fffffff; return h; }
}
