using UnityEngine;
namespace Vampire
{
    public static class SnailFieldProgress
    {
        static readonly int[] kills=new int[4];
        public static int RemovedCount {get {int n=0;for(int i=0;i<4;i++)if(Missing(i))n++;return n;}}
        public static int Mask {get {int n=0;for(int i=0;i<4;i++)if(Missing(i))n|=1<<i;return n;}}
        public static bool Missing(int ingredient)=>kills[Mathf.Clamp(ingredient,0,3)]>=3;
        public static void Record(int ingredient){kills[Mathf.Clamp(ingredient,0,3)]++;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset(){System.Array.Clear(kills,0,kills.Length);}
    }
}
