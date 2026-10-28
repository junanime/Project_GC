using UnityEngine;
namespace Vampire
{
    // Show the existing anatomical iris through a local patch of acid, without exposing other submerged objects.
    public sealed class CapsuleOutletReveal:MonoBehaviour
    {
        StomachOpening opening;Material material;Mesh mesh;
        public static void Create(StomachOpening o)
        {
            if(!AcidLakeEvent.Contains(o.Center))return;
            var go=new GameObject("잠긴 출구 · 수면 아래 덮개",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(o.transform,false);go.transform.position=o.Center;
            var v=go.AddComponent<CapsuleOutletReveal>();v.opening=o;v.material=new Material(Resources.Load<Shader>("AmphibiousCapsule/OutletWindow"));
            Vector2 r=o.Radii*1.06f;v.mesh=new Mesh();v.mesh.vertices=new[]{new Vector3(-r.x,-r.y),new Vector3(r.x,-r.y),new Vector3(r.x,r.y),new Vector3(-r.x,r.y)};v.mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};v.mesh.triangles=new[]{0,2,1,0,3,2};v.mesh.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=v.mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=v.material;renderer.sortingLayerName="Monster Full";renderer.sortingOrder=32003;
        }
        void LateUpdate(){material.SetFloat("_Opened",opening.Openness);material.SetFloat("_Clock",Time.time);}
        void OnDestroy(){if(material!=null)Destroy(material);if(mesh!=null)Destroy(mesh);}
    }
}
