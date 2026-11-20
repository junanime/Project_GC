using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class GStarCombatFeedbackSmoke
    {
        const string Folder = "field-feedback";

        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("GSTAR_FIELD: " + label);
            Debug.Log("GSTAR_FIELD_PASS " + label);
        }

        static void Frame(string name)
        {
            GStarUISmoke.Capture(Folder + "/" + name, 960, 540);
        }

        public static IEnumerator CheckField(LevelManager level)
        {
            var player = level.PlayerCharacter;
            var stats = Object.FindObjectOfType<StatsManager>();
            var dialog = level.EntityManager.AbilitySelectionDialog;
            var prefs = GamePreferences.Current.Copy();
            var display = prefs.Copy();
            display.reducedMotion = false;
            GamePreferences.Apply(display, false);
            Directory.CreateDirectory("Library/GStarUIQA/" + Folder);
            int oldRate = Time.captureFramerate;
            Time.captureFramerate = 24;
            var quest = player.GetComponent<PrescriptionRuntime>();
            var originalQuest = quest.Capture();
            if (player.CurrentHealth < player.MaxHealth) player.GainHealth(player.MaxHealth - player.CurrentHealth);
            float startingHealth = player.CurrentHealth;
            var runtime = new SyringeSpecialRuntime { mosquitoHealPerHit = 1f, mosquitoBossHealMultiplier = 1f };
            var healDeadline = ColonContentSmoke.Field(player, "nextMosquitoHeal");
            float initialDeadline = (float)healDeadline.GetValue(player);
            SyringeSpecialHitEffectUtility.ApplyMosquitoHeal(player, runtime, player);
            Check(player.CurrentHealth == player.MaxHealth && (float)healDeadline.GetValue(player) == initialDeadline,
                "full HP keeps the mosquito cooldown available");
            var visibleCamera = Camera.main;
            Vector3 anchor = visibleCamera.transform.localPosition;
            float maxKick = 0;
            for (int frame = 0; frame < 23; frame++)
            {
                if (frame == 5)
                {
                    player.TakeDamage(Mathf.Min(player.MaxHealth * .25f, player.CurrentHealth * .65f));
                    Check(player.CurrentHealth < startingHealth, "actual HP damage");
                }
                yield return null;
                maxKick = Mathf.Max(maxKick, Vector3.Distance(visibleCamera.transform.localPosition, anchor));
                Frame("hit-" + frame.ToString("D2"));
            }
            Check(maxKick > .035f && Vector3.Distance(visibleCamera.transform.localPosition, anchor) < .015f,
                "camera recoils and returns to its original anchor");
            float before = player.CurrentHealth;
            for (int i = 0; i < 16; i++)
                SyringeSpecialHitEffectUtility.ApplyMosquitoHeal(player, runtime, player);
            float firstHeal = player.CurrentHealth - before;
            Check(firstHeal > .01f && firstHeal <= 1.2f, "sixteen simultaneous impacts share one heal");
            yield return new WaitForSeconds(1.55f);
            SyringeSpecialHitEffectUtility.ApplyMosquitoHeal(player, runtime, player);
            Check(player.CurrentHealth > before + firstHeal, "healing becomes available after 1.5 seconds");

            int balance = stats.CoinsGained;
            int rankGold = RunScore.Current.Data.gold;
            int value = StageEventRuntimeModifiers.ApplyCoinValueMultiplier((int)CoinType.Bag80);
            var goldStats = player.GetComponent<PlayerGeneralStatRuntime>();
            if (goldStats != null) value = goldStats.ApplyGoldGainMultiplier(value);
            var coin = level.EntityManager.SpawnCoin(player.Position + Vector2.right * 1.2f, CoinType.Bag80, true);
            Check(coin != null && stats.CoinsGained == balance, "gold is a field pickup until collected");
            for (int i = 0; i < 35; i++)
            {
                yield return null;
                if (i == 14 && coin.gameObject.activeSelf) coin.Collect();
                if (i % 2 == 0) Frame("gold-" + i.ToString("D2"));
            }
            for (int i = 0; i < 80 && stats.CoinsGained == balance; i++) yield return null;
            Check(stats.CoinsGained >= balance + value && RunScore.Current.Data.gold == rankGold,
                $"collected gold funds wallet only (wallet {balance}->{stats.CoinsGained}, expected +{value}, rank {rankGold}->{RunScore.Current.Data.gold}, timescale {Time.timeScale})");
            var merchant = Object.FindObjectOfType<MerchantUIManager>();
            var npc = Object.FindObjectsOfType<MerchantNPC>().FirstOrDefault(candidate => candidate.CanInteract);
            Check(npc != null && npc.TryOpenShop(), "a real oral field merchant opens its offers");
            yield return null;
            var available = npc.GetShopItems().OrderBy(item => item.cost).First(item => !OctoberItemRuntime.Get(player).Has(item.octoberId));
            int wallet = stats.CoinsGained, price = RelicRuntime.Price(player, available.cost);
            if (wallet < price)
            {
                merchant.CloseShop();
                int bagCount = Mathf.CeilToInt((price - wallet) / (float)value);
                for (int i = 0; i < bagCount; i++)
                    level.EntityManager.SpawnCoin(player.Position, CoinType.Bag80, false).Collect();
                for (int i = 0; i < 120 && stats.CoinsGained < price; i++) yield return null;
                Check(npc.TryOpenShop(), "merchant retains the offer when revisited");
                yield return null;
                wallet = stats.CoinsGained;
            }
            Frame("merchant");
            Check(wallet >= price, "field gold covers an actual merchant offer");
            merchant.OnClickPurchaseItem(available, null);
            Check(stats.CoinsGained == wallet - price && RunScore.Current.Data.gold == rankGold, "shop deducts exact price without changing rank");
            yield return null;

            quest.Restore(new PrescriptionState {tag=0, quests=new[]{0,1,3}, progress=new[]{150f,1f,24f}});
            yield return null;
            quest.Record(PrescriptionRuntime.Goal.ActiveKills);
            for (int i = 0; i < 30 && !dialog.MenuOpen; i++) yield return null;
            Check(quest.Claimed && dialog.MenuOpen, "third objective opens the choice without input");
            yield return new WaitForSecondsRealtime(.55f);
            Frame("noble-choice");
            var card = dialog.GetComponentsInChildren<AbilityCard>().First();
            card.Selected();
            for (int i = 0; i < 180 && (dialog.MenuOpen || dialog.SelectionAnimating); i++) yield return null;
            Check(!dialog.MenuOpen && !quest.TryClaim(dialog), "choice finishes once");
            quest.Restore(originalQuest);
            player.GainHealth(player.MaxHealth);
            Time.captureFramerate = oldRate;
            GamePreferences.Apply(prefs, false);
        }
    }
}
