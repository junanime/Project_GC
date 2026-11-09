using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    // Runtime-only copies keep stage one and serialized mini-stage references intact.
    public sealed class ColonMonsterRoster : MonoBehaviour
    {
        public sealed class Entry
        {
            public string id, name;
            public float height, hp, speed;
            public Sprite sprite;
            public MonsterBlueprint blueprint;
            public bool elite;
        }
        public static ColonMonsterRoster Active { get; private set; }
        public static bool InColon => Active != null && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.IsColon;
        public const float SlimeHeight = .3f;
        public static readonly string[] NormalIds = { "N01", "N06", "N07", "N02", "N04", "N08", "N03", "N05" };
        public static readonly float[] IntroductionSeconds = { 0, 90, 180, 270, 360, 450, 570, 690 };
        public static readonly float[] NormalHealth = { 70, 85, 115, 155, 210, 290, 420, 600 };
        public readonly List<Entry> Normals = new List<Entry>();
        public readonly List<Entry> Elites = new List<Entry>();
        public readonly List<Entry> Specials = new List<Entry>();
        readonly Dictionary<MonsterBlueprint, MonsterBlueprint> replacements = new Dictionary<MonsterBlueprint, MonsterBlueprint>();
        readonly Dictionary<MonsterBlueprint, Entry> entries = new Dictionary<MonsterBlueprint, Entry>();
        readonly List<ScriptableObject> owned = new List<ScriptableObject>();
        public LevelBlueprint Level { get; private set; }
        public bool DirectStart { get; private set; }
        public float DirectMiniBossHealth { get; private set; }
        public int NormalFlatStart { get; private set; }
        public int PoolIndex { get; private set; }
        public static readonly string[] NewKinds={"gas","yogurt","stitch","salt","beetle"};
        public readonly List<MonsterBlueprint> NewSpecials=new List<MonsterBlueprint>();
        public float EntryDamageMultiplier { get; private set; }
        public float EntryAttackSpeedMultiplier { get; private set; }
        LevelManager owner;
        static readonly Dictionary<string, float> Ratios = new Dictionary<string, float>
        {
            {"N01",1.2f},{"N06",1.2f},{"N07",1.4f},{"N02",1.95f},{"N04",1.95f},
            {"N08",2.08f},{"N03",1.7f},{"N05",1.7f},{"S01",1.7f},{"S02",1.95f},
            {"S03",2.21f},{"S04",1.8f},{"S05",1.8f},{"S06",2.94f},{"Pea",1.05f}
        };
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"N01","동글 똥알"},{"N06","검은콩 반질이"},{"N07","한바퀴 똥말이"},{"N02","표고 갓둥이"},
            {"N04","호두 주름이"},{"N08","갈라진 똥돌"},{"N03","양배추 겹겹이"},{"N05","세층 똥탑"},
            {"S01","옥수수 팝콘포수"},{"S02","갈라진 밤알"},{"S03","완두 꼬투리 형제"},
            {"S04","검은목이 팔랑버퍼"},{"S05","흰목이 방패균"},{"S06","겹꼬투리 콩군단"},{"Pea","꼬투리 아기콩"}
        };
        T Copy<T>(T source) where T : ScriptableObject { var copy = Instantiate(source); owned.Add(copy); return copy; }
        public static LevelBlueprint Create(LevelManager manager)
        {
            var roster = manager.GetComponent<ColonMonsterRoster>() ?? manager.gameObject.AddComponent<ColonMonsterRoster>();
            return roster.Build(manager);
        }
        public LevelBlueprint Build(LevelManager manager)
        {
            if (Level != null) throw new InvalidOperationException("Colon roster already created");
            owner = manager; Active = this;
            DirectStart=StageProgression.Ensure(manager).IsDirectColonStart;
            var source = manager.CurrentLevelBlueprint;
            Level = Copy(source); Level.name = "Stage 2 · 대장 · 8종 단계별 스폰";
            EntryDamageMultiplier = manager.PlayerCharacter.DamageMultiplier;
            EntryAttackSpeedMultiplier = manager.PlayerCharacter.AttackSpeedMultiplier;
            var flat = source.monsters.SelectMany(p => p.monsterBlueprints).ToArray();
            PoolIndex = source.monsters.Length; NormalFlatStart = flat.Length;
            var normals = new List<MonsterBlueprint>();
            float[] speeds = { .78f, .86f, .84f, .92f, .85f, .92f, .88f, 1f };
            float[] damage = { 7, 8, 9, 11, 12, 14, 16, 18 };
            for (int i=0;i<8;i++)
            {
                var bp = Copy((MeleeMonsterBlueprint)flat[0]);
                bp.hp = NormalHealth[i]; bp.atk = damage[i]; bp.movespeed = speeds[i]; bp.acceleration = 5;
                SetRewards(bp, i); var entry = Register(bp, NormalIds[i]); Normals.Add(entry); normals.Add(bp);
            }
            int[] stageOneNormal = { 0, 1, 4, 5, 6, 7 };
            int[] mappedNormal = { 0, 1, 2, 3, 5, 7 };
            for(int i=0;i<stageOneNormal.Length;i++)replacements[flat[stageOneNormal[i]]] = Normals[mappedNormal[i]].blueprint;
            for (int i=0;i<8;i++)
            {
                var elite = Copy((EliteMonsterBlueprint)flat[8]);
                elite.sourceNormalBlueprint = (MeleeMonsterBlueprint)Normals[i].blueprint;
                elite.useSourceVisual = true; elite.useSourceCombatStats = false; elite.useSourceLootTables = false;
                elite.hp = Normals[i].hp; elite.hpMultiplier = 4.5f; elite.scaleMultiplier = 2f;
                elite.atk = damage[i] * 1.45f; elite.atkspeed = Normals[i].blueprint.atkspeed;
                elite.movespeed = speeds[i] * 1.06f; elite.acceleration = 5; elite.meleeLayer = ((MeleeMonsterBlueprint)Normals[i].blueprint).meleeLayer;
                elite.silverRewardCalls = 3; elite.guaranteedExtraCoinCount = 4; elite.guaranteedExtraCoinType = CoinType.Gold5;
                elite.gemLootTable = Gems(new Loot<GemType>{item=GemType.Green10,dropChance=.25f},new Loot<GemType>{item=GemType.Red50,dropChance=.75f});
                elite.coinLootTable = Coins(new Loot<CoinType>{item=CoinType.Gold5,dropChance=1}); elite.debugLog = false;
                var entry = Register(elite, NormalIds[i], 2f, true); Elites.Add(entry); normals.Add(elite);
            }
            for(int i=8;i<=13;i++)replacements[flat[i]]=Elites[Mathf.Min(i-8,7)].blueprint;
            replacements[flat[26]]=Elites[7].blueprint;
            AddSpecial(flat[14],"S01",140,0,16);
            var spore=(ExplodingMonsterBlueprint)AddSpecial(flat[15],"S02",90,2.8f,0);
            spore.explosionDamage=32; spore.explosionRadius=.65f; spore.armDelay=.4f; spore.debugLog=false;
            var sniper=(SniperMonsterBlueprint)replacements[flat[14]];
            sniper.fireSprites=new[]{sniper.resultSprite,sniper.resultSprite,sniper.resultSprite,sniper.resultSprite};
            sniper.aimDuration=1.4f;sniper.lockDuration=.4f;sniper.attackCooldown=2.2f;
            AddSugar((SugarCubeMonsterBlueprint)flat[2],"S03",200,false);
            AddSugar((SugarCubeMonsterBlueprint)flat[3],"S06",720,true);
            AddSpecial(flat[21],"S04",260,.65f,0); AddSpecial(flat[22],"S05",310,.6f,0);
            foreach(int index in new[]{24,25})
            {
                var bp=Copy(flat[index]);bp.hp=index==24?230:180;bp.atk=12;
                bp.gemLootTable=Gems(new Loot<GemType>{item=GemType.Green10,dropChance=1});
                replacements[flat[index]]=bp;
            }
            // Clots are stationary mini-stage entrances, not enemy wave units.
            var clot=Copy(flat[19]);clot.hp=180;replacements[flat[19]]=clot;
            if(DirectStart)ApplyDirectStartBalance(flat);
            float[] specialHp={220,170,280,150,190};float[] specialSpeed={.78f,1.05f,.7f,.8f,.95f};
            string[] specialNames={"가스주머니균","요구르트 발효균","균사 봉합사","소금결정균","똥폭탄 쇠똥구리"};
            for(int i=0;i<5;i++){
                var bp=Copy((MeleeMonsterBlueprint)flat[0]);bp.hp=DirectStart?Mathf.Round(specialHp[i]*.35f):specialHp[i];bp.atk=DirectStart?5:10;bp.movespeed=specialSpeed[i];
                string id="S"+(7+i).ToString("00");Names[id]=specialNames[i];Ratios[id]=i==4?1.8f:1.6f;
                bp.gemLootTable=Gems(new Loot<GemType>{item=GemType.Green10,dropChance=1});bp.coinLootTable=Coins(new Loot<CoinType>{item=CoinType.Gold5,dropChance=.5f});
                Specials.Add(Register(bp,id));NewSpecials.Add(bp);normals.Add(bp);
            }
            Names["S12"]="비트 두더지";Ratios["S12"]=1.8f;Specials.Add(Register(replacements[flat[25]],"S12"));
            (GetComponent<ColonSpecialDirector>()??gameObject.AddComponent<ColonSpecialDirector>()).Configure(this,manager);
            Level.monsters=source.monsters.Concat(new[]{new LevelBlueprint.MonstersContainer{monstersPrefab=source.monsters[0].monstersPrefab,monsterBlueprints=normals.ToArray()}}).ToArray();
            Level.monsterSpawnTable=BuildSpawnTable(source.monsterSpawnTable,NormalFlatStart,normals.Count);
            if(DirectStart)
            {
                Level.name="Stage 2 · 대장 · 기본 장비 직접 입장";
                // Match stage-one tier timing as well as HP; otherwise late colon types
                // arriving early would exceed the requested 10-20% difficulty step.
                Level.monsterSpawnTable.spawnChanceKeyframes=source.monsterSpawnTable.spawnChanceKeyframes.Select(key=>
                {
                    var weights=new float[NormalFlatStart+normals.Count];
                    for(int i=0;i<DirectNormalSources.Length;i++)
                    {int index=DirectNormalSources[i];if(index<key.spawnChances.Length)weights[NormalFlatStart+i]=key.spawnChances[index];}
                    return new MonsterSpawnTable.SpawnChanceKeyframe{t=key.t,spawnChances=weights};
                }).ToArray();
                foreach(var key in Level.monsterSpawnTable.spawnRateKeyframes)key.spawnRate*=DirectSpawnMultiplier(key.t);
                Level.normalSpawnPopulationLimit=Mathf.CeilToInt(source.normalSpawnPopulationLimit*1.05f);
            }
            return Level;
        }
        // This profile is selected only by the temporary preparation-stage picker.
        // Copies are modified here; stage-one assets and the grown-player colon run stay intact.
        public static float DirectHealthMultiplier(int tier)=>Mathf.Lerp(1.10f,1.20f,Mathf.Clamp01(tier/7f));
        public static float DirectSpawnMultiplier(float normalizedTime)=>Mathf.Lerp(1f,1.05f,Mathf.Clamp01(normalizedTime));
        public static readonly int[] DirectNormalSources={0,1,4,5,6,7,31,32};
        void ApplyDirectStartBalance(MonsterBlueprint[] flat)
        {
            for(int i=0;i<Normals.Count;i++)
            {
                var baseline=flat[DirectNormalSources[i]];var bp=Normals[i].blueprint;
                bp.hp=Mathf.Round(baseline.hp*DirectHealthMultiplier(i)*10f)/10f;bp.atk=baseline.atk;
                bp.movespeed=baseline.movespeed;bp.atkspeed=baseline.atkspeed;bp.acceleration=baseline.acceleration;
                bp.gemLootTable=baseline.gemLootTable;bp.coinLootTable=baseline.coinLootTable;
                var elite=(EliteMonsterBlueprint)Elites[i].blueprint;
                var baseElite=(EliteMonsterBlueprint)flat[i<6?8+i:26];
                elite.hp=bp.hp;elite.hpMultiplier=baseElite.hpMultiplier;
                elite.atk=bp.atk*1.2f;elite.movespeed=bp.movespeed*1.04f;elite.atkspeed=bp.atkspeed;
                elite.gemLootTable=baseline.gemLootTable;elite.coinLootTable=baseline.coinLootTable;
                elite.silverRewardCalls=baseElite.silverRewardCalls;
                elite.guaranteedExtraCoinCount=baseElite.guaranteedExtraCoinCount;elite.guaranteedExtraCoinType=baseElite.guaranteedExtraCoinType;
            }
            foreach(int index in new[]{2,3,14,15,21,22,24,25})
            {
                var source=flat[index];var bp=replacements[source];
                bp.hp=Mathf.Ceil(source.hp*1.15f);bp.atk=source.atk;
                bp.gemLootTable=source.gemLootTable;bp.coinLootTable=source.coinLootTable;
                if(bp is SugarCubeMonsterBlueprint sugar&&source is SugarCubeMonsterBlueprint original)
                    BalanceChildren(sugar,original);
            }
            var spore=(ExplodingMonsterBlueprint)replacements[flat[15]];
            spore.explosionDamage=((ExplodingMonsterBlueprint)flat[15]).explosionDamage;
            replacements[flat[19]].hp=Mathf.Ceil(flat[19].hp*1.10f);
            var chameleon=Resources.Load<MonsterBlueprint>("Chameleons/DriftBlueprint");
            if(chameleon==null)throw new InvalidOperationException("Missing stage-one chameleon baseline");
            DirectMiniBossHealth=Mathf.Ceil(chameleon.hp*1.20f);
            foreach(var pair in entries){pair.Value.hp=pair.Key.hp;pair.Value.speed=pair.Key.movespeed;}
        }
        static void BalanceChildren(SugarCubeMonsterBlueprint copy,SugarCubeMonsterBlueprint source)
        {
            if(copy.splitChildBlueprint==null||source.splitChildBlueprint==null)return;
            var child=copy.splitChildBlueprint;var baseline=source.splitChildBlueprint;
            child.hp=Mathf.Ceil(baseline.hp*1.15f);child.atk=baseline.atk;child.movespeed=baseline.movespeed;
            child.gemLootTable=baseline.gemLootTable;child.coinLootTable=baseline.coinLootTable;
            BalanceChildren(child,baseline);
        }
        MonsterBlueprint AddSpecial(MonsterBlueprint source,string id,float hp,float speed,float damage)
        {
            var bp=Copy(source);bp.hp=hp;bp.movespeed=speed;bp.atk=damage;
            bp.gemLootTable=Gems(new Loot<GemType>{item=GemType.Green10,dropChance=1});
            bp.coinLootTable=Coins(new Loot<CoinType>{item=CoinType.Gold5,dropChance=.6f});
            replacements[source]=bp;Specials.Add(Register(bp,id));return bp;
        }
        SugarCubeMonsterBlueprint AddSugar(SugarCubeMonsterBlueprint source,string id,float hp,bool elite)
        {
            var bp=(SugarCubeMonsterBlueprint)AddSpecial(source,id,hp,.85f,elite?22:12);
            bp.visualScaleMultiplier=1;bp.useSugarCubeWalkAnimationOverride=false;bp.sugarCubeWalkSpriteSequence=bp.walkSpriteSequence;bp.debugLog=false;
            bp.shadowGroundCenterUV=new Vector2(.5f,.08f);bp.shadowGroundWidthUV=.8f;
            if(source.splitChildBlueprint!=null)
            {
                var child=Copy(source.splitChildBlueprint);child.hp=elite?180:55;child.atk=elite?12:8;child.movespeed=.95f;
                child.visualScaleMultiplier=1;child.useSugarCubeWalkAnimationOverride=false;child.debugLog=false;
                child.gemLootTable=Gems(new Loot<GemType>{item=GemType.Green10,dropChance=1});child.coinLootTable=Coins();
                Register(child,elite?"S03":"Pea",elite?.72f:1);child.sugarCubeWalkSpriteSequence=child.walkSpriteSequence;
                bp.splitChildBlueprint=child;bp.splitChildCount=2;
                if(elite&&source.splitChildBlueprint.splitChildBlueprint!=null)
                {
                    var baby=Copy(source.splitChildBlueprint.splitChildBlueprint);baby.hp=50;baby.atk=8;baby.movespeed=1;
                    baby.visualScaleMultiplier=1;baby.useSugarCubeWalkAnimationOverride=false;baby.debugLog=false;baby.splitChildBlueprint=null;
                    baby.gemLootTable=Gems(new Loot<GemType>{item=GemType.Green10,dropChance=1});baby.coinLootTable=Coins();
                    Register(baby,"Pea");baby.sugarCubeWalkSpriteSequence=baby.walkSpriteSequence;child.splitChildBlueprint=baby;child.splitChildCount=2;
                }
                else child.splitChildBlueprint=null;
            }
            return bp;
        }
        Entry Register(MonsterBlueprint bp,string id,float scale=1,bool elite=false)
        {
            var sprite=id=="Pea"?Resources.LoadAll<Sprite>("ColonMonsters/Extras").FirstOrDefault(s=>s.name=="Pea"):Resources.Load<Sprite>("ColonMonsters/"+id);
            if(id=="S12")sprite=Resources.Load<Sprite>("ColonSpecials/mole");
            for(int i=0;i<5;i++)if(id=="S"+(7+i).ToString("00"))sprite=ColonSpecialEffects.Art(NewKinds[i],0);
            if(sprite==null)throw new InvalidOperationException("Missing colon monster art: "+id);
            bp.name=(elite?"엘리트 ":"")+Names[id];bp.resultSprite=sprite;bp.walkSpriteSequence=new[]{sprite};bp.walkFrameTime=.15f;
            bp.useGroundShadowFootprint=true;bp.groundShadowCenterUV=new Vector2(.5f,.06f);bp.groundShadowSizeUV=new Vector2(.72f,.13f);bp.preserveGroundShadowHeight=false;
            var entry=new Entry{id=id,name=bp.name,sprite=sprite,height=SlimeHeight*Ratios[id]*scale,hp=bp.hp,speed=bp.movespeed,blueprint=bp,elite=elite};entries[bp]=entry;return entry;
        }
        static LootTable<GemType> Gems(params Loot<GemType>[] rows)=>new LootTable<GemType>{lootTable=rows};
        static LootTable<CoinType> Coins(params Loot<CoinType>[] rows)=>new LootTable<CoinType>{lootTable=rows};
        static void SetRewards(MonsterBlueprint bp,int tier)
        {
            // XP increases with the fixed threat tiers; no dependence on the current player's upgrades.
            // Stage-one carryover levels need larger pickups to keep earning choices.
            float blue=Mathf.Lerp(.50f,.10f,tier/7f),red=Mathf.Lerp(0,.10f,tier/7f);
            bp.gemLootTable=Gems(new Loot<GemType>{item=GemType.Blue2,dropChance=blue},new Loot<GemType>{item=GemType.Green10,dropChance=1-blue-red},new Loot<GemType>{item=GemType.Red50,dropChance=red});
            bp.coinLootTable=Coins(new Loot<CoinType>{item=CoinType.Bronze1,dropChance=.36f},new Loot<CoinType>{item=CoinType.Silver2,dropChance=.18f},new Loot<CoinType>{item=CoinType.Gold5,dropChance=.06f});
        }
        public static MonsterSpawnTable BuildSpawnTable(MonsterSpawnTable source,int offset,int additional)
        {
            var keys=new List<MonsterSpawnTable.SpawnChanceKeyframe>();
            for(int tier=0;tier<8;tier++)
            {
                var weights=new float[offset+additional];
                for(int i=0;i<=tier;i++)weights[offset+i]=i==tier?.64f:i==tier-1?.24f:.12f/Mathf.Max(1,tier-1);
                // Hold each introduction until its intended time, then blend during a short 30-second handover.
                if(tier>0)keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe{t=IntroductionSeconds[tier]/900f,spawnChances=(float[])keys[keys.Count-1].spawnChances.Clone()});
                keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe{t=(IntroductionSeconds[tier]+(tier==0?0:30))/900f,spawnChances=weights});
            }
            keys.Add(new MonsterSpawnTable.SpawnChanceKeyframe{t=1,spawnChances=(float[])keys[keys.Count-1].spawnChances.Clone()});
            return new MonsterSpawnTable{spawnChanceKeyframes=keys.ToArray(),spawnRateKeyframes=source.spawnRateKeyframes.Select(k=>new MonsterSpawnTable.SpawnRateKeyframe{t=k.t,spawnRate=k.spawnRate}).ToArray()};
        }
        public MonsterBlueprint Resolve(MonsterBlueprint bp)=>replacements.TryGetValue(bp,out var mapped)?mapped:bp;
        public bool TryGet(MonsterBlueprint bp,out Entry entry)=>entries.TryGetValue(bp,out entry);
        public bool IsSpecial(MonsterBlueprint bp)=>bp!=null&&entries.TryGetValue(bp,out var entry)&&
            (entry.id.StartsWith("S",StringComparison.Ordinal)||entry.id=="Pea");
        public bool RewardEliteSilver(MonsterBlueprint bp)
        {
            if(bp==null||!entries.TryGetValue(bp,out var entry)||!entry.elite)return false;
            // Current fields may only install the pickup tracker when a jar is broken.
            // Pay these elites explicitly without enabling silver for every ordinary kill.
            for(int i=0;i<((EliteMonsterBlueprint)bp).silverRewardCalls;i++)SilverRunRewarder.RewardPickup(UnityEngine.Random.Range(1,3));
            return true;
        }
        public bool TryEliteIndex(int normalIndex,out int eliteIndex)
        {
            int tier=normalIndex-NormalFlatStart;eliteIndex=tier>=0&&tier<8?NormalFlatStart+8+tier:-1;return eliteIndex>=0;
        }
        public bool EliteUnlocked(int tier,float seconds)
        {
            if(tier<0||tier>=Normals.Count)return false;
            if(!DirectStart)return IntroductionSeconds[tier]<=seconds;
            return Level.monsterSpawnTable.GetProbability(Mathf.Clamp01(seconds/Level.levelTime),NormalFlatStart+tier)>0;
        }
        public bool IsNewElite(int flat)=>flat>=NormalFlatStart+8&&flat<NormalFlatStart+16;
        public bool Allowed(MonsterBlueprint bp)=>!(bp is AttackSpeedDebufferMonsterBlueprint)&&bp.name!="필드 미니 롤케이크";
        void OnDestroy(){if(Active==this)Active=null;foreach(var item in owned)if(item!=null)Destroy(item);}
    }
}
