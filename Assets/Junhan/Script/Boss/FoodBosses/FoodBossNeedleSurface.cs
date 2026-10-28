using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Vampire
{
    // One bit per source pixel, baked before compression. Runtime sprites have rectangular
    // meshes, so their bounds cannot locate an actual needle impact on the silhouette.
    public static class FoodBossNeedleSurface
    {
        sealed class Mask {public int width,height;public byte[] bits;}
        static Dictionary<string,Mask> masks;
        static void Load()
        {
            if(masks!=null)return;masks=new Dictionary<string,Mask>();
            var data=Resources.Load<TextAsset>("FoodBosses/NeedleSurfaces");if(data==null)return;
            using(var reader=new BinaryReader(new MemoryStream(data.bytes))){
                int count=reader.ReadInt32();
                for(int i=0;i<count;i++){string name=reader.ReadString();var m=new Mask{width=reader.ReadInt32(),height=reader.ReadInt32()};m.bits=reader.ReadBytes(reader.ReadInt32());masks.Add(name,m);}
            }
        }
        public static bool HasMask(Sprite sprite){Load();return sprite!=null&&masks.ContainsKey(sprite.texture.name);}
        public static Vector2 Surface(SpriteRenderer body,Vector2 incoming,Vector2 direction)
        {
            Load();var sprite=body.sprite;
            if(sprite==null||!masks.TryGetValue(sprite.texture.name,out var mask))return VisibleBodyGeometry.Surface(body,incoming,direction);
            var rect=sprite.rect;
            Vector2 local=body.transform.InverseTransformPoint(incoming),axis=body.transform.InverseTransformVector(direction.normalized);
            if(body.flipX){local.x=-local.x;axis.x=-axis.x;}if(body.flipY){local.y=-local.y;axis.y=-axis.y;}
            local=local*sprite.pixelsPerUnit+sprite.pivot;axis.Normalize();
            float span=rect.size.magnitude;Vector2 begin=local-axis*span;
            Vector2 nearest=local;float best=float.PositiveInfinity;
            for(float d=0;d<span*2;d+=.5f){
                Vector2 p=begin+axis*d;int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y);
                if(x<0||y<0||x>=rect.width||y>=rect.height)continue;
                int bit=(y+(int)rect.y)*mask.width+x+(int)rect.x;
                if((mask.bits[bit>>3]&(1<<(bit&7)))==0)continue;
                nearest=p;best=0;break;
            }
            // A collider edge can pass through transparent space beside a horn/leg.
            // Snap that uncommon case to the nearest opaque pixel, never the atlas padding.
            if(float.IsPositiveInfinity(best))for(int y=0;y<(int)rect.height;y+=2)for(int x=0;x<(int)rect.width;x+=2){
                int bit=(y+(int)rect.y)*mask.width+x+(int)rect.x;if((mask.bits[bit>>3]&(1<<(bit&7)))==0)continue;
                var p=new Vector2(x+.5f,y+.5f);float dist=(p-local).sqrMagnitude;if(dist<best){best=dist;nearest=p;}
            }
            Vector2 result=(nearest-sprite.pivot)/sprite.pixelsPerUnit;
            if(body.flipX)result.x=-result.x;if(body.flipY)result.y=-result.y;
            return body.transform.TransformPoint(result);
        }
    }
}
