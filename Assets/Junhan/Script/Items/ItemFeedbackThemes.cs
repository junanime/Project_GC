using UnityEngine;
using E = Vampire.RelicBlueprint.RelicEffectType;

namespace Vampire
{
    public enum ItemEffectShape { Strike, Heal, Wind, Magnet, Focus, Shield, Honey, Ferment, Orb, Mint,
        Ember, Lure, Chime, Overflow, Night, Clock, Seed, Dawn, Counter, Acid, Crystal, Poison, Vacuum,
        Seal, Ghost, Drum, Experience, Portal, Afterimage, Memory, Culture, Dragon, Feather, Coin, Petal }

    public readonly struct ItemEffectTheme
    {
        public readonly ItemEffectShape shape;
        public readonly Color color;
        public readonly string caption;
        public ItemEffectTheme(ItemEffectShape shape, string hex, string caption = "")
        { this.shape = shape; ColorUtility.TryParseHtmlString("#" + hex, out color); this.caption = caption; }
        public static ItemEffectTheme Item(int id)
        {
            switch (id)
            {
                case 1: return new ItemEffectTheme(ItemEffectShape.Strike,"F3C47D");
                case 2: return new ItemEffectTheme(ItemEffectShape.Heal,"FFD8CA");
                case 3: return new ItemEffectTheme(ItemEffectShape.Wind,"F7BC86");
                case 4: return new ItemEffectTheme(ItemEffectShape.Magnet,"B5DBA0");
                case 5: return new ItemEffectTheme(ItemEffectShape.Focus,"FFBC67");
                case 6: return new ItemEffectTheme(ItemEffectShape.Focus,"C3E68E","반짝!");
                case 7: return new ItemEffectTheme(ItemEffectShape.Chime,"66D8CF","톡!");
                case 8: return new ItemEffectTheme(ItemEffectShape.Shield,"F5D796","든든!");
                case 9: return new ItemEffectTheme(ItemEffectShape.Counter,"D6A47D","딱!");
                case 10: return new ItemEffectTheme(ItemEffectShape.Strike,"E3B9AC","톡!");
                case 11: return new ItemEffectTheme(ItemEffectShape.Honey,"FFCB59","찰싹!");
                case 12: return new ItemEffectTheme(ItemEffectShape.Ferment,"C3B9DC");
                case 13: return new ItemEffectTheme(ItemEffectShape.Orb,"7FE4D5");
                case 14: return new ItemEffectTheme(ItemEffectShape.Mint,"91E4C2","후웅!");
                case 15: return new ItemEffectTheme(ItemEffectShape.Heal,"E9B0C3","꿀꺽!");
                case 16: return new ItemEffectTheme(ItemEffectShape.Ember,"FFAC69");
                case 17: return new ItemEffectTheme(ItemEffectShape.Lure,"B7E29B");
                case 18: return new ItemEffectTheme(ItemEffectShape.Heal,"A8DC98");
                case 19: return new ItemEffectTheme(ItemEffectShape.Chime,"ADCFEB","메아리!");
                case 20: return new ItemEffectTheme(ItemEffectShape.Wind,"BAE7D9");
                case 21: return new ItemEffectTheme(ItemEffectShape.Strike,"FFD477","톡! 톡!");
                case 22: return new ItemEffectTheme(ItemEffectShape.Mint,"8CDDD4","통!");
                case 23: return new ItemEffectTheme(ItemEffectShape.Coin,"E3C17B");
                case 24: return new ItemEffectTheme(ItemEffectShape.Counter,"F6B793");
                case 25: return new ItemEffectTheme(ItemEffectShape.Strike,"DDA7F0","각성!");
                case 26: return new ItemEffectTheme(ItemEffectShape.Shield,"97DBDA","팡!");
                case 27: return new ItemEffectTheme(ItemEffectShape.Overflow,"96E9BD","가득!");
                case 28: return new ItemEffectTheme(ItemEffectShape.Night,"C7B2FB");
                case 29: return new ItemEffectTheme(ItemEffectShape.Clock,"F3D88B","째깍!");
                case 30: return new ItemEffectTheme(ItemEffectShape.Seed,"B5E5A0");
                case 31: return new ItemEffectTheme(ItemEffectShape.Dawn,"FFE7AE");
                case 32: return new ItemEffectTheme(ItemEffectShape.Counter,"F6A2A9","쿵!");
                case 33: return new ItemEffectTheme(ItemEffectShape.Acid,"CBEF7E");
                case 34: return new ItemEffectTheme(ItemEffectShape.Strike,"EDAC9C");
                case 35: return new ItemEffectTheme(ItemEffectShape.Shield,"EADBC3");
                case 36: return new ItemEffectTheme(ItemEffectShape.Heal,"F7B492");
                case 37: return new ItemEffectTheme(ItemEffectShape.Focus,"FFC2AE");
                case 38: return new ItemEffectTheme(ItemEffectShape.Heal,"A6E1B7");
                case 39: return new ItemEffectTheme(ItemEffectShape.Experience,"F6E0A1","두 배!");
                case 40: return new ItemEffectTheme(ItemEffectShape.Strike,"D3B0EF","팡!");
                case 41: return new ItemEffectTheme(ItemEffectShape.Heal,"F2ADC1");
                case 42: return new ItemEffectTheme(ItemEffectShape.Focus,"F1D293");
                case 43: return new ItemEffectTheme(ItemEffectShape.Shield,"91DBAE","팅!");
                case 44: return new ItemEffectTheme(ItemEffectShape.Chime,"C8B9ED","땡!");
                case 45: return new ItemEffectTheme(ItemEffectShape.Lure,"AFD6A5");
                case 46: return new ItemEffectTheme(ItemEffectShape.Poison,"B1DD92");
                case 47: return new ItemEffectTheme(ItemEffectShape.Honey,"F7DFB4");
                case 48: return new ItemEffectTheme(ItemEffectShape.Chime,"EAB5CD");
                case 49: return new ItemEffectTheme(ItemEffectShape.Vacuum,"A0DDEB","쏘옥!");
                case 50: return new ItemEffectTheme(ItemEffectShape.Crystal,"C4EEFF","파삭!");
                case 51: return new ItemEffectTheme(ItemEffectShape.Petal,"B2E1DA");
                case 52: return new ItemEffectTheme(ItemEffectShape.Chime,"F5DD92","딩!");
                case 53: return new ItemEffectTheme(ItemEffectShape.Ferment,"E7C3E7");
                case 54: return new ItemEffectTheme(ItemEffectShape.Strike,"EFA4AB");
                case 55: return new ItemEffectTheme(ItemEffectShape.Strike,"EAB0C4");
                case 56: return new ItemEffectTheme(ItemEffectShape.Focus,"F5C696");
                case 57: return new ItemEffectTheme(ItemEffectShape.Seal,"BFADED","멈춰!");
                case 58: return new ItemEffectTheme(ItemEffectShape.Ghost,"D5EBFF");
                case 59: return new ItemEffectTheme(ItemEffectShape.Drum,"F4CC88","둥!");
                case 60: return new ItemEffectTheme(ItemEffectShape.Experience,"91E0DD","쏙!");
                case 61: return new ItemEffectTheme(ItemEffectShape.Portal,"B3D9F5","슉!");
                case 62: return new ItemEffectTheme(ItemEffectShape.Afterimage,"B4D8ED");
                case 63: return new ItemEffectTheme(ItemEffectShape.Memory,"D3B9D9");
                case 64: return new ItemEffectTheme(ItemEffectShape.Culture,"B9E7CD");
                case 65: return new ItemEffectTheme(ItemEffectShape.Ferment,"D7EBF9","푸슉!");
                case 66: return new ItemEffectTheme(ItemEffectShape.Dragon,"A0E6E7","촤아!");
                default: return new ItemEffectTheme(ItemEffectShape.Petal,"E9D9AE");
            }
        }
        public static ItemEffectTheme Relic(E effect)
        {
            switch(effect)
            {
                case E.MaxHealth: case E.MaxHealthPercent: case E.Regeneration: case E.IdleRecovery: case E.HealingPickup:
                    return new ItemEffectTheme(ItemEffectShape.Heal,"AEE5BF");
                case E.MoveSpeed: case E.MoveSpeedPercent: case E.Evasion: return new ItemEffectTheme(ItemEffectShape.Feather,"B9E8D8");
                case E.Barrier: case E.DamageReduction: case E.HitInvincibility: return new ItemEffectTheme(ItemEffectShape.Shield,"A7DBED","팅!");
                case E.DebuffResistance: return new ItemEffectTheme(ItemEffectShape.Petal,"B1E7EA");
                case E.Thorns: return new ItemEffectTheme(ItemEffectShape.Crystal,"D3B0E9","톡!");
                case E.Revival: return new ItemEffectTheme(ItemEffectShape.Petal,"F1BDDB");
                case E.Gold: case E.ShopDiscount: return new ItemEffectTheme(ItemEffectShape.Coin,"F9DE94");
                case E.Experience: return new ItemEffectTheme(ItemEffectShape.Experience,"A5DEE8");
                case E.PickupRange: return new ItemEffectTheme(ItemEffectShape.Magnet,"ADDCD0");
                case E.SlowChance: return new ItemEffectTheme(ItemEffectShape.Honey,"F2D39A");
                case E.Area: case E.Knockback: return new ItemEffectTheme(ItemEffectShape.Mint,"A9E1D4");
                case E.Duration: return new ItemEffectTheme(ItemEffectShape.Clock,"D5C6E8");
                case E.CritChance: return new ItemEffectTheme(ItemEffectShape.Focus,"F4DC9F");
                case E.ProjectileCount: case E.Pierce: case E.Bounce: case E.AttackSpeed: return new ItemEffectTheme(ItemEffectShape.Wind,"BBDCF7");
                default: return new ItemEffectTheme(ItemEffectShape.Strike,"F5C599");
            }
        }
    }

