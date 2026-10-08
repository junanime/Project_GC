using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public enum ChameleonKind { Drift, Foam, Fanta, Latte }

    public static class ChameleonArt
    {
        public const float Width = 2.8f;
        public static readonly string[] Names = { "꾸룩 · 위산연동 카멜레온", "보글 · 제산거품 카멜레온", "톡톡 · 환타 카멜레온", "라떼 · 커피수혈 카멜레온" };
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<Sprite, Sprite> portraits = new Dictionary<Sprite, Sprite>();
        public static ChameleonKind Kind(MonsterBlueprint bp)
        {
            if (bp != null) for (int i = 0; i < Names.Length; i++) if (bp.name == Names[i]) return (ChameleonKind)i;
            return ChameleonKind.Fanta; // Existing scene/prefab GUID remains valid.
        }
        public static Sprite[] Frames(string name)
        {
            if (cache.TryGetValue(name, out var result)) return result;
            Color32[] pixels=null;
            result = Resources.LoadAll<Sprite>("Chameleons/" + name).OrderBy(s => s.name, StringComparer.Ordinal).Select(source =>
            {
                // Equal physical width, one grounded pivot, no per-animation transform scale changes.
                var s = Sprite.Create(source.texture, source.rect, new Vector2(.5f, 0), source.rect.width / Width, 0, name=="Projectiles"?SpriteMeshType.FullRect:SpriteMeshType.Tight);
                s.name = source.name;
                if(name!="Projectiles"){if(pixels==null)pixels=source.texture.GetPixels32();IsolateBodyMesh(s,pixels);}
                return s;
            }).ToArray();
            cache[name] = result; return result;
        }
        // Sprite geometry only: preserve the source PNG and discard detached bits from adjacent poses.
        // Horizontal one-pixel runs retain the original contour without reprocessing texture pixels.
        static void IsolateBodyMesh(Sprite sprite,Color32[] pixels)
        {
            int w=(int)sprite.rect.width,h=(int)sprite.rect.height,tw=sprite.texture.width,ox=(int)sprite.rect.x,oy=(int)sprite.rect.y;
            var visited=new bool[w*h];var queue=new Queue<int>();var largest=new List<int>();
            for(int seed=0;seed<visited.Length;seed++)
            {
                if(visited[seed]||pixels[(oy+seed/w)*tw+ox+seed%w].a<128)continue;
                var part=new List<int>();visited[seed]=true;queue.Enqueue(seed);
                while(queue.Count>0)
                {
                    int p=queue.Dequeue();part.Add(p);
                    foreach(int d in new[]{-1,1,-w,w}){int n=p+d;if(n<0||n>=visited.Length||Math.Abs(n%w-p%w)>1||visited[n]||pixels[(oy+n/w)*tw+ox+n%w].a<128)continue;visited[n]=true;queue.Enqueue(n);}
                }
                if(part.Count>largest.Count)largest=part;
            }
            var keep=new bool[w*h];foreach(int p in largest)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int x=p%w+dx,y=p/w+dy;if(x>=0&&x<w&&y>=0&&y<h)keep[y*w+x]=true;}
            var vertices=new List<Vector2>();var triangles=new List<ushort>();
            vertices.Add(Vector2.zero);vertices.Add(new Vector2(w,h));triangles.Add(0);triangles.Add(1);triangles.Add(1);
            for(int y=0;y<h;y++)for(int x=0;x<w;)
            {
                if(!keep[y*w+x]){x++;continue;}int first=x;while(x<w&&keep[y*w+x])x++;
                ushort n=(ushort)vertices.Count;vertices.Add(new Vector2(first,y));vertices.Add(new Vector2(x,y));vertices.Add(new Vector2(x,y+1));vertices.Add(new Vector2(first,y+1));
                triangles.Add(n);triangles.Add((ushort)(n+1));triangles.Add((ushort)(n+2));triangles.Add(n);triangles.Add((ushort)(n+2));triangles.Add((ushort)(n+3));
            }
            ChameleonMeshPump.Schedule(sprite,vertices.ToArray(),triangles.ToArray());
        }
        public static Sprite Frame(ChameleonKind kind, int index)
        {
            var list = Frames(kind.ToString());
            return list.Length == 0 ? null : list[Mathf.Clamp(index, 0, list.Length - 1)];
        }
        public static Sprite Projectile(ChameleonKind kind) => Fx((int)kind);
        public static Sprite Fx(int index)
        {
            var list = Frames("Projectiles"); return list.Length > index ? list[index] : null;
        }
        public static Sprite Portrait(ChameleonKind kind, int frame, bool faceOnly = false)
        {
            var source = Frame(kind, frame); if (source == null) return null;
            // Full-body special frame reused verbatim; crop removes feet, never stretches the head.
            if (faceOnly) return Crop(source, .36f, .32f, .64f, .68f);
            if (!portraits.TryGetValue(source, out var portrait)) portraits[source] = portrait = Crop(source, .14f, .22f, .86f, .78f);
            return portrait;
        }
        static readonly Dictionary<string, Sprite> crops = new Dictionary<string, Sprite>();
        static Sprite Crop(Sprite s, float x, float y, float w, float h)
        {
            string key = s.name + ":" + x;
            if (crops.TryGetValue(key, out var v)) return v;
            Rect r = s.rect; r = new Rect(r.x + r.width*x, r.y + r.height*y, r.width*w, r.height*h);
            return crops[key] = Sprite.Create(s.texture, r, new Vector2(.5f, 0), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        public static Color Accent(ChameleonKind kind)
        {
            switch (kind)
            {
                case ChameleonKind.Drift: return new Color(.82f,.95f,.15f);
                case ChameleonKind.Foam: return new Color(.55f,1,.91f);
                case ChameleonKind.Latte: return new Color(1,.83f,.54f);
                default: return new Color(1,.49f,.05f);
            }
        }
    }

    // Unity 2022 only permits OverrideGeometry from the player loop, including when a guide loads art in Awake.
    public sealed class ChameleonMeshPump : MonoBehaviour
    {
        static ChameleonMeshPump instance;readonly List<Action> pending=new List<Action>();
        public static void Schedule(Sprite sprite,Vector2[] vertices,ushort[] triangles)
        {
            if(!Application.isPlaying)return;
            if(instance==null){instance=new GameObject("Chameleon sprite mesh initialization").AddComponent<ChameleonMeshPump>();DontDestroyOnLoad(instance.gameObject);}
            instance.pending.Add(()=>{if(sprite!=null)sprite.OverrideGeometry(vertices,triangles);});instance.enabled=true;
        }
        void Update(){foreach(var job in pending)job();pending.Clear();enabled=false;}
    }

    // Conserves exactly 100%; never takes weight from an event which already occurred.
    public sealed class ChameleonLottery
    {
        readonly float[] weights = {25,25,25,25};
        readonly bool[] occurred = new bool[4];
        public bool Frozen { get; private set; }
        public float Weight(ChameleonKind kind) => weights[(int)kind];
        public bool Occurred(ChameleonKind kind) => occurred[(int)kind];
        public void Record(ChameleonKind kind)
        {
            if (Frozen) return;
            int target = (int)kind; occurred[target] = true;
            float remaining = 20;
            for (int pass = 0; pass < 4 && remaining > .00001f; pass++)
            {
                int donors = 0;
                for (int i=0;i<4;i++) if (i!=target&&!occurred[i]&&weights[i]>.00001f) donors++;
                if (donors==0) break;
                float share = remaining/donors, taken = 0;
                for (int i=0;i<4;i++) if (i!=target&&!occurred[i]&&weights[i]>.00001f)
                { float amount=Mathf.Min(share,weights[i]);weights[i]-=amount;taken+=amount; }
                weights[target]+=taken;remaining-=taken;
            }
        }
        public ChameleonKind Pick(float sample)
        {
            Frozen = true;float n=Mathf.Clamp(sample,0,.999999f)*100;
            for(int i=0;i<4;i++){n-=weights[i];if(n<0)return (ChameleonKind)i;}
            return ChameleonKind.Latte;
        }
    }

    public sealed class ChameleonMotion : MonoBehaviour
    {
        public SpriteRenderer Art { get; private set; }
        public ChameleonKind Kind { get; private set; }
        int first, count=1;float age, duration=1;bool loop, manual;
        public int FrameIndex { get; private set; }
        public void Configure(SpriteRenderer art, ChameleonKind kind) { Art=art;Kind=kind;Sample(0); }
        public void Sample(int frame) { manual=true;FrameIndex=frame;if(Art!=null)Art.sprite=ChameleonArt.Frame(Kind,frame); }
        public void Play(int start,int length,float seconds,bool repeat=false)
        {
            if(!manual&&first==start&&count==length&&loop==repeat)return;
            first=start;count=length;duration=Mathf.Max(.01f,seconds);age=0;loop=repeat;manual=false;Show();
        }
        void Show(){float p=loop?Mathf.Repeat(age/duration,1):Mathf.Clamp01(age/duration);FrameIndex=first+Mathf.Min(count-1,Mathf.FloorToInt(p*count));if(Art!=null)Art.sprite=ChameleonArt.Frame(Kind,FrameIndex);}
        void LateUpdate(){if(manual||ChameleonTime.Paused)return;age+=Time.deltaTime;Show();}
    }
    public static class ChameleonTime
    {
        static LevelManager level;
        public static bool Paused { get { if(level==null)level=UnityEngine.Object.FindObjectOfType<LevelManager>();return Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage||(level!=null&&(level.IsRunFlowPaused||level.IsLevelEnded)); } }
        public static System.Collections.IEnumerator Wait(float seconds)
        {for(float t=0;t<seconds;){if(!Paused)t+=Time.deltaTime;yield return null;}}
    }
}
