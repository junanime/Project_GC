using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Vampire
{
    // Read-only views of the current run. Never reads an offer's Description (which can roll RNG).
    public static class RunBookData
    {
        public sealed class Entry
        {
            public string id,title,body,badge;
            public Sprite icon;
            public Color color=new Color(.71f,.45f,.24f);
        }
        [Serializable] sealed class Acquisition
        {
            public string parent;
            public bool original;
            public int option,grade;
            public float amount;
        }
        public static List<Entry> Weapons(AbilityManager manager,ApothecaryUIConfig config)
        {
            var rows=new List<Entry>();if(manager==null)return rows;
            var owned=manager.GetComponentsInChildren<Ability>(true).Where(a=>a.Owned).ToArray();
            var parents=owned.OfType<SyringeSpecialAugmentAbility>().OrderBy(a=>(int)a.Type).ToArray();
            var state=manager.Ver4;
            var acquisitions=state!=null?state.CaptureRunSceneConditionalAugmentIds().Select(json=>JsonUtility.FromJson<Acquisition>(json)).Where(a=>a!=null).ToArray():Array.Empty<Acquisition>();
            foreach(var parent in parents)
            {
                string id=parent.Type.ToString();int type=(int)parent.Type;
                var source=config.weapons?.FirstOrDefault(a=>a!=null&&a.Type==parent.Type);
                string description=source!=null?source.Description:TutorialGuide.Instance?.Entries.FirstOrDefault(e=>e.id=="weapon/"+id)?.what;
                var body=new StringBuilder(string.IsNullOrWhiteSpace(description)?"획득한 특수 침의 효과가 기본 침에 적용됩니다.":description);
                int original=state!=null?state.Progress.Level(id):0;
                if(state!=null)
                    for(int i=0;i<3;i++)
                    {
                        int count=state.Progress.Count(id,i);if(count==0)continue;
                        body.Append("\n\n").Append(Ver4AugmentCatalog.OriginalNames[type*3+i]).Append(" ").Append(count).Append("/3\n").Append(Ver4AugmentCatalog.OriginalDescriptions[type*3+i]);
                    }
                var numeric=acquisitions.Where(a=>a.parent==id&&!a.original).ToArray();
                if(numeric.Length>0)
                {
                    body.Append("\n\n획득한 수치 강화 (공유 침에 적용)");
                    foreach(var group in numeric.GroupBy(a=>a.option).OrderBy(g=>g.Key))
                        body.Append("\n").Append(Ver4AugmentCatalog.NumericDescription(group.Key,group.Sum(a=>a.amount)));
                }
                rows.Add(new Entry{id="weapon/"+id,title=Ver4AugmentCatalog.ParentNames[type],body=body.ToString(),badge=$"오리지널 {original}/9",icon=parent.Image,color=new Color(.68f,.38f,.89f)});
            }
            foreach(var noble in owned.OfType<SyringeLegendaryAugmentAbility>())
                rows.Add(new Entry{id="noble/"+noble.Type,title=noble.Name,body=noble.Description,badge="고귀",icon=noble.Image,color=new Color(.94f,.64f,.13f)});
            if(parents.Length==0 && owned.OfType<SyringeDartAbility>().Any())
                rows.Insert(0,new Entry{id="basic",title="기본 침",body="바라보는 방향으로 기본 침을 발사합니다. 획득한 특수 무기 증강과 수치 강화가 침에 적용됩니다.",badge="기본 무기",icon=config.basicNeedle});
            return rows;
        }
        public static string[] Stats(Character player,SyringeDartAbility needle)
        {
            var extra=player.GetComponent<PlayerGeneralStatRuntime>();
            return new[]{needle!=null?needle.GetEffectiveDamage().ToString("0.#"):"—",
                needle!=null?(1/Mathf.Max(.01f,needle.GetEffectiveCooldown())).ToString("0.00")+"/초":"—",
                (player.CritChance*100).ToString("0.#")+"%",((extra!=null?extra.CritDamageMultiplier:1.5f)*100).ToString("0.#")+"%",
                (needle!=null?needle.GetEffectiveProjectileCount():player.SkillProjectileCount(1+player.AdditionalProjectiles)).ToString(),
                "×"+(needle!=null?needle.GetEffectiveProjectileSizeMultiplier():player.ProjectileSizeMultiplier).ToString("0.00"),
                needle!=null?needle.GetEffectiveSpeed().ToString("0.00"):"—",player.CurrentArmor.ToString("0.#"),
                player.CurrentMoveSpeed.ToString("0.00"),"×"+(extra!=null?extra.PickupRangeMultiplier:1).ToString("0.00"),
                (player.ExperienceMultiplier*100).ToString("0.#")+"%",((extra!=null?extra.GoldGainMultiplier:1)*100).ToString("0.#")+"%"};
        }
    }
}
