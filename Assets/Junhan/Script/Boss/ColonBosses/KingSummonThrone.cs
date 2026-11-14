using UnityEngine;

namespace Vampire
{
    // Scene-owned, reachable manual summon point. It never spawns a second throne during combat.
    public sealed class KingSummonThrone : InteractableEventObject
    {
        public float BowlWorldY => transform.position.y + 1.97f;
        public SpriteRenderer Toilet { get; private set; }
        ConstipationKingDirector director;
        Transform visuals;
        SpriteRenderer front, liquid, pool, shadow;
        readonly SpriteRenderer[] bubbles = new SpriteRenderer[10];
        readonly SpriteRenderer[] glints = new SpriteRenderer[10];
        readonly SpriteRenderer[] drops = new SpriteRenderer[12];
        readonly LineRenderer[] streams = new LineRenderer[3];
        Material lineMaterial, lipMaterial;
        Vector3 toiletStartScale;
        float clock, progress;
        bool summoning, consumed, fieldVisible = true;
        public static KingSummonThrone Create(ConstipationKingDirector owner, Vector2 at)
        {
            var go = new GameObject("왕의 빈 변기 · 상호작용 소환");
            go.transform.position = at;
            go.AddComponent<CircleCollider2D>().radius = 2.1f;
            var throne = go.AddComponent<KingSummonThrone>(); throne.director = owner; throne.Build();
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Boss, "왕의 빈 변기 · 변비왕 소환");
            return throne;
        }
        protected override void Awake() { base.Awake(); ConfigureReusableInteraction(); }
        protected override bool KeepVisibleAfterInteraction => true;
        protected override bool InteractionAvailable => !consumed && director != null && director.CanSummon;
        protected override Component PromptOwner => Toilet != null ? (Component)Toilet : this;
        protected override bool ExecuteInteraction(Character player) => director != null && director.TrySummon(player);
        void Build()
        {
            visuals = new GameObject("Porcelain throne and sewage").transform; visuals.SetParent(transform, false);
            var art = Resources.Load<Sprite>("ColonEnding/EmptyThrone");
            Toilet = Sprite("Empty porcelain throne", art, 508);
            if (art == null) { Debug.LogError("Missing approved EmptyThrone sprite", this); return; }
            Toilet.transform.localScale = Vector3.one * (3.7f / art.bounds.size.y);
            toiletStartScale = Toilet.transform.localScale;
            Toilet.transform.localPosition = new Vector3(-.18f, .16f - art.bounds.min.y * Toilet.transform.localScale.y, 0);
            front = Sprite("Foreground porcelain bowl lip", art, 515);
            front.transform.localPosition = Toilet.transform.localPosition; front.transform.localScale = Toilet.transform.localScale;
            var reveal = Resources.Load<Shader>("ColonEnding/SummonReveal");
            lipMaterial = new Material(reveal); lipMaterial.SetFloat("_KeepAbove", 0); lipMaterial.SetFloat("_ClipY", BowlWorldY + .02f);
            front.sharedMaterial = lipMaterial;
            shadow = Sprite("Throne ground shadow", BombCircleArt.Sprite, 475);
            liquid = Sprite("Rising brown bowl surface", BombCircleArt.Sprite, 509);
            pool = Sprite("Overflow ground pool", ConstipationKingPropArt.Get("OverflowPool"), 477);
            pool.sharedMaterial = ConstipationKingPropArt.FloorMaterial;
            for (int i = 0; i < bubbles.Length; i++) { bubbles[i] = Sprite("Sewage bubble", BombCircleArt.Sprite, 511); glints[i] = Sprite("Bubble wet highlight", BombCircleArt.Sprite, 512); }
            for (int i = 0; i < drops.Length; i++) drops[i] = Sprite("Overflow airborne droplet", BombCircleArt.Sprite, 516);
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < streams.Length; i++)
            {
                var line = new GameObject("Liquid flowing over the rim").AddComponent<LineRenderer>(); line.transform.SetParent(visuals, false);
                line.sharedMaterial = lineMaterial; line.useWorldSpace = false; line.positionCount = 20;
                line.numCapVertices = 5; line.numCornerVertices = 3; line.sortingOrder = 516;
                line.widthCurve = new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.4f,1),new Keyframe(1,.35f));
                streams[i] = line;
            }
            Sample(0, 0);
        }
        SpriteRenderer Sprite(string label, Sprite sprite, int order)
        {
            var r = new GameObject(label).AddComponent<SpriteRenderer>(); r.transform.SetParent(visuals, false);
            r.sprite = sprite; r.sortingOrder = order; return r;
        }
        public void BeginSummoning() { summoning = true; clock = 0; GetComponent<Collider2D>().enabled = false; }
        public void RestoreInvitation()
        {
            consumed = summoning = false; clock = 0;
            gameObject.SetActive(true); ResetInteractionAvailability(); SetFieldVisible(true); Sample(0, 0);
        }
        public void CompleteSummoning() { consumed = true; summoning = false; gameObject.SetActive(false); }
        public void SetFieldVisible(bool visible) { fieldVisible = visible; if (visuals != null) visuals.gameObject.SetActive(visible); }
        protected override void Update()
        {
            base.Update();
            if (consumed || summoning || director == null) return;
            SetFieldVisible(!MiniStageRuntimeState.IsInsideMiniStage && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.FieldVisible);
            if (fieldVisible && Time.timeScale > 0 && director.CanSummon) Sample(0, Time.deltaTime * .35f);
        }
        public void Sample(float q, float dt)
        {
            if (Toilet == null || Toilet.sprite == null) return;
            progress = Mathf.Clamp01(q); clock += dt;
            float strength = summoning ? Mathf.SmoothStep(0,1,progress/.18f) : .12f;
            float settling = Mathf.InverseLerp(.79f,1,progress);
            float align = Mathf.SmoothStep(0,1,Mathf.Clamp01(settling/.65f));
            float finish = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,settling));
            float alpha = 1-finish;
            // The heavy porcelain lifts and contracts into the combat pedestal first.
            // Reveal its matching combat art only after the outlines mostly agree.
            Toilet.transform.localScale = toiletStartScale * Mathf.Lerp(1,.9f,align);
            Toilet.transform.localPosition = new Vector3(-.18f+.20f*align,
                .16f+.55f*align-Toilet.sprite.bounds.min.y*Toilet.transform.localScale.y,0);
            front.transform.localPosition=Toilet.transform.localPosition;front.transform.localScale=Toilet.transform.localScale;
            Toilet.color = front.color = new Color(1,1,1,alpha);
            float shake = summoning ? Mathf.Sin(clock*29)*.035f*(1-Mathf.SmoothStep(0,1,progress/.7f)) : 0;
            visuals.localPosition = Vector3.right * shake;
            Ellipse(shadow,new Vector2(0,.05f),1.7f,.3f,new Color(.22f,.10f,.09f,.22f*alpha));
            float liquidY=1.96f+.37f*align;
            Ellipse(liquid,new Vector2(.10f+.20f*align,liquidY),1.15f*(1-.1f*align),.28f+strength*.07f,new Color(.42f,.20f,.075f,alpha*(1-align)));
            float spill = summoning ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.48f,progress)) : 0;
            Ellipse(pool,new Vector2(.05f,.04f),1.4f+spill*.7f,.40f+spill*.18f,new Color(1,1,1,spill*.8f*alpha));
            for(int i=0;i<bubbles.Length;i++)
            {
                float phase=Mathf.Repeat(clock*(.95f+i%3*.13f)+i*.127f,1);
                float x=Mathf.Sin(i*2.399963f)*.98f;
                float y=liquidY+Mathf.Cos(i*1.73f)*.10f+Mathf.Sin(phase*Mathf.PI)*(.05f+strength*.26f);
                float r=(.055f+i%3*.024f)*Mathf.Sin(phase*Mathf.PI)*(.6f+strength);
                float visible=alpha*(1-align)*(.5f+.5f*strength);
                Ellipse(bubbles[i],new Vector2(x,y),r,r*.72f,new Color(.62f,.34f,.12f,visible));
                Ellipse(glints[i],new Vector2(x-r*.25f,y+r*.28f),r*.30f,r*.13f,new Color(.96f,.66f,.29f,visible*.8f));
            }
            for(int i=0;i<streams.Length;i++)
            {
                var line=streams[i]; line.enabled=spill>.001f && alpha>.001f;
                line.startColor=new Color(.58f,.29f,.10f,alpha); line.endColor=new Color(.37f,.17f,.07f,alpha*.7f);
                line.widthMultiplier=(.14f+i*.025f)*spill*(1-align);
                Vector2 from=new Vector2((i-1)*.84f,1.80f),to=new Vector2((i-1)*1.16f,.10f);
                for(int j=0;j<line.positionCount;j++)
                {
                    float t=j/(float)(line.positionCount-1);
                    var point=Vector2.Lerp(from,to,t);point.x+=Mathf.Sin(t*8-clock*7+i)*.035f*t;
                    line.SetPosition(j,point);
                }
            }
            for(int i=0;i<drops.Length;i++)
            {
                float t=Mathf.Repeat(clock*1.25f+i*.081f,1),side=Mathf.Sin(i*2.399963f);
                float surge=summoning?strength*alpha*(1-align):0;
                Vector2 from=new Vector2(side*.85f,1.96f);
                Vector2 pos=from+new Vector2(side*t*.65f,4*(.35f+i%3*.12f)*t*(1-t)-t*.70f);
                float fade=Mathf.Sin(t*Mathf.PI)*surge;
                Ellipse(drops[i],pos,.045f+i%3*.012f,.075f+i%2*.016f,new Color(.73f,.43f,.16f,fade));
            }
        }
        static void Ellipse(SpriteRenderer r,Vector2 at,float rx,float ry,Color c)
        {
            if(r==null || r.sprite==null)return;
            r.enabled=c.a>.001f;r.color=c;r.transform.localPosition=at;
            r.transform.localScale=new Vector3(rx*2/r.sprite.bounds.size.x,ry*2/r.sprite.bounds.size.y,1);
        }
        void OnDestroy() { if(lineMaterial!=null)Destroy(lineMaterial);if(lipMaterial!=null)Destroy(lipMaterial); }
    }
}
