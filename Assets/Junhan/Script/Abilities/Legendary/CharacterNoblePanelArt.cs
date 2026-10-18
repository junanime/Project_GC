using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Acquisition-card artwork only. Catalog/HUD icons and augment effects stay independent.</summary>
    public static class CharacterNoblePanelArt
    {
        const string Root = "CharacterNoblePanels/";
        static readonly HashSet<string> Cutouts = new HashSet<string> {
            "Ashi/HeavySnipe", "Hyuki/HeavySnipe", "Hyuki/HedgehogNeedle", "Hyuki/GastricPeristalsisWave",
            "Hyuki/MucosalFortress", "Shini/CloneCulture", "Shini/HeavySnipe", "Shini/OrganCompression"
        };
        public static bool IsCutout(string character, string augment) => Cutouts.Contains(character + "/" + augment);
        public static Sprite Frame => Resources.Load<Sprite>(Root + "Frame");
        public static string PlayingCharacterKey
        {
            get
            {
                // A lobby preview can change independently of the character in the current run.
                var level = Object.FindObjectOfType<LevelManager>();
                var blueprint = level != null && level.PlayerCharacter != null
                    ? level.PlayerCharacter.Blueprint : CrossSceneData.CharacterBlueprint;
                return OctoberArt.CharacterKey(blueprint);
            }
        }
        public static Sprite Get(string augmentKey) => Get(PlayingCharacterKey, augmentKey);
        public static Sprite Get(string characterKey, string augmentKey)
        {
            // Do not fall back to a different character's art. No static cache retains other runs' textures.
            var sprites = Resources.LoadAll<Sprite>(Root + (IsCutout(characterKey, augmentKey) ? "Cutouts/" : "") + characterKey + "/" + augmentKey);
            return sprites.Length == 1 ? sprites[0] : null;
        }
    }
}
