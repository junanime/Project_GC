using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Semantic tag for PLAYER AUGMENT artwork only. Never tag a character, enemy,
    // field event or collider root. Shared factories tag their dedicated visual roots.
    [DisallowMultipleComponent, DefaultExecutionOrder(32000)]
    public sealed class AugmentVfxTag : MonoBehaviour
    {
        static readonly int Opacity = Shader.PropertyToID("_AugmentOpacity");
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Renderer> visuals = new List<Renderer>();
        MaterialPropertyBlock properties;
        float lastOpacity = -1;

        public static void Mark(GameObject visualRoot)
        {
            if(visualRoot == null)return;
            var tag=visualRoot.GetComponent<AugmentVfxTag>();
            if(tag == null)visualRoot.AddComponent<AugmentVfxTag>(); // OnEnable refreshes the new tag.
            else tag.Refresh();
        }
        void OnEnable(){Refresh();}
        void OnTransformChildrenChanged(){Refresh();}
        void Refresh()
        {
            GetComponentsInChildren(true,visuals);
            properties=properties??new MaterialPropertyBlock();
            foreach(var visual in visuals)
            {
                var material=visual.sharedMaterial;
                if(material == null || material.HasProperty(Opacity))continue;
                // Sprite/line/mesh effects share the same unlit sprite convention.
                if(material.shader.name != "Sprites/Default")continue;
                var replacement=new Material(material){name=material.name+" (augment opacity)",shader=Resources.Load<Shader>("AugmentVfx/OpacitySprite")};
                ownedMaterials.Add(replacement);
                visual.sharedMaterial=replacement;
            }
            Apply(GamePreferences.Current.augmentVfxOpacity);
        }
        void LateUpdate()
        {
            float opacity = GamePreferences.Current.augmentVfxOpacity;
            // All tagged shaders default to 1 when a sibling renderer replaces its
            // animated property block. Custom opacity still needs the late-layer override.
            if (opacity != 1f || lastOpacity != 1f) Apply(opacity);
        }
        void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);}
        void Apply(float opacity)
        {
            lastOpacity=opacity;
            foreach(var visual in visuals)
            {
                if(visual==null)continue;
                visual.GetPropertyBlock(properties);
                properties.SetFloat(Opacity,opacity);
                visual.SetPropertyBlock(properties);
            }
        }
    }
}
