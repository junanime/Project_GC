using UnityEngine;

namespace Vampire
{
    public sealed class StageEntryLoadingArt : ScriptableObject
    {
        public Sprite corridor;
        public Sprite[] hyuki, shini, ari, ashi;
        public Sprite[] bacteria, slime;
        public Sprite[] Runner(string key)
        {
            switch (key) { case "Hyuki": return hyuki; case "Shini": return shini; case "Ari": return ari; default: return ashi; }
        }
    }
}
