using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Vampire
{
    // One fuel ledger owns keyboard, pointer, pickups, overhead digits and scene transfer.
    [DefaultExecutionOrder(720)]
    public sealed class ShiniEmberRuntime : MonoBehaviour
    {
        public const float DropChance=.69f, Preparation=.88f, ShortDuration=1.5f, FadeDuration=.46f;
        public const int InitialCost=6;
        public const float FuelInterval=.25f, DamagePerSecond=35f, Range=4f, HalfAngle=35f;
        public static bool ShouldDrop(float roll) => roll<DropChance;
        public int Fuel { get; private set; }
        public int Capacity => 24+6*(skill.PassiveLevel-1);
        public float DamagePower => 1+.2f*(skill.ActiveLevel-1);
        public bool Busy { get; private set; }
        public bool Emitting => Busy && age>=Preparation && stopped<0;
        public int GroundCount => drops.Count;
        public bool CanStart => !Busy && Fuel>=InitialCost;
        Character owner;CharacterSkillRuntime skill;ShiniEmberArt art;SpriteRenderer body,pose,fire;
        readonly SpriteRenderer[] numbers=new SpriteRenderer[2];
        sealed class Drop {public SpriteRenderer visual;public Vector3 position;public bool flying;public float born;}
        readonly List<Drop> drops=new List<Drop>();readonly Stack<SpriteRenderer> spare=new Stack<SpriteRenderer>();
        Material fireMaterial;float age,stopped=-1,nextFuel,nextHit,bodyHeight,poseScale;bool keyboardHeld,pointerHeld;
        Vector2 direction;Vector3 fireOrigin;PlayerCombatBars combatBars;
        static readonly int[] CastLoop={6,7,8,7};
        public void Bind(Character player,CharacterSkillRuntime runtime)
        {
            owner=player;skill=runtime;art=Resources.Load<ShiniEmberArt>("ShiniEmberArt");
            body=GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(!skill.IsShini||art==null||body==null){enabled=false;return;}
            var reference=owner.Blueprint.idleSpriteSequence[0];bodyHeight=reference.bounds.size.y*Mathf.Abs(body.transform.lossyScale.y);
            poseScale=bodyHeight/Mathf.Max(.01f,art.castHeight);
            pose=Make("Shini inflated chest pose",true);fire=Make("Shini continuous fan fire",true);
            fireMaterial=new Material(art.flameMaterial);fire.sharedMaterial=fireMaterial;
            for(int i=0;i<2;i++)numbers[i]=Make("Shini flame fuel digit "+i,true);
            Monster.Died+=OnMonsterDied;
        }
        SpriteRenderer Make(string name,bool child)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>();if(child)r.transform.SetParent(transform,false);
            r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+5;r.enabled=false;return r;
        }
        void OnMonsterDied(Monster monster)
        {
            if(owner!=null&&owner.IsAlive&&skill.IsShini&&monster.WasKilledByPlayer&&ShouldDrop(Random.value))
                SpawnEmber(monster.transform.position);
        }
        public void SpawnEmber(Vector3 position)
        {
            if(art==null)return;
            var r=spare.Count>0?spare.Pop():Make("Shini dropped ember",false);r.sprite=art.ember;r.enabled=true;
            r.transform.localScale=Vector3.one*(art.emberHeight/Mathf.Max(.01f,art.ember.bounds.size.y));r.transform.position=position;
            drops.Add(new Drop{visual=r,position=position,born=Time.time});
        }
        public bool AddFuel(int amount=1){if(amount<=0||Fuel>=Capacity)return false;Fuel=Mathf.Min(Capacity,Fuel+amount);return true;}
        public void RestoreFuel(int value){Cancel();Fuel=Mathf.Clamp(value,0,Capacity);}
        public bool Press(bool keyboard=false)
        {
            if(!skill.CanActivate||!CanStart)return false;
            Fuel-=InitialCost;Busy=true;age=0;stopped=-1;nextHit=Preparation+.2f;nextFuel=Preparation+ShortDuration;
            keyboardHeld=keyboard;pointerHeld=!keyboard;
            direction=owner.LookDirection.sqrMagnitude>.001f?owner.LookDirection.normalized:Vector2.right;
            owner.GetComponent<PrescriptionRuntime>()?.Record(PrescriptionRuntime.Goal.ActiveUses);return true;
        }
        public bool Tap(){bool ok=Press();pointerHeld=false;return ok;}
        public void ReleasePointer(){pointerHeld=false;}
        public void Cancel(){Busy=false;keyboardHeld=pointerHeld=false;stopped=-1;if(body!=null)body.forceRenderingOff=false;if(pose!=null)pose.enabled=false;if(fire!=null)fire.enabled=false;}
        void Update()
        {
            if(owner==null||!owner.IsAlive){Cancel();return;}
            if(Time.timeScale<=0||owner.IsPortalTravelling||owner.IsPortalPoseHeld||owner.IsTrapBound||owner.IsDashing
                ||(ApothecaryUI.Instance!=null&&ApothecaryUI.Instance.Page!="hud")){Cancel();return;}
            if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame)Press(true);
            if(keyboardHeld&&(Keyboard.current==null||!Keyboard.current.rKey.isPressed))keyboardHeld=false;
            Advance(Time.deltaTime);UpdateDrops(Time.deltaTime);
        }
        public void Advance(float dt)
        {
            if(!Busy||dt<=0)return;
            float end=age+dt;
            // Process charges and damage chronologically so low frame rates cannot provide free flame time.
            while(stopped<0&&Mathf.Min(nextFuel,nextHit)<=end){
                float at=Mathf.Min(nextFuel,nextHit);age=at;
                if(nextFuel<=nextHit){
                    if(!(keyboardHeld||pointerHeld)||Fuel<=0){stopped=at;break;}
                    Fuel--;nextFuel+=FuelInterval;
                }else{DealTick(.2f);nextHit+=.2f;}
            }
            age=end;
            if(stopped<0&&age>=Preparation+ShortDuration&&!(keyboardHeld||pointerHeld))stopped=age;
            if(stopped>=0&&age-stopped>=FadeDuration)Cancel();
        }
        public static bool InCone(Vector2 delta,Vector2 facing) => delta.sqrMagnitude<=Range*Range&&(delta.sqrMagnitude<.0001f||Vector2.Dot(delta.normalized,facing)>=Mathf.Cos(HalfAngle*Mathf.Deg2Rad));
        void DealTick(float seconds)
        {
            var origin=(Vector2)owner.CenterTransform.position;
            foreach(var target in Ver4HitEffects.Nearby(origin,Range,LayerMask.GetMask("Monster Full","Chest"),owner))
            {
                if(target is Monster m&&m.IsFieldRuntimeSuspended||!InCone((Vector2)target.transform.position-origin,direction))continue;
                float damage=DamagePerSecond*seconds*owner.DamageMultiplier*DamagePower;
                var stats=PlayerGeneralStatRuntime.GetOrCreate(owner);if(stats!=null)damage=stats.ApplyBossDamageBonus(target,damage);
                var corrosion=target.GetComponent<CorrosionStatus>();if(corrosion!=null)damage*=corrosion.GetDamageTakenMultiplier();
                Ver4HitEffects.Damage(target,damage,Vector2.zero,owner,"후우우—!",true);
            }
        }
        void UpdateDrops(float dt)
        {
            var pickup=owner.CollectableCollider;
            for(int i=drops.Count-1;i>=0;i--){var d=drops[i];
                if(Fuel>=Capacity)d.flying=false;
                else if(!d.flying&&pickup!=null&&pickup.enabled&&pickup.OverlapPoint(d.position))d.flying=true;
                if(d.flying){d.position=Vector3.MoveTowards(d.position,owner.CenterTransform.position,dt*8);
                    if(Vector2.Distance(d.position,owner.CenterTransform.position)<.12f&&AddFuel()){d.visual.enabled=false;spare.Push(d.visual);drops.RemoveAt(i);continue;}}
                d.visual.transform.position=d.position+Vector3.up*(.025f*Mathf.Sin((Time.time-d.born)*5));
            }
        }
        void LateUpdate()
        {
            if(body==null||art==null)return;
            bool visible=owner.IsAlive&&!owner.IsPortalTravelling&&!owner.IsPortalPoseHeld;
            DrawDigits(visible);if(!Busy){pose.enabled=fire.enabled=false;return;}
            int frame=age<Preparation?(age<.12f?1:age<.32f?2:age<.56f?3:4):age<Preparation+.14f?5:CastLoop[(int)((age-Preparation)*7)%4];
            if(stopped>=0&&age-stopped>.1f)frame=age-stopped<.24f?9:age-stopped<.37f?10:11;
            bool left=direction.x<0;var feet=new Vector3(body.bounds.center.x,body.bounds.min.y,body.transform.position.z);
            pose.sprite=art.cast[frame];pose.transform.position=feet;pose.transform.localScale=Vector3.one*poseScale;pose.flipX=left;pose.color=body.color;pose.enabled=visible&&body.enabled;body.forceRenderingOff=pose.enabled;
            var mouth=art.mouths[frame]*poseScale;if(left)mouth.x=-mouth.x;
            if(stopped<0)fireOrigin=feet+(Vector3)mouth;
            float firing=age-Preparation;fire.enabled=visible&&firing>=0;
            if(!fire.enabled)return;
            fire.sprite=art.flames[(int)(firing*10)%art.flames.Length];fire.transform.position=fireOrigin;
            fire.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            fire.transform.localScale=Vector3.one*(Range/5.1f);
            var rect=fire.sprite.rect;var tex=fire.sprite.texture;
            fireMaterial.SetVector("_FrameUV",new Vector4(rect.x/tex.width,rect.y/tex.height,rect.width/tex.width,rect.height/tex.height));
            fireMaterial.SetFloat("_Head",Mathf.Lerp(.01f,1.05f,Mathf.SmoothStep(0,1,firing/.34f)));
            fireMaterial.SetFloat("_Tail",stopped<0?-.1f:Mathf.Lerp(-.1f,1.1f,Mathf.SmoothStep(0,1,(age-stopped)/FadeDuration)));
        }
        void DrawDigits(bool visible)
        {
            float height=bodyHeight*.36f;int count=Fuel>=10?2:1;float width=height*.77f;
            float pixelsToWorld=Camera.main!=null?Camera.main.orthographicSize*2/Mathf.Max(1,Screen.height):.01f;
            float y=body.bounds.max.y+height*.5f+pixelsToWorld*25;
            if(combatBars==null)combatBars=GetComponent<PlayerCombatBars>();
            if(combatBars!=null)y=combatBars.FuelAnchor(height).y;
            for(int i=0;i<2;i++){var r=numbers[i];r.enabled=visible&&i<count;if(!r.enabled)continue;
                int digit=count==1?Fuel:i==0?Fuel/10:Fuel%10;r.sprite=art.digits[digit];
                r.transform.localScale=Vector3.one*(height/r.sprite.bounds.size.y);
                r.transform.position=new Vector3(transform.position.x+(i-(count-1)*.5f)*width,y,body.transform.position.z);r.sortingOrder=body.sortingOrder+30;
            }
        }
        void OnApplicationFocus(bool focus){if(!focus)Cancel();}
        void OnDisable(){Cancel();}
        void OnDestroy(){Monster.Died-=OnMonsterDied;if(fireMaterial!=null)Destroy(fireMaterial);foreach(var d in drops)if(d.visual!=null)Destroy(d.visual.gameObject);foreach(var r in spare)if(r!=null)Destroy(r.gameObject);}
    }
}
