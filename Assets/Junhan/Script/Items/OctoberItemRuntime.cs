using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    [Serializable] public sealed class OctoberItemState
    {
        public List<int> owned=new List<int>();
        public float delayedExp,overflow,lostHp,elapsed;
        public int kills,streak,attacks,hits;
        public List<OctoberResistance> resistance=new List<OctoberResistance>();
        public float[] cooldownRemaining;
        public float hurtAge,healAge,killAge,expRemaining,baseDamage=8;
        public bool shield8,shield26,crit,walnut,drum,dragon;
    }
    [Serializable] public sealed class OctoberResistance {public string enemy;public int count;}
    public sealed class OctoberItemRuntime : MonoBehaviour
    {
        public OctoberItemState State {get;private set;}=new OctoberItemState();
        public Character Player {get;private set;}
        public float LastBaseDamage {get;private set;}=8;
        public float StatusDuration=>Has(48)?1.25f:1;
        public float SlowMultiplier(float original)=>Has(47)?Mathf.Max(.3f,original-.15f):original;
        public bool Has(int id)=>State.owned.Contains(id);
        readonly float[] cooldown=new float[67];
        readonly HashSet<int> grazed=new HashSet<int>();
        readonly List<Vector2> trail=new List<Vector2>();
        readonly List<OctoberItemZone> fires=new List<OctoberItemZone>();
        Vector2 previous,facing=Vector2.right,portal;
        float lastHurt,lastHeal,lastKill,movingDistance,drumDistance,expDue,portalUntil,scanClock,trailClock,damageCharge;
        int sameHits,lastTarget,grazes,lastHour,attacksRecent,hitsRecent,travelRecent;
        bool charge8,charge26,critReady,walnut,drumReady,dragonReady,releasingExp;
        SyringeDartAbility weapon;
        SpriteRenderer orb,guardian;
        LevelManager level;
        public static OctoberItemRuntime Get(Character p)=>p==null?null:p.GetComponent<OctoberItemRuntime>()??p.gameObject.AddComponent<OctoberItemRuntime>();
        void Awake(){Player=GetComponent<Character>();level=FindObjectOfType<LevelManager>();previous=transform.position;lastHurt=lastHeal=Time.time;Monster.Died+=Killed;}
        void OnDestroy(){Monster.Died-=Killed;}
        public bool Give(MerchantItemBlueprint item,bool restore=false)
        {
            if(item==null||item.octoberId<1||Has(item.octoberId))return false;
            int id=item.octoberId;State.owned.Add(id);cooldown[id]=Time.time;
            if(id==8)cooldown[id]=Time.time+12;
            if(!restore)switch(id)
            {
                case 2:Player.AddMaxHealthBonus(20);Player.GainHealth(20);break;
                case 3:Player.AddMoveSpeedBoost(.08f);break;
                case 4:Player.AddMagnetRange(.8f);break;
                case 25:foreach(var enemy in FindObjectsOfType<Monster>())if(!(enemy is BossMonster)&&!(enemy is MiniBossMonster)&&!(enemy.Blueprint is EliteMonsterBlueprint))enemy.moveSpeed*=1.15f;break;
                case 20:Player.AddDashRechargeSpeed(Player.DashRechargeTime*.15f);break;
                case 51:Player.AddInvincibilityTime(.2f);break;
            }
            return true;
        }
        public OctoberItemState Capture()
        {
            State.cooldownRemaining=cooldown.Select(t=>Mathf.Max(0,t-Time.time)).ToArray();
            State.hurtAge=Time.time-lastHurt;State.healAge=Time.time-lastHeal;State.killAge=Time.time-lastKill;
            State.expRemaining=Mathf.Max(0,expDue-Time.time);State.baseDamage=LastBaseDamage;
            State.shield8=charge8;State.shield26=charge26;State.crit=critReady;State.walnut=walnut;State.drum=drumReady;State.dragon=dragonReady;
            return State;
        }
        public void Restore(OctoberItemState state)
        {
            if(state==null)return;State=state;
            for(int i=0;i<cooldown.Length;i++)cooldown[i]=Time.time+(state.cooldownRemaining!=null&&i<state.cooldownRemaining.Length?state.cooldownRemaining[i]:0);
            lastHurt=Time.time-state.hurtAge;lastHeal=Time.time-state.healAge;lastKill=Time.time-state.killAge;expDue=Time.time+state.expRemaining;LastBaseDamage=Mathf.Max(1,state.baseDamage);
            charge8=state.shield8;charge26=state.shield26;critReady=state.crit;walnut=state.walnut;drumReady=state.drum;dragonReady=state.dragon;
        }
        public bool Ready(int id,float interval)
        {if(!Has(id)||Time.time<cooldown[id])return false;cooldown[id]=Time.time+interval;return true;}
        public void Attack(SyringeDartAbility source)
        {
            weapon=source;facing=Player.LookDirection.sqrMagnitude>.001f?Player.LookDirection.normalized:facing;
            State.attacks++;attacksRecent++;
            if(Has(7)&&State.attacks%6==0)StartCoroutine(Echo(.12f,.6f));
        }
        IEnumerator Echo(float delay,float strength){yield return new WaitForSeconds(delay);if(Player!=null&&Player.IsAlive&&weapon!=null)weapon.FireItemEcho(facing,strength);}
        public float BeforeHit(Component target,float raw,bool secondary,ref bool critical,ref float knockback)
        {
            if(target==null)return raw;
            if(!secondary)LastBaseDamage=Mathf.Max(1,raw/(critical?2:1));float multiplier=1;
            float distance=Vector2.Distance(transform.position,target.transform.position);
            var status=target.GetComponent<OctoberItemTargetStatus>();int statuses=StatusCount(target);
            if(Has(1))multiplier+=.12f;if(Has(5)&&distance>=4)multiplier+=.18f;
            if(Has(12)&&statuses>0)multiplier+=.15f;if(Has(25))multiplier+=.5f;
            if(Has(24)&&Time.time-lastKill<4)multiplier+=Mathf.Min(.3f,State.streak*.03f);
            if(Has(34)&&Ver4HitEffects.Health(target)>=Ver4HitEffects.MaxHealth(target)-.001f)multiplier+=.25f;
            if(Has(37)&&critical)multiplier+=.25f;
            if(Has(53)&&statuses>=2)multiplier+=.25f;
            if(Has(54)&&Ver4HitEffects.Health(target)<=Ver4HitEffects.MaxHealth(target)*.3f)multiplier+=.25f;
            if(Has(56)&&distance<=2)multiplier+=.25f;
            if(Has(22))knockback*=1.3f;
            if(!secondary)
            {
                if(critReady){if(!critical)multiplier*=2;critical=true;critReady=false;}
                if(walnut){multiplier+=.4f;walnut=false;}
                if(Has(40)&&State.hits>0&&State.hits%5==0)multiplier+=.6f;
                if(Has(23))multiplier*=UnityEngine.Random.value<.5f?1.4f:.8f;
                if(Has(10)&&UnityEngine.Random.value<.2f)knockback+=1.5f;
                if(Has(27)&&State.overflow>0){raw+=State.overflow*2;State.overflow=0;}
                State.hits++;
            }
            return raw*multiplier;
        }
        public void AfterHit(Component target,float damage,bool critical,bool secondary,float healthBefore)
        {
            if(target==null||secondary)return;
            bool killed=healthBefore>0&&Ver4HitEffects.Health(target)<=0;
            if(killed&&Has(16))
            {
                fires.RemoveAll(z=>z==null);if(fires.Count>=3){Destroy(fires[0].gameObject);fires.RemoveAt(0);}
                fires.Add(Zone(target.transform.position,1.25f,3,LastBaseDamage*.3f,16));
            }
            if(Ver4HitEffects.Health(target)<=0)return;
            var status=target.GetComponent<OctoberItemTargetStatus>()??target.gameObject.AddComponent<OctoberItemTargetStatus>();
            if(Has(11)&&UnityEngine.Random.value<.2f)Slow(target,.25f,2);
            if(Has(42))status.VulnerableUntil=Time.time+3*StatusDuration;
            if(Has(46)&&UnityEngine.Random.value<.2f)status.Poison(this,3*StatusDuration,LastBaseDamage*.2f);
            if(Has(44)&&UnityEngine.Random.value<.12f&&Ready(44,1)&&!Ver4HitEffects.IsBoss(target))
                (target.GetComponent<NeuralBlockedMonsterStatus>()??target.gameObject.AddComponent<NeuralBlockedMonsterStatus>()).Apply(.6f*StatusDuration);
            int id=target.GetInstanceID();sameHits=id==lastTarget?sameHits+1:1;lastTarget=id;
            if(Has(21)&&sameHits>=4){sameHits=0;Hit(target,LastBaseDamage*.8f,21);}
            if(critical&&Ready(41,1))Player.GainHealth(1);
            bool slow=IsSlow(target);
            if(critical&&slow&&Ready(50,1)){ConsumeSlow(target);Hit(target,LastBaseDamage,50);}
            if(Has(55))
            {
                if(target is Monster monster)monster.TakeItemPureDamage(LastBaseDamage*.2f);
                else Hit(target,LastBaseDamage*.2f,55);
            }
            if(drumReady&&Has(59)){drumReady=false;Zone(target.transform.position,2,.7f,LastBaseDamage*.3f,59,true);}
            bool burning=IsBurning(target);
            if(slow&&burning&&Ready(65,3))
            {
                ConsumeSlow(target);var burn=target.GetComponent<Ver4NeedleStatus>();if(burn!=null)burn.ConsumeBurn(out _);
                status.BurnUntil=0;var old=target.GetComponent<BurnStatus>();if(old!=null)Destroy(old);
                Pulse(target.transform.position,2,LastBaseDamage*1.6f,65);
            }
        }
        public float Incoming(float damage,MonsterBlueprint source)
        {
            if(source!=null&&Has(25)&&!(source is BossMonsterBlueprint)&&!(source is EliteMonsterBlueprint))damage*=1.15f;
            if(source!=null&&Has(63)){var resistance=State.resistance.Find(m=>m.enemy==source.name);if(resistance!=null)damage*=1-Mathf.Min(.2f,resistance.count*.04f);}
            if(charge8){damage*=.6f;charge8=false;cooldown[8]=Time.time+12;}
            if(charge26){damage*=.4f;charge26=false;}
            if(damage>=Player.MaxHealth*.15f&&Ready(35,8))damage*=.75f;
            return Mathf.Max(0,damage);
        }
        public void Hurt(float actual,MonsterBlueprint source)
        {
            if(actual<=0)return;lastHurt=Time.time;State.lostHp+=actual;hitsRecent++;damageCharge=0;
            if(source!=null&&Has(63)){var resistance=State.resistance.Find(m=>m.enemy==source.name);if(resistance==null){resistance=new OctoberResistance{enemy=source.name};State.resistance.Add(resistance);}resistance.count=Mathf.Min(5,resistance.count+1);}
            if(Ready(9,4))walnut=true;
        }
        public float Healing(float value)=>value*(Has(18)?1.25f:1);
        public void Healed(float actual,float overflow)
        {
            if(Has(27))State.overflow=Mathf.Min(Player.MaxHealth*.25f,State.overflow+Mathf.Max(0,overflow));
            if(actual<=0)return;
            if(dragonReady&&Has(66)){dragonReady=false;Wave(facing,6,LastBaseDamage*2.5f,66);}
            lastHeal=Time.time;
        }
        public float Experience(float amount)
        {
            if(releasingExp)return amount;
            if(Has(36)&&UnityEngine.Random.value<.1f&&Ready(36,.5f))Player.GainHealth(1);
            if(Has(39)&&UnityEngine.Random.value<.08f)amount*=2;
            if(Has(60)){if(State.delayedExp<=0)expDue=Time.time+3;State.delayedExp+=amount*1.15f;return 0;}
            return amount;
        }
        public void FlushExperience(){if(State.delayedExp<=0||Player==null)return;float e=State.delayedExp;State.delayedExp=0;releasingExp=true;try{Player.GainExp(e);}finally{releasingExp=false;}}
        public Vector2 DashDestination(Vector2 target)
        {
            Vector2 start=transform.position;
            if(Ready(17,8))Zone(start,3,2,0,17,true,true);
            if(Ready(45,6))Zone(start,3,1,0,45,true,true);
            if(Has(61))
            {
                if(Time.time<portalUntil){target=portal;portalUntil=0;cooldown[61]=Time.time+12;}
                else if(Ready(61,12)){portal=start;portalUntil=Time.time+4;Zone(portal,.65f,4,0,61);}
            }
            return target;
        }
        void Killed(Monster monster)
        {
            if(monster==null||!monster.WasKilledByPlayer||Player==null||!Player.IsAlive)return;
            State.kills++;State.streak=Time.time-lastKill<=4?State.streak+1:1;lastKill=Time.time;
            if(Has(6)&&UnityEngine.Random.value<.12f)critReady=true;
        }
        void Update()
        {
            if(Player==null||!Player.IsAlive||Time.timeScale<=0)return;
            State.elapsed+=Time.deltaTime;
            Vector2 now=transform.position;float travel=Vector2.Distance(now,previous);previous=now;
            if(travel<3){movingDistance+=travel;drumDistance+=travel;if(travel>.01f)travelRecent++;}
            if(Has(59)&&drumDistance>=10){drumDistance=0;drumReady=true;}
            if(Has(33)&&movingDistance>=12&&travel<.005f){movingDistance=0;Zone(now+facing*1.5f,1.6f,3,LastBaseDamage*.6f,33);}
            if(Has(66)&&Time.time-lastHeal>=12)dragonReady=true;
            if(Has(8)&&Time.time>=cooldown[8])charge8=true;
            if(Has(26)&&Time.time-lastHurt>=10)charge26=true;
            if(Has(38)&&Time.time-lastHurt>=5&&Ready(38,1))Player.GainHealth(Player.MaxHealth*.005f);
            if(Has(15)&&State.kills>=20&&Player.CurrentHealth<=Player.MaxHealth*.35f){State.kills-=20;Player.GainHealth(Player.MaxHealth*.15f);}
            if(State.delayedExp>0&&Time.time>=expDue)FlushExperience();
            if(weapon==null)weapon=FindObjectOfType<SyringeDartAbility>();
            if(Ready(19,5)&&weapon!=null)StartCoroutine(Echo(0,.45f));
            if(Ready(14,8))Pulse(now,2.5f,LastBaseDamage*.25f,14,2);
            float runTime=level!=null?level.CurrentLevelTime:State.elapsed;float day=level!=null&&level.LevelDuration>0?level.LevelDuration:1440;int hour=Mathf.FloorToInt(runTime/day*24);
            if(Has(28)&&(hour%24>=18||hour%24<6)&&Ready(28,3))Pulse(now,3,LastBaseDamage*.6f,28);
            if(hour>lastHour){lastHour=hour;if(Has(29))Pulse(now,5,LastBaseDamage*1.5f,29);}
            if(Has(31)&&hour>=23&&Ready(31,1))Pulse(now,3,LastBaseDamage*1.8f,31);
            if(Ready(32,8)&&State.lostHp>0){Pulse(now,4,Mathf.Min(LastBaseDamage*4,State.lostHp*2),32);State.lostHp=0;}
            if(Ready(49,7))Zone(now+facing*2,2,1.5f,0,49,true);
            if(Has(13))
            {
                if(orb==null)orb=MakeSprite(13,"공전 약환",.55f);
                Vector2 p=now+new Vector2(Mathf.Cos(Time.time*2.6f),Mathf.Sin(Time.time*2.6f))*1.5f;orb.transform.position=p;
                if(Ready(13,.5f))Pulse(p,.5f,LastBaseDamage*.35f,13);
            }
            if(Has(30))
            {
                float growth=Mathf.Min(3,Mathf.Floor(State.elapsed/60));
                if(guardian==null)guardian=MakeSprite(30,"수호 씨앗",.7f);
                guardian.transform.position=now+new Vector2(-.8f,.8f);guardian.transform.localScale=Vector3.one*(.07f+growth*.01f);
                if(Ready(30,1.5f))Bolt(30,LastBaseDamage*(.3f+growth*.1f));
            }
            if(Ready(58,4))Bolt(58,LastBaseDamage*1.6f);
            if((trailClock-=Time.deltaTime)<=0){trailClock=.1f;trail.Add(now);if(trail.Count>30)trail.RemoveAt(0);}
            if(Ready(62,10))StartCoroutine(Backtrack(trail.ToArray()));
            if(Ready(64,15))
            {
                if(hitsRecent*4>attacksRecent){Player.GainHealth(Player.MaxHealth*.06f);Flash(now,64);}
                else if(attacksRecent>travelRecent/5)Bolt(64,LastBaseDamage*1.2f);
                else foreach(var t in Nearby(now,4))Slow(t,.25f,3);
                attacksRecent=hitsRecent=travelRecent=0;
            }
            if((scanClock-=Time.deltaTime)<=0){scanClock=.1f;Projectiles();}
        }
        void Projectiles()
        {
            if(!Has(43)&&!Has(52)&&!Has(57))return;
            var projectiles=OctoberEnemyProjectile.Active.ToArray();
            bool seal=Ready(57,12);
            foreach(var p in projectiles)
            {
                if(p==null||!p.IsHostile)continue;
                float d=Vector2.Distance(transform.position,p.transform.position);
                if(Has(52)&&d<1&&d>.35f&&grazed.Add(p.GetInstanceID()))grazes++;
                if(d<1.6f&&Vector2.Dot(((Vector2)p.transform.position-(Vector2)transform.position).normalized,facing)>0&&Ready(43,10)){p.Remove();Flash(transform.position,43);continue;}
                if(seal&&d<2.5f)p.Seal(.6f);
            }
            if(grazes>=5&&Ready(52,6))
            {grazes=0;foreach(var p in projectiles)if(p!=null&&Vector2.Distance(transform.position,p.transform.position)<2)p.Remove();Flash(transform.position,52);}
            if(grazed.Count>512)grazed.Clear();
        }
        IEnumerator Backtrack(Vector2[] points)
        {
            var hit=new HashSet<int>();var image=MakeSprite(62,"반추 잔상",.7f);
            for(int i=points.Length-1;i>=0;i--)
            {if(!Player.IsAlive)break;image.transform.position=points[i];foreach(var t in Nearby(points[i],.9f))if(hit.Add(t.GetInstanceID()))Hit(t,LastBaseDamage*.8f,62);yield return new WaitForSeconds(.05f);}
            if(image!=null)Destroy(image.gameObject);
        }
        public List<Component> Nearby(Vector2 position,float radius)=>Ver4HitEffects.Nearby(position,radius,~0,Player);
        public void Hit(Component target,float amount,int item){Ver4HitEffects.Damage(target,amount,Vector2.zero,Player,Name(item));}
        public void Pulse(Vector2 p,float radius,float damage,int item,float push=0)
        {Ver4HitEffects.AreaDamage(p,radius,damage,push,Player,~0,Name(item));Flash(p,item,radius*.7f);}
        public void Wave(Vector2 direction,float range,float damage,int item)
        {foreach(var t in Nearby(transform.position,range))if(Vector2.Dot(((Vector2)t.transform.position-(Vector2)transform.position).normalized,direction)>.65f)Hit(t,damage,item);Flash((Vector2)transform.position+direction*2,item,2);}
        void Bolt(int item,float damage)
        {var target=Nearby(transform.position,9).FirstOrDefault();if(target!=null)StartCoroutine(Fly(target,item,damage));}
        IEnumerator Fly(Component target,int item,float damage)
        {
            var image=MakeSprite(item,"아이템 소환체",.55f);image.transform.position=transform.position;
            float left=2;
            while(target!=null&&Ver4HitEffects.Health(target)>0&&(left-=Time.deltaTime)>0)
            {image.transform.position=Vector2.MoveTowards(image.transform.position,target.transform.position,9*Time.deltaTime);if(Vector2.Distance(image.transform.position,target.transform.position)<.25f){Hit(target,damage,item);break;}yield return null;}
            if(image!=null)Destroy(image.gameObject);
        }
        public OctoberItemZone Zone(Vector2 p,float radius,float duration,float dps,int id,bool pull=false,bool lure=false)
        {var z=new GameObject(Name(id)+" effect").AddComponent<OctoberItemZone>();z.Configure(this,p,radius,duration,dps,id,pull,lure);return z;}
        public SpriteRenderer MakeSprite(int id,string name,float width)
        {
            var g=new GameObject(name);g.transform.SetParent(transform,false);var r=g.AddComponent<SpriteRenderer>();r.sprite=OctoberArt.Get("OctoberContent/Items/Item"+id.ToString("00"));r.sortingOrder=120;
            if(r.sprite!=null)g.transform.localScale=Vector3.one*(width/r.sprite.bounds.size.x);return r;
        }
        public void Flash(Vector2 p,int id,float width=.7f){var image=MakeSprite(id,Name(id),width);image.transform.position=p;Destroy(image.gameObject,.35f);}
        public static string Name(int id){var item=Resources.Load<MerchantItemBlueprint>("OctoberContent/Catalog/Item"+id.ToString("00"));return item!=null?item.itemName:"아이템";}
        public void Slow(Component t,float strength,float duration)
        {if(t==null)return;float s=1-SlowMultiplier(1-strength);(t.GetComponent<HoneySlowStatus>()??t.gameObject.AddComponent<HoneySlowStatus>()).Apply(duration*StatusDuration,1-s);}
        public static bool IsSlow(Component t)=>t.GetComponent<HoneySlowStatus>()?.IsActive==true||(t.GetComponent<IceChillStatus>()?.Stacks??0)>0;
        public static bool IsBurning(Component t)=>t.GetComponent<BurnStatus>()!=null||(t.GetComponent<Ver4NeedleStatus>()?.BurnStacks??0)>0||(t.GetComponent<OctoberItemTargetStatus>()?.BurnUntil??0)>Time.time;
        static int StatusCount(Component t)=>(IsSlow(t)?1:0)+(IsBurning(t)?1:0)+(t.GetComponent<PoisonStatus>()!=null||(t.GetComponent<OctoberItemTargetStatus>()?.PoisonUntil??0)>Time.time?1:0)+(t.GetComponent<NeuralBlockedMonsterStatus>()?.Active==true?1:0);
        static void ConsumeSlow(Component t){var honey=t.GetComponent<HoneySlowStatus>();if(honey!=null){honey.enabled=false;Destroy(honey);}var ice=t.GetComponent<IceChillStatus>();if(ice!=null){ice.enabled=false;Destroy(ice);}}
    }
}
