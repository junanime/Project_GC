using UnityEngine;

namespace Vampire
{
    public static class SupplyCapsuleArt
    {
        public static Sprite Closed => OctoberArt.Get("SupplyCapsule/ClosedClean");
        public static Sprite Parachute => OctoberArt.Get("SupplyCapsule/Parachute");
        static Sprite orb;
        static Sprite droplet;
        public static Sprite Droplet
        {
            get
            {
                if(droplet!=null)return droplet;
                var tex=new Texture2D(12,12,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                var colors=new Color[144];
                for(int y=0;y<12;y++)for(int x=0;x<12;x++)
                    if(Vector2.Distance(new Vector2(x,y),new Vector2(5.5f,5.5f))<5.5f)colors[y*12+x]=x<5&&y>6?Color.white:new Color(.9f,.9f,.9f);
                tex.SetPixels(colors);tex.Apply();droplet=Sprite.Create(tex,new Rect(0,0,12,12),new Vector2(.5f,.5f),12);return droplet;
            }
        }
        public static Sprite Orb
        {
            get
            {
                if(orb!=null)return orb;
                const int size=24;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float d=Vector2.Distance(new Vector2(x,y),new Vector2(11.5f,11.5f));
                    if(d>11)continue;
                    var c=d>9?new Color(.76f,.36f,.035f):d>7?new Color(1,.66f,.09f):new Color(1,.88f,.30f);
                    if(x>=5&&x<=10&&y>=14&&y<=18)c=new Color(1,1,.85f);
                    pixels[y*size+x]=c;
                }
                tex.SetPixels(pixels);tex.Apply();orb=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),24);orb.name="Supply golden orb";return orb;
            }
        }
    }
}
