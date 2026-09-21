using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class CharacterDesignTests
    {
        public static void Verify()
        {
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Check(config.characters.Select(c => c.name).SequenceEqual(new[] { "아시", "아리", "혁이", "신이" }), "four-character catalog, no Test placeholders");
            var baseline = config.characters[0];
            foreach (var data in config.characters)
            {
                Check(data.owned && data.profileSprite != null, data.name + " selectable with separate portrait");
                Check(ApothecaryUI.CharacterProfile(data) == data.profileSprite, data.name + " card uses portrait");
                Check(data.walkSpriteSequence.Length == 8 && data.idleSpriteSequence.Length == 8, data.name + " eight walk and idle frames");
                Check(data.dashSpriteSequence.Length == (data.name == "혁이" ? 2 : 8), data.name + " approved dash pose count");
                Check(data.capturedSpriteSequence.Length > 0, data.name + " capture expression exists");
                Check(data.hp == baseline.hp && data.armor == baseline.armor && data.movespeed == baseline.movespeed && data.luck == baseline.luck && data.recovery == baseline.recovery && data.acceleration == baseline.acceleration, data.name + " identical baseline stats");
                Check(data.startingAbilities.SequenceEqual(baseline.startingAbilities), data.name + " identical baseline needle, no new skills");
                foreach (var sprite in data.walkSpriteSequence.Concat(data.idleSpriteSequence).Concat(data.dashSpriteSequence).Concat(data.capturedSpriteSequence))
                {
                    Check(sprite != null && sprite.pixelsPerUnit > 0, data.name + " valid sprite " + sprite.name);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    Check(!importer.mipmapEnabled && importer.alphaIsTransparency, "sprite alpha and mipmap settings " + sprite.name);
                }
                if (data != baseline)
                {
                    Check(data.fitDashAnimationToDuration && data.dashArtMovesRight, data.name + " dash timing/direction metadata");
                    Check(data.idleSpriteSequence.All(s => AssetDatabase.GetAssetPath(s).Contains(data.name == "혁이" ? "Hyuki_Idle" : data.name == "아리" ? "Ari_Idle" : "Shini_Idle")), data.name + " approved idle asset wired");
                }
            }
            new AshiAnimationTests().StateSpritesKeepComparableWorldSize();
            new AshiAnimationTests().MainBlueprintUsesEightEyebrowWalkFrames();
            Check(true, "Ashi existing animation regressions");
        }
        public static void Run() { Verify(); EditorApplication.Exit(0); }
        public static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("[CharacterDesignTest] FAIL " + message);
            Debug.Log("[CharacterDesignTest] PASS " + message);
        }
    }
}
