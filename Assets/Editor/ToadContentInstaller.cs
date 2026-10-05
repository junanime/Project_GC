using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;

public static class ToadContentInstaller
{
    const string Root="Assets/Resources/ToadUpdate/";
    public const string PrefabPath="Assets/Junhan/Prefabs/Boss/AcidToad.prefab";
    public const string BlueprintPath="Assets/Resources/ToadUpdate/AcidToadBlueprint.asset";
    [MenuItem("Tools/Junhan2/Install acid toad and walnut update")]
    public static void Install()
    {
        foreach(string name in new[]{"ToadLocomotion","ToadAttack","ToadJump","SquirrelMotion","SquirrelReact","AcidFx","LightWoodUI"})Import(name);
        if(File.Exists(Root+"ToadHead.png"))Import("ToadHead");
        var source=AssetDatabase.LoadAssetAtPath<MiniBossMonsterBlueprint>("Assets/Junhan/Prefabs/Boss/MiniBoss_08min.asset");
        var bp=AssetDatabase.LoadAssetAtPath<MiniBossMonsterBlueprint>(BlueprintPath);
        if(bp==null){bp=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(bp,BlueprintPath);}
        bp.name="위산 두꺼비";bp.description="볼에 위산을 모아 뿜는 미니보스. 산성 구체 3발, 점프 착지, 산성액 발사.";
        bp.hp=1200;bp.atk=16;bp.movespeed=.75f;bp.visualScaleMultiplier=1;bp.walkFrameTime=.10625f;
        bp.walkSpriteSequence=Sprites("ToadLocomotion").Take(8).ToArray();bp.resultSprite=bp.walkSpriteSequence[0];
        bp.useGroundShadowFootprint=true;bp.groundShadowCenterUV=new Vector2(.5f,.03f);bp.groundShadowSizeUV=new Vector2(.8f,.15f);
        EditorUtility.SetDirty(bp);
        var root=PrefabUtility.LoadPrefabContents("Assets/Junhan/Prefabs/Boss/MiniBossMonster.prefab");
        try
        {
            var old=root.GetComponent<MiniBossMonster>();var oldSo=new SerializedObject(old);
            string[] names={"defaultMaterial","whiteMaterial","dissolveMaterial","deathParticles","shadow"};
            var values=names.Select(n=>oldSo.FindProperty(n).objectReferenceValue).ToArray();
            UnityEngine.Object.DestroyImmediate(old);
            var monster=root.AddComponent<AcidToadMonster>();var so=new SerializedObject(monster);
            for(int i=0;i<names.Length;i++)so.FindProperty(names[i]).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();
            root.name="위산 두꺼비";root.transform.localScale=Vector3.one;
            var art=root.GetComponentsInChildren<SpriteRenderer>().First(s=>!s.name.ToLowerInvariant().Contains("shadow"));
            art.sprite=bp.walkSpriteSequence[0];art.transform.localScale=Vector3.one;art.transform.localPosition=Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach(var guid in AssetDatabase.FindAssets("t:LevelBlueprint"))
        {
            var level=AssetDatabase.LoadAssetAtPath<LevelBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
            if(level.monsters==null||level.monsters.Length==0)continue;
            if(!level.monsters.Any(c=>c.monstersPrefab==prefab))
                level.monsters=level.monsters.Concat(new[]{new LevelBlueprint.MonstersContainer{monstersPrefab=prefab,monsterBlueprints=new MonsterBlueprint[]{bp}}}).ToArray();
            EditorUtility.SetDirty(level);
        }
        InstallSquirrel();
        AssetDatabase.SaveAssets();Debug.Log("TOAD_INSTALL_PASS");
    }
    static Sprite[] Sprites(string sheet)=>AssetDatabase.LoadAllAssetsAtPath(Root+sheet+".png").OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
    static void Import(string name)
    {
        string path=Root+name+".png";AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int columns=4,rows=name=="ToadLocomotion"||name=="ToadAttack"||name=="SquirrelMotion"||name=="ToadHead"?4:2;
        Rect[] rects;
        if(name=="LightWoodUI")
        {
            // The generated atlas deliberately preserves the wide 4x1 tray, not square cells.
            rects=new[]{new Rect(0,texture.height-330,1005,330),new Rect(1020,texture.height-300,754,210),
                new Rect(128,34,780,texture.height-545),new Rect(1195,75,350,335)};
        }
        else
        {
            rects=new Rect[columns*rows];
            var components=name=="AcidFx"?null:Components(texture);
            for(int i=0;i<rects.Length;i++)
            {
                int x0=i%columns*texture.width/columns,x1=(i%columns+1)*texture.width/columns;
                int y0=texture.height-(i/columns+1)*texture.height/rows,y1=texture.height-i/columns*texture.height/rows;
                var cell=new Rect(x0,y0,x1-x0,y1-y0);
                rects[i]=components==null?AlphaBounds(texture,cell):components.Where(r=>cell.Contains(r.center)).OrderByDescending(r=>r.width*r.height).FirstOrDefault();
                if(rects[i].width<=0)throw new Exception("No sprite component: "+name+" "+i);
            }
        }
        var metadata=new SpriteMetaData[rects.Length];
        for(int i=0;i<rects.Length;i++)metadata[i]=new SpriteMetaData{name=name+"_"+i.ToString("00"),rect=rects[i],alignment=9,pivot=name=="LightWoodUI"||name=="AcidFx"||name=="ToadHead"?new Vector2(.5f,.5f):new Vector2(.5f,0)};
        importer.spritesheet=metadata;
        importer.spritePixelsPerUnit=name=="ToadHead"?rects[0].width/2.3f:name.StartsWith("Toad")?rects[0].width/2.8f:name.StartsWith("Squirrel")?rects[0].width/1.4f:128;
        importer.SaveAndReimport();
    }
    static System.Collections.Generic.List<Rect> Components(Texture2D texture)
    {
        var pixels=texture.GetPixels32();int w=texture.width,h=texture.height;var seen=new bool[pixels.Length];
        var found=new System.Collections.Generic.List<Rect>();var queue=new System.Collections.Generic.Queue<int>();
        for(int seed=0;seed<pixels.Length;seed++)
        {
            if(seen[seed]||pixels[seed].a<100)continue;
            int minX=w,minY=h,maxX=0,maxY=0,area=0;queue.Enqueue(seed);seen[seed]=true;
            while(queue.Count>0)
            {
                int at=queue.Dequeue(),x=at%w,y=at/w;area++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                foreach(int step in new[]{-1,1,-w,w}){int n=at+step;if(n<0||n>=pixels.Length||Math.Abs(n%w-x)>1||seen[n]||pixels[n].a<100)continue;seen[n]=true;queue.Enqueue(n);}
            }
            if(area>500){int x=Math.Max(0,minX-1),y=Math.Max(0,minY-1);found.Add(new Rect(x,y,Math.Min(w,maxX+2)-x,Math.Min(h,maxY+2)-y));}
        }
        return found;
    }
    static Rect AlphaBounds(Texture2D t,Rect cell)
    {
        var pixels=t.GetPixels32();int minX=(int)cell.xMax,minY=(int)cell.yMax,maxX=(int)cell.x,maxY=(int)cell.y;
        for(int y=(int)cell.y;y<(int)cell.yMax;y++)for(int x=(int)cell.x;x<(int)cell.xMax;x++)if(pixels[y*t.width+x].a>32)
        {minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        return maxX>minX&&maxY>minY?new Rect(minX,minY,maxX-minX+1,maxY-minY+1):cell;
    }
    static void InstallSquirrel()
    {
        float width=1.2f;
        var levels=AssetDatabase.FindAssets("t:LevelBlueprint").Select(g=>AssetDatabase.LoadAssetAtPath<LevelBlueprint>(AssetDatabase.GUIDToAssetPath(g)));
        foreach(var level in levels)foreach(var container in level.monsters??Array.Empty<LevelBlueprint.MonstersContainer>())
            foreach(var b in container.monsterBlueprints??Array.Empty<MonsterBlueprint>())
                if(b!=null&&b.walkSpriteSequence!=null&&b.walkSpriteSequence.Length>0&&b.walkSpriteSequence[0]!=null&&AssetDatabase.GetAssetPath(b.walkSpriteSequence[0]).Contains("PopcornPom"))
                {
                    var sr=container.monstersPrefab.GetComponentsInChildren<SpriteRenderer>().First(s=>!s.name.ToLowerInvariant().Contains("shadow"));
                    width=b.walkSpriteSequence[0].bounds.size.x*Mathf.Abs(sr.transform.lossyScale.x);break;
                }
        const string path="Assets/Prefabs/Monsters/SpecialMonster_TreasureRunner.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so=new SerializedObject(root.GetComponent<TreasureRunnerMonster>());
            so.FindProperty("walnutBodyWidth").floatValue=width;so.FindProperty("approachMoveSpeed").floatValue=1.1f;
            so.FindProperty("fleeMoveSpeed").floatValue=2.65f;so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        var bp=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/TreasureRunner Monster Blueprint.asset");
        bp.name="호두 다람쥐 도둑";bp.walkSpriteSequence=Sprites("SquirrelMotion").Take(8).ToArray();bp.walkFrameTime=.14375f;bp.resultSprite=bp.walkSpriteSequence[0];
        bp.useGroundShadowFootprint=true;bp.groundShadowCenterUV=new Vector2(.53f,.035f);bp.groundShadowSizeUV=new Vector2(.65f,.15f);
        EditorUtility.SetDirty(bp);Debug.Log("WALNUT_POPCORN_WIDTH="+width);
    }
}
