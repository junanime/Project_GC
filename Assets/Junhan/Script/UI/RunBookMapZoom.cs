using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Vampire
{
    // A single transform carries map texture, flood overlay and discovered markers.
    public sealed class RunBookMapZoom:MonoBehaviour,IScrollHandler,IPointerDownHandler,IPointerUpHandler,IDragHandler
    {
        readonly Dictionary<int,Vector2> pointers=new Dictionary<int,Vector2>();
        RectTransform viewport,map;Camera eventCamera;
        RectTransform[] markers;
        public float Zoom {get;private set;}=1;
        public void Initialize(RectTransform frame,RectTransform image)
        {
            viewport=frame;map=image;
            var images=map.GetComponentsInChildren<Image>();
            markers=new RectTransform[images.Length];
            for(int i=0;i<images.Length;i++)markers[i]=images[i].rectTransform;
        }
        Vector2 Local(Vector2 screen){RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,screen,eventCamera,out var p);return p;}
        public void Gesture(Vector2 previous,Vector2 current,float ratio)
        {
            if(map==null||float.IsNaN(ratio)||float.IsInfinity(ratio))return;
            float next=Mathf.Clamp(Zoom*ratio,1,5);
            var offset=current-(previous-map.anchoredPosition)*(next/Zoom);
            Zoom=next;map.localScale=Vector3.one*Zoom;
            // Positions zoom with the map; icons remain readable without hiding nearby points.
            foreach(var marker in markers)if(marker!=null)marker.localScale=Vector3.one*(Mathf.Min(Zoom,1.35f)/Zoom);
            Vector2 limit=Vector2.Max(Vector2.zero,(map.rect.size*Zoom-viewport.rect.size)*.5f);
            map.anchoredPosition=new Vector2(Mathf.Clamp(offset.x,-limit.x,limit.x),Mathf.Clamp(offset.y,-limit.y,limit.y));
        }
        public void OnScroll(PointerEventData e){eventCamera=e.enterEventCamera;var at=Local(e.position);Gesture(at,at,Mathf.Pow(1.18f,Mathf.Clamp(e.scrollDelta.y,-1,1)));e.Use();}
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||pointers.Count>=2)return;eventCamera=e.pressEventCamera;pointers[e.pointerId]=Local(e.position);}
        public void OnPointerUp(PointerEventData e){pointers.Remove(e.pointerId);}
        public void OnDrag(PointerEventData e)
        {
            if(!pointers.TryGetValue(e.pointerId,out var before))return;
            Vector2 after=Local(e.position);
            if(pointers.Count==2){foreach(var pair in pointers)if(pair.Key!=e.pointerId){float distance=Vector2.Distance(before,pair.Value);if(distance>5)Gesture((before+pair.Value)*.5f,(after+pair.Value)*.5f,Vector2.Distance(after,pair.Value)/distance);break;}}
            else Gesture(before,after,1);
            pointers[e.pointerId]=after;e.Use();
        }
        void OnDisable(){pointers.Clear();}
        void OnApplicationFocus(bool focus){if(!focus)pointers.Clear();}
    }
}
