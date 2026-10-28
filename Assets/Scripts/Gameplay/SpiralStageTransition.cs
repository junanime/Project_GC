using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire
{
    public sealed class SpiralStageTransition:MonoBehaviour
    {
        public const float Duration=2f;
        public float Closed {get;private set;}
        Material material;Canvas canvas;
        public static SpiralStageTransition Create()
        {
            var go=new GameObject("나선형 스테이지 전환",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var s=go.AddComponent<SpiralStageTransition>();s.canvas=go.GetComponent<Canvas>();s.canvas.renderMode=RenderMode.ScreenSpaceOverlay;s.canvas.sortingOrder=30000;
            var image=new GameObject("Five curved iris blades",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();image.transform.SetParent(go.transform,false);
            image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            s.material=new Material(Resources.Load<Shader>("AmphibiousCapsule/Iris"));image.material=s.material;s.SetClosed(0);return s;
        }
        public void SetClosed(float value){Closed=value;material.SetFloat("_Progress",value);material.SetFloat("_Aspect",(float)Screen.width/Mathf.Max(1,Screen.height));}
        public void SetVisible(bool visible)=>canvas.enabled=visible;
        public IEnumerator Animate(bool close)
        {
            SetVisible(true);SetClosed(close?0:1);
            for(float t=0;t<Duration;t+=Time.deltaTime){float p=Mathf.SmoothStep(0,1,t/Duration);SetClosed(close?p:1-p);yield return null;}
            SetClosed(close?1:0);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
