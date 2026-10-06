using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Cosmetic only: all damage, collision, cooldowns and targeting stay with the
    // existing ability controllers. A pooled carrier owns these reusable layers.
    [DefaultExecutionOrder(1100)]
    public sealed class PhoenixRadianceVisual : MonoBehaviour
    {
        readonly List<PhoenixRadianceLayer> layers = new List<PhoenixRadianceLayer>();
        SpriteRenderer carrier, target;
        SyringeAugmentVfx lease;
        string key;
        float age, duration, charge = 1, amount = 1, displayedAmount = 1;
        int stackIndex;
        bool loop;
        public string Key => key;
        public MeshRenderer FirstRenderer => layers.Count > 0 ? layers[0].Renderer : null;
        public float Age => age;
        public float Charge => charge;
        public int ColorStages => Mathf.Clamp(Mathf.RoundToInt(amount),0,7);
        public static bool Supports(string name) => PhoenixNobleVfx.Supports(name) && !name.StartsWith("OrganCompression");

        public void Bind(SpriteRenderer source, string theme, bool looping, float lifetime)
        {
            carrier = source; lease = GetComponent<SyringeAugmentVfx>(); target = lease != null ? lease.Target : source;
            age = 0; duration = Mathf.Max(.1f,lifetime); loop = looping; charge = 1; amount = displayedAmount = 1; stackIndex = 0;
            if(theme=="HungrySpirit" || theme=="CursorControlGlow")amount=displayedAmount=0;
            if(key != theme) { foreach(var l in layers) Destroy(l.Renderer.gameObject); layers.Clear(); key=theme; Build(); }
            carrier.forceRenderingOff = true;
            enabled = true;
            Render();
        }
        void Add(string sheet, int count = 1) { for(int i=0;i<count;i++) layers.Add(new PhoenixRadianceLayer(transform,sheet)); }
        void Build()
        {
            if(key.StartsWith("LifeBurn")) { Add("RainbowFan"); Add("FeatherKit",10); }
            else if(key == "HungrySpirit") { Add("FeatherKit",21); }
            else if(key.StartsWith("Clone")) { Add("CloneWings"); Add("FeatherKit",5); }
            else if(key == "OrbitFeather") { Add("QuillNeedle"); }
            else if(key == "HeavySnipe" || key == "HeavySnipeFullCharge") { Add("HeavyCharge"); Add("FeatherKit",8); }
            else if(key == "CursorControlGlow") { Add("CometRibbon"); Add("QuillNeedle"); Add("FeatherKit",7); }
            else if(key == "NeuralBlock") { Add("FeatherKit"); }
            else if(key == "GastricPeristalsisWave") { Add("GoldRing"); }
            else if(key == "MucosalFortress" || key == "MucosalFortressCreate") { Add("BlueWing"); }
            else Add("FeatherKit",12);
        }
        public void SetCharge(float value) { charge=Mathf.Clamp01(value); }
        public void SetAmount(float value) { amount=Mathf.Max(0,value); }
        public void SetStack(int index) { stackIndex=Mathf.Clamp(index,0,2); }
        void LateUpdate() { age += Time.deltaTime; displayedAmount = Mathf.MoveTowards(displayedAmount, amount, Time.deltaTime * 8); Render(); }
        void Render()
        {
            if(carrier==null)return;
            foreach(var l in layers)l.Hide();
            if(!carrier.enabled)return;
            bool reduced = GamePreferences.Current.reducedMotion;
            float motion = reduced ? 0 : age;
            var sort = target != null ? target : carrier;
            Vector3 center = transform.position;
            float h = target != null && target.sprite != null ? target.bounds.size.y : .7f;
            float w = target != null && target.sprite != null ? target.bounds.size.x : .7f;
            h=Mathf.Max(.1f,h); w=Mathf.Max(.1f,w);
            float sourceSpan=target!=null&&target.sprite!=null
                ? Mathf.Max(target.sprite.bounds.size.x*Mathf.Abs(target.transform.lossyScale.x),target.sprite.bounds.size.y*Mathf.Abs(target.transform.lossyScale.y)) : .7f;
            float fade = loop ? 1 : 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,age/duration));
            float enter = Mathf.SmoothStep(0,1,age/.25f);
            float frame = PhoenixRadianceLayer.LoopFrame(motion,10);
            float orientation = transform.eulerAngles.z;
            Vector2 carrierSize=carrier.sprite != null ? Vector2.Scale(carrier.sprite.bounds.size, new Vector2(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y))) : Vector2.one;
            if(key.StartsWith("LifeBurn"))
            {
                center=target!=null?target.bounds.center:center;
                float growth=Mathf.Lerp(.4f,1,Mathf.SmoothStep(0,1,age/.65f));
                layers[0].Draw(center+Vector3.up*h*.8f,new Vector2(4.2f,3.35f)*h*growth,0,frame,.94f*fade*enter,sort,-3,motion,.008f);
                SparkleFeathers(1,center,h*1.5f,sort,motion,fade*.58f);
            }
            else if(key=="HungrySpirit")
            {
                center=target!=null?target.bounds.center:center;
                float visible=Mathf.Clamp01(displayedAmount)*21;
                for(int i=0;i<21;i++)
                {
                    // Alternate from centre to sides. Gold, purple and green accumulate
                    // independently, so every acquired stack visibly sprouts new feathers.
                    float born=Mathf.SmoothStep(0,1,Mathf.Clamp01(visible-i)); if(born<=0)continue;
                    int tier=i/9, n=i%9, side=n%2==0?1:-1;
                    float angle=side*((n+1)/2)*16f;
                    float length=h*(2.2f-tier*.37f);
                    Vector3 root=center+Vector3.down*h*.25f;
                    float tilt=angle+Mathf.Sin(motion*1.7f+i)*1.4f;
                    var direction=Quaternion.Euler(0,0,tilt)*Vector3.up;
                    layers[i].Draw(root+direction*length*.38f*born, new Vector2(length*.73f,length)*born,tilt,8+i%3,.93f*enter,sort,-4+Mathf.Min(tier,1),motion,.012f);
                }
            }
            else if(key.StartsWith("Clone"))
            {
                center=target!=null?target.bounds.center:center;
                layers[0].Draw(center+Vector3.up*h*.05f,Vector2.one*Mathf.Max(w,h)*2.1f,0,PhoenixRadianceLayer.LoopFrame(motion,25),.95f*fade*enter,sort,-1,motion);
                SparkleFeathers(1,center,h*1.3f,sort,motion,fade*.55f);
            }
            else if(key=="OrbitFeather")
            {
                float length=Mathf.Clamp(Mathf.Max(carrierSize.x,carrierSize.y)*1.6f,.65f,1.45f);
                layers[0].Draw(center,Vector2.one*length,orientation,frame,1,carrier,0,motion,.008f);
            }
            else if(key=="HeavySnipe" || key=="HeavySnipeFullCharge")
            {
                float length=Mathf.Clamp(sourceSpan*1.65f, .7f, 3.4f);
                center += Quaternion.Euler(0,0,orientation)*Vector3.right*length*.25f;
                layers[0].Draw(center,Vector2.one*length,orientation,charge*11,1,sort,2,motion,.003f*charge);
                if(charge>.8f)SparkleFeathers(1,center,length*.45f,sort,motion,(charge-.8f)*2);
            }
            else if(key=="CursorControlGlow")
            {
                float length=Mathf.Clamp(sourceSpan*2.1f,.8f,2.1f);
                var right=Quaternion.Euler(0,0,orientation)*Vector3.right;
                float stages=Mathf.Clamp(displayedAmount,0,7);
                float tailLength=length*2.3f;
                // One continuous atlas ribbon revealed in seven connected colour bands.
                // Endpoint remains attached to the flying needle for all stage counts.
                layers[0].Draw(center-right*tailLength*.3f,new Vector2(tailLength, length*.9f),orientation,frame,.85f,sort,0,motion,.012f,stages/7f);
                layers[1].Draw(center,Vector2.one*length,orientation,frame,1,sort,1,motion,.007f);
                for(int i=0;i<7;i++)
                {
                    float alpha=Mathf.Clamp01(stages-i)*.65f;
                    float t=Mathf.Repeat(motion*.7f+i*.137f,1);
                    Vector3 side=Quaternion.Euler(0,0,orientation)*Vector3.up;
                    var pos=center-right*(i/7f*tailLength*.72f+t*.15f)+side*Mathf.Sin(motion*2+i)*length*.2f;
                    layers[2+i].Draw(pos,Vector2.one*length*.2f,orientation-90+i*9,1+i,alpha,sort,2,motion);
                }
            }
            else if(key=="NeuralBlock")
            {
                if(target!=null)center=new Vector3(target.bounds.center.x,target.bounds.max.y+h*.12f,target.bounds.center.z);
                float size=Mathf.Clamp(h*.58f,.28f,.7f);
                layers[0].Draw(center,Vector2.one*size,-25,9,.95f*enter,sort,5,motion,.014f);
            }
            else if(key=="GastricPeristalsisWave")
            {
                // Rotate a complete painted ring continuously: blending dissimilar
                // fine ring ornaments creates distracting crosshatching at small sizes.
                layers[0].Draw(center,carrierSize,motion*12,0,.9f,carrier,0,motion,0,1,Mathf.Max(32,carrierSize.x*34));
            }
            else if(key=="MucosalFortress" || key=="MucosalFortressCreate")
            {
                float wrap=Mathf.SmoothStep(0,1,age/.65f);
                float angle=stackIndex*120;
                // Hold a curved C-shell, not a closed opaque disk; overlapping wings form
                // the sphere while the player remains visible through cyan translucency.
                float pose=wrap*7;
                float opacity=key=="MucosalFortressCreate"?.35f*fade:.56f;
                layers[0].Draw(center,carrierSize,angle,pose,opacity,sort,2+stackIndex,motion,.005f,1,0,new Color(.68f,.87f,1));
            }
            else if(key=="CursorControl") { /* Old player-back ornament intentionally absent. */ }
            else
            {
                float radius=Mathf.Clamp(Mathf.Max(carrierSize.x,carrierSize.y)*.5f,.35f,2.4f);
                SparkleFeathers(0,center,radius,sort,motion,fade*.8f,true);
            }
        }
        void SparkleFeathers(int first, Vector3 center, float radius, SpriteRenderer sort, float phase, float alpha, bool burst=false)
        {
            for(int i=first;i<layers.Count;i++)
            {
                float t=burst?Mathf.Clamp01(age/duration):Mathf.Repeat(phase*.5f+i*.173f,1);
                float a=i*2.399963f+phase*.25f;
                Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                float r=radius*(burst?Mathf.Lerp(.12f,1.25f,t):Mathf.Lerp(.6f,1,t));
                float opacity=alpha*Mathf.Sin(t*Mathf.PI);
                float size=radius*(burst?.28f:.15f);
                int feather=key.StartsWith("Mucosal")?11:key.StartsWith("Heavy")?0:1+i%7;
                layers[i].Draw(center+d*r,Vector2.one*size,a*Mathf.Rad2Deg-90+phase*18,feather,opacity,sort,4,phase,.01f);
            }
        }
        void OnDisable() { foreach(var l in layers)l.Hide(); }
    }
}
