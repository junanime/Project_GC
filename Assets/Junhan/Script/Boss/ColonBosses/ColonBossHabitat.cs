using UnityEngine;

namespace Vampire
{
    public sealed class ColonBossHabitat : InteractableEventObject
    {
        public ColonBossKind Kind { get; private set; }
        public Vector2 WallNormal { get; private set; }
        public Vector2[] Doors { get; private set; }
        public bool Defeated { get; private set; }
        public bool Summoned { get; private set; }
        bool discoveryAnnounced;
        public Color MapTint => Defeated?new Color(.55f,.65f,.58f,.7f):Summoned?new Color(1,.62f,.45f):Color.white;
        public string MapBadge => Defeated?"✓":Summoned?"!":"";
        void LateUpdate(){var marker=GetComponent<MapMarker>();if(!discoveryAnnounced&&!Summoned&&marker!=null&&marker.IsDiscovered){discoveryAnnounced=true;ColonBossDirector.ShowNotice("미니보스 거주지 발견",ColonBossDefinition.Get(Kind).Name+" · 가까이서 상호작용하면 전투 시작");}}

        ColonBossDirector director;
        SpriteRenderer dormant, house, houseLip;
        Transform houseRig;
        SpriteMask mask;
        Sprite[] pipeMasks;
        Vector3[] pipeMaskOffsets;
        SpriteRenderer clippedBody;
        int clippedBodyLayer;
        SpriteRenderer[] lips, doors;
        protected override bool KeepVisibleAfterInteraction => true;
        protected override bool InteractionAvailable => director != null && director.CanSummon(this);
        public static ColonBossHabitat Create(ColonBossDirector owner, ColonBossKind kind, Vector2 position, Vector2 wallNormal)
        {
            var go = new GameObject(ColonBossDefinition.Get(kind).Name + " · 잠든 거주지"); go.transform.position = position;
            var c = go.AddComponent<CircleCollider2D>(); c.isTrigger = true; c.radius = kind == ColonBossKind.Manatee ? 6.8f : 2.5f;
            var h = go.AddComponent<ColonBossHabitat>(); h.director = owner; h.Kind = kind; h.WallNormal = wallNormal;
            h.Build(); var marker=go.AddComponent<MapMarker>();
            marker.Configure(MapMarkerKind.Event, ColonBossDefinition.Get(kind).Name + "의 거주지");
            marker.RequireNearbyDiscovery=true;marker.ResetDiscovery();return h;
        }
        void Build()
        {
            var d = ColonBossDefinition.Get(Kind);
            dormant = Sprite("잠든 보스", ColonBossVisual.Load(d.Id,"Dormant"), transform, d.Size, 505);
            if (Kind != ColonBossKind.Manatee) { Doors = new[] { (Vector2)transform.position }; return; }
            dormant.enabled = false;
            Vector2 normal = WallNormal.sqrMagnitude > .1f ? WallNormal.normalized : Vector2.down;
            Doors = new Vector2[3]; doors = new SpriteRenderer[3];
            houseRig=new GameObject("House aligned to central wall").transform;houseRig.SetParent(transform,false);
            houseRig.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(normal.y,normal.x)*Mathf.Rad2Deg-90);
            house = Sprite("중앙벽 파이프 집",ColonBossVisual.Load(d.Id,"HouseBase"),houseRig,13f,490);
            houseLip = Sprite("입구 앞 테두리",ColonBossVisual.Load(d.Id,"HouseLip"),houseRig,13f,516);
            // This broad wall prop must stay behind the player approaching any opening.
            GroundVisualSorting.Apply(house,490);GroundVisualSorting.Apply(houseLip,516);
            var maskArt=ColonBossVisual.Load(d.Id,"HouseMask");
            if(maskArt.Length>0){mask=new GameObject("Pipe-only emergence mask").AddComponent<SpriteMask>();mask.transform.SetParent(houseRig,false);mask.sprite=maskArt[0];mask.transform.localScale=house.transform.localScale;mask.isCustomRangeActive=true;mask.frontSortingLayerID=mask.backSortingLayerID=house.sortingLayerID;mask.frontSortingOrder=515;mask.backSortingOrder=508;}
            for (int i=0;i<3;i++)
            {
                Doors[i] = ColonBossDirector.ManateeDoorPosition(transform.position,normal,i);
                var root = new GameObject("Pipe opening " + i).transform; root.SetParent(transform,true); root.position = Doors[i];
                root.rotation=houseRig.rotation;
                doors[i] = Sprite("닫히는 문",ColonBossVisual.Load(d.Id,"HouseDoor"),root,1.50f,515);
                GroundVisualSorting.Apply(doors[i],515);
                Door(i,1);
            }
            if(mask!=null)BuildPipeMasks(maskArt[0]);
        }
        void BuildPipeMasks(Sprite source)
        {
            // Each port keeps the original mask pixels and world alignment, but only
            // the active opening may reveal the body hidden behind the broad house.
            pipeMasks=new Sprite[Doors.Length];pipeMaskOffsets=new Vector3[Doors.Length];
            var centers=new float[Doors.Length];
            for(int i=0;i<Doors.Length;i++)
            {
                Vector3 local=houseRig.InverseTransformPoint(Doors[i]);
                centers[i]=local.x/house.transform.localScale.x*source.pixelsPerUnit+source.pivot.x;
            }
            for(int i=0;i<Doors.Length;i++)
            {
                float left=i==0?0:Mathf.Round((centers[i-1]+centers[i])*.5f);
                float right=i==Doors.Length-1?source.rect.width:Mathf.Round((centers[i]+centers[i+1])*.5f);
                var rect=new Rect(source.rect.x+left,source.rect.y,right-left,source.rect.height);
                pipeMasks[i]=UnityEngine.Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
                pipeMasks[i].name=source.name+" · port "+i;
                var offset=new Vector3((left+(right-left)*.5f-source.pivot.x)/source.pixelsPerUnit,
                    (source.rect.height*.5f-source.pivot.y)/source.pixelsPerUnit,0);
                pipeMaskOffsets[i]=Vector3.Scale(offset,house.transform.localScale);
            }
            SetActivePipe(0);
        }
        public void SetActivePipe(int index)
        {
            if(mask==null||pipeMasks==null||pipeMasks.Length==0)return;
            index=Mathf.Clamp(index,0,pipeMasks.Length-1);
            mask.sprite=pipeMasks[index];mask.transform.localPosition=pipeMaskOffsets[index];
        }
        void OnDestroy()
        {
            RestorePipeLayer();
            if(pipeMasks!=null)foreach(var sprite in pipeMasks)if(sprite!=null)Destroy(sprite);
        }
        static SpriteRenderer Sprite(string name,Sprite[] source,Transform parent,float width,int order)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(parent,false);r.sortingOrder=order;
            if(source.Length>0){r.sprite=source[0];r.transform.localScale=Vector3.one*width/Mathf.Max(.01f,r.sprite.bounds.size.x);}return r;
        }
        protected override bool ExecuteInteraction(Character player) => director.TrySummon(this,player);
        public bool Activate(Character player) { if(HasInteracted||!ExecuteInteraction(player))return false;CompleteInteraction();return true; }
        public void BeginEmergence() { Summoned=true;if(dormant!=null)dormant.enabled=false;var marker=GetComponent<MapMarker>();if(marker!=null){marker.enabled=true;marker.Configure(MapMarkerKind.Event,ColonBossDefinition.Get(Kind).Name+" · 전투 중");marker.SetDiscovered(true);} }
        public void OnArrivalComplete() { BeginEmergence(); }
        public void ClipToPipe(ColonBossVisual visual,bool inside)
        {
            if(visual==null||visual.Main==null)return;
            var body=visual.Main;
            if(inside&&mask!=null)
            {
                if(clippedBody!=body)
                {
                    RestorePipeLayer();clippedBody=body;clippedBodyLayer=body.sortingLayerID;
                }
                body.sortingLayerID=house.sortingLayerID;
                body.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;
            }
            else
            {
                if(clippedBody==body)RestorePipeLayer();
                body.maskInteraction=SpriteMaskInteraction.None;
            }
        }
        void RestorePipeLayer()
        {
            if(clippedBody!=null){clippedBody.sortingLayerID=clippedBodyLayer;clippedBody.maskInteraction=SpriteMaskInteraction.None;}
            clippedBody=null;
        }
        public void OnDefeated() { Defeated=true;var marker=GetComponent<MapMarker>();if(marker!=null)marker.Configure(MapMarkerKind.Event,ColonBossDefinition.Get(Kind).Name+" · 처치 완료");director.NotifyDefeated(this);for(int i=0;i<3;i++)Door(i,0); }
        public void Door(int index,float open)
        {
            if(doors==null||index<0||index>=doors.Length||doors[index]==null)return;
            doors[index].enabled=open<.99f;
            doors[index].transform.localPosition=Vector3.up*open*1.2f;
            var color=doors[index].color;color.a=1-open*.7f;doors[index].color=color;
        }
        public Vector2 ClampTerritory(Vector2 target)
        {
            Vector2 center=(Vector2)transform.position+WallNormal.normalized*3;
            return center+Vector2.ClampMagnitude(target-center,8);
        }
        public Vector2 DoorExit(int index) => Doors[Mathf.Clamp(index,0,Doors.Length-1)] + (WallNormal.sqrMagnitude>.1f?WallNormal.normalized:Vector2.down)*1.5f;
    }
}
