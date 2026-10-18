using System.Collections;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public static class CurrencyJarArt
    {
        static Sprite[] jars, coins;
        public static Sprite Jar(bool silver) => (jars ?? (jars = Resources.LoadAll<Sprite>("CurrencyJars/Jars"))).FirstOrDefault(s => s.name == (silver ? "Silver" : "Gold"));
        public static Sprite Coin(bool silver) => (coins ?? (coins = Resources.LoadAll<Sprite>("CurrencyJars/Coins"))).FirstOrDefault(s => s.name == (silver ? "Silver" : "Gold"));
        public static SpriteRenderer Visual(Transform parent, string name, Sprite sprite, float height, int order)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.transform.SetParent(parent, false); r.sprite = sprite;
            r.transform.localScale = Vector3.one * height / Mathf.Max(.001f, sprite.bounds.size.y);
            r.sortingOrder = order; return r;
        }
    }

    // Deliberately not a Monster: no kill credit, XP, life-steal or monster silver rewards.
    public sealed class CurrencyJar : IDamageable
    {
        public const float AshiBodyHeight = .5185186f;
        public const float Height = AshiBodyHeight * .55f;
        public bool Silver { get; private set; }
        public bool Broken { get; private set; }
        public bool CanBreak => !Broken && level != null && !level.IsLevelEnded && !level.IsRunFlowPaused && !MiniStageRuntimeState.IsInsideMiniStage && Time.timeScale > 0;
        public int DropAmount { get; private set; }
        LevelManager level;
        SpriteRenderer visual, shadow;
        Collider2D hitbox;
        Sprite[] fragments;
        void OnDestroy()
        {
            if (fragments != null) foreach (var sprite in fragments) if (sprite != null) Destroy(sprite);
        }

        public static CurrencyJar Create(LevelManager level, Vector2 position, bool silver, Transform parent)
        {
            var go = new GameObject(silver ? "Silver chocolate jar" : "Gold chocolate jar");
            go.transform.SetParent(parent, false); go.transform.position = position;
            var jar = go.AddComponent<CurrencyJar>(); jar.level = level; jar.Silver = silver;
            jar.visual = CurrencyJarArt.Visual(go.transform, "Glass jar", CurrencyJarArt.Jar(silver), Height, 5);
            jar.shadow = CurrencyJarArt.Visual(go.transform, "Ground shadow", SupplyCapsuleArt.Droplet, Height * .22f, 0);
            jar.shadow.transform.localScale = new Vector3(Height * .84f, Height * .20f, 1);
            jar.shadow.color = new Color(.20f, .06f, .12f, .3f);
            GroundVisualSorting.Apply(jar.shadow, 0);
            var hit = new GameObject("Player attack hitbox"); hit.transform.SetParent(go.transform, false);
            hit.layer = LayerMask.NameToLayer("Monster Full");
            var box = hit.AddComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = new Vector2(jar.visual.bounds.size.x * .88f, Height * .90f); box.offset = Vector2.up * Height * .48f;
            jar.hitbox = box;
            var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            return jar;
        }
        public static int RollAmount(bool silver, float roll)
        {
            roll = Mathf.Clamp01(roll);
            return silver ? (roll < .50f ? 5 : roll < .88f ? 10 : 20) : (roll < .50f ? 10 : roll < .85f ? 30 : 50);
        }
        public override void Knockback(Vector2 amount) { }
        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false)
        {
            if (!CanBreak || damage <= 0) return;
            Broken = true; hitbox.enabled = false; DropAmount = RollAmount(Silver, Random.value);
            CurrencyJarDrop.Create(level, transform.position, Silver, DropAmount, transform.parent);
            StartCoroutine(Shatter());
        }
        IEnumerator Shatter()
        {
            Vector3 original = visual.transform.localScale;
            visual.transform.localScale = Vector3.Scale(original, new Vector3(1.12f, .88f, 1));
            yield return new WaitForSeconds(.045f);
            visual.enabled = false; shadow.enabled = false;
            var sprite = visual.sprite;
            var pieces = new SpriteRenderer[9]; var origins = new Vector3[9]; var velocities = new Vector2[9];
            fragments = new Sprite[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                // Slice only the glass rim/sides and cork; never duplicate the painted coins.
                var rect = sprite.rect;
                var uv = i == 8 ? new Rect(.26f, .87f, .48f, .12f) : new Rect(i % 2 == 0 ? .06f : .83f, .13f + (i / 2) * .15f, .10f, .13f);
                var piece = Sprite.Create(sprite.texture, new Rect(rect.x + rect.width * uv.x, rect.y + rect.height * uv.y, rect.width * uv.width, rect.height * uv.height), Vector2.one * .5f, sprite.pixelsPerUnit);
                fragments[i] = piece;
                pieces[i] = CurrencyJarArt.Visual(transform, "Glass fragment", piece, i == 8 ? Height * .16f : Height * .15f, 6);
                origins[i] = new Vector3((i % 2 == 0 ? -.2f : .2f) * Height, Height * .45f, 0);
                velocities[i] = new Vector2((i % 2 == 0 ? -1 : 1) * Random.Range(.15f, .55f), Random.Range(.35f, .8f));
            }
            float t = 0;
            while (t < .65f)
            {
                if (level.IsRunFlowPaused || MiniStageRuntimeState.IsInsideMiniStage) { yield return null; continue; }
                t += Time.deltaTime;
                for (int i = 0; i < pieces.Length; i++)
                {
                    pieces[i].transform.localPosition = origins[i] + (Vector3)(velocities[i] * t + Vector2.down * (1.4f * t * t));
                    pieces[i].transform.localRotation = Quaternion.Euler(0, 0, t * (i % 2 == 0 ? -240 : 240));
                    pieces[i].color = new Color(1, 1, 1, 1 - Mathf.InverseLerp(.25f, .65f, t));
                }
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    // One award owns a small visible stack: splitting sprites must not multiply or round rewards.
    public sealed class CurrencyJarDrop : MonoBehaviour
    {
        public int Amount { get; private set; }
        public bool Silver { get; private set; }
        public bool Collected { get; private set; }
        public bool Landed => age >= .6f;
        LevelManager level;
        float age;
        bool attracting;
        SpriteRenderer[] coins;
        Vector3[] endpoints;
        public static CurrencyJarDrop Create(LevelManager owner, Vector2 position, bool silver, int amount, Transform parent)
        {
            var go = new GameObject(silver ? "Silver chocolate coins" : "Gold chocolate coins");
            go.transform.SetParent(parent, false); go.transform.position = position;
            var drop = go.AddComponent<CurrencyJarDrop>(); drop.level = owner; drop.Amount = amount; drop.Silver = silver;
            drop.coins = new SpriteRenderer[3]; drop.endpoints = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                drop.coins[i] = CurrencyJarArt.Visual(go.transform, "Chocolate coin", CurrencyJarArt.Coin(silver), .115f, 7 + i);
                drop.endpoints[i] = new Vector3((i - 1) * .12f, i == 1 ? .03f : -.02f, 0);
            }
            return drop;
        }
        void Update()
        {
            if (Collected || Time.timeScale <= 0 || level == null || level.IsLevelEnded || level.IsRunFlowPaused || MiniStageRuntimeState.IsInsideMiniStage || !level.PlayerCharacter.IsAlive) return;
            age += Time.deltaTime;
            if (!Landed)
            {
                float t = Mathf.Clamp01(age / .6f);
                for (int i = 0; i < coins.Length; i++)
                    coins[i].transform.localPosition = Vector3.Lerp(Vector3.up * CurrencyJar.Height * .7f, endpoints[i], t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (.25f + i * .04f);
                return;
            }
            var actor = level.PlayerCharacter;
            if (!attracting)
            {
                for (int i = 0; i < coins.Length; i++) coins[i].transform.localPosition = endpoints[i];
                var area = actor.CollectableCollider;
                attracting = area != null && area.enabled && Vector2.Distance(area.ClosestPoint(transform.position), transform.position) < .12f;
            }
            if (!attracting) return;
            transform.position = Vector3.MoveTowards(transform.position, actor.CenterTransform.position, Time.deltaTime * 5f);
            if (Vector2.Distance(transform.position, actor.CenterTransform.position) > .06f) return;
            Collected = true;
            if (Silver) SilverRunRewarder.RewardPickup(Amount);
            else
            {
                int amount = StageEventRuntimeModifiers.ApplyCoinValueMultiplier(Amount);
                var stats = actor.GetComponent<PlayerGeneralStatRuntime>();
                if (stats != null) amount = stats.ApplyGoldGainMultiplier(amount);
                FindObjectOfType<StatsManager>()?.IncreaseCoinsGained(amount);
                HungrySpiritPickupSignal.NotifyExpOrCoinPickedUp(actor);
            }
            GameAudioManager.PlaySfx(Silver ? GameAudioManager.GameSfxId.SilverPickup : GameAudioManager.GameSfxId.GoldPickup);
            Destroy(gameObject);
        }
    }
}
