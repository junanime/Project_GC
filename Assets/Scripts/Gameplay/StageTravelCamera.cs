using UnityEngine;
namespace Vampire
{
    // A short establishing shot lets the whole anatomical lid be seen before returning to gameplay zoom.
    [DefaultExecutionOrder(20000)]
    public sealed class StageTravelCamera:MonoBehaviour
    {
        Camera cameraView;float originalSize,age,enterAge;bool releasing,softEntry;Vector3 focus,originalLocalPosition,followOffset,entryPosition;Transform player;
        public static StageTravelCamera Begin(StomachOpening inlet,Vector2 landing)
        {
            var c=Camera.main;if(c==null)return null;
            var shot=c.gameObject.AddComponent<StageTravelCamera>();shot.cameraView=c;shot.originalSize=c.orthographicSize;
            shot.originalLocalPosition=c.transform.localPosition;shot.player=FindObjectOfType<LevelManager>().PlayerCharacter.transform;shot.followOffset=c.transform.position-shot.player.position;
            shot.focus=inlet.Upper?(inlet.FrontLip+landing)*.5f+Vector2.up*inlet.Radii.y*.55f:(inlet.Center+landing)*.5f;shot.focus.z=c.transform.position.z;
            shot.wide=Mathf.Max(shot.originalSize,inlet.Radii.y*1.6f);shot.softEntry=!inlet.Upper;shot.entryPosition=c.transform.position;
            if(!shot.softEntry)c.orthographicSize=shot.wide;return shot;
        }
        float wide;
        public void Release(){releasing=true;age=0;}
        void LateUpdate()
        {
            if(cameraView==null)return;
            if(!releasing){enterAge+=Time.deltaTime;float enter=softEntry?Mathf.SmoothStep(0,1,enterAge/.65f):1;cameraView.transform.position=Vector3.Lerp(entryPosition,focus,enter);cameraView.orthographicSize=Mathf.Lerp(originalSize,wide,enter);return;}
            age+=Time.deltaTime;float t=Mathf.SmoothStep(0,1,age);
            Vector3 target=cameraView.transform.parent!=null?cameraView.transform.parent.TransformPoint(originalLocalPosition):player.position+followOffset;
            cameraView.transform.position=Vector3.Lerp(focus,target,t);
            cameraView.orthographicSize=Mathf.Lerp(wide,originalSize,t);
            if(age>=1)Destroy(this);
        }
        void OnDestroy(){if(cameraView!=null){cameraView.orthographicSize=originalSize;if(cameraView.transform.parent!=null)cameraView.transform.localPosition=originalLocalPosition;else if(player!=null)cameraView.transform.position=player.position+followOffset;}}
    }
}
