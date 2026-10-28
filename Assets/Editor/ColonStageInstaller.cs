using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class ColonStageInstaller
    {
        const string Root="Assets/Resources/";
        [MenuItem("Tools/Junhan2/Install colon stage and boss travel")]
        public static void Install()
        {
            Import(Root+"ColonMap/Layout.png",false);
            Import(Root+"BossTravel/Hole.png",true);
            string suitsPath=Root+"BossTravel/Suits.png";Import(suitsPath,true);
            var importer=(TextureImporter)AssetImporter.GetAtPath(suitsPath);importer.isReadable=true;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(suitsPath);var pixels=texture.GetPixels32();
            var sprites=new SpriteMetaData[5];
            for(int i=0;i<5;i++)
            {
                int left=i*texture.width/5,right=(i+1)*texture.width/5,minX=right,minY=texture.height,maxX=left,maxY=0;
                for(int y=0;y<texture.height;y++)for(int x=left;x<right;x++)if(pixels[y*texture.width+x].a>32){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                if(maxX<=minX||maxY<=minY)throw new Exception("Missing suit cell "+i);
                sprites[i]=new SpriteMetaData{name="Suit"+i,rect=Rect.MinMaxRect(minX,minY,maxX+1,maxY+1),alignment=9,pivot=Vector2.one*.5f};
            }
            importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritesheet=sprites;importer.isReadable=false;importer.SaveAndReimport();
            var art=AssetDatabase.LoadAssetAtPath<BossTravelDefinition>(Root+"BossTravel/Definition.asset");
            if(art==null){art=ScriptableObject.CreateInstance<BossTravelDefinition>();AssetDatabase.CreateAsset(art,Root+"BossTravel/Definition.asset");}
            art.hole=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"BossTravel/Hole.png");art.suits=AssetDatabase.LoadAllAssetsAtPath(suitsPath).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
            var ashi=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            var idle=ashi.idleSpriteSequence.First(s=>s!=null);var rect=VisibleBodyGeometry.Bounds(idle);
            art.ashiHeight=rect.height*1.25f;EditorUtility.SetDirty(art);
            var original=AssetDatabase.LoadAssetAtPath<StomachMapDefinition>(Root+"StomachMap/Stage1.asset");
            var def=AssetDatabase.LoadAssetAtPath<StomachMapDefinition>(Root+"ColonMap/Stage2.asset");
            if(def==null){def=ScriptableObject.CreateInstance<StomachMapDefinition>();AssetDatabase.CreateAsset(def,Root+"ColonMap/Stage2.asset");}
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"StomachMap/ColonTerrain.shader");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Colon shader did not compile");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"ColonMap/Terrain.mat");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Root+"ColonMap/Terrain.mat");}material.shader=shader;
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"ColonMap/Layout.png");material.SetTexture("_DetailTex",original.terrainMaterial.GetTexture("_DetailTex"));
            def.terrainMaterial=material;def.colon=true;def.worldWidth=original.worldWidth;def.aspect=original.aspect;def.chunkSize=original.chunkSize;def.pixelWorldSize=original.pixelWorldSize;
            def.wallWidth=original.wallWidth;def.climbableWidth=original.climbableWidth;def.startUV=new Vector2(.89f,.80f);
            def.inlet=new Vector4(.894f,.902f,.028f,.046f);def.outlet=new Vector4(.155f,.155f,.034f,.05f);
            EditorUtility.SetDirty(def);EditorUtility.SetDirty(material);
            // The original stage retains its exact approved anatomical opening coordinates.
            original.inlet=StomachOpening.Inlet;original.outlet=StomachOpening.Outlet;EditorUtility.SetDirty(original);
            AssetDatabase.SaveAssets();Debug.Log("[ColonStage] Installed: identical world and pixel size; 5 wearable remnants; approved escape hole.");
        }
        static void Import(string path,bool sprite)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=100;t.alphaIsTransparency=sprite;t.mipmapEnabled=!sprite;t.filterMode=sprite?FilterMode.Bilinear:FilterMode.Trilinear;
            t.npotScale=TextureImporterNPOTScale.None;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=2048;
            t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
        }
    }
}
