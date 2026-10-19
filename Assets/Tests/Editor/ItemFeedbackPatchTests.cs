using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class ItemFeedbackPatchTests
    {
        [Test] public void RetiredDuplicateKeepsSavedSelectionAndCatalog()
        {
            var catalog = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Assert.AreEqual(34, catalog.relics.Length);
            var legacy = catalog.relics.Single(r => r.relicId == "lotus_chips");
            var canonical = catalog.relics.Single(r => r.relicId == "beads");
            Assert.AreEqual(canonical.effectType, legacy.effectType);
            Assert.AreEqual(canonical.effectValue, legacy.effectValue);
            string unlockKey = "RelicUnlocked_lotus_chips", equipKey = "EquippedRelicId";
            bool hadUnlock = PlayerPrefs.HasKey(unlockKey), hadEquip = PlayerPrefs.HasKey(equipKey);
            int oldUnlock = PlayerPrefs.GetInt(unlockKey); string oldEquip = PlayerPrefs.GetString(equipKey);
            try
            {
                PlayerPrefs.DeleteKey(unlockKey); PlayerPrefs.DeleteKey(equipKey);
                Assert.AreEqual(33, RelicSelectionPolicy.ForLobby(catalog.relics).Length);
                Assert.IsFalse(RelicSelectionPolicy.IsNewOffer(legacy));
                Assert.IsTrue(RelicSelectionPolicy.IsNewOffer(canonical));
                Assert.IsTrue(RelicSelectionPolicy.SameChoice(legacy.relicId, canonical.relicId));
                RelicSaveData.Unlock(legacy.relicId); RelicSaveData.Equip(legacy.relicId);
                Assert.AreEqual(34, RelicSelectionPolicy.ForLobby(catalog.relics).Length);
                Assert.AreEqual("lotus_chips", RelicSaveData.GetEquippedRelicId());
                Assert.IsFalse(RelicSelectionPolicy.IsNewOffer(legacy));
            }
            finally
            {
                if(hadUnlock)PlayerPrefs.SetInt(unlockKey,oldUnlock);else PlayerPrefs.DeleteKey(unlockKey);
                if(hadEquip)PlayerPrefs.SetString(equipKey,oldEquip);else PlayerPrefs.DeleteKey(equipKey);
                PlayerPrefs.Save();
            }
        }

        [Test] public void CatalogDescriptionsAndPricesStayConsistent()
        {
            for (int id = 1; id <= 66; id++)
            {
                var item = Resources.Load<MerchantItemBlueprint>("OctoberContent/Catalog/Item" + id.ToString("00"));
                Assert.NotNull(item);
                Assert.AreEqual(OctoberItemBalance.Description[id], item.description, "description " + id);
                Assert.AreEqual(ItemOfferPolicy.Gold(item.itemRarity), item.cost, "gold " + id);
                Assert.AreEqual(ItemOfferPolicy.Silver(item.itemRarity), item.silverCost, "silver " + id);
            }
        }

        [Test] public void CloseRangeBonusUsesActualHitDistance()
        {
            var owner = new GameObject("patch test owner"); var target = new GameObject("patch test target");
            try
            {
                var runtime = owner.AddComponent<OctoberItemRuntime>(); runtime.State.owned.Add(56);
                bool critical = false; float knockback = 1;
                target.transform.position = new Vector3(2, 0, 0);
                Assert.That(runtime.BeforeHit(target.transform,100,false,ref critical,ref knockback),Is.EqualTo(135).Within(.001));
                target.transform.position = new Vector3(2.01f,0,0);
                Assert.That(runtime.BeforeHit(target.transform,100,false,ref critical,ref knockback),Is.EqualTo(100).Within(.001));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(target); }
        }

        [Test] public void DawnHasBaselineAndPreservesFinalHourPower()
        {
            Assert.AreEqual(4,OctoberItemBalance.DawnInterval(0));
            Assert.AreEqual(.3f,OctoberItemBalance.DawnDamage(22));
            Assert.AreEqual(1,OctoberItemBalance.DawnInterval(23));
            Assert.AreEqual(1.8f,OctoberItemBalance.DawnDamage(23));
            Assert.AreEqual(1.8f,OctoberItemBalance.DawnDamage(24));
        }

        [Test] public void PressureProcCountsPrimaryHitsAndStillDamagesWhenPoolIsFull()
        {
            var owner=new GameObject("pressure test");var enemy=new GameObject("pressure target");
            try
            {
                var target=enemy.AddComponent<BossPartDamageTestPart>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(BossPartDamageTestPart).GetField("maxHealth",flags).SetValue(target,1000f);
                typeof(BossPartDamageTestPart).GetField("hitFlashDuration",flags).SetValue(target,0f);
                target.ResetPart();
                var runtime=owner.AddComponent<OctoberItemRuntime>();runtime.State.owned.Add(21);
                bool critical=false;float knockback=0;
                runtime.BeforeHit(target,100,false,ref critical,ref knockback);
                for(int i=0;i<4;i++)runtime.AfterHit(target,100,false,true,target.CurrentHealth);
                Assert.AreEqual(1000,target.CurrentHealth,"secondary attacks cannot trigger point strike");
                var feedback=ItemFeedback.For(runtime);
                for(int i=0;i<100;i++)feedback.Show(ItemFeedbackKind.Steam,Vector3.zero,1,1);
                for(int i=0;i<3;i++)runtime.AfterHit(target,100,false,false,target.CurrentHealth);
                Assert.AreEqual(1000,target.CurrentHealth);
                runtime.AfterHit(target,100,false,false,target.CurrentHealth);
                Assert.That(target.CurrentHealth,Is.EqualTo(920).Within(.001),"visual budget does not suppress damage");
                runtime.AfterHit(target,100,false,false,target.CurrentHealth);
                Assert.That(target.CurrentHealth,Is.EqualTo(920).Within(.001),"counter resets after proc");
            }
            finally {Object.DestroyImmediate(owner);Object.DestroyImmediate(enemy);}
        }

        [Test] public void PoolIsBoundedReusesGeometryAndProtectsNewLeases()
        {
            var owner = new GameObject("feedback budget test");
            try
            {
                var feedback = ItemFeedback.For(owner.transform);
                var first = feedback.Show(ItemFeedbackKind.FireZone,Vector3.zero,3,1);
                int oldLease = first.Lease; var mesh = first.GetComponent<MeshFilter>().sharedMesh;
                first.Release(oldLease);
                var second = feedback.Show(ItemFeedbackKind.Spirit,Vector3.zero,2,1);
                Assert.AreSame(first,second); Assert.AreSame(mesh,second.GetComponent<MeshFilter>().sharedMesh);
                first.Release(oldLease); Assert.IsTrue(second.gameObject.activeSelf,"old zone cleanup must not hide a reused effect");
                for(int i=0;i<100;i++) feedback.Show(ItemFeedbackKind.Steam,Vector3.zero,1,1);
                Assert.AreEqual(ItemFeedback.Capacity-1,feedback.CreatedCount);
                Assert.NotNull(feedback.Show(ItemFeedbackKind.Revival,Vector3.zero,1,1));
                Assert.AreEqual(ItemFeedback.Capacity,feedback.CreatedCount);
                Assert.IsNull(feedback.Show(ItemFeedbackKind.Spirit,Vector3.zero,1,1));
                owner.SetActive(false);
                // EditMode does not dispatch MonoBehaviour lifecycle messages for this runtime-only component.
                typeof(ItemFeedback).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(feedback,null);
                Assert.IsFalse(second.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test] public void EchoDamagesNearbyTargetOncePerCooldown()
        {
            var owner=new GameObject("echo test");var enemy=new GameObject("echo target");
            try
            {
                enemy.AddComponent<CircleCollider2D>();
                var target=enemy.AddComponent<BossPartDamageTestPart>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(BossPartDamageTestPart).GetField("maxHealth",flags).SetValue(target,1000f);
                typeof(BossPartDamageTestPart).GetField("hitFlashDuration",flags).SetValue(target,0f);
                target.ResetPart();Physics2D.SyncTransforms();
                var runtime=owner.AddComponent<OctoberItemRuntime>();runtime.State.owned.Add(22);
                bool critical=false;float knockback=10;
                runtime.BeforeHit(target,100,false,ref critical,ref knockback);
                Assert.That(knockback,Is.EqualTo(13).Within(.001));
                runtime.AfterHit(target,100,false,true,1000);
                Assert.AreEqual(1000,target.CurrentHealth);
                runtime.AfterHit(target,100,false,false,1000);
                Assert.That(target.CurrentHealth,Is.EqualTo(960).Within(.001));
                runtime.AfterHit(target,100,false,false,960);
                Assert.That(target.CurrentHealth,Is.EqualTo(960).Within(.001));
            }
            finally {Object.DestroyImmediate(owner);Object.DestroyImmediate(enemy);}
        }

        public static void Run()
        {
            try
            {
                var tests=new ItemFeedbackPatchTests();
                tests.RetiredDuplicateKeepsSavedSelectionAndCatalog();
                tests.CatalogDescriptionsAndPricesStayConsistent();
                tests.CloseRangeBonusUsesActualHitDistance();
                tests.DawnHasBaselineAndPreservesFinalHourPower();
                tests.PressureProcCountsPrimaryHitsAndStillDamagesWhenPoolIsFull();
                tests.PoolIsBoundedReusesGeometryAndProtectsNewLeases();
                tests.EchoDamagesNearbyTargetOncePerCooldown();
                RenderPreview();
                Debug.Log("ITEM_FEEDBACK_PATCH_PASS 7 tests; 66 catalog rows; preview rendered");
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void RenderPreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var owner=new GameObject("Feedback preview");var feedback=ItemFeedback.For(owner.transform);
            var cam=new GameObject("Preview camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=4.4f;
            cam.transform.position=new Vector3(0,0,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.09f,.12f);
            var texture=new RenderTexture(1400,850,24);cam.targetTexture=texture;
            var kinds=new[]{ItemFeedbackKind.PressureMark,ItemFeedbackKind.PressureBurst,ItemFeedbackKind.FireZone,ItemFeedbackKind.Spirit,ItemFeedbackKind.Steam,ItemFeedbackKind.Revival};
            for(int i=0;i<kinds.Length;i++)
            {
                var effect=feedback.Show(kinds[i],new Vector3((i%3-1)*4, i<3?1.8f:-1.8f,0),3,1,null,3);
                typeof(ItemFeedbackVisual).GetMethod("Draw",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,new object[]{.35f});
            }
            cam.Render();RenderTexture.active=texture;
            var capture=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);capture.Apply();
            Directory.CreateDirectory("Library/ItemFeedbackQA");File.WriteAllBytes("Library/ItemFeedbackQA/preview.png",capture.EncodeToPNG());
            RenderTexture.active=null;cam.targetTexture=null;
            Object.DestroyImmediate(capture);Object.DestroyImmediate(texture);Object.DestroyImmediate(owner);Object.DestroyImmediate(cam.gameObject);
        }
    }
}
