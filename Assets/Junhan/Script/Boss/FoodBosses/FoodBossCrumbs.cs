using UnityEngine;
namespace Vampire
{
    // Small baked flakes at the real claw contact. Visual only: no hitbox or reward.
    public sealed class FoodBossCrumbs:MonoBehaviour
    {
        const int Count=7;
        Mesh mesh;Material material;float age;
        readonly Vector2[] offsets=new Vector2[Count],velocities=new Vector2[Count];
        readonly float[] sizes=new float[Count],angles=new float[Count];
        readonly Vector3[] vertices=new Vector3[Count*4];readonly Color[] colors=new Color[Count*4];
        public static void Emit(FoodBossRuntime boss,Vector2 at)
        {
            var c=new GameObject("Claw contact bread flakes").AddComponent<FoodBossCrumbs>();c.transform.SetParent(boss.Effects,true);c.transform.position=at;
            c.mesh=new Mesh();c.mesh.MarkDynamic();c.gameObject.AddComponent<MeshFilter>().sharedMesh=c.mesh;
            var renderer=c.gameObject.AddComponent<MeshRenderer>();c.material=new Material(Shader.Find("Sprites/Default"));renderer.sharedMaterial=c.material;renderer.sortingOrder=515;
            var indices=new int[Count*6];
            for(int i=0;i<Count;i++)
            {
                // Deterministic visual variation does not consume the gameplay RNG.
                float angle=(i*137.5f+15)*Mathf.Deg2Rad;
                c.velocities[i]=new Vector2(Mathf.Cos(angle)*(.65f+i*.09f),.75f+(i%3)*.23f);
                c.sizes[i]=.045f+(i%3)*.013f;c.angles[i]=angle;
                int v=i*4,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            c.Draw();c.mesh.triangles=indices;
        }
        void Update()
        {
            if(FoodBossRuntime.Paused)return;age+=Time.deltaTime;if(age>=.48f){Destroy(gameObject);return;}
            for(int i=0;i<Count;i++){offsets[i]+=velocities[i]*Time.deltaTime;velocities[i]+=Vector2.down*(4*Time.deltaTime);angles[i]+=Time.deltaTime*(i%2==0?6:-7);}
            Draw();
        }
        void Draw()
        {
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.48f,age));
            for(int i=0;i<Count;i++)for(int j=0;j<4;j++)
            {
                float a=angles[i]+j*Mathf.PI*.5f;vertices[i*4+j]=offsets[i]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(sizes[i]*(j%2==0?1:.72f));
                colors[i*4+j]=j<2?new Color(1,.81f,.44f,alpha):new Color(.68f,.38f,.15f,alpha);
            }
            mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
