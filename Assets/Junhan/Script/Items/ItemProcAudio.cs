using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Short, original one-shots. No looping beds; at most three concurrent voices per player.
    public sealed class ItemProcAudio : MonoBehaviour
    {
        AudioSource[] voices;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,float> next=new Dictionary<string,float>();
        float nextAny;bool paused;Character player;
        public static ItemProcAudio For(Component owner)=>owner.GetComponent<ItemProcAudio>()??owner.gameObject.AddComponent<ItemProcAudio>();
        void Awake(){player=GetComponent<Character>();voices=new AudioSource[3];for(int i=0;i<3;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;voices[i].priority=160;}}
        public static string Family(ItemEffectShape shape)
        {
            switch(shape)
            {
                case ItemEffectShape.Heal:case ItemEffectShape.Seed:case ItemEffectShape.Petal:return "bloom";
                case ItemEffectShape.Shield:case ItemEffectShape.Memory:case ItemEffectShape.Crystal:case ItemEffectShape.Seal:return "crystal";
                case ItemEffectShape.Wind:case ItemEffectShape.Mint:case ItemEffectShape.Feather:case ItemEffectShape.Dragon:case ItemEffectShape.Vacuum:case ItemEffectShape.Portal:return "sweep";
                case ItemEffectShape.Honey:case ItemEffectShape.Poison:case ItemEffectShape.Acid:case ItemEffectShape.Ferment:case ItemEffectShape.Culture:case ItemEffectShape.Overflow:return "bubble";
                case ItemEffectShape.Clock:case ItemEffectShape.Chime:case ItemEffectShape.Experience:case ItemEffectShape.Coin:case ItemEffectShape.Ghost:return "chime";
                default:return "pop";
            }
        }
        public void Play(string family)
        {
            if(!Application.isPlaying||Time.timeScale<=0||Time.time<nextAny||next.TryGetValue(family,out var due)&&Time.time<due)return;
            if(!clips.TryGetValue(family,out var clip)){clip=Resources.Load<AudioClip>("OctoberContent/ProcAudio/"+family);clips[family]=clip;}
            if(clip==null)return;
            var v=System.Array.Find(voices,s=>!s.isPlaying);if(v==null)return;
            nextAny=Time.time+.12f;next[family]=Time.time+.3f;
            v.clip=clip;v.volume=GamePreferences.Current.effects*.24f;v.Play();
        }
        void Update()
        {
            if(player!=null && !player.IsAlive){OnDisable();return;}
            bool stop=Time.timeScale<=0;
            foreach(var v in voices){v.volume=GamePreferences.Current.effects*.24f;if(stop!=paused){if(stop)v.Pause();else v.UnPause();}}
            paused=stop;
        }
        void OnDisable(){if(voices!=null)foreach(var v in voices)if(v!=null)v.Stop();paused=false;}
        void OnDestroy(){if(voices!=null)foreach(var v in voices)if(v!=null)ItemFeedback.Dispose(v);}
    }
}
