using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum OralTasteKind { Sweet, Salty, Sour, Bitter, Spicy }

    /// <summary>Independent floor encounters. They never borrow the jaw/ceiling controller's state.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(16000)]
    public sealed class OralTasteEvents : MonoBehaviour
    {
        public static OralTasteEvents Instance { get; private set; }
        public OralTasteKind Kind { get; private set; }
        public bool Active { get; private set; }
        public float Age { get; private set; }
        public float Duration { get; private set; }
        public const float SpicyWarning = 6f, SpicySeconds = 15f, HoneyFeedSeconds = 2f;
        public bool SpicyActive => Active && Kind == OralTasteKind.Spicy && Age >= SpicyWarning && Age < SpicyWarning + SpicySeconds;
        public IReadOnlyList<Vector2> HoneyPositions => honeyPositions;
        public int HoneyFedCount { get; private set; }
        public int SaltSpawnedCount { get; private set; }
        public Vector2 LemonPosition { get; private set; }
        public Vector2 LemonDirection { get; private set; }
        public float LemonLength { get; private set; }
        public Vector2 BitterPosition { get; private set; }
        public float BitterRemaining { get; private set; }
        public int BitterRewards { get; private set; }
        public float BurnRemaining { get; private set; }
        public int BurnTicks { get; private set; }
        public LevelManager Level { get; private set; }
        public bool FloorUnavailable => OralEventArt.Paused(Level) || OralFieldEvents.Instance != null && OralFieldEvents.Instance.CurrentEvent == OralEventKind.Ceiling;

        FiniteStomachMap map;
        Transform artRoot, statusRoot;
        readonly List<Vector2> honeyPositions = new List<Vector2>();
        readonly List<SpriteRenderer> objects = new List<SpriteRenderer>();
        readonly List<Renderer> spiceRenderers = new List<Renderer>();
        readonly List<Object> owned = new List<Object>();
        readonly List<Object> encounterOwned = new List<Object>();
        readonly List<SpriteRenderer> honeyDrops = new List<SpriteRenderer>();
        readonly List<Monster> scratch = new List<Monster>();
        readonly List<HoneyGuest> guests = new List<HoneyGuest>();
        readonly List<SpriteRenderer> embers = new List<SpriteRenderer>();
        readonly int[] honeyServings = new int[3];
        readonly Dictionary<Monster, int> eaten = new Dictionary<Monster, int>();
        readonly Dictionary<Monster, float> bounceCooldown = new Dictionary<Monster, float>();
        readonly List<Monster> stale = new List<Monster>();
        Sprite honey, lemon, bitter, salt;
        Texture2D spicy;
        SpriteRenderer bitterVisual;
        SnailSlowStatus slow;
        MeleeMonsterBlueprint saltBlueprint;
        int saltPool = -1;
        float nextScan, saltClock, burnTick, bouncePlayerCooldown, statusAge, bitterGlow;
        bool bitterEaten, configured;
        sealed class HoneyGuest { public Monster Monster; public OralTasteLease Lease; public int Generation, Piece; public float Feed; }

        public void Configure(LevelManager level)
        {
            CancelAll(); Level = level; map = FiniteStomachMap.Instance; Instance = this;
            honey = LoadArt("Honeycomb", new Vector2(.5f, .12f));
            lemon = LoadArt("Lemon", new Vector2(.5f, .3f)); bitter = LoadArt("Bitter", new Vector2(.5f, .15f));
            salt = LoadArt("Salt", new Vector2(.5f, 0)); spicy = Resources.Load<Texture2D>("OralMap/Events/Spicy");
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var bp = config != null && config.characters.Length > 0 ? config.characters[0] : level.PlayerCharacter.Blueprint;
            var body = level.PlayerCharacter.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            float width = bp != null && bp.idleSpriteSequence.Length > 0 ? PromptSpriteGeometry.Bounds(bp.idleSpriteSequence[0]).width : .5f;
            LemonLength = Mathf.Max(.8f, width * (body != null ? Mathf.Abs(body.transform.lossyScale.x) : 1) * 2.2f);
            statusRoot = new GameObject("맛 이벤트 상태 표시").transform; statusRoot.SetParent(transform, false);
            for (int i = 0; i < 10; i++) embers.Add(OralEventArt.Sprite(statusRoot, "화상·쓴맛 상태 " + i, OralEventArt.Disc, 170));
            statusRoot.gameObject.SetActive(false);
            BuildSaltBlueprint(); configured = true;
        }
        Sprite LoadArt(string name, Vector2 pivot)
        {
            var texture = Resources.Load<Texture2D>("OralMap/Events/" + name); if (texture == null) return null;
            Rect rect = new Rect(0, 0, texture.width, texture.height);
            if (texture.isReadable)
            {
                var pixels = texture.GetPixels32(); int l=texture.width,r=0,b=texture.height,t=0;
                for (int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) if(pixels[y*texture.width+x].a>40)
                {l=Mathf.Min(l,x);r=Mathf.Max(r,x);b=Mathf.Min(b,y);t=Mathf.Max(t,y);}
                if(r>l&&t>b) rect=new Rect(l,b,r-l+1,t-b+1);
            }
            var sprite = Sprite.Create(texture,rect,pivot,100,0,SpriteMeshType.FullRect); sprite.name = "구강 " + name; owned.Add(sprite); return sprite;
        }
        void BuildSaltBlueprint()
        {
            if(Level?.CurrentLevelBlueprint == null || salt == null)return;
            for(int pool=0;pool<Level.CurrentLevelBlueprint.monsters.Length;pool++)
            {
                var group=Level.CurrentLevelBlueprint.monsters[pool];
                if(group.monstersPrefab==null || group.monstersPrefab.GetComponent<Monster>()?.GetType()!=typeof(MeleeMonster))continue;
                foreach(var source in group.monsterBlueprints)
                {
                    if(!(source is MeleeMonsterBlueprint original)||!OralMonsterRoster.IsBlueprint(source))continue;
                    saltBlueprint=Instantiate(original); owned.Add(saltBlueprint); saltPool=pool;
                    saltBlueprint.name="구강 소금결정 · 짠맛 무리";saltBlueprint.hp=Mathf.Max(6,original.hp*1.2f);saltBlueprint.atk=5;
                    saltBlueprint.movespeed=.68f;saltBlueprint.armor=0;saltBlueprint.recovery=0;
                    var tiny=Sprite.Create(salt.texture,salt.rect,new Vector2(.5f,0),salt.rect.height/.38f,0,SpriteMeshType.FullRect);owned.Add(tiny);
                    saltBlueprint.walkSpriteSequence=new[]{tiny};saltBlueprint.resultSprite=tiny;return;
                }
            }
        }
        public bool Begin(OralTasteKind kind)
        {
            if(!configured || Active || FloorUnavailable || map==null || !map.IsOral || Level.PlayerCharacter==null)return false;
            if(kind==OralTasteKind.Spicy && OralFieldEvents.Instance?.CurrentEvent==OralEventKind.Jaw)return false;
            ClearEncounter();Kind=kind;Age=0;Active=true;
            Duration=kind==OralTasteKind.Spicy?SpicyWarning+SpicySeconds:kind==OralTasteKind.Sweet?16:kind==OralTasteKind.Salty?12:18;
            artRoot=new GameObject("구강 맛 이벤트 · "+kind).transform;artRoot.SetParent(transform,false);
            var player=Level.PlayerCharacter;var at=OralMapGeometry.Clamp(map,player.Position+Vector2.up*2,1,OralZone.Tongue);
            if(kind==OralTasteKind.Sweet)
            {
                HoneyFedCount=0;eaten.Clear();
                for(int i=0;i<3;i++){var p=OralMapGeometry.Clamp(map,at+new Vector2((i-1)*2.4f,i%2*1.6f),1,OralZone.Tongue);honeyPositions.Add(p);honeyServings[i]=2;objects.Add(Prop(honey,"벌꿀집 · 흐르는 꿀",p,LemonLength*.65f));}
                for(int i=0;i<12;i++){var r=OralEventArt.Sprite(artRoot,"벌꿀집에서 흐르는 꿀방울",OralEventArt.Disc,20);r.color=new Color(1,.64f,.14f,.8f);GroundVisualSorting.Apply(r,20);honeyDrops.Add(r);}
            }
            else if(kind==OralTasteKind.Salty){SaltSpawnedCount=0;saltClock=.6f;}
            else if(kind==OralTasteKind.Sour)
            {
                LemonPosition=at;LemonDirection=(OralMapGeometry.TongueCenter(map)-at).normalized;
                if(LemonDirection.sqrMagnitude<.5f)LemonDirection=Vector2.right;
                var r=Prop(lemon,"레몬 껍질 · 반동 발판",at,LemonLength);r.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(LemonDirection.y,LemonDirection.x)*Mathf.Rad2Deg);objects.Add(r);
                for(int i=0;i<3;i++){var line=OralEventArt.Line(artRoot,"반동 방향",new Color(1,.96f,.55f,.85f),.045f,15,true);line.positionCount=3;var q=at+LemonDirection*(LemonLength*.6f+i*.18f);var side=new Vector2(-LemonDirection.y,LemonDirection.x)*.09f;line.SetPosition(0,q-LemonDirection*.1f+side);line.SetPosition(1,q);line.SetPosition(2,q-LemonDirection*.1f-side);}
                bouncePlayerCooldown=0;bounceCooldown.Clear();
            }
            else if(kind==OralTasteKind.Bitter)
            {
                BitterPosition=at;bitterEaten=false;BitterRemaining=0;bitterVisual=Prop(bitter,"쓴맛 한입 · 상호작용",at,LemonLength*.55f);objects.Add(bitterVisual);
                var interaction=bitterVisual.gameObject.AddComponent<OralBitterInteraction>();interaction.Configure(this);
                bitterVisual.gameObject.AddComponent<MapMarker>().Configure(MapMarkerKind.Event,"쓴 한입 · 잠깐 느려진 뒤 체력 회복");
            }
            else BuildSpice();
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.DangerWave);return true;
        }
        SpriteRenderer Prop(Sprite sprite,string name,Vector2 position,float width)
        {
            var r=OralEventArt.Sprite(artRoot,name,sprite,18);r.transform.position=position;
            if(sprite!=null)r.transform.localScale=Vector3.one*width/Mathf.Max(.001f,sprite.bounds.size.x);GroundVisualSorting.Apply(r,18);return r;
        }
        public bool ContainsSpice(Vector2 p)=>SpicyActive && map!=null && OralMapGeometry.ZoneAt(map,p)==OralZone.Tongue && OralMapGeometry.TongueDistance(map,p,out _)>=.05f;
        void BuildSpice()
        {
            var center=OralMapGeometry.TongueCenter(map);var radii=OralMapGeometry.TongueRadii(map);
            // A restrained floor wash joins overlapping smears; the exact same tongue boundary clips all meshes.
            BuildClippedMesh(center,radii.x*2.1f,radii.y*2.3f,0,null,new Color(1,.28f,.08f,.22f),3);
            for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
            {
                var p=center+new Vector2(x*radii.x*.39f,y*radii.y*.36f);
                BuildClippedMesh(p,radii.x*(.8f+.08f*((x+y+10)%3)),radii.y*.8f,((x+y)&1)==0?28:-22,spicy,Color.white,4);
            }
        }
        void BuildClippedMesh(Vector2 center,float width,float height,float angle,Texture2D texture,Color tint,int order)
        {
            const int grid=24;var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            var rot=Quaternion.Euler(0,0,angle);
            for(int y=0;y<grid;y++)for(int x=0;x<grid;x++)
            {
                Vector2 a=new Vector2(x/(float)grid,y/(float)grid),b=a+Vector2.right/grid,d=a+Vector2.up/grid,e=a+Vector2.one/grid;
                Vector3 p0=center+(Vector2)(rot*new Vector3((a.x-.5f)*width,(a.y-.5f)*height));
                Vector3 p1=center+(Vector2)(rot*new Vector3((b.x-.5f)*width,(b.y-.5f)*height));
                Vector3 p2=center+(Vector2)(rot*new Vector3((d.x-.5f)*width,(d.y-.5f)*height));
                Vector3 p3=center+(Vector2)(rot*new Vector3((e.x-.5f)*width,(e.y-.5f)*height));
                if(OralMapGeometry.TongueDistance(map,p0,out _) < .025f || OralMapGeometry.TongueDistance(map,p1,out _) < .025f || OralMapGeometry.TongueDistance(map,p2,out _) < .025f || OralMapGeometry.TongueDistance(map,p3,out _) < .025f)continue;
                int n=vertices.Count;vertices.Add(p0);vertices.Add(p1);vertices.Add(p2);vertices.Add(p3);uv.Add(a);uv.Add(b);uv.Add(d);uv.Add(e);indices.Add(n);indices.Add(n+2);indices.Add(n+1);indices.Add(n+1);indices.Add(n+2);indices.Add(n+3);
            }
            if(vertices.Count==0)return;
            var go=new GameObject("혀 경계로 잘린 매운맛 B");go.transform.SetParent(artRoot,false);go.transform.position=Vector3.zero;var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();encounterOwned.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Shader.Find("Sprites/Default"));material.mainTexture=texture!=null?texture:Texture2D.whiteTexture;material.color=tint;encounterOwned.Add(material);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;GroundVisualSorting.Apply(renderer,order);spiceRenderers.Add(renderer);
        }
        void Update()
        {
            if(!configured)return;
            bool hidden=MiniStageRuntimeState.IsInsideMiniStage || StageEntryLoading.IsLoading || OralFieldEvents.Instance?.CurrentEvent==OralEventKind.Ceiling;
            if(artRoot!=null)artRoot.gameObject.SetActive(!hidden);
            if(FloorUnavailable){if(statusRoot!=null)statusRoot.gameObject.SetActive(false);if(slow!=null)slow.Exit(GetInstanceID(),0);return;}
            float dt=Time.deltaTime;statusAge+=dt;TickStatus(dt);
            if(!Active)return;Age+=dt;
            if(Kind==OralTasteKind.Sweet)TickHoney(dt);
            else if(Kind==OralTasteKind.Salty)TickSalt(dt);
            else if(Kind==OralTasteKind.Sour)TickLemon(dt);
            else if(Kind==OralTasteKind.Spicy)TickSpice(dt);
            else if(Kind==OralTasteKind.Bitter && bitterVisual!=null && !bitterEaten)bitterVisual.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(Age*2)*3);
            if(Age>=Duration){Active=false;ClearEncounter();}
        }
        void Snapshot(){scratch.Clear();if(Level?.EntityManager?.LivingMonsters!=null)foreach(var m in Level.EntityManager.LivingMonsters)scratch.Add(m);}
        static bool Eligible(Monster m)=>m!=null&&m.gameObject.activeInHierarchy&&m.HP>0&&!m.IsMiniStageOwned&&!m.IsFieldRuntimeSuspended&&!(m is BossMonster)&&!(m is MiniBossMonster)&&!(m is BloodClotMonster)&&!(m is TrapMonster);
        void TickHoney(float dt)
        {
            if((nextScan-=dt)<=0)
            {
                nextScan=.35f;Snapshot();foreach(var m in scratch)
                {
                    if(!Eligible(m)||!OralMonsterRoster.IsBlueprint(m.Blueprint)||guests.Count>=12||OralMapGeometry.ZoneAt(map,m.Position)!=OralZone.Tongue)continue;
                    var lease=m.GetComponent<OralTasteLease>()??m.gameObject.AddComponent<OralTasteLease>();
                    if(eaten.TryGetValue(m,out int gen)&&gen==lease.Generation)continue;
                    bool exists=false;foreach(var guest in guests)if(guest.Monster==m&&guest.Generation==lease.Generation){exists=true;break;}if(exists)continue;
                    int nearest=-1;float dist=36;for(int i=0;i<honeyPositions.Count;i++){float d=(honeyPositions[i]-m.Position).sqrMagnitude;if(honeyServings[i]>0&&d<dist){nearest=i;dist=d;}}
                    if(nearest>=0)guests.Add(new HoneyGuest{Monster=m,Lease=lease,Generation=lease.Generation,Piece=nearest});
                }
            }
            for(int i=guests.Count-1;i>=0;i--)
            {
                var g=guests[i];var m=g.Monster;
                if(!Eligible(m)||g.Lease==null||g.Lease.Generation!=g.Generation||honeyServings[g.Piece]<=0){guests.RemoveAt(i);continue;}
                if(Vector2.Distance(m.Position,honeyPositions[g.Piece])>.55f||m.IceMoveMultiplier<=.02f){g.Feed=0;continue;}
                g.Feed+=dt;
                if(g.Feed>=HoneyFeedSeconds)
                {
                    honeyServings[g.Piece]--;HoneyFedCount++;eaten[m]=g.Generation;
                    (m.GetComponent<MonsterCombatBuffRuntime>()??m.gameObject.AddComponent<MonsterCombatBuffRuntime>()).ApplyHealthBuff(24);
                    guests.RemoveAt(i);
                }
            }
            for(int i=0;i<objects.Count;i++){var r=objects[i];r.enabled=honeyServings[i]>0;r.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(Age*2+i)*1.5f);}
            for(int i=0;i<honeyDrops.Count;i++){var r=honeyDrops[i];int piece=i/4;float t=Mathf.Repeat(Age*.85f+i*.27f,1);r.enabled=honeyServings[piece]>0;r.transform.position=honeyPositions[piece]+new Vector2(Mathf.Sin(i*4)*LemonLength*.18f,.24f*(1-t));r.transform.localScale=new Vector3(.055f,.07f+t*.045f,1);r.color=new Color(1,.67f,.17f,(1-t)*.8f);}
        }
        void FixedUpdate()
        {
            if(!Active||Kind!=OralTasteKind.Sweet||FloorUnavailable)return;
            foreach(var g in guests)
            {
                if(!Eligible(g.Monster)||g.Lease==null||g.Lease.Generation!=g.Generation||honeyServings[g.Piece]<=0||g.Monster.IceMoveMultiplier<=.02f)continue;
                var body=g.Monster.GetComponent<Rigidbody2D>();if(body==null||!body.simulated)continue;
                var delta=honeyPositions[g.Piece]-g.Monster.Position;
                body.velocity=delta.magnitude>.45f?delta.normalized*Mathf.Min(1.1f,g.Monster.moveSpeed):Vector2.zero;
            }
        }
        void TickSalt(float dt)
        {
            if(saltBlueprint==null||saltPool<0||SaltSpawnedCount>=18)return;
            saltClock-=dt;if(saltClock>0)return;saltClock=.3f;
            if(Level.EntityManager.LivingMonsters.Count>=112)return;
            var player=Level.PlayerCharacter;int i=SaltSpawnedCount;float angle=i*2.399963f;
            var p=OralMapGeometry.Clamp(map,player.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(4.2f+i%3*.55f),.7f,OralZone.Tongue);
            if(Vector2.Distance(p,player.Position)<2.3f)return;
            var m=Level.EntityManager.SpawnMonster(saltPool,p,saltBlueprint);if(m!=null)SaltSpawnedCount++;
        }
        void TickLemon(float dt)
        {
            bouncePlayerCooldown=Mathf.Max(0,bouncePlayerCooldown-dt);var p=Level.PlayerCharacter;
            if(bouncePlayerCooldown<=0&&!p.IsDashing&&!OralStageRuntime.Instance.IsAirborne && Vector2.Distance(p.Position,LemonPosition)<LemonLength*.42f)
            {if(StartBounce(p.gameObject,p.Position)){bouncePlayerCooldown=1.5f;}}
            Snapshot();stale.Clear();foreach(var pair in bounceCooldown){if(pair.Key==null||!pair.Key.gameObject.activeInHierarchy)stale.Add(pair.Key);}foreach(var key in stale)bounceCooldown.Remove(key);
            foreach(var m in scratch)
            {
                if(!Eligible(m)||Vector2.Distance(m.Position,LemonPosition)>LemonLength*.42f)continue;
                if(bounceCooldown.TryGetValue(m,out float until)&&Age<until)continue;
                if(StartBounce(m.gameObject,m.Position))bounceCooldown[m]=Age+1.5f;
            }
            if(objects.Count>0){float pulse=Mathf.Max(0,1-bouncePlayerCooldown/.25f);objects[0].color=new Color(1,1,.86f,1);}
        }
        bool StartBounce(GameObject actor,Vector2 from)
        {
            var bounce=actor.GetComponent<OralTasteBounce>()??actor.AddComponent<OralTasteBounce>();if(bounce.Active)return false;
            Vector2 landing=OralMapGeometry.Clamp(map,from+LemonDirection*Mathf.Max(2,LemonLength*1.7f),.55f,OralZone.Tongue);
            bounce.Begin(this,landing);return true;
        }
        public bool TryBounceBoulder(Vector2 position,out Vector2 direction)
        {direction=LemonDirection;return Active&&Kind==OralTasteKind.Sour&&!FloorUnavailable&&Vector2.Distance(position,LemonPosition)<LemonLength*.5f;}
        public bool TryEatBitter()
        {
            if(!Active||Kind!=OralTasteKind.Bitter||bitterEaten||FloorUnavailable||OralStageRuntime.Instance.IsAirborne||Vector2.Distance(Level.PlayerCharacter.Position,BitterPosition)>1.3f)return false;
            bitterEaten=true;BitterRemaining=5;bitterVisual.gameObject.SetActive(false);return true;
        }
        void TickSpice(float dt)
        {
            bool hot=SpicyActive;float strength=hot?.6f:.1f+.08f*Mathf.Sin(Age*7)*Mathf.Sin(Age*7);
            foreach(var r in spiceRenderers)if(r!=null){var c=r.sharedMaterial.color;c.a=strength;r.sharedMaterial.color=c;}
            var p=Level.PlayerCharacter;
            if(hot&&ContainsSpice(p.Position)&&!p.IsDashing&&!OralStageRuntime.Instance.IsAirborne)
            {if(BurnRemaining<=0)burnTick=0;BurnRemaining=3;}
        }
        void TickStatus(float dt)
        {
            var p=Level.PlayerCharacter;
            if(BurnRemaining>0)
            {BurnRemaining=Mathf.Max(0,BurnRemaining-dt);burnTick-=dt;if(burnTick<=0){burnTick+=1;BurnTicks++;p.TakeDamage(3);}}
            if(BitterRemaining>0)
            {
                if(slow==null)slow=p.GetComponent<SnailSlowStatus>()??p.gameObject.AddComponent<SnailSlowStatus>();slow.Enter(GetInstanceID(),.2f);
                BitterRemaining=Mathf.Max(0,BitterRemaining-dt);
                if(BitterRemaining<=0){slow.Exit(GetInstanceID(),0);p.GainHealth(Mathf.Max(8,p.MaxHealth*.2f));BitterRewards++;bitterGlow=1.4f;}
            }
            bitterGlow=Mathf.Max(0,bitterGlow-dt);
            bool visible=BurnRemaining>0||BitterRemaining>0||bitterGlow>0;statusRoot.gameObject.SetActive(visible);if(!visible)return;
            for(int i=0;i<embers.Count;i++)
            {
                float t=Mathf.Repeat(statusAge*1.4f+i/(float)embers.Count,1);var r=embers[i];r.transform.position=p.Position+new Vector2(Mathf.Sin(i*8.1f)*.32f,.05f+t*.6f);
                r.transform.localScale=Vector3.one*(.04f+(1-t)*.045f);r.color=BurnRemaining>0?new Color(1,.42f,.08f,1-t):BitterRemaining>0?new Color(.65f,.3f,.8f,1-t):new Color(.4f,1,.5f,1-t);
            }
        }
        void ClearEncounter()
        {
            guests.Clear();honeyPositions.Clear();objects.Clear();honeyDrops.Clear();spiceRenderers.Clear();bounceCooldown.Clear();bitterVisual=null;
            if(artRoot!=null){artRoot.gameObject.SetActive(false);Destroy(artRoot.gameObject);artRoot=null;}
            foreach(var item in encounterOwned)if(item!=null)Destroy(item);encounterOwned.Clear();
        }
        public void CancelAll()
        {
            Active=false;ClearEncounter();BurnRemaining=BitterRemaining=0;burnTick=0;
            if(slow!=null)slow.Exit(GetInstanceID(),0);
            if(statusRoot!=null){Destroy(statusRoot.gameObject);statusRoot=null;}embers.Clear();
            CancelBounces();
            // Active death effects can already be absent from LivingMonsters. Reclaim
            // every active actor using this owned blueprint before freeing its art.
            if(saltBlueprint!=null)
                foreach(var monster in FindObjectsOfType<Monster>())
                    if(monster!=null&&monster.Blueprint==saltBlueprint)monster.DespawnForStageTransfer();
            foreach(var obj in owned)if(obj!=null)Destroy(obj);owned.Clear();saltBlueprint=null;saltPool=-1;configured=false;
        }
        public void CancelBounces(){foreach(var bounce in FindObjectsOfType<OralTasteBounce>())if(bounce.Owner==this)bounce.Restore();}
        void OnDisable(){CancelAll();}
        void OnDestroy(){CancelAll();if(Instance==this)Instance=null;}
    }

    public sealed class OralTasteLease : MonoBehaviour
    {
        public int Generation {get;private set;}
        void OnEnable(){Generation++;}
    }
    public sealed class OralBitterInteraction : MonoBehaviour
    {
        OralTasteEvents owner;
        public void Configure(OralTasteEvents value){owner=value;InteractionFocus.Register(this,()=>Available,()=>this);}
        public bool Available=>owner!=null&&owner.Active&&owner.Kind==OralTasteKind.Bitter&&!owner.FloorUnavailable&&
            !(OralStageRuntime.Instance?.IsAirborne??false)&&Vector2.Distance(owner.Level.PlayerCharacter.Position,owner.BitterPosition)<=1.3f;
        void Update(){bool available=Available;PixelInteractionPrompt.Show(this,available);if(available&&GameInput.TryConsumeInteraction(KeyCode.E,this))owner.TryEatBitter();}
        void OnDisable(){PixelInteractionPrompt.Show(this,false);}
    }

    /// <summary>Short bounce with a stable ground footprint and an unscaled original body.</summary>
    [DefaultExecutionOrder(20000)]
    public sealed class OralTasteBounce : MonoBehaviour
    {
        public OralTasteEvents Owner {get;private set;}
        public bool Active {get;private set;}
        public Vector3 VisualWorldOffset => Active && body!=null ? body.transform.position-(body.transform.parent!=null?body.transform.parent.TransformPoint(origin):origin) : Vector3.zero;
        Vector2 from,to;float age,height;SpriteRenderer body;Vector3 origin;Rigidbody2D rb;Monster monster;Character player;bool priorSuspended,priorSimulated,pendingPhysicsRestore;
        bool StageTravelling => Owner!=null&&Owner.Level!=null&&(Owner.Level.GetComponent<StageProgression>()?.Travelling??false);
        bool PlayerTransitionOwnsPhysics => StageEntryLoading.IsLoading||StageTravelling||(player!=null&&player.IsPortalTravelling);
        public void Begin(OralTasteEvents owner,Vector2 landing)
        {
            Restore();Owner=owner;monster=GetComponent<Monster>();player=GetComponent<Character>();from=transform.position;to=landing;age=0;Active=true;
            body=monster!=null?monster.BodyRenderer:GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();if(body!=null)origin=body.transform.localPosition;rb=GetComponent<Rigidbody2D>();
            height=Mathf.Max(.35f,body!=null?body.bounds.size.y*1.5f:.6f);
            if(monster!=null){priorSuspended=monster.IsFieldRuntimeSuspended;monster.SetFieldRuntimeSuspended(true);}
            else if(rb!=null){priorSimulated=rb.simulated;rb.velocity=Vector2.zero;rb.simulated=false;}
        }
        void LateUpdate()
        {
            // A portal can snapshot the bounce's disabled body. Defer restoration
            // until travel ends, including when the arrival is inside a mini stage.
            if(pendingPhysicsRestore&&!PlayerTransitionOwnsPhysics)
            {if(rb!=null)rb.simulated=priorSimulated;pendingPhysicsRestore=false;}
            if(!Active)return;
            if(Owner==null||!gameObject.activeInHierarchy||monster!=null&&!monster.IsAlive||player!=null&&(!player.IsAlive||player.IsPortalTravelling)){Restore();return;}
            if(Owner.FloorUnavailable)
            {if(MiniStageRuntimeState.IsInsideMiniStage||StageEntryLoading.IsLoading||OralFieldEvents.Instance?.CurrentEvent==OralEventKind.Ceiling)Restore();return;}
            age+=Time.deltaTime;float t=Mathf.Clamp01(age/.6f);var p=Vector2.Lerp(from,to,Mathf.SmoothStep(0,1,t));transform.position=new Vector3(p.x,p.y,transform.position.z);
            if(rb!=null){rb.position=p;rb.velocity=Vector2.zero;}
            if(body!=null){var world=Vector3.up*Mathf.Sin(t*Mathf.PI)*height;body.transform.localPosition=origin+(body.transform.parent!=null?body.transform.parent.InverseTransformVector(world):world);}
            if(monster!=null)Owner.Level.EntityManager.Grid?.UpdateClient(monster);
            if(t>=1)Restore();
        }
        public void Restore()
        {
            if(!Active)return;Active=false;if(body!=null)body.transform.localPosition=origin;body=null;
            if(monster!=null&&!priorSuspended)
            {
                if(!MiniStageRuntimeState.IsInsideMiniStage&&!StageEntryLoading.IsLoading&&!StageTravelling)
                    monster.SetFieldRuntimeSuspended(false);
            }
            else if(player!=null&&rb!=null)
            {
                if(PlayerTransitionOwnsPhysics)pendingPhysicsRestore=true;
                else{rb.simulated=priorSimulated;pendingPhysicsRestore=false;}
            }
        }
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
    }
}
