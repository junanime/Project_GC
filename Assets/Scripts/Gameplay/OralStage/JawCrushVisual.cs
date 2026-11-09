using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vampire
{
    /// <summary>
    /// A jaw impact snapshots the art which was actually visible before damage. Only this
    /// detached, subdivided sprite mesh is flattened: actor transforms, animation, collision
    /// and damage remain untouched. The snapshot also survives a lethal pooled monster hit.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class JawCrushVisual : MonoBehaviour
    {
        public const float SquashSeconds = .10f, HoldSeconds = .55f, RecoverSeconds = .30f;
        public const float CorpseHoldSeconds = .70f, CorpseFadeSeconds = .18f;
        public const float FlatWidth = 1.75f, FlatHeight = .28f;
        static readonly List<JawCrushVisual> active = new List<JawCrushVisual>();
        static Character damageActor;
        public static int ActiveCount { get { int n=0; foreach(var v in active) if(v!=null && v.IsPlaying)n++; return n; } }
        public static JawCrushVisual ForActor(Component actor)
        {
            for(int i=active.Count-1;i>=0;i--)
                if(active[i]!=null && active[i].Actor==actor && active[i].IsPlaying &&
                    active[i].guard!=null && active[i].guard.Generation==active[i].generation)return active[i];
            return null;
        }
        public static bool IsApplyingDamageTo(Character actor) => actor!=null && actor==damageActor;
        public static IEnumerator FinishPlayerDeath(Character actor, SpriteRenderer original)
        {
            // TakeDamage starts DeathAnimation synchronously, before its caller can
            // create the confirmed-hit snapshot. Give that caller its first frame.
            yield return null;
            var visual=ForActor(actor);
            while(visual!=null && visual.IsPlaying)
            {
                Time.timeScale=0;
                yield return null;
            }
            if(original!=null)original.enabled=false;
            if(visual!=null)visual.Cancel();
        }
        public Component Actor { get; private set; }
        public SpriteRenderer OriginalRenderer { get; private set; }
        public IReadOnlyList<MeshRenderer> ProxyRenderers => proxyRenderers;
        public bool IsPlaying { get; private set; }
        public bool IsCorpse { get; private set; }
        public float Elapsed { get; private set; }
        public float Compression { get; private set; }
        public float Progress => Mathf.Clamp01(Elapsed/(IsCorpse?CorpseHoldSeconds+CorpseFadeSeconds:SquashSeconds+HoldSeconds+RecoverSeconds));

        sealed class Source
        {
            public SpriteRenderer Renderer;
            public Sprite Sprite;
            public Vector3[] Positions;
            public Vector2[] UV;
            public int[] Indices;
            public Color Color;
            public int Layer, Order;
            public string Name;
        }
        internal sealed class HiddenSource
        {
            public SpriteRenderer Renderer;
            public bool IntendedHidden, Owned;
        }
        sealed class Layer
        {
            public Mesh Mesh;
            public Material Material;
            public MeshRenderer Renderer;
            public Vector3[] Rest, Working;
            public Color Color;
        }
        sealed class Snapshot
        {
            public Component Actor;
            public SpriteRenderer Original;
            public JawCrushVisualOwner Guard;
            public int Generation;
            public Vector3 Position;
            public Bounds Bounds;
            public readonly List<Source> Sources = new List<Source>();
            public readonly List<SpriteRenderer> Candidates = new List<SpriteRenderer>();
        }
        readonly List<Layer> layers = new List<Layer>();
        readonly List<MeshRenderer> proxyRenderers = new List<MeshRenderer>();
        readonly List<HiddenSource> hidden = new List<HiddenSource>();
        JawCrushVisualOwner guard;
        OralStageRuntime stage;
        LevelManager level;
        Character character;
        Monster monster;
        int generation;
        Vector3 previousActorPosition;
        float width, height, deadAge;
        bool detached, finished, disposing;
        static readonly int ColorId=Shader.PropertyToID("_Color");
        MaterialPropertyBlock properties;

        void Awake(){properties=new MaterialPropertyBlock();}

        public static bool ApplyDamage(Character target, float damage)
        {
            if(target==null || !target.IsAlive || damage<=0)return false;
            var snapshot=Capture(target);
            float hp=target.CurrentHealth;
            int revives=target.ReviveCount;
            var stats=Object.FindObjectOfType<StatsManager>();
            float taken=stats!=null?stats.DamageTaken:0;
            var priorDamageActor=damageActor;
            damageActor=snapshot.Sources.Count>0?target:null;
            try { target.TakeDamage(damage); }
            finally { damageActor=priorDamageActor; }
            // A lethal hit can immediately revive above the previous HP, or a reactive
            // item can heal. The damage ledger still distinguishes it from shield/dodge.
            bool hurt=target.CurrentHealth<hp || !target.IsAlive || target.ReviveCount<revives ||
                (stats!=null && stats.DamageTaken>taken);
            if(hurt)Play(snapshot);
            return hurt;
        }
        public static bool ApplyDamage(Monster target, float damage)
        {
            if(target==null || !target.IsAlive || damage<=0)return false;
            var snapshot=Capture(target);
            float hp=target.HP;
            target.TakeDamage(damage);
            bool hurt=target==null || target.HP<hp || !target.IsAlive;
            if(hurt)Play(snapshot);
            return hurt;
        }

        static Snapshot Capture(Component actor)
        {
            var old=ForActor(actor);
            if(old!=null)old.Cancel();
            var m=actor as Monster;
            var c=actor as Character;
            var primary=m!=null?m.BodyRenderer:c.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            var owner=actor.GetComponent<JawCrushVisualOwner>()??actor.gameObject.AddComponent<JawCrushVisualOwner>();
            owner.ReleaseFinishedCorpse();
            var result=new Snapshot { Actor=actor, Original=primary, Guard=owner,
                Generation=owner.Generation, Position=actor.transform.position };
            var snail=actor.GetComponentInChildren<SnailMiniVisual>(true);
            var seaweed=actor.GetComponent<SeaweedTrapVisual>();
            bool any=false;
            foreach(var r in actor.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string label=r.name.ToLowerInvariant();
                bool body=r==primary || (primary!=null && r.transform.IsChildOf(primary.transform));
                if(c!=null)body=body || r.name=="Ari controlled marble phoenix" || r.name=="Shini inflated chest pose";
                if(snail!=null)body=body || r==snail.Body || r==snail.Shell;
                if(seaweed!=null)body=body || r==seaweed.Rear || r==seaweed.Front;
                // These are attached body parts, not combat projectiles/auras/UI/shadows.
                if(m!=null && (r.name=="Held dung" || r.name=="Spreading mushroom fold"))body=true;
                if(!body || label.Contains("shadow") || label.Contains("needle") || label.Contains("barrier"))continue;
                result.Candidates.Add(r);
                if(!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || r.sprite==null || r.color.a<=.001f)continue;
                var source=SnapshotSprite(r);
                result.Sources.Add(source);
                var bounds=VisibleBodyGeometry.InSpace(r,actor.transform);
                // Convert the baked opaque bounds to world space, preserving arbitrary
                // sprite pivots and actor scaling. Transparent atlas padding is ignored.
                for(int i=0;i<4;i++)
                {
                    var p=actor.transform.TransformPoint(new Vector3((i&1)==0?bounds.min.x:bounds.max.x,
                        (i&2)==0?bounds.min.y:bounds.max.y,bounds.center.z));
                    if(!any){result.Bounds=new Bounds(p,Vector3.zero);any=true;} else result.Bounds.Encapsulate(p);
                }
            }
            return result;
        }

        static Source SnapshotSprite(SpriteRenderer renderer)
        {
            var sprite=renderer.sprite;
            var points=sprite.vertices;var uv=sprite.uv;var triangles=sprite.triangles;
            var positions=new List<Vector3>();var textureUV=new List<Vector2>();var indices=new List<int>();
            // Subdivide existing triangles, retaining the exact atlas UV/packing and
            // alpha silhouette. This bends the original art instead of resizing a quad.
            const int steps=4;
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                var grid=new int[steps+1,steps+1];
                for(int i=0;i<=steps;i++)for(int j=0;j<=steps-i;j++)
                {
                    float u=(float)i/steps,v=(float)j/steps;
                    Vector2 p=points[a]*(1-u-v)+points[b]*u+points[c]*v;
                    if(renderer.flipX)p.x=-p.x;if(renderer.flipY)p.y=-p.y;
                    grid[i,j]=positions.Count;
                    positions.Add(renderer.transform.TransformPoint(p));
                    textureUV.Add(uv[a]*(1-u-v)+uv[b]*u+uv[c]*v);
                }
                for(int i=0;i<steps;i++)for(int j=0;j<steps-i;j++)
                {
                    indices.Add(grid[i,j]);indices.Add(grid[i+1,j]);indices.Add(grid[i,j+1]);
                    if(j<steps-i-1){indices.Add(grid[i+1,j]);indices.Add(grid[i+1,j+1]);indices.Add(grid[i,j+1]);}
                }
            }
            var group=renderer.GetComponentInParent<SortingGroup>();
            return new Source {Renderer=renderer,Name=renderer.name,Sprite=sprite,Positions=positions.ToArray(),UV=textureUV.ToArray(),
                Indices=indices.ToArray(),Color=renderer.color,Layer=group!=null?group.sortingLayerID:renderer.sortingLayerID,
                Order=group!=null?group.sortingOrder+renderer.sortingOrder:renderer.sortingOrder};
        }

        static void Play(Snapshot snapshot)
        {
            if(snapshot==null || snapshot.Sources.Count==0)return;
            var go=new GameObject("Jaw flattened sprite snapshot");
            var visual=go.AddComponent<JawCrushVisual>();
            visual.Actor=snapshot.Actor;visual.OriginalRenderer=snapshot.Original;
            visual.character=snapshot.Actor as Character;visual.monster=snapshot.Actor as Monster;
            visual.stage=OralStageRuntime.Instance;visual.level=visual.stage!=null?visual.stage.Level:null;
            visual.guard=snapshot.Guard;visual.generation=snapshot.Generation;
            visual.previousActorPosition=snapshot.Position;
            visual.width=Mathf.Max(.01f,snapshot.Bounds.size.x);visual.height=Mathf.Max(.01f,snapshot.Bounds.size.y);
            go.transform.position=new Vector3(snapshot.Bounds.center.x,snapshot.Bounds.min.y,snapshot.Position.z);
            foreach(var candidate in snapshot.Candidates)
                if(candidate!=null)visual.hidden.Add(new HiddenSource {Renderer=candidate});
            foreach(var source in snapshot.Sources)visual.AddLayer(source);
            visual.IsPlaying=true;active.Add(visual);
            if(visual.guard!=null && visual.guard.Generation==visual.generation && visual.guard.gameObject.activeInHierarchy)
                visual.guard.Track(visual);
            else visual.detached=true;
            if(!visual.ActorAlive || visual.detached)visual.BecomeCorpse();
            visual.ApplyPose(0,1);
            visual.HideSources();
        }

        void AddLayer(Source source)
        {
            var child=new GameObject("Flattened "+source.Name);
            child.transform.SetParent(transform,false);
            var filter=child.AddComponent<MeshFilter>();var renderer=child.AddComponent<MeshRenderer>();
            var mesh=new Mesh {name="Jaw sprite UV deformation"};mesh.MarkDynamic();
            var vertices=new Vector3[source.Positions.Length];
            for(int i=0;i<vertices.Length;i++)vertices[i]=source.Positions[i]-transform.position;
            mesh.vertices=vertices;mesh.uv=source.UV;mesh.triangles=source.Indices;mesh.RecalculateBounds();
            var colors=new Color[vertices.Length];for(int i=0;i<colors.Length;i++)colors[i]=source.Color;mesh.colors=colors;
            var material=new Material(Shader.Find("Sprites/Default")){name="Jaw original sprite pixels",mainTexture=source.Sprite.texture};
            filter.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.sortingLayerID=source.Layer;renderer.sortingOrder=source.Order;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            layers.Add(new Layer {Mesh=mesh,Material=material,Renderer=renderer,Rest=vertices,Working=new Vector3[vertices.Length],Color=source.Color});
            proxyRenderers.Add(renderer);
        }
        bool ActorAlive => character!=null?character.IsAlive:monster!=null && monster.IsAlive;
        bool OwnerCurrent => !detached && guard!=null && guard.Generation==generation && guard.gameObject.activeInHierarchy;

        void Update()
        {
            if(disposing)return;
            bool validStage=stage!=null && stage==OralStageRuntime.Instance && stage.Map!=null && stage.Map.IsOral;
            if(!validStage || StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage ||
                (character!=null && (character.IsPortalTravelling || character.IsPortalPoseHeld))) {Cancel();return;}
            if(OwnerCurrent && !ActorAlive)BecomeCorpse();
            if(!OwnerCurrent && !IsCorpse){Cancel();return;}
            if(finished)
            {
                // A compound corpse may keep child art enabled during its particles.
                // Keep only visibility ownership until it despawns/revives, not a picture.
                if(!OwnerCurrent || ActorAlive || !HasEnabledSource())Cancel();
                return;
            }
            if(!IsCorpse && Actor!=null)
            {
                Vector3 current=Actor.transform.position,delta=current-previousActorPosition;
                if(delta.sqrMagnitude>9f){Cancel();return;}
                transform.position+=delta;previousActorPosition=current;
            }
            bool playerDead=level!=null && level.PlayerCharacter!=null && !level.PlayerCharacter.IsAlive;
            bool paused=!playerDead && (Time.timeScale<=0 || (level!=null && level.IsRunFlowPaused) ||
                (level!=null && level.PlayerCharacter!=null && level.PlayerCharacter.IsChoosingAugment));
            if(paused)return;
            // Player death stops scaled gameplay immediately; its detached death snapshot
            // gets its short finishing beat. Ordinary menus/rewards freeze all snapshots.
            float dt=playerDead?Time.unscaledDeltaTime:Time.deltaTime;
            Elapsed+=dt;if(IsCorpse)deadAge+=dt;
            if(IsCorpse)
            {
                Compression=Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/SquashSeconds));
                float alpha=1-Mathf.Clamp01((deadAge-CorpseHoldSeconds)/CorpseFadeSeconds);
                ApplyPose(Compression,alpha);
                if(deadAge>=CorpseHoldSeconds+CorpseFadeSeconds)FinishPicture();
            }
            else
            {
                if(Elapsed<SquashSeconds)Compression=Mathf.SmoothStep(0,1,Elapsed/SquashSeconds);
                else if(Elapsed<SquashSeconds+HoldSeconds)Compression=1;
                else
                {
                    float recovery=Mathf.Clamp01((Elapsed-SquashSeconds-HoldSeconds)/RecoverSeconds);
                    // A small single overshoot sells elasticity without rubbery oscillation.
                    Compression=recovery<.72f?1-Mathf.SmoothStep(0,1,recovery/.72f)*1.10f:
                        Mathf.Lerp(-.10f,0,Mathf.SmoothStep(0,1,(recovery-.72f)/.28f));
                }
                ApplyPose(Compression,1);
                if(Elapsed>=SquashSeconds+HoldSeconds+RecoverSeconds)Cancel();
            }
        }
        void LateUpdate()
        {
            if(disposing)return;
            if(character!=null && !character.IsAlive && IsPlaying)Time.timeScale=0;
            if(OwnerCurrent)HideSources();
        }
        void BecomeCorpse()
        {
            if(IsCorpse)return;
            IsCorpse=true;deadAge=0;
            // Keep the impact position, not a subsequently sinking/flying death pose.
        }
        void ApplyPose(float compression,float alpha)
        {
            float xScale=Mathf.LerpUnclamped(1,FlatWidth,compression);
            float yScale=Mathf.LerpUnclamped(1,FlatHeight,compression);
            foreach(var layer in layers)
            {
                for(int i=0;i<layer.Rest.Length;i++)
                {
                    var p=layer.Rest[i];float h=Mathf.Clamp01(p.y/height),side=Mathf.Clamp01(Mathf.Abs(p.x)/(width*.5f));
                    // Broader feet/outer hem, gently domed upper silhouette: still the
                    // source face/clothes/monster shell, compressed into a low pancake.
                    float spread=1+Mathf.Max(0,compression)*.11f*(1-h)*side;
                    layer.Working[i]=new Vector3(p.x*xScale*spread,p.y*yScale*(1-Mathf.Max(0,compression)*.10f*side),p.z);
                }
                layer.Mesh.vertices=layer.Working;layer.Mesh.RecalculateBounds();
                properties.Clear();properties.SetColor(ColorId,new Color(1,1,1,alpha));layer.Renderer.SetPropertyBlock(properties);
            }
        }
        internal void RestoreBeforeAnimation()
        {
            foreach(var source in hidden)
            {
                if(source.Owned && source.Renderer!=null)source.Renderer.forceRenderingOff=source.IntendedHidden;
                source.Owned=false;
            }
        }
        void HideSources()
        {
            if(!OwnerCurrent)return;
            foreach(var source in hidden)
            {
                var renderer=source.Renderer;if(renderer==null)continue;
                if(!source.Owned)source.IntendedHidden=renderer.forceRenderingOff;
                renderer.forceRenderingOff=true;source.Owned=true;
            }
        }
        internal void OwnerDisabled()
        {
            RestoreBeforeAnimation();detached=true;
            if(!IsCorpse && !ActorAlive)BecomeCorpse();
            if(!IsCorpse || finished)Cancel();
        }
        void FinishPicture()
        {
            IsPlaying=false;finished=true;
            foreach(var r in proxyRenderers)if(r!=null)r.enabled=false;
            if(!OwnerCurrent || !HasEnabledSource())Cancel();
        }
        bool HasEnabledSource()
        {
            foreach(var source in hidden)
                if(source.Renderer!=null && source.Renderer.enabled && source.Renderer.gameObject.activeInHierarchy)return true;
            return false;
        }
        public void Cancel()
        {
            if(disposing)return;
            disposing=true;IsPlaying=false;RestoreBeforeAnimation();
            if(guard!=null)guard.Untrack(this);
            active.Remove(this);
            Destroy(gameObject);
        }
        void OnDisable()
        {
            RestoreBeforeAnimation();IsPlaying=false;
            foreach(var renderer in proxyRenderers)if(renderer!=null)renderer.enabled=false;
            if(guard!=null)guard.Untrack(this);
            active.Remove(this);
            // Disabling just this behaviour must not leave a frozen detached picture.
            if(!disposing){disposing=true;Destroy(gameObject);}
        }
        void OnDestroy()
        {
            foreach(var layer in layers)
            {
                if(layer.Mesh!=null)Destroy(layer.Mesh);
                if(layer.Material!=null)Destroy(layer.Material);
            }
            layers.Clear();proxyRenderers.Clear();
        }
    }
}
