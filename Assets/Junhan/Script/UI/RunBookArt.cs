using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public static class RunBookArt
    {
        static readonly Dictionary<int,Sprite> icons=new Dictionary<int,Sprite>();
        public static Sprite Backplate => OctoberArt.Get("RunBook/Backplate");
        public static Sprite Icon(int index)
        {
            if(icons.TryGetValue(index,out var cached)&&cached!=null)return cached;
            var texture=Resources.Load<Texture2D>("RunBook/Icons");
            if(texture==null)return null;
            int w=texture.width/4,h=texture.height/4;
            var rect=new Rect(index%4*w,(3-index/4)*h,w,h);
            // Native Sprite slicing keeps the generated PNG byte-for-byte unchanged.
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            sprite.name="Run book icon "+index;icons[index]=sprite;return sprite;
        }
    }
}
