using UnityEngine;

namespace Vampire
{
    // A pooled monster is introduced when it is actually visible, never on an off-screen spawn.
    public sealed class TutorialDiscoveryTrigger : MonoBehaviour
    {
        string id;Monster owner;float next;
        public static void Bind(GameObject target,string key,Monster monster=null)
        {
            if (GStarDemo.Enabled) return;
            var trigger=target.GetComponent<TutorialDiscoveryTrigger>();
            if(string.IsNullOrEmpty(key)||TutorialGuide.HasSeen(key)){if(trigger!=null)trigger.enabled=false;return;}
            if(trigger==null)trigger=target.AddComponent<TutorialDiscoveryTrigger>();
            trigger.id=key;trigger.owner=monster;trigger.next=0;trigger.enabled=!string.IsNullOrEmpty(key);
        }
        public static bool Visible(Vector2 position)
        {
            var camera=Camera.main;if(camera==null)return false;
            var p=camera.WorldToViewportPoint(position);
            return p.z>0&&p.x>.06f&&p.x<.94f&&p.y>.10f&&p.y<.88f;
        }
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            if(TutorialGuide.HasSeen(id)){enabled=false;return;}
            if(StageEntryLoading.IsLoading||Time.timeScale<=0||owner!=null&&(!owner.IsAlive||owner.IsMiniStageOwned))return;
            if(Visible(transform.position)){TutorialGuide.QueueContent(id);enabled=false;}
        }
    }
}
