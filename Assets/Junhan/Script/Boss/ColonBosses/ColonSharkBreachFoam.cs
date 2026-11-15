using UnityEngine;

namespace Vampire
{
    // A short ground burst at the already-telegraphed hit; the landing variant is purely cosmetic.
    // It owns no collider or damage loop and is destroyed with the encounter's effect root.
    public sealed class ColonSharkBreachFoam : MonoBehaviour
    {
        public bool IsBreach { get; private set; }
        public float CreatedAt { get; private set; }
        ColonBossRuntime owner;
        Vector2 center;
        float radius,age;
        LineRenderer ring;
        SpriteRenderer[] foam;
        public static ColonSharkBreachFoam Create(ColonBossRuntime boss,Vector2 at,float reach,bool breach)
        {
            var go=new GameObject(breach?"Shark breach · synchronized ground impact":"Shark landing · harmless foam");
            go.transform.SetParent(boss.Effects,false);
            var fx=go.AddComponent<ColonSharkBreachFoam>();fx.owner=boss;fx.center=at;fx.radius=reach;
            fx.IsBreach=breach;fx.CreatedAt=Time.time;
            fx.ring=FoodBossEffects.Line(go.transform,new Vector2[41],breach?.13f:.055f,new Color(1,.95f,.73f),false,486,true);
            ColonAttackWall.Mask(fx.ring.gameObject,at);
            fx.foam=new SpriteRenderer[breach?12:6];
            for(int i=0;i<fx.foam.Length;i++)
            {
                var r=new GameObject("Foam droplet "+i).AddComponent<SpriteRenderer>();r.transform.SetParent(go.transform,false);
                r.sprite=BombCircleArt.Sprite;r.sortingOrder=487;fx.foam[i]=r;ColonAttackWall.Mask(r.gameObject,at);
            }
            fx.Draw(0);return fx;
        }
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}
            if(owner.EncounterPaused||owner.Immobilized)return;
            age+=Time.deltaTime;float q=age/(IsBreach?.48f:.36f);
            if(q>=1){Destroy(gameObject);return;}Draw(q);
        }
        void Draw(float q)
        {
            float spread=Mathf.Lerp(.18f,radius,Mathf.Sqrt(q));
            var color=new Color(1,.95f,.74f,(IsBreach?.90f:.38f)*(1-q));ring.startColor=ring.endColor=color;
            for(int i=0;i<41;i++)
            {
                float a=i/40f*Mathf.PI*2,rough=IsBreach?1+Mathf.Sin(i*2.37f)*.045f*(1-q):1;
                ring.SetPosition(i,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.57f)*spread*rough);
            }
            for(int i=0;i<foam.Length;i++)
            {
                float a=i/(float)foam.Length*Mathf.PI*2;
                Vector2 landing=ColonAttackWall.Stop(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.57f)*radius*(.55f+(i%3)*.11f),.04f);
                float lift=Mathf.Sin(q*Mathf.PI)*(IsBreach?.55f:.14f)*(1+(i%2)*.25f);
                foam[i].transform.position=Vector2.Lerp(center,landing,q)+Vector2.up*lift;
                float diameter=(IsBreach?.16f:.09f)*(1-q*.75f);
                foam[i].transform.localScale=Vector3.one*diameter/Mathf.Max(.001f,foam[i].sprite.bounds.size.x);
                foam[i].color=color;
            }
        }
    }
}
