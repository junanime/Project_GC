using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Vampire.Editor
{
    public static class AshiSkillInstaller
    {
        const string Folder="Assets/Junhan/Art/AshiSkills/";
        static Sprite[] Import(string file,bool sheet)
        {
            string path=Folder+file+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=sheet?SpriteImportMode.Multiple:SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Point;importer.maxTextureSize=2048;
            importer.spritePixelsPerUnit=400;
            if(sheet)
            {
                importer.GetSourceTextureWidthAndHeight(out int w,out int h);
                var frames=new SpriteMetaData[8];
                for(int i=0;i<8;i++)
                {
                    int col=i%4,row=i/4;
                    int left=Mathf.RoundToInt(col*w/4f),right=Mathf.RoundToInt((col+1)*w/4f);
                    int bottom=Mathf.RoundToInt((1-row)*h/2f),top=Mathf.RoundToInt((2-row)*h/2f);
                    frames[i]=new SpriteMetaData{name=file+"_"+i.ToString("00"),rect=new Rect(left,bottom,right-left,top-bottom),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)};
                }
                importer.spritesheet=frames;
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
        [MenuItem("Tools/Ashi/Install skill assets")]
        public static void Install()
        {
            const string path="Assets/Junhan/Resources/AshiSkills.asset";
            var data=AssetDatabase.LoadAssetAtPath<CharacterSkillDefinition>(path);
            if(data==null){data=ScriptableObject.CreateInstance<CharacterSkillDefinition>();AssetDatabase.CreateAsset(data,path);}
            data.passiveIcon=Import("marathon-icon",false)[0];
            data.activeIcon=Import("sprint-icon-v2-finish",false)[0];
            data.cutin=Import("ashi-active-cutin-sheet",true);
            data.passiveWind=Import("marathon-wind-sheet",true);
            data.activeWind=Import("sprint-wind-sheet",true);
            data.projectileWind=Import("projectile-wind-sheet",true);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var ashi=config.characters.Single(c=>c.name=="아시");ashi.skills=data;
            EditorUtility.SetDirty(ashi);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            Debug.Log("[AshiSkills] Assets installed, 8 frames per animation.");
        }
        public static void Run(){try{Install();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
