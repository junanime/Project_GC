using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class FantaToadSmoke
    {
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        static void Check(bool ok,string name)
        { if(!ok)throw new Exception("FANTA_FAIL "+name);Debug.Log("FANTA_PASS "+name); }
        public static void Run()
        {
            foreach(var name in new[]{"ToadLocomotion","ToadAttack","ToadJump","ToadHead","AcidFx"})
            {
                var frames=AcidToadArt.Frames(name);
                Check(frames.Length==(name=="ToadJump"||name=="AcidFx"?8:16),name+" frame count");
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/ToadUpdate/"+name+".png");
                Check(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,name+" crisp pixel import");
                var pixels=frames[0].texture.GetPixels32();int solid=0,orange=0,green=0,clear=0;
                foreach(var p in pixels)
                {
                    if(p.a<32){clear++;continue;}if(p.a<200)continue;solid++;
                    if(p.r>p.g*1.15f&&p.g>p.b*1.3f)orange++;
                    if(p.g>p.r*1.2f&&p.g>p.b*1.15f)green++;
                }
                Check(clear>pixels.Length*.15f&&solid>pixels.Length*.1f,name+" transparent background");
                Check(orange>solid*.2f&&green<solid*.02f,name+" orange soda palette, no green art");
                Check(frames.All(s=>s.rect.width>20&&s.rect.height>20),name+" no empty slices");
                if(name=="ToadLocomotion"||name=="ToadAttack"||name=="ToadJump")
                {
                    Check(Mathf.Abs(frames[0].bounds.size.x-2.8f)<.02f,name+" fixed reference body width");
                    Check(frames.All(s=>s.bounds.size.x>1.9f&&s.bounds.size.x<3.4f&&s.pivot.y<.01f),name+" grounded pivots and no scale spikes");
                }
            }
            var bp=Resources.Load<MiniBossMonsterBlueprint>("ToadUpdate/AcidToadBlueprint");
            Check(bp.name=="환타 두꺼비"&&bp.hp==1200,"Name changed; combat balance preserved");
            Check(bp.resultSprite==AcidToadArt.Frame("ToadLocomotion",0)&&bp.walkSpriteSequence.All(s=>s!=null),"Blueprint sprite links preserved");
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            Check(catalog.entries.Single(e=>e.id=="monster/acid-toad").title=="환타 두꺼비","Tutorial renamed with persisted ID preserved");
            Check(catalog.entries.Single(e=>e.id=="event/acid-reflux").title=="탄산액 발사","Shared event copy updated");
            Check(StageEventCornerArt.Accent(StageEventVisualKind.Reflux).r>StageEventCornerArt.Accent(StageEventVisualKind.Reflux).g,"Event accent orange");
            Debug.Log("FANTA_ASSET_CHECKS_FINISHED");
            ToadUpdateSmoke.Run();
        }
    }
}