    public sealed partial class ItemFeedback
    {
        readonly float[] nextItem = new float[67];
        float nextRelic;
        public ItemFeedbackVisual Item(int id, Vector3 position, float seconds=.65f, float radius=.65f,
            Transform follow=null, bool sustained=false, bool acquisition=false, bool sound=true)
        {
            if(id<1 || id>66)return null;
            if(!sustained && !acquisition && Time.time<nextItem[id])return null;
            if(!sustained)nextItem[id]=Time.time+.22f;
            return Theme(ItemEffectTheme.Item(id), position, seconds, radius, follow, sustained, acquisition, sound, id);
        }
        public ItemFeedbackVisual Relic(RelicBlueprint relic, bool acquisition=false)
        {
            if(relic==null || !acquisition && Time.time<nextRelic)return null;
            if(!acquisition)nextRelic=Time.time+1.2f;
            return Theme(ItemEffectTheme.Relic(relic.effectType),transform.position,.85f,.85f,transform,false,acquisition,true,(int)relic.effectType);
        }
        public ItemFeedbackVisual Theme(ItemEffectTheme theme,Vector3 p,float seconds,float radius,Transform follow=null,
            bool sustained=false,bool acquisition=false,bool sound=true,int variant=0)
        {
            var v=Show(ItemFeedbackKind.Themed,p,seconds,radius,follow);
            if(v==null)return null;
            string text="";
            if(!acquisition && Time.time>=nextCaption){text=theme.caption;if(text.Length>0)nextCaption=Time.time+.5f;}
            v.ConfigureTheme(theme,sustained,acquisition,text,variant);
            if(sound && Application.isPlaying && character!=null)
                ItemProcAudio.For(this).Play(acquisition?"acquire":ItemProcAudio.Family(theme.shape));
            return v;
        }
    }

}
