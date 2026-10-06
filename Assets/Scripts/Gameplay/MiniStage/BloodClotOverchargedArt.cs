using UnityEngine;

namespace Vampire
{
    public sealed class BloodClotOverchargedArt : MonoBehaviour
    {
        Sprite replacement;
        public static void Apply(GameObject portal)
        {
            var art=portal.GetComponent<BloodClotOverchargedArt>()??portal.AddComponent<BloodClotOverchargedArt>();
            art.Refresh();
        }
        public void Refresh()
        {
            var renderer=GetComponentInChildren<SpriteRenderer>();
            var source=OctoberArt.Get("BloodClotTravel/Overcharged");
            if(renderer==null || source==null || renderer.sprite==null || renderer.sprite==replacement)return;
            float width=renderer.sprite.bounds.size.x;
            if(replacement!=null)Destroy(replacement);
            replacement=Sprite.Create(source.texture,source.rect,Vector2.one*.5f,source.rect.width/width);
            replacement.name="Overcharged blood clot";
            renderer.sprite=replacement;
        }
        void OnDestroy(){if(replacement!=null)Destroy(replacement);}
    }
}
