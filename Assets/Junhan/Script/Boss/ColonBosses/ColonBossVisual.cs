using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class ColonBossVisual : MonoBehaviour
    {
        [Serializable] class NutPose
        {
            public int pose;
            public bool visible;
            public Vector2[] positions;
            public float[] heights;
        }
        [Serializable] class ArtInfo
        {
            public float referencePixelWidth = 240, referencePixelHeight = 180, pixelsPerUnit = 125, walkCycle = 1.6f;
            public Vector2 pipeFaceUV, mouthUV;
            public NutPose[] nutPoses;
        }
        public SpriteRenderer Main { get; private set; }
        // Body tint must also cover authored spines drawn in front of carried chestnuts.
        public int ForegroundSortingOrder => Main == null ? 0 : nutFront == null
            ? Main.sortingOrder : Mathf.Max(Main.sortingOrder, nutFront.sortingOrder);
        public Transform Center => Main.transform;
        public int Facing { get; private set; } = -1;
        public bool Visible = true;
        public float Lift, Roll, Squash, Stretch, Opacity = 1;
        public float BalletLanding, BellyPulse, FlipperSweep;
        public int FlipperSide;
        public bool ExternalChestnuts;
        public bool IsPoseDisplayed(int pose) => displayedPose==pose && poseBlend>=.39f;
        int requestedPose = -1, displayedPose = -1;
        float poseBlend = 1;
        public int Pose
        {
            get => requestedPose;
            set { if(requestedPose==value)return; requestedPose=value; poseBlend=0; }
        }
        public float WalkRate = 1;
        public float ReferenceHeight { get; private set; }
        ColonBossRuntime boss;
        Sprite[] walk, bareWalk, poses, summon, nutFrontFrames;
        public bool Bare;
        // Projection is retained as the pattern's activation blend; it no longer flattens
        // or rolls the shell. GroundSpin is exclusively the horizontal surface angle.
        public float GroundSpin,GroundSpinProjection;
        public int GroundSpinFrame { get; private set; }=-1;
        public const float GroundPlaneY=.58f;
        public Transform GroundSpinPlane => spinPlane;
        public Vector2 GroundSpinCenter
        {
            get
            {
                if(spinPlane==null||GroundSpinProjection<=0)return (Vector2)transform.position+Vector2.up*(ReferenceHeight*.47f+Lift);
                var center=spinMeshCenter;if(Main.flipX)center.x=-center.x;
                return Main.transform.TransformPoint(center);
            }
        }
        Transform spinPlane;
        Sprite spinMeshSource;
        Vector2[] spinMeshVertices;
        public IReadOnlyList<Vector2> GroundSpinFootprint => spinMeshVertices;
        Vector2 spinMeshCenter;
        Material articulationMaterial;
        MaterialPropertyBlock articulationBlock;
        // Opaque hull of the approved 220x160 Pose_003, in source pixels (bottom-left origin).
        // This is a contact guide only: no replacement image, mesh, or second body is rendered.
        // Sheet rectangles contain transparent corners that would make the shell hover.
        static readonly Vector2[] TurtleShellPixels={
            new Vector2(21.5f,74.5f),new Vector2(22.5f,78.5f),new Vector2(24.5f,82.5f),new Vector2(26.5f,85.5f),
            new Vector2(39.5f,102.5f),new Vector2(49.5f,112.5f),new Vector2(56.5f,117.5f),new Vector2(92.5f,138.5f),
            new Vector2(115.5f,138.5f),new Vector2(120.5f,137.5f),new Vector2(127.5f,135.5f),new Vector2(129.5f,134.5f),
            new Vector2(154.5f,118.5f),new Vector2(160.5f,114.5f),new Vector2(170.5f,105.5f),new Vector2(171.5f,104.5f),
            new Vector2(176.5f,97.5f),new Vector2(190.5f,76.5f),new Vector2(191.5f,72.5f),new Vector2(191.5f,56.5f),
            new Vector2(190.5f,52.5f),new Vector2(188.5f,47.5f),new Vector2(187.5f,45.5f),new Vector2(184.5f,41.5f),
            new Vector2(162.5f,19.5f),new Vector2(156.5f,15.5f),new Vector2(144.5f,9.5f),new Vector2(141.5f,8.5f),
            new Vector2(131.5f,6.5f),new Vector2(125.5f,5.5f),new Vector2(86.5f,5.5f),new Vector2(80.5f,6.5f),
            new Vector2(71.5f,8.5f),new Vector2(61.5f,12.5f),new Vector2(54.5f,16.5f),new Vector2(51.5f,18.5f),
            new Vector2(41.5f,28.5f),new Vector2(32.5f,38.5f),new Vector2(27.5f,44.5f),new Vector2(24.5f,50.5f),
            new Vector2(22.5f,55.5f),new Vector2(21.5f,60.5f)
        };
        static readonly Dictionary<string,Sprite[]> cache=new Dictionary<string,Sprite[]>();
        SpriteRenderer shadow;
        SpriteRenderer[] carriedNuts;
        SpriteRenderer nutFront;
        static readonly Vector2[] NutSockets={new Vector2(.0555f,.7670f),new Vector2(.3633f,.5904f),new Vector2(.5803f,.3633f)};
        static readonly float[] NutHeights={.21194f,.21698f,.1968f};
        ArtInfo art;
        float scale, cycle, clock, flash;
        int summonFrame = -1;
        public static Sprite[] Load(string id, string sheet)
        {
            string path="ColonBosses/"+id+"/"+sheet;
            if(cache.TryGetValue(path,out var stored))return stored;
            var frames = Resources.LoadAll<Sprite>(path);
            Array.Sort(frames, (a,b) => string.CompareOrdinal(a.name,b.name)); cache[path]=frames;return frames;
        }
        public void Initialize(ColonBossRuntime owner)
        {
            boss = owner; var id = owner.Definition.Id;
            walk = Load(id,"Walk"); poses = Load(id,"Pose"); summon = Load(id,"Summon");
            if(owner.Kind==ColonBossKind.Hedgehog){bareWalk=Load(id,"WalkBare");nutFrontFrames=Load(id,"NutForeground");}
            var json = Resources.Load<TextAsset>("ColonBosses/" + id + "/BossArt");
            var info = json != null ? JsonUtility.FromJson<ArtInfo>(json.text) : new ArtInfo();
            art = info;
            scale = owner.Definition.Size / (info.referencePixelWidth / info.pixelsPerUnit); cycle = info.walkCycle;
            ReferenceHeight = info.referencePixelHeight / info.pixelsPerUnit * scale;
            Main = new GameObject("Body · one reference scale").AddComponent<SpriteRenderer>(); Main.transform.SetParent(transform,false); Main.sortingOrder = 510;
            if(owner.Kind==ColonBossKind.Turtle)
            {
                var shader=Resources.Load<Shader>("ColonBosses/ShellYaw");
                if(shader!=null){articulationMaterial=new Material(shader);Main.sharedMaterial=articulationMaterial;articulationBlock=new MaterialPropertyBlock();}
            }
            if(owner.Kind==ColonBossKind.Turtle)
            {
                spinPlane=new GameObject("Upright shell ground contact").transform;spinPlane.SetParent(transform,false);
                Main.transform.SetParent(spinPlane,false);
            }
            shadow = new GameObject("Ground contact shadow").AddComponent<SpriteRenderer>(); shadow.transform.SetParent(transform,false);
            shadow.sprite = BombCircleArt.Sprite; shadow.sortingOrder = 478; shadow.color = new Color(0,0,0,.19f);
            shadow.transform.localScale = new Vector3(owner.Definition.Size * .7f,.28f,1) / Mathf.Max(.01f,shadow.sprite.bounds.size.x);
            if(owner.Kind==ColonBossKind.Hedgehog)
            {
                carriedNuts=new SpriteRenderer[3];var nut=Resources.Load<Sprite>("ColonMonsters/S02");
                for(int i=0;i<3;i++){var r=new GameObject("Carried stage-two chestnut "+i).AddComponent<SpriteRenderer>();r.transform.SetParent(Main.transform,false);r.sprite=nut;r.sortingOrder=511;if(nut!=null)r.transform.localScale=Vector3.one*NutHeights[i]/nut.bounds.size.y;carriedNuts[i]=r;}
                nutFront=new GameObject("Spines in front of carried chestnuts").AddComponent<SpriteRenderer>();
                nutFront.transform.SetParent(Main.transform,false);nutFront.sortingOrder=512;
            }
            Sample();
        }
        public void Face(Vector2 direction) { if (Mathf.Abs(direction.x) > .15f) Facing = direction.x < 0 ? -1 : 1; }
        public void Flash() { flash = .10f; }
        public Vector2 Socket(float u,float v)
        {
            if(Main==null||Main.sprite==null)return transform.position;
            var bounds=Main.sprite.bounds;
            Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,u),Mathf.Lerp(bounds.min.y,bounds.max.y,v),0);
            if(Main.flipX)local.x=-local.x;
            return Main.transform.TransformPoint(local);
        }
        // The pipe reveals the authored head, never a cropped patch of the belly.
        // This is called while hidden, so the pose can be prepared without a visible snap.
        public void PreparePipePeek()
        {
            summonFrame=-1; requestedPose=displayedPose=6; poseBlend=1;
            Lift=Roll=Squash=Stretch=GroundSpin=GroundSpinProjection=0; Sample();
        }
        public Vector2 PipeFace => art != null && art.pipeFaceUV != Vector2.zero
            ? Socket(art.pipeFaceUV.x,art.pipeFaceUV.y) : Socket(.2431f,.5819f);
        public Vector2 Mouth => art != null && art.mouthUV != Vector2.zero ? Socket(art.mouthUV.x,art.mouthUV.y) :
            boss.Kind==ColonBossKind.Flamingo?Socket(.18f,.74f):
            boss.Kind==ColonBossKind.Shark?Socket(.13f,.43f):boss.Kind==ColonBossKind.Pudding?Socket(.30f,.65f):
            boss.Kind==ColonBossKind.Turtle?Socket(.15f,.67f):boss.Kind==ColonBossKind.Manatee?Socket(.18f,.59f):Socket(.15f,.35f);
        public void Summon(float progress) { summonFrame = Mathf.Clamp(Mathf.FloorToInt(progress * summon.Length),0,summon.Length-1); Sample(); }
        public void ResetPose() { Pose = summonFrame = -1; Lift = Roll = GroundSpin = GroundSpinProjection = Squash = Stretch = BalletLanding = BellyPulse = FlipperSweep = 0; WalkRate = 1; Visible = true; Opacity = 1; }
        NutPose Attachment()
        {
            int attachmentPose=Mathf.Max(0,displayedPose);
            if(art?.nutPoses!=null)foreach(var p in art.nutPoses)if(p.pose==attachmentPose)return p;
            return null;
        }
        public bool ChestnutsExposed => displayedPose<0||displayedPose==0||Attachment()==null||Attachment().visible;
        public Vector2 ChestnutSocket(int slot)
        {
            slot=Mathf.Clamp(slot,0,2);var p=Attachment();
            Vector2 local=p?.positions!=null&&slot<p.positions.Length?p.positions[slot]:NutSockets[slot];
            if(Main.flipX)local.x=-local.x;
            return Main.transform.TransformPoint(local);
        }
        public float ChestnutHeight(int slot)
        {
            slot=Mathf.Clamp(slot,0,2);var p=Attachment();
            return (p?.heights!=null&&slot<p.heights.Length?p.heights[slot]:NutHeights[slot])*Main.transform.lossyScale.y;
        }
        public Vector2 FlipperTip(int side)
        {
            // Same pivot/angle as ShellYaw: the projectile leaves the visibly moving fin.
            Vector2 pivot=side==0?new Vector2(.40f,.27f):new Vector2(.66f,.23f);
            Vector2 sourceTip=side==0?new Vector2(.10f,.38f):new Vector2(.82f,.12f);
            float angle=(FlipperSweep-.5f)*.63f*(side==0?1:-1);
            float cosine=Mathf.Cos(angle),sine=Mathf.Sin(angle);
            Vector2 d=sourceTip-pivot;
            Vector2 tip=pivot+new Vector2(d.x*cosine-d.y*sine,d.x*sine+d.y*cosine);
            // Invert the feathered UV warp too: the upper edge of the left fin blends
            // into its shoulder, so a rigid socket alone drifts during anticipation.
            for(int i=0;i<12;i++)
            {
                d=tip-pivot;
                float vertical=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.39f,.55f,tip.y));
                float horizontal=side==0?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.48f,tip.x)):
                    Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,.72f,tip.x));
                Vector2 sampled=Vector2.Lerp(tip,pivot+new Vector2(d.x*cosine+d.y*sine,-d.x*sine+d.y*cosine),vertical*horizontal);
                tip+=sourceTip-sampled;
            }
            return Socket(tip.x,tip.y);
        }
        void OnDestroy(){if(articulationMaterial!=null)Destroy(articulationMaterial);}
        void LateUpdate()
        {
            if (boss == null || boss.EncounterPaused&&!boss.Dead || boss.Immobilized&&!boss.Dead) return;
            clock += Time.deltaTime; flash = Mathf.Max(0,flash-Time.deltaTime);
            // Change the authored pose at the bottom of a short anticipation/recovery arc.
            // No cross-fade: doubled eyes, mouths and limbs must never overlap.
            poseBlend=Mathf.Min(1,poseBlend+Time.deltaTime/.18f);
            if(poseBlend>=.39f)displayedPose=requestedPose;
            Sample();
        }
        void Sample()
        {
            if (Main == null || walk == null || walk.Length == 0) return;
            Sprite sprite;
            if (summonFrame >= 0 && summon.Length > 0) sprite = summon[Mathf.Min(summonFrame,summon.Length-1)];
            else if (displayedPose >= 0 && poses.Length > 0) sprite = poses[Mathf.Min(displayedPose,poses.Length-1)];
            else {var active=Bare&&bareWalk!=null&&bareWalk.Length>0?bareWalk:walk;sprite = active[boss.Moving ? Mathf.FloorToInt(clock * WalkRate / cycle * active.Length) % active.Length : 0];}
            GroundSpinFrame=-1;
            if(boss.Kind==ColonBossKind.Turtle&&displayedPose==3&&GroundSpinProjection>0)
            {
                int turn=Mathf.FloorToInt(Mathf.Repeat(GroundSpin*(Facing>0?-1:1),360)/45f)%ColonTurtleSpinFrames.Count;
                var turned=ColonTurtleSpinFrames.Get(turn);
                if(turned!=null){sprite=turned;GroundSpinFrame=turn;}
            }
            Main.sprite = sprite; Main.enabled = Visible;
            float breathe = !boss.Moving && !boss.Busy && boss.Kind != ColonBossKind.Turtle ? Mathf.Sin(clock*2.8f)*.012f : 0;
            float anticipation=summonFrame<0&&!boss.Dead?Mathf.Sin(poseBlend*Mathf.PI)*.025f:0;
            float belly=Mathf.Sin(Mathf.Clamp01(BellyPulse)*Mathf.PI*2)*Mathf.Exp(-BellyPulse*2)*.095f;
            float landing=Mathf.Sin(Mathf.Clamp01(BalletLanding)*Mathf.PI*3)*Mathf.Exp(-BalletLanding*3)*.065f;
            Main.transform.localScale = new Vector3(scale*(1+Stretch+(Squash+anticipation)*.4f+belly+landing),scale*(1-Squash-anticipation+breathe-belly*.65f-landing*.65f),1);
            // One complete turntable frame replaces the body, including rim, underside
            // and stalk. Normalize its opaque footprint to the original retracted pose.
            // No stationary body/second shell is drawn beneath the rotating artwork.
            if(GroundSpinFrame>=0)
            {
                Vector2 pixels=ColonTurtleSpinFrames.PixelSize(GroundSpinFrame);
                Main.transform.localScale=new Vector3(scale*ColonTurtleSpinFrames.ReferenceWidth/pixels.x,
                    scale*ColonTurtleSpinFrames.ReferenceHeight/pixels.y,1);
            }
            Main.flipX = Facing > 0;
            Vector3 center=Vector3.up*(ReferenceHeight*.47f);
            var rotation=Quaternion.Euler(0,0,Roll);
            Main.transform.localRotation = rotation;
            if(spinPlane!=null&&GroundSpinProjection>0)
            {
                float blend=Mathf.Clamp01(GroundSpinProjection);
                if(GroundSpinFrame>=0)
                {
                    Vector2 pixels=ColonTurtleSpinFrames.PixelSize(GroundSpinFrame)/sprite.pixelsPerUnit;
                    if(spinMeshSource!=sprite)
                    {
                        spinMeshSource=sprite;
                        spinMeshVertices=new[]{new Vector2(-pixels.x*.5f,0),new Vector2(pixels.x*.5f,0),
                            new Vector2(pixels.x*.5f,pixels.y),new Vector2(-pixels.x*.5f,pixels.y)};
                        spinMeshCenter=new Vector2(0,pixels.y*.5f);
                    }
                }
                else if(spinMeshSource!=sprite)
                {
                    spinMeshSource=sprite;spinMeshVertices=new Vector2[TurtleShellPixels.Length];
                    Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
                    for(int i=0;i<spinMeshVertices.Length;i++)
                    {
                        Vector2 pixel=Vector2.Scale(TurtleShellPixels[i],new Vector2(sprite.rect.width/220,sprite.rect.height/160));
                        var v=(pixel-sprite.pivot)/sprite.pixelsPerUnit;spinMeshVertices[i]=v;
                        minimum=Vector2.Min(minimum,v);maximum=Vector2.Max(maximum,v);
                    }
                    spinMeshCenter=(minimum+maximum)*.5f;
                }
                Main.transform.localPosition=Vector3.up*Lift+center-rotation*center;
                spinPlane.localScale=Vector3.one;
                // Keep the approved upright silhouette resting on the floor throughout yaw.
                float lowest=float.PositiveInfinity;
                foreach(var vertex in spinMeshVertices)
                {
                    Vector3 p=Vector3.Scale(vertex,Main.transform.localScale);if(Main.flipX)p.x=-p.x;
                    lowest=Mathf.Min(lowest,(rotation*p).y);
                }
                spinPlane.localPosition=Vector3.up*(.015f-lowest)*blend;
            }
            else
            {
                if(spinPlane!=null){spinPlane.localScale=Vector3.one;spinPlane.localPosition=Vector3.zero;}
                Main.transform.localPosition=Vector3.up*Lift+center-rotation*center;
            }
            Main.color = flash > 0 ? new Color(1,.73f,.73f,Opacity) : new Color(1,1,1,Opacity);
            if(articulationBlock!=null)
            {
                Main.GetPropertyBlock(articulationBlock);
                Rect rect=sprite.textureRect;
                articulationBlock.SetVector("_Frame",new Vector4(rect.x/sprite.texture.width,rect.y/sprite.texture.height,rect.width/sprite.texture.width,rect.height/sprite.texture.height));
                // Mirroring the facing sprite must not reverse the visible surface rotation.
                articulationBlock.SetFloat("_Yaw",GroundSpin*Mathf.Deg2Rad*(Main.flipX?1:-1));
                articulationBlock.SetFloat("_Spin",0); // Rotation is authored over the whole body, never a partial-shell UV layer.
                articulationBlock.SetFloat("_Sweep",displayedPose==4||displayedPose==5?FlipperSweep:0);
                articulationBlock.SetFloat("_SweepActive",displayedPose==4||displayedPose==5?1:0);
                articulationBlock.SetFloat("_Side",FlipperSide);
                Main.SetPropertyBlock(articulationBlock);
            }
            if(carriedNuts!=null)
            {
                NutPose attachment=Attachment();
                for(int i=0;i<carriedNuts.Length;i++)
                {
                    var nut=carriedNuts[i];
                    // Each authored pose owns its sockets; a curled body must not leave nuts floating.
                    nut.enabled=Visible&&!Bare&&!ExternalChestnuts&&displayedPose>0&&summonFrame<0&&(attachment==null||attachment.visible);
                    var p=attachment?.positions!=null&&i<attachment.positions.Length?attachment.positions[i]:NutSockets[i];
                    float height=attachment?.heights!=null&&i<attachment.heights.Length?attachment.heights[i]:NutHeights[i];
                    if(Main.flipX)p.x=-p.x;
                    nut.transform.localPosition=p;
                    if(nut.sprite!=null)nut.transform.localScale=Vector3.one*height/nut.sprite.bounds.size.y;
                    nut.flipX=Main.flipX;nut.color=new Color(1,1,1,Opacity);
                }
                if(nutFront!=null)
                {
                    int frontFrame=Mathf.Max(0,displayedPose);
                    nutFront.enabled=(carriedNuts[0].enabled||ExternalChestnuts&&Visible&&summonFrame<0&&ChestnutsExposed)&&nutFrontFrames!=null&&frontFrame<nutFrontFrames.Length;
                    if(nutFront.enabled)nutFront.sprite=nutFrontFrames[frontFrame];
                    nutFront.flipX=Main.flipX;nutFront.color=Main.color;
                }
            }
            shadow.enabled = Visible && Main.maskInteraction == SpriteMaskInteraction.None; shadow.color = new Color(0,0,0,.19f*Opacity*Mathf.Clamp01(1-Lift*.13f));
        }
    }
}
