using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public static class OctoberArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        public static readonly Color Ink = new Color(.025f,.05f,.15f);
        public static Sprite Get(string key, string part = null)
        {
            string id=key+"/"+part;
            if (!Cache.TryGetValue(id,out var value))
            {
                var sprites=Resources.LoadAll<Sprite>(key);
                value=part==null?sprites.FirstOrDefault():sprites.FirstOrDefault(s=>s.name==part);
                if(value!=null)Cache[id]=value;
            }
            return value;
        }
        public static string CharacterKey(CharacterBlueprint c)
        {
            string n=c!=null?c.name:"아시";
            return n.Contains("혁")?"Hyuki":n.Contains("신")?"Shini":n.Contains("아리")?"Ari":"Ashi";
        }
        public static Sprite Character(CharacterBlueprint c,int state) => Get("OctoberUI/"+CharacterKey(c)+"States",state.ToString());
        public static Sprite Button(bool active) => Get("OctoberUI/ButtonStates",active?"Active":"Idle");
    }

    // Small unscaled motions keep the title scene alive, and respect reduced motion.
    public sealed class OctoberTitleMotion : MonoBehaviour
    {
        public string role;
        Vector3 origin;
        void Start() {origin=transform.localPosition;}
        void Update()
        {
            float t=Time.unscaledTime;
            if(GamePreferences.Current.reducedMotion){transform.localPosition=origin;transform.localRotation=Quaternion.identity;return;}
            Vector3 d=Vector3.zero;float angle=0;
            switch(role)
            {
                case "Hyuki": d.x=Mathf.Sin(t*35)*1.4f;d.y=Mathf.Sin(t*27)*.6f;break;
                case "Shini": d.x=Mathf.Sin(t*.8f)*34;d.y=-d.x*.6f+Mathf.Abs(Mathf.Sin(t*11))*3;angle=Mathf.Sin(t*11)*2;break;
                case "Ari": d.x=Mathf.Max(0,Mathf.Sin(t*2.6f))*4;angle=-Mathf.Max(0,Mathf.Sin(t*2.6f))*2;break;
                case "Germ": d.x=Mathf.Max(0,Mathf.Sin(t*2.6f))*3;d.y=Mathf.Sin(t*4)*1.5f;break;
                default:d.y=Mathf.Sin(t*2)*2;angle=Mathf.Sin(t*1.4f)*2;break;
            }
            transform.localPosition=origin+d;transform.localRotation=Quaternion.Euler(0,0,angle);
        }
    }
}
