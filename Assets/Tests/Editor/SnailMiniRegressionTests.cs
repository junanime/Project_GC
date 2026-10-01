using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public class SnailMiniRegressionTests
    {
        [TestCase(false,0,"Blueberry")] [TestCase(false,1,"Strawberry")]
        [TestCase(false,2,"Melon")] [TestCase(false,3,"Mango")]
        [TestCase(true,0,"Ball")] [TestCase(true,1,"Kisses")]
        [TestCase(true,2,"Tablet")] [TestCase(true,3,"Bar")]
        public void EachKindHasDistinctRigidShellAndMatchingBody(bool chocolate,int ingredient,string expected)
        {
            var actor=new GameObject("Mini art test");
            try
            {
                SnailBossMinion.MakeArt(actor.transform,chocolate,ingredient);
                var visual=actor.GetComponentInChildren<SnailMiniVisual>();
                Assert.NotNull(visual);
                Assert.AreEqual("Minis/"+expected,SnailMiniVisual.ShellKey(chocolate,ingredient));
                Assert.NotNull(visual.Shell.sprite);
                Assert.AreEqual(expected,visual.Shell.sprite.name);
                Assert.AreEqual(chocolate?"Body2":"Body1",visual.Body.sprite.name);
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(visual.Shell.sprite));
                Assert.IsTrue(importer.alphaIsTransparency);
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
                var sprite=visual.Shell.sprite; var size=visual.Shell.transform.localScale;
                for(int f=0;f<16;f++)
                {
                    visual.ApplyPose(f/16f,true);
                    Assert.AreSame(sprite,visual.Shell.sprite);
                    Assert.AreEqual(size,visual.Shell.transform.localScale);
                }
                visual.ApplyPose(.25f,true); var contracted=visual.Body.transform.localScale;
                visual.ApplyPose(.75f,true);
                Assert.AreNotEqual(contracted,visual.Body.transform.localScale);
                Assert.AreEqual(2,actor.GetComponentsInChildren<SpriteRenderer>().Length);
            }
            finally { Object.DestroyImmediate(actor); }
        }

        [Test] public void WalkingTracksDistanceFacingAndIgnoresPauseAndTeleport()
        {
            var actor=new GameObject("Walk rule test");
            try
            {
                var visual=actor.AddComponent<SnailMiniVisual>(); visual.Configure(actor.transform,false,0);
                visual.Advance(Vector2.right*.18f,.1f);
                Assert.AreEqual(.25f,visual.WalkPhase,.0001f); Assert.IsTrue(visual.FacingRight);
                visual.Advance(Vector2.left*.18f,0);
                Assert.AreEqual(.25f,visual.WalkPhase,.0001f); Assert.IsTrue(visual.FacingRight);
                visual.Advance(Vector2.left*20,.1f);
                Assert.AreEqual(.25f,visual.WalkPhase,.0001f); Assert.IsTrue(visual.FacingRight);
                visual.Advance(Vector2.left*.18f,.1f); Assert.IsFalse(visual.FacingRight);
                visual.Advance(Vector2.zero,.1f);
                Assert.AreEqual(1,visual.Shell.transform.localScale.x);
                visual.Configure(actor.transform,true,3);
                Assert.AreEqual(0,visual.WalkPhase); Assert.IsFalse(visual.FacingRight);
                Assert.AreEqual(2,actor.GetComponentsInChildren<SpriteRenderer>().Length);
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
