using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum FoodBossKind { Rolpangi, Denikani, Maribi, Tangdengi, Hurumi }
    public enum FoodBossAttack { Basic, Radial, Dash, UniqueOne, UniqueTwo, Recovery }
    public static class FoodBossArt
    {
        public static readonly string[] Names={"롤팽이","데니카니","마리비","탕뎅이","후루미"};
        public static readonly Color[] Warnings={new Color(1,.28f,.57f),new Color(1,.52f,.10f),new Color(1,.88f,.12f),new Color(.5f,.25f,1),new Color(.04f,.78f,.92f)};
        public static readonly float[] Widths={4.2f,4.4f,3.9f,4.2f,4.4f};
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        static readonly int[] widths={301,309,309,321,304,310,320,320,296,311};
        static readonly int[] heights={262,265,256,278,297,182,224,224,282,287};
        static readonly float[] pixelWidths={287,293,295,304,290,292,245,257,279,298};
        public static Sprite Walk(FoodBossKind kind,bool phase,int frame)
        {
            int i=(int)kind*2+(phase?1:0);bool beetle=kind==FoodBossKind.Tangdengi;frame%=beetle?18:8;string key=$"Walk{(int)kind}P{(phase?2:1)}";
            string id=key+"/"+frame;if(cache.TryGetValue(id,out var s))return s;
            var t=Resources.Load<Texture2D>("FoodBosses/"+key);if(t==null)return null;
            // Every pose shares the phase's fixed canvas, pivot and pixels/unit.
            s=Sprite.Create(t,new Rect(frame%4*widths[i],((beetle?4:1)-frame/4)*heights[i],widths[i],heights[i]),new Vector2(.5f,4f/heights[i]),100,0,SpriteMeshType.FullRect);
            cache[id]=s;return s;
        }
        public static float WalkScale(FoodBossKind kind,bool phase)
        {
            int i=(int)kind*2+(phase?1:0);
            // Preserve the pre-remake world silhouette: beetle 4.2 / 5.082 units.
            // Every state within a phase uses this same scale and ground pivot.
            float extra=phase?(kind==FoodBossKind.Tangdengi?1.21f:kind==FoodBossKind.Hurumi?1.07f:1):1;
            return Widths[(int)kind]*100/pixelWidths[i]*extra;
        }
        public static Sprite Get(string key)
        {
            if(cache.TryGetValue(key,out var s))return s;
            s=Resources.Load<Sprite>("FoodBosses/"+key);
            if(s==null){var t=Resources.Load<Texture2D>("FoodBosses/"+key);if(t!=null)s=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);}
            cache[key]=s;return s;
        }
        public static Sprite Projectile(FoodBossKind kind,bool phase=false)
        {
            string key=kind==FoodBossKind.Rolpangi?"Blueberry":kind==FoodBossKind.Denikani?"Butter":kind==FoodBossKind.Maribi?"LemonSeed":kind==FoodBossKind.Tangdengi?"Grape":"Naruto";
            return Get(key)??Resources.Load<Sprite>("SnailBoss/"+(kind==FoodBossKind.Rolpangi?"Blueberry":"Mango"));
        }
        public static SpriteRenderer Make(Transform parent,string key,float width,int order=510)
        {var r=new GameObject(key).AddComponent<SpriteRenderer>();r.transform.SetParent(parent,false);r.sprite=Get(key);r.sortingOrder=order;if(r.sprite!=null)r.transform.localScale=Vector3.one*(width/r.sprite.bounds.size.x);return r;}
    }
}
