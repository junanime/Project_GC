using UnityEngine;
namespace Vampire {
 public static class ColonSaltParts {
  static Sprite[] pieces;
  public static Sprite Get(int i){if(pieces==null){var art=ColonSpecialEffects.Art("salt",0);pieces=new Sprite[4];
   // Keep the face on the lower central crystal instead of slicing through it.
   var regions=new[]{new Rect(0,.53f,1,.47f),new Rect(0,0,.30f,.53f),new Rect(.30f,0,.40f,.53f),new Rect(.70f,0,.30f,.53f)};
   for(int k=0;k<4;k++){var s=art.rect;var n=regions[k];var rect=new Rect(s.x+n.x*s.width,s.y+n.y*s.height,n.width*s.width,n.height*s.height);pieces[k]=Sprite.Create(art.texture,rect,new Vector2(.5f,.5f),art.pixelsPerUnit);pieces[k].name="Salt body crystal "+k;}}return pieces[i];}
 }
}
