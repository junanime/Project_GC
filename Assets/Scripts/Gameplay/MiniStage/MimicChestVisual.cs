using UnityEngine;

namespace Vampire
{
    // Reuse the game's chest art, adding a mouth and eyes only for the selected mimic.
    // Restore the pooled monster's normal renderer when this room's monster despawns.
    public sealed class MimicChestVisual : MonoBehaviour
    {
        SpriteRenderer original, chest;
        Transform art, mouth;
        bool active, originalEnabled;
        static Sprite square;
        public void Configure()
        {
            original=GetComponentInChildren<SpriteAnimator>(true)?.GetComponent<SpriteRenderer>();
            if(original==null)return;
            originalEnabled=original.enabled;original.enabled=false;active=true;
            if(art==null)
            {
                art=new GameObject("Chest mimic body").transform;art.SetParent(transform,false);
                var shell=new GameObject("Chest shell");shell.transform.SetParent(art,false);
                chest=shell.AddComponent<SpriteRenderer>();
                var blueprint=RemakeBalance.Current.levelUpChest;
                chest.sprite=blueprint.Visual(blueprint.closedChest);
                shell.transform.localScale=Vector3.one*(1.2f/chest.sprite.bounds.size.x);
                chest.sortingLayerID=original.sortingLayerID;chest.sortingOrder=original.sortingOrder+1;
                mouth=Piece("Mouth",Vector2.zero,new Vector2(.78f,.18f),new Color(.05f,.035f,.055f)).transform;
                mouth.localPosition=new Vector3(0,-.11f,-.01f);
                for(int i=0;i<4;i++)
                {
                    var tooth=Piece("Tooth",new Vector2(-.27f+i*.18f,-.07f),new Vector2(.09f,.10f),new Color(1,.96f,.79f));
                    tooth.sortingOrder+=1;
                }
                for(int i=0;i<2;i++)
                {
                    var eye=Piece("Eye",new Vector2(i==0?-.22f:.22f,.12f),new Vector2(.13f,.11f),Color.white);
                    var pupil=Piece("Pupil",new Vector2(i==0?-.20f:.20f,.10f),new Vector2(.06f,.06f),Color.black);pupil.sortingOrder+=1;
                }
            }
            art.gameObject.SetActive(true);
        }
        SpriteRenderer Piece(string label,Vector2 position,Vector2 size,Color color)
        {
            if(square==null)square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            var go=new GameObject(label);go.transform.SetParent(art,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=color;renderer.sortingLayerID=chest.sortingLayerID;renderer.sortingOrder=chest.sortingOrder+1;return renderer;
        }
        void LateUpdate()
        {
            if(!active||art==null)return;
            original.enabled=false;chest.color=original.color;
            float bounce=Mathf.Abs(Mathf.Sin(Time.time*8));art.localPosition=new Vector3(0,.08f*bounce,0);
            mouth.localScale=new Vector3(.78f,.16f+bounce*.10f,1);
        }
        void OnDisable()
        {
            if(!active)return;active=false;
            if(original!=null)original.enabled=originalEnabled;
            if(art!=null)art.gameObject.SetActive(false);
        }
    }
}
