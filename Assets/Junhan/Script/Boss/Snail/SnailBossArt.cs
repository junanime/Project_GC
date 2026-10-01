using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    public static class SnailBossArt
    {
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static Sprite Get(string key)
        {
            if(!cache.TryGetValue(key,out var sprite)) cache[key]=sprite=Resources.Load<Sprite>("SnailBoss/"+key);
            return sprite;
        }
        public static SpriteRenderer Make(Transform parent,string key,float width,int order)
        {
            var go=new GameObject(key); go.transform.SetParent(parent,false);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=order;
            Set(sr,key,width); return sr;
        }
        public static void Set(SpriteRenderer sr,string key,float width)
        {
            sr.sprite=Get(key);
            if(sr.sprite!=null) sr.transform.localScale=Vector3.one*(width/sr.sprite.bounds.size.x);
        }
        public static string Projectile(bool chocolate,int ingredient)=>new[]{"Blueberry","Strawberry","Melon","Mango","Ball","Kisses","Tablet","Bar"}[(chocolate?4:0)+ingredient];
    }
}
