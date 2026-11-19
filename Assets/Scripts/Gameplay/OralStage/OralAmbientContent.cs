using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Persistent optional props coexist with the disruptive oral event timeline.</summary>
    [DefaultExecutionOrder(12300)]
    public sealed class OralAmbientContent : MonoBehaviour
    {
        public static OralAmbientContent Instance {get;private set;}
        public LevelManager Level {get;private set;}
        public float PlayerMoveMultiplier => pushing ? .7f : 1f;
        public IReadOnlyList<OralRollingStone> Stones => stones;
        public bool RockActive => stones.Count > 0;
        public Vector2 RockPosition => RockActive ? stones[0].Position : Vector2.zero;
        public int RockHits {get;private set;}
        public OralFoodBreaks FoodBreaks {get;private set;}
        public bool GuardActive => guard!=null;
        public Vector2 GuardPosition => guard!=null?guard.Position:Vector2.zero;
        public Vector2 GuardGoal => guard!=null?guard.Route.Landing:Vector2.zero;
        public OralGuardEscort Guard => guard;
        public int GuardFailed {get;private set;}
        bool guardResultRecorded;
        public int GuardCompleted {get;private set;}
        public Chest GuardReward {get;private set;}
        public const float GuardLifetime=55f;
        FiniteStomachMap map; Character player;
        OralGuardEscort guard;
        readonly List<Monster> scratch = new List<Monster>();
        readonly List<OralRollingStone> stones = new List<OralRollingStone>(8);
        static readonly Dictionary<string,Sprite> art = new Dictionary<string,Sprite>();
        bool pushing, visualsHidden;
        float ashiHeight=.6f;
        public void Configure(LevelManager level)
        {
            if(Instance!=null&&Instance!=this)Instance.ClearAll();Instance=this;Level=level;player=level.PlayerCharacter;map=FiniteStomachMap.Instance;
            FoodBreaks = new OralFoodBreaks(transform);
            var renderer=player.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(renderer!=null)ashiHeight=Mathf.Clamp(PromptSpriteGeometry.WorldBounds(renderer).size.y,.3f,2);
            PlaceStones();
        }
        public static Sprite EventSprite(string id)
        {
            if(art.TryGetValue(id,out var result)&&result!=null)return result;
            result=Resources.Load<Sprite>("OralMap/Events/"+id);
            if(result==null){var texture=Resources.Load<Texture2D>("OralMap/Events/"+id);if(texture!=null)result=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,100);}
            art[id]=result;return result;
        }
        void PlaceStones()
        {
            var layout = Level.GetComponent<FieldObjectLayout>();
            var random = new System.Random((layout != null ? layout.Seed : 0) ^ 97313);
            Vector2 center = OralMapGeometry.TongueCenter(map), radii = OralMapGeometry.TongueRadii(map);
            // A nearby first stone, then one in each sector, all on walkable tongue.
            float rotation = (float)random.NextDouble() * 360f;
            for (int i = 0; i < 8; i++)
            {
                for (int trial = 0; trial < 36; trial++)
                {
                    float angle = i == 0 ? trial * 51f : rotation + (i - 1) * (360f / 7f) + (float)random.NextDouble()*44f-22f;
                    Vector2 direction = OralMapGeometry.Rotate(Vector2.right, angle);
                    Vector2 spot = i == 0 ? player.Position + direction * (2.6f + trial * .12f)
                        : center + Vector2.Scale(direction, radii) * (.34f + .51f * (float)random.NextDouble());
                    if (OralMapGeometry.TongueDistance(map, spot, out _) < 1.4f || !ClearSpot(spot, layout)) continue;
                    AddStone(spot);
                    break;
                }
            }
        }

        bool ClearSpot(Vector2 spot, FieldObjectLayout layout)
        {
            foreach (var stone in stones) if ((spot - stone.Position).sqrMagnitude < 9f) return false;
            if (layout != null)
                foreach (var slot in layout.Slots)
                    if ((spot-slot.position).sqrMagnitude < 1.8f*1.8f) return false;
            return (spot-player.Position).sqrMagnitude > 1.8f*1.8f;
        }

        void AddStone(Vector2 spot)
        {
            var sprite = EventSprite("Rock");
            if (sprite != null) stones.Add(new OralRollingStone(transform, spot, sprite, ashiHeight));
        }

        public bool BeginBoulder()
        {
            // Explicitly place a single fixture when testing a cleared map.
            if (stones.Count != 0 || player == null || map == null) return false;
            AddStone(OralMapGeometry.Clamp(map, player.Position+Vector2.right*1.4f,1f,OralZone.Tongue));
            return RockActive;
        }

        public bool TryHoneyLane(out Vector2 center, out Vector2 direction)
        {
            center = direction = Vector2.zero;
            if (player == null) return false;
            OralRollingStone nearest = null;
            float distance = 64f;
            foreach (var stone in stones)
            {
                float d = (stone.Position-player.Position).sqrMagnitude;
                if (!stone.Falling && d < distance) { distance = d; nearest = stone; }
            }
            if (nearest == null) return false;
            var aim = (nearest.Position-player.Position).normalized;
            if (aim.sqrMagnitude < .1f) aim = Vector2.right;
            var layout=Level.GetComponent<FieldObjectLayout>();
            for (int i=0;i<8;i++)
            {
                var axis=OralMapGeometry.Rotate(aim,(i%2==0?1:-1)*((i+1)/2)*37f);
                var target=nearest.Position + axis*3.4f;
                if (OralMapGeometry.TongueDistance(map,target+axis*1.2f,out _) < 1.2f || !ClearSpot(target,layout)) continue;
                center=target;direction=axis;return true;
            }
            return false;
        }
        public void PushAlongDash(Character actor, Vector2 from, Vector2 to, int dashId)
        {
            var stage=OralStageRuntime.Instance;
            if (actor!=player || stage==null || map==null || !map.IsOral || (to-from).sqrMagnitude < .00001f ||
                stage.IsAirborne || stage.OnCeiling || OralEventArt.Paused(Level)) return;
            foreach (var stone in stones) stone.PushAlongDash(from,to,dashId);
        }

        public bool BeginGuard()
        {
            if(guard!=null||player==null||map==null)return false;
            var go=new GameObject("민트 구강수호대 · 어금니 호위");go.transform.SetParent(transform,false);
            var escort=go.AddComponent<OralGuardEscort>();
            var frames=OralGuardArt.Frames;
            if(!escort.Configure(Level,map,player.Position,ashiHeight,frames!=null&&frames.Length>0?frames[0]:null))
            {go.SetActive(false);Destroy(go);return false;}
            guard=escort;guardResultRecorded=false;GuardReward=null;return true;
        }
        void Update()
        {
            if(Level==null||player==null||map==null)return;
            bool hidden=MiniStageRuntimeState.IsInsideMiniStage||StageEntryLoading.IsLoading||player.IsPortalTravelling||(Level.GetComponent<StageProgression>()?.Travelling??false)||(OralFieldEvents.Instance?.OnCeiling??false);
            if (hidden != visualsHidden)
            {
                visualsHidden = hidden;
                foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.forceRenderingOff = hidden;
            }
            bool paused=hidden||OralEventArt.Paused(Level);
            if(guard!=null)guard.SetSuspended(paused,hidden);
            if(paused){SetPush(false);return;}
            float dt = Time.deltaTime;
            FoodBreaks.Tick(dt);
            bool push = false, haveSnapshot = false;
            for (int i=stones.Count-1;i>=0;i--)
            {
                var stone = stones[i];
                if (!stone.NeedsUpdate(player)) continue;
                if (!haveSnapshot) { Snapshot(); haveSnapshot = true; }
                float remaining = Mathf.Min(dt,.25f);
                while (remaining > 0 && !stone.Finished)
                {
                    float step = Mathf.Min(.05f,remaining);
                    if(stone.Tick(step,map,player,scratch,out int hits)) push=true;
                    RockHits+=hits; remaining-=step;
                }
                if (!stone.Finished) continue;
                stone.Release(); stones.RemoveAt(i);
            }
            SetPush(push);
            if(guard!=null)TickGuard(dt);
        }
        void SetPush(bool value){if(pushing==value)return;pushing=value;if(player!=null)player.UpdateMoveSpeed();}
        void TickGuard(float dt)
        {
            guard.Tick(dt);
            if(!guardResultRecorded&&(guard.Complete||guard.Failed))
            {
                guardResultRecorded=true;
                if(guard.Complete)
                {
                    GuardCompleted++;
                    var blueprint=RemakeBalance.Current?.levelUpChest;
                    if(blueprint!=null&&Level.EntityManager!=null)
                    {GuardReward=Level.EntityManager.SpawnChest(blueprint,guard.Route.Landing);if(GuardReward!=null)GuardReward.CompleteInitialPlacement();}
                    GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.ChestOpen);
                }
                else GuardFailed++;
            }
            if(guard.ReadyToRemove)RemoveGuard();
        }
        void Snapshot(){scratch.Clear();if(Level.EntityManager?.LivingMonsters!=null)foreach(var m in Level.EntityManager.LivingMonsters)scratch.Add(m);}
        void RemoveGuard(){if(guard!=null){guard.gameObject.SetActive(false);Destroy(guard.gameObject);}guard=null;}
        public void ClearAll()
        {
            foreach (var stone in stones) stone.Release();
            stones.Clear(); RockHits=0;
            RemoveGuard(); SetPush(false);
        }
        public void CancelAll(){ClearAll();}
        void OnDisable(){ClearAll();}
        void OnDestroy(){ClearAll();if(Instance==this)Instance=null;}
    }
}
