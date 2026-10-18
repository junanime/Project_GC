using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Blend only the edge of the approved illustration window into the shared cream paper.
    // No colour key: white feathers/sleeves and the character's light details stay opaque.
    [RequireComponent(typeof(Image))]
    public sealed class NobleArtFeather : BaseMeshEffect
    {
        static readonly float[] Steps = { 0, .015f, .985f, 1 };
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive()||mesh.currentVertCount!=4)return;
            UIVertex bottomLeft=new UIVertex(),topRight=new UIVertex();
            mesh.PopulateUIVertex(ref bottomLeft,0);mesh.PopulateUIVertex(ref topRight,2);
            mesh.Clear();
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                var v=bottomLeft;
                v.position=new Vector3(Mathf.Lerp(bottomLeft.position.x,topRight.position.x,Steps[x]),Mathf.Lerp(bottomLeft.position.y,topRight.position.y,Steps[y]),bottomLeft.position.z);
                v.uv0=new Vector4(Mathf.Lerp(bottomLeft.uv0.x,topRight.uv0.x,Steps[x]),Mathf.Lerp(bottomLeft.uv0.y,topRight.uv0.y,Steps[y]),0,0);
                if(x==0||x==3||y==0||y==3)v.color.a=0;
                mesh.AddVert(v);
            }
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)
            {
                int i=y*4+x;mesh.AddTriangle(i,i+4,i+5);mesh.AddTriangle(i+5,i+1,i);
            }
        }
    }
}
