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
        public int NormalFlatStart { get; private set; }
        public int PoolIndex { get; private set; }
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
            Level.monsters=source.monsters.Concat(new[]{new LevelBlueprint.MonstersContainer{monstersPrefab=source.monsters[0].monstersPrefab,monsterBlueprints=normals.ToArray()}}).ToArray();
            Level.monsterSpawnTable=BuildSpawnTable(source.monsterSpawnTable,NormalFlatStart,normals.Count);
            return Level;
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
            for(int i=0;i<3;i++)SilverRunRewarder.RewardPickup(UnityEngine.Random.Range(1,3));
            return true;
        }
        public bool TryEliteIndex(int normalIndex,out int eliteIndex)
        {
            int tier=normalIndex-NormalFlatStart;eliteIndex=tier>=0&&tier<8?NormalFlatStart+8+tier:-1;return eliteIndex>=0;
        }
        public bool IsNewElite(int flat)=>flat>=NormalFlatStart+8&&flat<NormalFlatStart+16;
        public bool Allowed(MonsterBlueprint bp)=>!(bp is AttackSpeedDebufferMonsterBlueprint)&&bp.name!="필드 미니 롤케이크";
        void OnDestroy(){if(Active==this)Active=null;foreach(var item in owned)if(item!=null)Destroy(item);}
    }
}
