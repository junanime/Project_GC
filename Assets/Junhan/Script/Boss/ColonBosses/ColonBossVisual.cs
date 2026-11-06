using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class ColonBossVisual : MonoBehaviour
    {
        [Serializable] class ArtInfo { public float referencePixelWidth = 240, referencePixelHeight = 180, pixelsPerUnit = 125, walkCycle = 1.6f; }
        public SpriteRenderer Main { get; private set; }
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
        Sprite[] walk, bareWalk, poses, summon;
        public bool Bare;
        public float GroundSpin;
        static readonly Dictionary<string,Sprite[]> cache=new Dictionary<string,Sprite[]>();
        SpriteRenderer shadow;
        SpriteRenderer[] carriedNuts;
        static readonly Vector2[] NutSockets={new Vector2(.0555f,.7670f),new Vector2(.3633f,.5904f),new Vector2(.5803f,.3633f)};
        static readonly float[] NutHeights={.21194f,.21698f,.1968f};
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
            if(owner.Kind==ColonBossKind.Hedgehog)bareWalk=Load(id,"WalkBare");
            var json = Resources.Load<TextAsset>("ColonBosses/" + id + "/BossArt");
            var info = json != null ? JsonUtility.FromJson<ArtInfo>(json.text) : new ArtInfo();
            scale = owner.Definition.Size / (info.referencePixelWidth / info.pixelsPerUnit); cycle = info.walkCycle;
            ReferenceHeight = info.referencePixelHeight / info.pixelsPerUnit * scale;
            Main = new GameObject("Body · one reference scale").AddComponent<SpriteRenderer>(); Main.transform.SetParent(transform,false); Main.sortingOrder = 510;
            shadow = new GameObject("Ground contact shadow").AddComponent<SpriteRenderer>(); shadow.transform.SetParent(transform,false);
            shadow.sprite = BombCircleArt.Sprite; shadow.sortingOrder = 478; shadow.color = new Color(0,0,0,.19f);
            shadow.transform.localScale = new Vector3(owner.Definition.Size * .7f,.28f,1) / Mathf.Max(.01f,shadow.sprite.bounds.size.x);
            if(owner.Kind==ColonBossKind.Hedgehog)
            {
                carriedNuts=new SpriteRenderer[3];var nut=Resources.Load<Sprite>("ColonMonsters/S02");
                for(int i=0;i<3;i++){var r=new GameObject("Carried stage-two chestnut "+i).AddComponent<SpriteRenderer>();r.transform.SetParent(Main.transform,false);r.sprite=nut;r.sortingOrder=511;if(nut!=null)r.transform.localScale=Vector3.one*NutHeights[i]/nut.bounds.size.y;carriedNuts[i]=r;}
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
            Lift=Roll=Squash=Stretch=GroundSpin=0; Sample();
        }
        public Vector2 PipeFace => Socket(.2431f,.5819f);
        public Vector2 Mouth => boss.Kind==ColonBossKind.Flamingo?Socket(.18f,.74f):
            boss.Kind==ColonBossKind.Shark?Socket(.13f,.43f):boss.Kind==ColonBossKind.Pudding?Socket(.30f,.65f):
            boss.Kind==ColonBossKind.Turtle?Socket(.15f,.67f):boss.Kind==ColonBossKind.Manatee?Socket(.18f,.59f):Socket(.15f,.35f);
        public void Summon(float progress) { summonFrame = Mathf.Clamp(Mathf.FloorToInt(progress * summon.Length),0,summon.Length-1); Sample(); }
        public void ResetPose() { Pose = summonFrame = -1; Lift = Roll = GroundSpin = Squash = Stretch = 0; WalkRate = 1; Visible = true; Opacity = 1; }
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
            float spin=GroundSpin==0?1:Mathf.Cos(GroundSpin*Mathf.Deg2Rad);
            float anticipation=summonFrame<0&&!boss.Dead?Mathf.Sin(poseBlend*Mathf.PI)*.025f:0;
            Main.transform.localScale = new Vector3(scale*(1+Stretch+(Squash+anticipation)*.4f)*(GroundSpin==0?1:.82f+.18f*Mathf.Abs(spin)),scale*(1-Squash-anticipation+breathe),1);
            Main.flipX = (Facing > 0) ^ (GroundSpin!=0&&spin<0);
            Vector3 center=Vector3.up*(ReferenceHeight*.47f);
            Main.transform.localPosition = Vector3.up * Lift+center-Quaternion.Euler(0,0,Roll)*center;
            Main.transform.localRotation = Quaternion.Euler(0,0,Roll);
            Main.color = flash > 0 ? new Color(1,.73f,.73f,Opacity) : new Color(1,1,1,Opacity);
            if(carriedNuts!=null)for(int i=0;i<3;i++){var nut=carriedNuts[i];nut.enabled=Visible&&!Bare&&displayedPose>0&&summonFrame<0;var p=NutSockets[i];if(Main.flipX)p.x=-p.x;nut.transform.localPosition=p;nut.flipX=Main.flipX;nut.color=new Color(1,1,1,Opacity);}
            shadow.enabled = Visible && Main.maskInteraction == SpriteMaskInteraction.None; shadow.color = new Color(0,0,0,.19f*Opacity*Mathf.Clamp01(1-Lift*.13f));
        }
    }
}
