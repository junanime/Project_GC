using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SnailBossRuntime : IDamageable
    {
        public SnailBossSettings settings;
        public bool AutoPatterns=true;
        public SnailBossVisual Visual {get;private set;}
        public Character Player {get;private set;}
        public bool Chocolate {get;private set;}
        public bool Dead {get;private set;}
        public bool Transitioning {get;private set;}
        public bool Groggy=>groggyTime>0;
        public bool IsDashing {get;private set;}
        public float Health {get;private set;}
        public int MissingMask {get;private set;}
        public SnailPattern CurrentPattern {get;private set;}
        public bool Busy=>routine!=null;
        public Transform EffectsRoot {get;private set;}
        public static bool Paused=>Time.timeScale<=0||MiniStageRuntimeState.IsInsideMiniStage;
        Rigidbody2D rb; Coroutine routine; float timer=2,groggyTime,contactTime,flashTime; int nextPattern; bool initialized,wasPaused;
        Image hpFill; Text hpText; GameObject hud;
        public void Initialize(Character player)
        {
            if(initialized)return;initialized=true;Player=player;
            if(settings==null)settings=Resources.Load<SnailBossSettings>("SnailBossSettings");
            if(settings==null){Debug.LogError("Missing SnailBossSettings",this);enabled=false;return;}
            rb=GetComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;rb.drag=0;
            var preview=transform.Find("Summon preview");if(preview!=null)preview.gameObject.SetActive(false);
            Visual=GetComponent<SnailBossVisual>()??gameObject.AddComponent<SnailBossVisual>();Visual.Configure(settings.bodyWidth);
            Health=settings.maxHealth*SnailBossRules.SpawnFraction(SnailFieldProgress.RemovedCount);MissingMask=SnailFieldProgress.Mask;
            Visual.SetMissing(MissingMask);
            EffectsRoot=new GameObject("Snail encounter effects").transform;EffectsRoot.SetParent(transform.parent);
            CreateHud();
        }
        void Start(){if(!initialized)Initialize(FindObjectOfType<Character>());}
        void FixedUpdate()
        {
            if(!initialized||Visual==null||settings==null)return;
            var box=GetComponent<BoxCollider2D>();if(box==null)return;
            bool rolling=Visual.Action==SnailAction.Roll;
            box.size=rolling?new Vector2(settings.bodyWidth*.77f,settings.bodyWidth*.71f):new Vector2(settings.bodyWidth*.95f,settings.bodyWidth*.77f);
            box.offset=rolling?(Vector2)transform.InverseTransformPoint(Visual.Shell.transform.position):Vector2.up*(settings.bodyWidth*.385f);
        }
        void Update()
        {
            if(!initialized||settings==null||Dead)return;
            if(Paused)
            {
                if(!wasPaused){CancelAction();ClearEffects();wasPaused=true;}
                rb.velocity=Vector2.zero;return;
            }
            if(wasPaused){wasPaused=false;timer=1;}
            if(Player==null)return;
            if(!Busy&&!Transitioning&&Health<=settings.maxHealth*settings.phaseThreshold+.01f&&!Chocolate)
            {groggyTime=0;routine=StartCoroutine(PhaseTransition());return;}
            if(Transitioning)return;
            if(groggyTime>0)
            {
                groggyTime-=Time.deltaTime;Visual.Pose(SnailAction.Groggy);rb.velocity=Vector2.zero;return;
            }
            flashTime=Mathf.Max(0,flashTime-Time.deltaTime);Visual.Flash(flashTime*4);
            if(!AutoPatterns)return;
            if(Busy)return;
            Visual.Face(Player.transform.position.x-transform.position.x);
            float distance=Vector2.Distance(Player.transform.position,transform.position);
            rb.velocity=distance>5?((Vector2)(Player.transform.position-transform.position)).normalized*settings.walkSpeed:Vector2.zero;
            Visual.Pose(rb.velocity.sqrMagnitude>.01f?SnailAction.Walk:SnailAction.Idle);
            timer-=Time.deltaTime;
            if(timer<=0)
            {
                SnailPattern[] cycle={SnailPattern.Basic,SnailPattern.Bomb,SnailPattern.Basic,SnailPattern.Fan,SnailPattern.Homing,SnailPattern.Dash,SnailPattern.Summon,SnailPattern.Basic,SnailPattern.Absorb,SnailPattern.Dash};
                for(int n=0;n<cycle.Length;n++){var p=cycle[nextPattern++%cycle.Length];if(CanUse(p)){UsePattern(p);break;}}
            }
        }
        public bool CanUse(SnailPattern pattern)
        {
            int ingredient=SnailBossRules.Ingredient(pattern);
            return Chocolate||ingredient<0||(MissingMask&(1<<ingredient))==0;
        }
        public bool UsePattern(SnailPattern pattern)
        {
            if(Dead||Transitioning||Groggy||Busy||Paused||!CanUse(pattern))return false;
            CurrentPattern=pattern;routine=StartCoroutine(Execute(pattern));return true;
        }
        IEnumerator Execute(SnailPattern pattern)
        {
            rb.velocity=Vector2.zero;
            yield return Windup(pattern==SnailPattern.Dash);
            switch(pattern)
            {
                case SnailPattern.Basic: yield return Basic();break;
                case SnailPattern.Bomb: yield return Bomb();break;
                case SnailPattern.Fan: yield return Fan();break;
                case SnailPattern.Homing: FireHoming();break;
                case SnailPattern.Summon: SpawnMinions(false);yield return Wait(.7f);break;
                case SnailPattern.Absorb: SpawnMinions(true);Visual.Pose(SnailAction.Absorb);yield return Wait(5f);break;
                case SnailPattern.Dash: yield return Dash();break;
            }
            Visual.Pose(SnailAction.Idle);rb.velocity=Vector2.zero;routine=null;timer=pattern==SnailPattern.Basic?settings.basicCooldown:settings.patternGap;
        }
        IEnumerator Windup(bool dash)
        {
            Visual.Face(Player.transform.position.x-transform.position.x);Visual.Pose(dash?SnailAction.DashPuff:SnailAction.Puff);
            yield return Wait(.45f);Visual.Pose(SnailAction.Spit);
        }
        public IEnumerator Wait(float seconds)
        {
            for(float t=0;t<seconds;){if(!Paused)t+=Time.deltaTime;yield return null;}
        }
        IEnumerator Basic()
        {
            int bursts=SnailBossRules.BurstCount(Chocolate);
            for(int b=0;b<bursts;b++)
            {
                if(b>0){Visual.Pose(SnailAction.Puff);yield return Wait(.18f);Visual.Pose(SnailAction.Spit);}
                for(int i=0;i<settings.radialCount;i++)
                {
                    float angle=i*360f/settings.radialCount+b*(360f/settings.radialCount/bursts);
                    SnailBossProjectile.Linear(this,SnailBossArt.Projectile(Chocolate,0),Visual.Mouth,Direction(angle),settings.projectileSpeed,.32f,settings.bulletDamage,Chocolate?"ChocolateSplash":"Jam");
                }
                yield return Wait((Chocolate?settings.chocolateBurstGap:settings.vanillaBurstGap)-(b+1<bursts?.18f:0));
            }
        }
        IEnumerator Bomb()
        {
            for(int i=0;i<SnailBossRules.BombCount(Chocolate);i++)
            {
                Vector2 target=Player.transform.position;
                if(Chocolate) target+=Direction(i*137.5f)*(i%2==0?.65f:1.2f);
                SnailBossProjectile.Bomb(this,Visual.Mouth,target);
                yield return Wait(SnailBossRules.BombGap(Chocolate));
            }
        }
        IEnumerator Fan()
        {
            Vector2 dir=((Vector2)Player.transform.position-(Vector2)Visual.Mouth).normalized;
            if(!Chocolate)
            {
                for(int i=0;i<6;i++)
                {
                    float a=(i-2.5f)*settings.melonSpread;
                    Vector2 arc=Vector2.Perpendicular(dir)*(i-2.5f)*.34f+dir*(.25f-Mathf.Pow((i-2.5f)/2.5f,2)*.4f);
                    var p=SnailBossProjectile.Linear(this,"Melon"+i,Visual.Mouth+(Vector3)arc,Rotate(dir,a),3,.5f,settings.bulletDamage,null);
                    p.transform.rotation=Quaternion.Euler(0,0,(i-2.5f)*12);
                }
            }
            else
            {
                for(int board=0;board<2;board++)
                {
                    Vector2 forward=Rotate(dir,(board-.5f)*settings.plateDirectionDifference),side=Vector2.Perpendicular(forward);
                    for(int lane=0;lane<3;lane++)for(int depth=0;depth<6;depth++)
                    {
                        Vector2 pos=(Vector2)Visual.Mouth+side*(lane-1)*.26f+forward*depth*.26f;
                        SnailBossProjectile.Linear(this,"Tablet",pos,Rotate(forward,SnailBossRules.PlateLaneDegrees(lane,settings.plateLaneAngle)),3.5f,.25f,settings.bulletDamage,null);
                    }
                    yield return Wait(.8f);
                }
            }
            yield return Wait(.5f);
        }
        void FireHoming()=>SnailBossProjectile.Homing(this,Visual.Mouth);
        void SpawnMinions(bool absorb)
        {
            int n=absorb?SnailBossRules.AbsorbCount(Chocolate):6;
            for(int i=0;i<n;i++)
            {
                Vector2 pos=(Vector2)transform.position+Direction(360f*i/n)*(absorb?7:3);
                SnailBossMinion.Spawn(this,pos,Random.Range(0,4),absorb);
            }
            SnailBossVfx.Burst(this,transform.position,Chocolate?"ChocolateSplash":"CreamSplash",8,.4f);
        }
        IEnumerator Dash()
        {
            int count=Chocolate?2:1;
            for(int i=0;i<count;i++)
            {
                if(i>0)yield return Windup(true);
                Vector2 start=rb.position,dir=((Vector2)Player.transform.position-start).normalized;
                if(dir.sqrMagnitude<.01f)dir=Vector2.left;
                Vector2 end=start+dir*settings.dashDistance;
                Visual.Pose(SnailAction.Spit);
                // Entire wet warning becomes slowing terrain immediately, before the roll.
                SnailBossPuddle.Path(this,start,end);
                SnailBossVfx.Burst(this,Visual.Mouth,Chocolate?"ChocolateSplash":"CreamSplash",9,.7f);
                for(int jet=0;jet<14;jet++)SnailBossVfx.Puff(this,Chocolate?"ChocolateSplash":"CreamSplash",Vector2.Lerp(Visual.Mouth,end,jet/13f),.45f,.2f);
                yield return Wait(i==0?settings.dashWarning:Mathf.Max(0,settings.dashGap-.45f));
                IsDashing=true;Visual.Pose(SnailAction.Roll);
                for(float t=0;t<settings.dashDistance/settings.dashSpeed;t+=Time.deltaTime)
                {
                    rb.position=Vector2.MoveTowards(rb.position,end,settings.dashSpeed*Time.deltaTime);
                    if(Time.frameCount%4==0)SnailBossVfx.Burst(this,rb.position,Chocolate?"ChocolateSplash":"CreamSplash",2,.35f);
                    yield return null;
                    if(Groggy)break;
                }
                IsDashing=false;rb.velocity=Vector2.zero;
                if(Groggy)yield break;
                Visual.Pose(SnailAction.Idle);if(i+1>=count)yield return Wait(.35f);
            }
        }
        IEnumerator PhaseTransition()
        {
            Transitioning=true;rb.velocity=Vector2.zero;ClearEffects();Visual.Pose(SnailAction.Transition);
            yield return Wait(settings.transitionDuration*.5f);
            Chocolate=true;Visual.SetPhase(true);Visual.Pose(SnailAction.Transition);UpdateHud();
            SnailBossVfx.Burst(this,transform.position,"ChocolateSplash",16,.8f);
            yield return Wait(settings.transitionDuration*.5f);
            Transitioning=false;Visual.Pose(SnailAction.Idle);routine=null;timer=1;
        }
        public void TriggerTrap()
        {
            if(!IsDashing||Dead)return;
            CancelAction();groggyTime=settings.groggyDuration;
            Health=Mathf.Max(0,Health-settings.maxHealth*settings.trapDamageFraction);
            Visual.Pose(SnailAction.Groggy);
            if(Chocolate)Visual.Stumble();
            SnailBossVfx.Burst(this,transform.position,Chocolate?"ChocolateSplash":"Mango",12,.8f);
            UpdateHud();
            if(Health<=0){Dead=true;ClearEffects();StartCoroutine(Death());}
        }
        public void HealFraction(float fraction){if(Dead||Transitioning)return;Health=Mathf.Min(settings.maxHealth,Health+settings.maxHealth*fraction);UpdateHud();}
        public override void TakeDamage(float damage,Vector2 knockback=default,bool isCritical=false)
        {
            if(!initialized||Dead||Transitioning||Paused||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return;
            Health=Mathf.Max(0,Health-damage*(Groggy?settings.groggyMultiplier:1));flashTime=.12f;UpdateHud();
            if(Health<=0){CancelAction();Dead=true;ClearEffects();StartCoroutine(Death());}
            else if(!Chocolate&&Health<=settings.maxHealth*settings.phaseThreshold)
            {CancelAction();groggyTime=0;routine=StartCoroutine(PhaseTransition());}
        }
        public override void Knockback(Vector2 knockback){}
        IEnumerator Death()
        {
            Visual.Pose(SnailAction.Dead);SnailBossVfx.Burst(this,transform.position,Chocolate?"ChocolateSplash":"CreamSplash",24,1);
            yield return Wait(1.5f);FindObjectOfType<LevelManager>()?.LevelPassed(null);
        }
        void OnTriggerStay2D(Collider2D other)
        {
            if(Dead||Paused||Groggy||Transitioning||Time.time<contactTime)return;
            var p=other.GetComponentInParent<Character>();if(p==null)return;
            p.TakeDamage(settings.contactDamage);contactTime=Time.time+.7f;
        }
        void CancelAction()
        {
            if(routine!=null)StopCoroutine(routine);routine=null;IsDashing=false;Transitioning=false;
            if(rb!=null)rb.velocity=Vector2.zero;
            if(Visual!=null)Visual.Pose(SnailAction.Idle);timer=1;
        }
        void ClearEffects(){if(EffectsRoot!=null)for(int i=EffectsRoot.childCount-1;i>=0;i--)Destroy(EffectsRoot.GetChild(i).gameObject);Player?.GetComponent<SnailSlowStatus>()?.Clear();}
        void OnDisable(){CancelAction();ClearEffects();if(hud!=null)hud.SetActive(false);}
        void OnDestroy(){if(EffectsRoot!=null)Destroy(EffectsRoot.gameObject);if(hud!=null)Destroy(hud);}
        public static Vector2 Direction(float degrees)=>new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));
        public static Vector2 Rotate(Vector2 dir,float degrees)=>Quaternion.Euler(0,0,degrees)*dir;
        void CreateHud()
        {
            hud=new GameObject("Roll cake boss health",typeof(Canvas),typeof(CanvasScaler));
            var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var back=new GameObject("Health",typeof(RectTransform),typeof(Image));back.transform.SetParent(hud.transform,false);
            var rect=(RectTransform)back.transform;rect.anchorMin=new Vector2(.25f,.94f);rect.anchorMax=new Vector2(.75f,.97f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            back.GetComponent<Image>().color=new Color(.14f,.08f,.08f,.85f);
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(back.transform,false);
            var fr=(RectTransform)fill.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
            hpFill=fill.GetComponent<Image>();hpFill.color=new Color(.95f,.4f,.5f);hpFill.raycastTarget=false;
            var title=new GameObject("Label",typeof(RectTransform),typeof(Text));title.transform.SetParent(back.transform,false);
            var tr=(RectTransform)title.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            hpText=title.GetComponent<Text>();hpText.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");hpText.fontSize=17;hpText.alignment=TextAnchor.MiddleCenter;hpText.raycastTarget=false;
            UpdateHud();
        }
        void UpdateHud()
        {
            if(hpFill==null)return;hpFill.rectTransform.anchorMax=new Vector2(Health/settings.maxHealth,1);
            hpFill.color=Chocolate?new Color(.46f,.24f,.12f):new Color(.95f,.4f,.5f);
            hpText.text=(Chocolate?"Chocolate roll":"Fruit cream roll")+"   "+Mathf.CeilToInt(Health)+" / "+settings.maxHealth;
        }
    }
}
