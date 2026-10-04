using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class PlayQa1004Tests
    {
        [Test] public void DashSegmentsShowReadyPartialAndEmptyIndependently()
        {
            Assert.AreEqual(1,PlayerCombatBars.SegmentFill(0,1,.4f));
            Assert.AreEqual(.4f,PlayerCombatBars.SegmentFill(1,1,.4f));
            Assert.AreEqual(0,PlayerCombatBars.SegmentFill(2,1,.4f));
            Assert.AreEqual(1,PlayerCombatBars.SegmentFill(2,3,0));
            Assert.GreaterOrEqual(PlayerCombatBars.DividerWidth,3);
        }
        [Test] public void CountdownNeverTurnsNegativeOrEndsEarly()
        {
            var go=new GameObject("Countdown",typeof(RectTransform),typeof(TextMeshProUGUI));
            try
            {
                var timer=go.AddComponent<GameTimer>();var text=go.GetComponent<TextMeshProUGUI>();
                typeof(GameTimer).GetMethod("Awake",PacingPatchTests.Hidden).Invoke(timer,null);
                timer.SetTime(900);Assert.AreEqual("15:00",text.text);
                timer.SetTime(.01f);Assert.AreEqual("00:01",text.text);
                timer.SetTime(0);Assert.AreEqual("00:00",text.text);
                timer.SetTime(-1);Assert.AreEqual("00:00",text.text);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void FasterFoodAndIndependentEruptionHavePersistedDefaults()
        {
            var food=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MiniStages/Room_FallingFood.prefab").GetComponent<MiniStageFallingFoodRoom>();
            Assert.AreEqual(.45f,PacingPatchTests.Get(food,"waveIntervalMin"));
            Assert.AreEqual(.8f,PacingPatchTests.Get(food,"waveIntervalMax"));
            Assert.AreEqual(.65f,PacingPatchTests.Get(food,"warningTimeMin"));
            Assert.AreEqual(.9f,PacingPatchTests.Get(food,"warningTimeMax"));
            var strike=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MiniStages/PF_FallingFoodStrike.prefab").GetComponent<MiniStageFallingFoodStrike>();
            Assert.AreEqual(.28f,PacingPatchTests.Get(strike,"fallMotionDuration"));
            Assert.AreEqual(35,Resources.Load<CharacterSkillDefinition>("ShiniSkills").shiniEruptionDamage);
            Assert.AreEqual(6,RemakeBalance.Current.sniperTeleportCooldown);
        }
        [Test] public void WeaponChoicePreferenceDefaultsEnabledAndCanBeRestored()
        {
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var w=config.weapons.First(x=>x!=null);string key=StartingNeedleSelection.EnabledKey(w.Type);
            bool exists=PlayerPrefs.HasKey(key);int value=PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);Assert.IsTrue(StartingNeedleSelection.Enabled(w));
                PlayerPrefs.SetInt(key,0);Assert.IsFalse(StartingNeedleSelection.Enabled(w));
                PlayerPrefs.SetInt(key,1);Assert.IsTrue(StartingNeedleSelection.Enabled(w));
            }
            finally {if(exists)PlayerPrefs.SetInt(key,value);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
    }
}
