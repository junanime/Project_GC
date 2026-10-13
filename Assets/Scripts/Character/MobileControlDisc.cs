using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Geometry keeps touch circles smooth at any phone resolution; no opaque sprite backing.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MobileControlDisc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect=GetPixelAdjustedRect();
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            const int segments=64;
            mesh.AddVert(rect.center,color,Vector2.zero);
            var rim=new Color(.93f,.83f,.58f,.60f);
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(rect.center+direction*(radius-2),color,Vector2.zero);
                mesh.AddVert(rect.center+direction*radius,rim,Vector2.zero);
                if(i==0)continue;
                int inner=1+i*2;
                mesh.AddTriangle(0,inner-2,inner);
                mesh.AddTriangle(inner-2,inner-1,inner+1);
                mesh.AddTriangle(inner-2,inner+1,inner);
            }
        }
        public override bool Raycast(Vector2 screenPoint,Camera eventCamera)
        {
            if(!base.Raycast(screenPoint,eventCamera))return false;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out var point))return false;
            return Vector2.Distance(point,rectTransform.rect.center)<=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
        }
    }
}
