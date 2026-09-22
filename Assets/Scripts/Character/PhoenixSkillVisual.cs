using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // World-space artwork is independent of the character's sprite and transform.
    [DefaultExecutionOrder(510)]
    public sealed class PhoenixSkillVisual : MonoBehaviour
    {
        Character owner; CharacterSkillRuntime skill; SpriteRenderer body,first,second;
        readonly List<SpriteRenderer> crystals=new List<SpriteRenderer>();

        Material fireMaterial,blizzardMaterial; MeshRenderer blizzard; Mesh blizzardMesh;
        float elapsed=10,phase;
        public const float Duration=3f;
        public bool Playing => elapsed<Duration;
        public bool Absorbed => !Playing;
        public int CrystalCount => crystals.Count;
        public float BlizzardStrength { get; private set; }
        public float BlizzardPhase => phase;
        public void Bind(Character character,CharacterSkillRuntime runtime)
        {
            owner=character;skill=runtime;
            if(!skill.IsShini&&!skill.IsHyuki)return;
            body=GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            first=Make("Phoenix current pose");second=Make("Phoenix next pose");
            if(skill.IsShini)
            {
                gameObject.AddComponent<ShiniPhoenixWrapVisual>().Bind(character,runtime);
            }
            else
            {
                gameObject.AddComponent<HyukiSnowVisual>().Bind(character,runtime);
                fireMaterial=new Material(Shader.Find("Vampire/PhoenixIce"));first.sharedMaterial=second.sharedMaterial=fireMaterial;
                var go=new GameObject("Hyuki diagonal blizzard",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                blizzard=go.GetComponent<MeshRenderer>();blizzardMaterial=new Material(Shader.Find("Vampire/PhoenixBlizzard"));blizzard.sharedMaterial=blizzardMaterial;
                blizzardMaterial.mainTexture=skill.Definition.blizzardTexture;
                blizzardMesh=new Mesh {vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(-1,1),new Vector3(1,1)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},triangles=new[]{0,2,1,1,2,3}};
                go.GetComponent<MeshFilter>().sharedMesh=blizzardMesh;blizzard.enabled=false;
            }
        }
        SpriteRenderer Make(string label)
        {
            var r=new GameObject(label).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);r.enabled=false;
            if(body!=null){r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+4;}
            return r;
        }
        public void Play(float start=0){if(first==null)return;elapsed=start;}
        void LateUpdate()
        {
            if(first==null)return;
            if(!owner.IsAlive){Hide();return;}
            float delta=Time.deltaTime;phase+=delta;elapsed+=delta;
            if(skill.IsShini)elapsed=skill.IsSummoning?Duration-skill.SummonRemaining:Mathf.Max(Duration,elapsed);
            UpdateCrystals();
            var frames=skill.Definition.phoenixFrames;
            bool show=!skill.IsShini&&Playing&&frames!=null&&frames.Length>0;
            first.enabled=second.enabled=show;
            if(show)
            {
                float t=elapsed/Duration,index=t*(frames.Length-1);
                int i=Mathf.Min((int)index,frames.Length-1);float blend=index-i;
                first.sprite=frames[i];second.sprite=frames[Mathf.Min(i+1,frames.Length-1)];
                float opacity=Mathf.SmoothStep(0,1,t/.12f)*(1-Mathf.SmoothStep(0,1,(t-.85f)/.15f));
                first.color=new Color(1,1,1,opacity*(1-blend));second.color=new Color(1,1,1,opacity*blend);
                float height=skill.IsHyuki?Mathf.Lerp(.3f,.9f,Mathf.SmoothStep(0,1,t/.35f)):Mathf.Lerp(1.5f,0,Mathf.SmoothStep(0,1,(t-.35f)/.4f));
                float width=skill.IsHyuki?2.7f:Mathf.Lerp(3.2f,1,Mathf.SmoothStep(0,1,(t-.45f)/.4f));
                foreach(var r in new[]{first,second})
                {
                    r.transform.position=transform.position+Vector3.up*height;
                    float scale=width/Mathf.Max(.01f,r.sprite.bounds.size.x);
                    r.transform.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),1);
                }
            }
            BlizzardStrength=skill.IsHyuki&&Playing?Mathf.SmoothStep(0,1,(elapsed-.55f)/.8f)*(1-Mathf.SmoothStep(0,1,(elapsed-1.65f)/1.35f)):0;
            if(blizzard!=null)
            {
                var cam=Camera.main;blizzard.enabled=BlizzardStrength>0&&cam!=null;
                if(blizzard.enabled)
                {
                    blizzard.sortingLayerID=body!=null?body.sortingLayerID:0;blizzard.sortingOrder=30000;
                    float height=cam.orthographicSize;
                    blizzard.transform.position=new Vector3(cam.transform.position.x,cam.transform.position.y,transform.position.z-1);
                    blizzard.transform.localScale=new Vector3(height*cam.aspect/Mathf.Abs(transform.lossyScale.x),height/Mathf.Abs(transform.lossyScale.y),1);
                    blizzardMaterial.SetFloat("_Strength",BlizzardStrength);blizzardMaterial.SetFloat("_Phase",phase);
                }
            }
        }
        void UpdateCrystals()
        {
            var frames=skill.Definition.iceComponents;
            int wanted=skill.IsHyuki&&owner.IsSkillIdle&&frames!=null&&frames.Length>0?skill.SleepStacks/CharacterSkillRuntime.SleepStacksPerCrystal:0;
            while(crystals.Count>wanted){int n=crystals.Count-1;Destroy(crystals[n].gameObject);crystals.RemoveAt(n);}
            while(crystals.Count<wanted)crystals.Add(Make("Sleep stack crystal"));
            for(int i=0;i<crystals.Count;i++)
            {
                var r=crystals[i];r.enabled=true;r.sprite=frames[0];
                float a=phase*1.2f+i*Mathf.PI*2/Mathf.Max(1,crystals.Count);
                float radius=.65f+.12f*(i/10);r.transform.position=transform.position+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.55f+.15f,-.02f);
                float scale=.34f/r.sprite.bounds.size.x;r.transform.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),1);
                r.color=new Color(.85f,1,1,.85f);r.sortingOrder=body!=null?body.sortingOrder+(Mathf.Sin(a)>0?-1:2):2;
            }
        }

        void Hide()
        {
            elapsed=10;BlizzardStrength=0;
            if(first!=null)first.enabled=false;if(second!=null)second.enabled=false;if(blizzard!=null)blizzard.enabled=false;
            foreach(var r in crystals)if(r!=null)Destroy(r.gameObject);crystals.Clear();
        }
        void OnDisable(){Hide();}
        void OnDestroy(){if(fireMaterial!=null)Destroy(fireMaterial);if(blizzardMaterial!=null)Destroy(blizzardMaterial);if(blizzardMesh!=null)Destroy(blizzardMesh);}
    }
}
