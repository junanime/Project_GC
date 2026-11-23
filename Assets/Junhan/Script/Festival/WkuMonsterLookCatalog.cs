using System;
using UnityEngine;
namespace Vampire
{
    [Serializable] public sealed class WkuMonsterLook
    {
        public string id, label;public int stage;public Sprite[] frames;public float frameSeconds=.12f;
    }
    public sealed class WkuMonsterLookCatalog:ScriptableObject
    {
        public WkuMonsterLook[] looks;
        static WkuMonsterLookCatalog cache;static int[] bag;static int cursor;static System.Random random;
        public static WkuMonsterLookCatalog Current=>cache!=null?cache:cache=Resources.Load<WkuMonsterLookCatalog>("WkuPromo/MonsterLooks");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){cache=null;bag=null;cursor=0;random=new System.Random(Guid.NewGuid().GetHashCode());}
#if UNITY_EDITOR
        public static void RestartCycleForQA(){bag=null;cursor=0;}
#endif
        public static WkuMonsterLook Next()
        {
            var catalog=Current;if(catalog==null||catalog.looks==null||catalog.looks.Length==0)return null;
            if(bag==null||cursor>=bag.Length)
            {
                bag=new int[catalog.looks.Length];for(int i=0;i<bag.Length;i++)bag[i]=i;
                if(random==null)random=new System.Random(Guid.NewGuid().GetHashCode());
                for(int i=bag.Length-1;i>0;i--){int j=random.Next(i+1);int value=bag[i];bag[i]=bag[j];bag[j]=value;}cursor=0;
            }
            return catalog.looks[bag[cursor++]];
        }
    }
}
