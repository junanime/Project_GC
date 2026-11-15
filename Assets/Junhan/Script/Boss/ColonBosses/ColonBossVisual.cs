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
        public float GroundSpin,GroundSpinProjection;
        public const float GroundPlaneY=.58f;
        public Transform GroundSpinPlane => spinPlane;
        public Vector2 GroundSpinCenter => spinPlane!=null&&GroundSpinProjection>0
            ? (Vector2)spinPlane.position : (Vector2)transform.position+Vector2.up*(ReferenceHeight*.47f+Lift);
        Transform spinPlane;
        Sprite spinMeshSource;
        Vector2[] spinMeshVertices;
        public IReadOnlyList<Vector2> GroundSpinFootprint => spinMeshVertices;
        Vector2 spinMeshCenter;
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
                spinPlane=new GameObject("Shell ground-plane projection").transform;spinPlane.SetParent(transform,false);
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
        public void ResetPose() { Pose = summonFrame = -1; Lift = Roll = GroundSpin = GroundSpinProjection = Squash = Stretch = 0; WalkRate = 1; Visible = true; Opacity = 1; }
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
            Main.sprite = sprite; Main.enabled = Visible;
            float breathe = !boss.Moving && !boss.Busy && boss.Kind != ColonBossKind.Turtle ? Mathf.Sin(clock*2.8f)*.012f : 0;
            float anticipation=summonFrame<0&&!boss.Dead?Mathf.Sin(poseBlend*Mathf.PI)*.025f:0;
            Main.transform.localScale = new Vector3(scale*(1+Stretch+(Squash+anticipation)*.4f),scale*(1-Squash-anticipation+breathe),1);
            // Rotate the one original sprite before projecting it into the horizontal ground plane.
            // Scaling the sprite itself before rotation would make the ellipse stand up like a wheel.
            Main.flipX = Facing > 0;
            Vector3 center=Vector3.up*(ReferenceHeight*.47f);
            var rotation=Quaternion.Euler(0,0,Roll+GroundSpin);
            Main.transform.localRotation = rotation;
            if(spinPlane!=null&&GroundSpinProjection>0)
            {
                float blend=Mathf.Clamp01(GroundSpinProjection),projection=Mathf.Lerp(1,GroundPlaneY,blend);
                if(spinMeshSource!=sprite)
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
                Vector3 pivot=Vector3.Scale(spinMeshCenter,Main.transform.localScale);
                if(Main.flipX)pivot.x=-pivot.x;
                Main.transform.localPosition=-(rotation*pivot);
                spinPlane.localScale=new Vector3(1,projection,1);
                // Use the authored opaque outline, not transparent sheet corners, to hold its
                // lowest visible point on the floor as the asymmetric shell rotates.
                float lowest=float.PositiveInfinity;
                foreach(var vertex in spinMeshVertices)
                {
                    Vector3 p=Vector3.Scale(vertex,Main.transform.localScale);if(Main.flipX)p.x=-p.x;
                    lowest=Mathf.Min(lowest,(rotation*(p-pivot)).y);
                }
                Vector3 upright=Vector3.up*Lift+pivot;
                Vector3 grounded=new Vector3(0,Lift+.015f-lowest*projection,0);
                spinPlane.localPosition=Vector3.Lerp(upright,grounded,blend);
            }
            else
            {
                if(spinPlane!=null){spinPlane.localScale=Vector3.one;spinPlane.localPosition=Vector3.zero;}
                Main.transform.localPosition=Vector3.up*Lift+center-rotation*center;
            }
            Main.color = flash > 0 ? new Color(1,.73f,.73f,Opacity) : new Color(1,1,1,Opacity);
            if(carriedNuts!=null)
            {
                NutPose attachment=null;
                if(art?.nutPoses!=null)
                    foreach(var candidate in art.nutPoses)
                        if(candidate.pose==displayedPose){attachment=candidate;break;}
                for(int i=0;i<carriedNuts.Length;i++)
                {
                    var nut=carriedNuts[i];
                    // Each authored pose owns its sockets; a curled body must not leave nuts floating.
                    nut.enabled=Visible&&!Bare&&displayedPose>0&&summonFrame<0&&(attachment==null||attachment.visible);
                    var p=attachment?.positions!=null&&i<attachment.positions.Length?attachment.positions[i]:NutSockets[i];
                    float height=attachment?.heights!=null&&i<attachment.heights.Length?attachment.heights[i]:NutHeights[i];
                    if(Main.flipX)p.x=-p.x;
                    nut.transform.localPosition=p;
                    if(nut.sprite!=null)nut.transform.localScale=Vector3.one*height/nut.sprite.bounds.size.y;
                    nut.flipX=Main.flipX;nut.color=new Color(1,1,1,Opacity);
                }
                if(nutFront!=null)
                {
                    nutFront.enabled=carriedNuts[0].enabled&&nutFrontFrames!=null&&displayedPose<nutFrontFrames.Length;
                    if(nutFront.enabled)nutFront.sprite=nutFrontFrames[displayedPose];
                    nutFront.flipX=Main.flipX;nutFront.color=Main.color;
                }
            }
            shadow.enabled = Visible && Main.maskInteraction == SpriteMaskInteraction.None; shadow.color = new Color(0,0,0,.19f*Opacity*Mathf.Clamp01(1-Lift*.13f));
        }
    }
}
