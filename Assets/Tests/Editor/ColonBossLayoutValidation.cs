#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Vampire
{
    // Uses the real placement/geometry code. Run only after entering the colon map in Play mode.
    public static class ColonBossLayoutValidation
    {
        [Serializable] public sealed class Report
        {
            public bool success;
            public int samples, failures, homesChecked, clusteredPairs, runsWithClosePair, uniqueLayouts;
            public float minimumHomeGap=999, minimumPropGap=999, minimumEntryGap=999;
            public long milliseconds;
            public List<string> errors=new List<string>();
        }

        public static Report Run(LevelManager level,int samples=256,string outputPath=null)
        {
            if(level==null||FiniteStomachMap.Instance==null||!FiniteStomachMap.Instance.IsColon)
                throw new InvalidOperationException("Enter stage two before validating colon habitat placement.");
            var watch=Stopwatch.StartNew();
            var map=FiniteStomachMap.Instance;
            Vector2 entry=level.PlayerCharacter.transform.position;
            var report=new Report{samples=samples};var layouts=new HashSet<string>();
            for(int sample=0;sample<samples;sample++)
            {
                int seed=11717+sample*7919;
                try
                {
                    var props=FieldObjectLayout.Generate(map,entry,seed);
                    var homes=ColonBossDirector.Generate(map,entry,seed^0x62B7,props);
                    var repeat=ColonBossDirector.Generate(map,entry,seed^0x62B7,props);
                    Require(props.Count==86,"Initial field object count is not 86");
                    Require(homes.Count==6,"Missing boss home");
                    var kinds=new HashSet<ColonBossKind>();bool close=false;string fingerprint="";
                    for(int i=0;i<homes.Count;i++)
                    {
                        var h=homes[i];kinds.Add(h.kind);report.homesChecked++;
                        fingerprint+=h.kind+":"+h.position.ToString("F2")+";";
                        Require(repeat[i].kind==h.kind&&(repeat[i].position-h.position).sqrMagnitude<.000001f,"Same seed changed a home");
                        float entryGap=Vector2.Distance(h.position,entry);
                        report.minimumEntryGap=Mathf.Min(report.minimumEntryGap,entryGap);
                        Require(entryGap>=ColonBossDirector.EntryClearance-.001f,"Home overlaps the entry clearance");
                        if(h.kind==ColonBossKind.Manatee)
                            Require(ColonBossDirector.HouseFits(map,h.position,h.wallNormal),"Manatee house/door approach intersects a wall");
                        else
                            Require(ColonBossDirector.GroundFits(map,h.position,ColonBossDefinition.Get(h.kind).HabitatRadius),"Home footprint intersects a wall");
                        foreach(var prop in props)
                        {
                            float gap=Vector2.Distance(h.position,prop.position);
                            report.minimumPropGap=Mathf.Min(report.minimumPropGap,gap);
                            Require(gap>=ColonBossDirector.PropClearance+(h.kind==ColonBossKind.Manatee?2:0)-.001f,"Home overlaps an initial prop");
                        }
                        for(int j=0;j<i;j++)
                        {
                            float gap=Vector2.Distance(h.position,homes[j].position);
                            report.minimumHomeGap=Mathf.Min(report.minimumHomeGap,gap);
                            float required=h.partner==j?ColonBossDirector.MinimumSeparation:ColonBossDirector.PreferredSeparation;
                            Require(gap>=required-.001f,"Unintended home overlap/spacing violation");
                        }
                        if(h.clustered){close=true;report.clusteredPairs++;Require(h.partner>=0&&h.partner<i,"Invalid close-pair anchor");}
                    }
                    Require(kinds.Count==6,"Duplicate/missing boss kind");
                    layouts.Add(fingerprint);if(close)report.runsWithClosePair++;
                }
                catch(Exception error){report.failures++;report.errors.Add("seed "+seed+": "+error.Message);}
            }
            watch.Stop();report.milliseconds=watch.ElapsedMilliseconds;report.uniqueLayouts=layouts.Count;
            report.success=report.failures==0&&report.homesChecked==samples*6&&report.uniqueLayouts==samples;
            if(outputPath!=null)
            {
                string directory=Path.GetDirectoryName(outputPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
                File.WriteAllText(outputPath,JsonUtility.ToJson(report,true));
            }
            UnityEngine.Debug.Log("COLON_BOSS_LAYOUT "+JsonUtility.ToJson(report));return report;
        }
        static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    }
}
#endif
