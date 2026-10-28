using System.Collections;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    public static partial class ItemProcProof
    {
        static IEnumerator HomingProof(OctoberItemRuntime r,Vector2 origin)
        {
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,r.Player,origin+Vector2.right*3);
            boss.AutoPatterns=false;boss.enabled=false;yield return null;
            yield return (IEnumerator)Call(r,"Fly",boss,58,12f);
            yield return new WaitForSeconds(.3f);Object.Destroy(boss.gameObject);
        }
        static IEnumerator Integration(Character p,Vector2 origin)
        {
            Clear(p);yield return null;
            var r=OctoberItemRuntime.Get(p);r.enabled=false;
            foreach(FoodBossKind kind in System.Enum.GetValues(typeof(FoodBossKind)))
            {
                var boss=FoodBossRuntime.Spawn(kind,p,origin+Vector2.right*1.5f);boss.AutoPatterns=false;boss.enabled=false;
                yield return null;Physics2D.SyncTransforms();
                float hp=boss.Health;r.Pulse(boss.transform.position,1.5f,10,22);Check(boss.Health<hp,"shockwave hits new boss "+kind);
                hp=boss.Health;r.Wave(Vector2.right,6,10,66);Check(boss.Health<hp,"forward wave hits new boss "+kind);
                hp=boss.Health;yield return (IEnumerator)Call(r,"Fly",boss,58,10f);Check(boss.Health<hp,"homing spirit hits new boss "+kind);
                var bullet=FoodBossBullet.Fire(boss,origin+Vector2.up*1.4f,Vector2.right,2,5);
                var registration=bullet.GetComponent<OctoberEnemyProjectile>();registration.Seal(.6f);Vector2 pos=bullet.transform.position;
                yield return new WaitForSeconds(.2f);Check(bullet!=null&&!bullet.enabled&&Vector2.Distance(pos,bullet.transform.position)<.001f,"seal freezes new boss bullet "+kind);
                yield return new WaitForSeconds(.5f);Check(bullet==null,"sealed bullet removed "+kind);
                var thorns=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics.First(x=>x!=null&&x.effectType==RelicBlueprint.RelicEffectType.Thorns);
                var relics=RelicRuntime.Ensure(p);relics.Equip(thorns);relics.enabled=false;hp=boss.Health;relics.Hurt(10);Check(boss.Health<hp,"relic reflection hits new boss "+kind);relics.Equip(null);
                Object.Destroy(boss.gameObject);yield return null;
            }
            // Ground effects must survive their normal lifetime without following the player.
            r.enabled=true;var zone=r.Zone(origin+Vector2.right,1.6f,.6f,0,33);Vector2 at=zone.transform.position;
            yield return new WaitForSeconds(.2f);Check(zone!=null&&Vector2.Distance(zone.transform.position,at)<.001f,"acid puddle persists at world location");
            var ground=Object.FindObjectsOfType<ItemFeedbackVisual>().FirstOrDefault(v=>v.GetComponent<MeshRenderer>().sortingLayerName=="GroundEffects");Check(ground!=null,"zone on ground effects sorting layer");
            yield return new WaitForSeconds(.5f);Check(zone==null,"zone expires");
            var feedback=ItemFeedback.For(p);var effect=feedback.Item(14,origin,2,2,sustained:true,sound:false);int lease=effect.Lease;
            var prefs=GamePreferences.Current.Copy();prefs.augmentVfxOpacity=0;GamePreferences.Apply(prefs,false,false);yield return null;
            var block=new MaterialPropertyBlock();effect.GetComponent<MeshRenderer>().GetPropertyBlock(block);Check(block.GetColor("_Color").a==0,"player VFX opacity setting applies");
            prefs.augmentVfxOpacity=1;GamePreferences.Apply(prefs,false,false);yield return null;
            Time.timeScale=0;float age=(float)Field(effect,"age").GetValue(effect);yield return new WaitForSecondsRealtime(.15f);Check((float)Field(effect,"age").GetValue(effect)==age,"effect pauses with gameplay");Time.timeScale=1;
            effect.Release(lease);Clear(p);yield return null;
        }
    }
}
