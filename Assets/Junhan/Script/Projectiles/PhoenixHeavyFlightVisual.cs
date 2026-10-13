using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(1110)]
    public sealed class PhoenixHeavyFlightVisual : MonoBehaviour
    {
        SyringeProjectile projectile;
        SpriteRenderer source;
        PhoenixRadianceLayer needle;
        float age;
        public void Bind(SyringeProjectile owner,SpriteRenderer original)
        {
            projectile=owner;source=original;age=0;
            if(needle==null)needle=new PhoenixRadianceLayer(transform,"HeavyCharge");
            enabled=true;if(source!=null)source.forceRenderingOff=true;
        }
        public void Clear()
        {
            if(source!=null)source.forceRenderingOff=false;
            needle?.Hide();enabled=false;
        }
        void LateUpdate()
        {
            if(projectile==null||!projectile.IsFlying||source==null||!source.enabled){needle?.Hide();return;}
            age+=Time.deltaTime;
            var baseSize=source.sprite.bounds.size;var scale=source.transform.lossyScale;
            float length=Mathf.Clamp(Mathf.Max(baseSize.x*Mathf.Abs(scale.x),baseSize.y*Mathf.Abs(scale.y))*1.65f,.7f,3.4f);
            Vector2 d=projectile.VisualFlightDirection;
            needle.Draw(transform.position+(Vector3)d*length*.25f,Vector2.one*length,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,0,1,source,0,age);
        }
        void OnDisable(){if(source!=null)source.forceRenderingOff=false;needle?.Hide();}
    }
}
