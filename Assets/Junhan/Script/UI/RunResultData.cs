using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public static class RunResultData
    {
        public sealed class DamageRow
        {
            public string name;
            public float damage;
            public string[] sources;
        }
        public static readonly Color[] DamageColors = {
            new Color(.91f,.20f,.24f), new Color(1,.49f,.12f), new Color(1,.78f,.12f),
            new Color(.21f,.72f,.40f), new Color(.56f,.57f,.61f)
        };
        public static List<DamageRow> DamageRows(IReadOnlyDictionary<string,float> source)
        {
            var sorted=(source??new Dictionary<string,float>()).Where(p=>p.Value>0&&!float.IsNaN(p.Value)&&!float.IsInfinity(p.Value))
                .OrderByDescending(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray();
            var result=sorted.Take(4).Select(p=>new DamageRow{name=p.Key,damage=p.Value,sources=new[]{p.Key}}).ToList();
            result.Add(new DamageRow{name="기타 증강",damage=sorted.Skip(4).Sum(p=>p.Value),sources=sorted.Skip(4).Select(p=>p.Key).ToArray()});
            return result;
        }
        // Top-to-bottom first, then the next column. Shrink all cells together to keep every icon inside.
        public static Vector2Int IconGrid(int count,float width,float height,float preferred=48)
        {
            if(count<=0)return Vector2Int.one;
            int rows=Mathf.Max(1,Mathf.FloorToInt(height/preferred));
            int columns=Mathf.CeilToInt(count/(float)rows);
            float best=Mathf.Min(preferred,width/columns,height/rows);
            for(int r=rows+1;r<=count;r++)
            {
                int c=Mathf.CeilToInt(count/(float)r);
                float size=Mathf.Min(preferred,width/c,height/r);
                if(size>best+.01f){rows=r;columns=c;best=size;}
            }
            return new Vector2Int(columns,rows);
        }
    }
}
