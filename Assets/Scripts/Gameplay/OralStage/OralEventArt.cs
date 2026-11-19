using UnityEngine;

namespace Vampire
{
    // Dedicated preview-approved art; never substitutes the existing reward chest.
    public static class OralEventArt
    {
        static Sprite brush, balloon, burst, disc;
        static Material lineMaterial;
        public static Sprite Brush => brush != null ? brush : brush = Crop("OralMap/EventBrush", 1774,887,new Rect(46,152,1683,569), new Vector2(.85f,.22f));
        public static Sprite Balloon => balloon != null ? balloon : balloon = Crop("OralMap/EventBalloon", 1263,1246,new Rect(101,686,457,505), new Vector2(.5f,0));
        public static Sprite Burst => burst != null ? burst : burst = Crop("OralMap/EventBalloon", 1263,1246,new Rect(723,146,430,387), Vector2.one*.5f);
        public static Sprite Disc
        {
            get
            {
                if(disc!=null)return disc;
                var t=new Texture2D(64,64,TextureFormat.RGBA32,false);t.name="Oral event soft footprint";
                for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=(new Vector2(x+.5f,y+.5f)-Vector2.one*32).magnitude/32;t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-d)*3)));}
                t.Apply();disc=UnityEngine.Sprite.Create(t,new Rect(0,0,64,64),Vector2.one*.5f,64);return disc;
            }
        }
        internal static Rect ImportedRect(Texture2D texture, int width, int height, Rect native)
        {
            float x = texture.width / (float)width, y = texture.height / (float)height;
            return new Rect(native.x*x,native.y*y,native.width*x,native.height*y);
        }
        static Sprite Crop(string resource,int width,int height,Rect native,Vector2 pivot)
        {
            var texture = Resources.Load<Texture2D>(resource);
            return texture == null ? null : UnityEngine.Sprite.Create(texture,ImportedRect(texture,width,height,native),pivot,100);
        }
        public static SpriteRenderer Sprite(Transform parent,string name,Sprite sprite,int order=15)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(parent,false);r.sprite=sprite;r.sortingOrder=order;return r;
        }
        public static LineRenderer Line(Transform parent,string name,Color color,float width,int order=0,bool floor=false)
        {
            if(lineMaterial==null)lineMaterial=new Material(Shader.Find("Sprites/Default"));
            var l=new GameObject(name).AddComponent<LineRenderer>();l.transform.SetParent(parent,false);l.sharedMaterial=lineMaterial;l.useWorldSpace=true;
            l.startColor=l.endColor=color;l.startWidth=l.endWidth=width;l.numCapVertices=3;l.sortingOrder=order;if(floor)GroundVisualSorting.Apply(l,order);return l;
        }
        public static bool Paused(LevelManager level) => level==null||level.PlayerCharacter==null||!level.PlayerCharacter.IsAlive||level.IsLevelEnded||level.IsRunFlowPaused||
            Time.timeScale<=0||level.PlayerCharacter.IsChoosingAugment||StageEntryLoading.IsLoading||MiniStageRuntimeState.IsInsideMiniStage||level.PlayerCharacter.IsPortalTravelling||
            (level.GetComponent<StageProgression>()?.Travelling??false);
    }
}
