using System.Linq;
using UnityEngine;

namespace Vampire
{
    // A visual-aligned play rectangle is also the authoritative dash/teleport boundary.
    public sealed class MiniStageArenaGeometry : MonoBehaviour
    {
        public static MiniStageArenaGeometry Current {get;private set;}
        public Rect PlayArea {get;private set;}
        Character player;
        Vector2[] polygon;
        public static MiniStageArenaGeometry Install(MiniStageRoomBase room,Character player)
        {
            var arena=room.GetComponent<MiniStageArenaGeometry>()??room.gameObject.AddComponent<MiniStageArenaGeometry>();
            arena.Configure(room,player);return arena;
        }
        void Configure(MiniStageRoomBase room,Character actor)
        {
            Current=this;player=actor;
            string type=room.GetType().Name+" "+room.name;
            int art=type.Contains("Falling")?1:type.Contains("Exploding")?8:type.Contains("Sniper")?4:type.Contains("Gold")?2:type.Contains("Exp")?7:type.Contains("Bonus")?3:5;
            var bg=GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s=>s.name=="MiniStage_Background");
            Vector2 size=bg!=null?(Vector2)bg.bounds.size:new Vector2(28,28);
            Vector2 center=bg!=null?(Vector2)bg.bounds.center:(Vector2)transform.position;
            if(size.x<5||size.y<5)size=new Vector2(28,28);
            if(bg==null){var g=new GameObject("MiniStage_Background");g.transform.SetParent(transform,false);bg=g.AddComponent<SpriteRenderer>();}
            bg.sprite=OctoberArt.Get("OctoberContent/Rooms/Room"+art);bg.drawMode=SpriteDrawMode.Simple;
            if(bg.sprite!=null)bg.transform.localScale=new Vector3(size.x/bg.sprite.bounds.size.x/transform.lossyScale.x,size.y/bg.sprite.bounds.size.y/transform.lossyScale.y,1);
            bg.transform.position=new Vector3(center.x,center.y,0);bg.sortingOrder=-100;
            PlayArea=new Rect(center.x-size.x*.315f,center.y-size.y*.32f,size.x*.63f,size.y*.64f);
            foreach(var collider in GetComponentsInChildren<Collider2D>(true))
                if(!collider.isTrigger && (collider.name.Contains("Border")||collider.transform.parent.name=="Walls"))collider.enabled=false;
            var walls=new GameObject("October solid walls");walls.transform.SetParent(transform,false);
            float thick=Mathf.Max(3,size.x*.18f);
            float left=PlayArea.xMin,right=PlayArea.xMax,low=PlayArea.yMin,high=PlayArea.yMax;
            float bevel=PlayArea.width*.16f;
            polygon=new[]{new Vector2(left+bevel,low),new Vector2(right-bevel,low),new Vector2(right,low+bevel),new Vector2(right,high-bevel),new Vector2(right-bevel,high),new Vector2(left+bevel,high),new Vector2(left,high-bevel),new Vector2(left,low+bevel)};
            for(int i=0;i<polygon.Length;i++)
            {
                Vector2 a=polygon[i],b=polygon[(i+1)%polygon.Length],edge=b-a;
                Vector2 outward=new Vector2(edge.y,-edge.x).normalized;
                var wall=Wall(walls.transform,(a+b)*.5f+outward*thick*.5f,new Vector2(edge.magnitude+.1f,thick));
                wall.rotation=Quaternion.Euler(0,0,Mathf.Atan2(edge.y,edge.x)*Mathf.Rad2Deg);
            }
            room.PlayerStartPoint.position=Clamp(room.PlayerStartPoint.position,.65f);
            if(room.ReturnInteractable!=null)room.ReturnInteractable.transform.position=Clamp(room.ReturnInteractable.transform.position,1.2f);
            foreach(var point in GetComponentsInChildren<Transform>(true))
                if(point.name.Contains("SpawnPoint")&&!point.name.Contains("Sniper"))point.position=Clamp(point.position,1.0f);
        }
        Transform Wall(Transform parent,Vector2 position,Vector2 size)
        {
            var wall=new GameObject("Visual wall collider");wall.transform.SetParent(parent,false);wall.transform.position=position;
            var body=wall.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Static;
            wall.AddComponent<BoxCollider2D>().size=size;return wall.transform;
        }
        public bool Contains(Vector2 p,float radius=0)
        {
            if(polygon==null)return PlayArea.Contains(p);
            for(int i=0;i<polygon.Length;i++)
            {var edge=polygon[(i+1)%polygon.Length]-polygon[i];var inward=new Vector2(-edge.y,edge.x).normalized;if(Vector2.Dot(p-polygon[i],inward)<radius-.001f)return false;}
            return true;
        }
        public Vector2 Clamp(Vector2 p,float radius=.5f)
        {
            if(polygon==null)return PlayArea.center;
            for(int pass=0;pass<4;pass++)for(int i=0;i<polygon.Length;i++)
            {var edge=polygon[(i+1)%polygon.Length]-polygon[i];var inward=new Vector2(-edge.y,edge.x).normalized;float d=Vector2.Dot(p-polygon[i],inward);if(d<radius)p+=inward*(radius-d);}
            return p;
        }
        public Vector2 RandomInside(float radius=.5f)
        {
            for(int i=0;i<20;i++){var p=new Vector2(Random.Range(PlayArea.xMin,PlayArea.xMax),Random.Range(PlayArea.yMin,PlayArea.yMax));if(Contains(p,radius))return p;}
            return PlayArea.center;
        }
        public Vector2 Perimeter(float fraction,float inset=.7f)
        {
            float d=Mathf.Repeat(fraction,1)*polygon.Length;int side=Mathf.FloorToInt(d);
            return Clamp(Vector2.Lerp(polygon[side],polygon[(side+1)%polygon.Length],d-side),inset);
        }
        public static Vector2 ClampMovement(Rigidbody2D body,Vector2 destination)
        {
            if(Current==null||body==null||Current.player==null||body.gameObject!=Current.player.gameObject)return destination;
            if(Vector2.Distance(body.position,Current.PlayArea.center)>Current.PlayArea.width*2)return destination;
            return Current.Clamp(destination,Current.PlayerRadius());
        }
        float PlayerRadius(){var c=player.GetComponent<Collider2D>();return c!=null?Mathf.Max(c.bounds.extents.x,c.bounds.extents.y)+.04f:.45f;}
        void LateUpdate()
        {
            if(player!=null && player.IsPortalTravelling)return;
            if(player==null||Vector2.Distance(player.transform.position,PlayArea.center)>PlayArea.width*2)return;
            var rb=player.GetComponent<Rigidbody2D>();var pos=Clamp(player.transform.position,PlayerRadius());
            if(Vector2.Distance(player.transform.position,pos)>.001f)
            {if(rb!=null)rb.position=pos;else player.transform.position=pos;}
        }
        void OnDisable(){if(Current==this)Current=null;}
    }
}
