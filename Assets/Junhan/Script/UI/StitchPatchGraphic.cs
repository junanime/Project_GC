using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Resolution-independent applique edges. Decoration never participates in UI hit testing.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StitchPatchGraphic : MaskableGraphic
    {
        public Color accent = new Color(.91f,.48f,.43f);
        public Color paper = new Color(1,.96f,.86f);
        public bool fill, corners, stars, twinkle, button, starOnly, titleGap, hideBottom, bannerOnly, bookmarkOnly, cornerButtons, lightThread, effectRibbon, flourishOnly;
        public bool bannerStars=true;
        public float bannerBottom=.42f;
        public float inset=7, radius=12;
        float nextFrame;
        protected override void Awake(){base.Awake();raycastTarget=false;}
        void Update()
        {
            if(twinkle && !GamePreferences.Current.reducedMotion && Time.unscaledTime>=nextFrame)
            {nextFrame=Time.unscaledTime+1f/15;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();if(r.width<4||r.height<4)return;
            if(flourishOnly){Diamond(vh,r.center,3,accent);Dot(vh,r.center+Vector2.left*7,1,accent);Dot(vh,r.center+Vector2.right*7,1,accent);return;}
            if(bannerOnly){Banner(vh,r);return;}
            if(bookmarkOnly){Bookmark(vh,r);return;}
            if(starOnly){Star(vh,r.center,Mathf.Min(r.width,r.height)*.43f,accent);return;}
            var edge=Shrink(r,2);float round=Mathf.Min(radius,edge.height*.48f);
            // A skin owns its outline and one inset seam. Overlay mode owns ONLY the seam.
            if(fill)
            {
                var shadow=edge;shadow.y-=1.6f;
                Solid(vh,shadow,round,button?new Color(.12f,.16f,.20f):new Color(.57f,.32f,.25f,.5f));
                Solid(vh,edge,round,button?new Color(.13f,.18f,.22f):Color.Lerp(accent,new Color(.55f,.32f,.24f),.2f));
                Solid(vh,Shrink(edge,1.8f),Mathf.Max(1,round-1.8f),Color.Lerp(accent,Color.white,.66f));
                Solid(vh,Shrink(edge,3.8f),Mathf.Max(1,round-3.8f),button?accent:paper,true);
            }
            var seam=Shrink(r,Mathf.Min(inset,Mathf.Min(r.width,r.height)*.25f));
            float seamRadius=Mathf.Min(Mathf.Max(2,round-inset+2),seam.height*.45f);
            float perimeter=2*(seam.width+seam.height-4*seamRadius)+2*Mathf.PI*seamRadius;
            int count=Mathf.Max(8,Mathf.RoundToInt(perimeter/9));float step=perimeter/count;
            Color thread=button||lightThread?Color.Lerp(accent,Color.white,.85f):Color.Lerp(accent,Color.white,.12f);
            for(int i=0;i<count;i++)
            {
                Vector2 a=Along(seam,seamRadius,(i*step+.7f)/perimeter),b=Along(seam,seamRadius,(i*step+step*.62f)/perimeter);
                if(hideBottom&&a.y<r.yMin+inset*2)continue;
                if(titleGap&&a.y>r.yMax-inset*2&&a.x>Mathf.Lerp(r.x,r.xMax,.29f)&&a.x<Mathf.Lerp(r.x,r.xMax,.69f))continue;
                Stitch(vh,a,b,thread);
            }
            if(corners)
            {
                float size=Mathf.Min(39,Mathf.Min(r.width,r.height)*.15f);
                for(int c=0;c<4;c++)
                {
                    Vector2 origin=new Vector2(c%2==0?r.x+2:r.xMax-2,c<2?r.y+2:r.yMax-2);
                    Vector2 axis=new Vector2(c%2==0?1:-1,c<2?1:-1);
                    Corner(vh,origin,axis,size,accent,cornerButtons);
                }
            }
            if(button&&r.width>110&&r.height>34)
            {Star(vh,new Vector2(r.x+17,r.center.y),4,new Color(1,.88f,.55f));Star(vh,new Vector2(r.xMax-17,r.center.y),4,new Color(1,.88f,.55f));}
            if(effectRibbon){Diamond(vh,new Vector2(r.x+15,r.center.y),4,accent);Diamond(vh,new Vector2(r.xMax-15,r.center.y),4,accent);}
            if(stars)
            {
                for(int i=0;i<6;i++)
                {
                    float x=i%2==0?r.x+28:r.xMax-28;
                    float y=Mathf.Lerp(r.y+30,r.yMax-30,new[]{.065f,.67f,.94f}[i/2]);
                    Diamond(vh,new Vector2(x,y),7,Color.Lerp(accent,paper,.25f));
                    float pulse=GamePreferences.Current.reducedMotion?.25f:Mathf.Pow(Mathf.Max(0,Mathf.Sin(Time.unscaledTime*1.5f+i*1.7f)),8);
                    if(twinkle&&pulse>.05f)
                    {
                        var white=new Color(1,1,.93f,pulse*.7f);
                        Line(vh,new Vector2(x-3,y+3),new Vector2(x+3,y+3),.9f,white);
                        Line(vh,new Vector2(x,y),new Vector2(x,y+6),.9f,white);
                    }
                }
            }
        }
        void Bookmark(VertexHelper vh,Rect r)
        {
            var inner=Shrink(r,8);
            var path=new System.Collections.Generic.List<Vector2>{new Vector2(inner.x,inner.yMax-6),new Vector2(inner.x+6,inner.yMax),new Vector2(inner.xMax-6,inner.yMax),new Vector2(inner.xMax,inner.yMax-6),new Vector2(inner.xMax,inner.y)};
            for(int i=1;i<=32;i++){float u=i/32f;path.Add(new Vector2(Mathf.Lerp(inner.xMax,inner.x,u),inner.y+Mathf.Sin(u*Mathf.PI)*r.height*.19f));}
            path.Add(new Vector2(inner.x,inner.yMax-6));
            float distance=0;
            for(int i=1;i<path.Count;i++)
            {
                var a=path[i-1];var b=path[i];float length=Vector2.Distance(a,b);
                for(float d=0;d<length;d+=1)
                    if(Mathf.Repeat(distance+d,8)<4.8f)Line(vh,Vector2.Lerp(a,b,d/length),Vector2.Lerp(a,b,Mathf.Min(length,d+1)/length),1.2f,accent);
                distance+=length;
            }
            Star(vh,new Vector2(r.x+16,r.y+19),5,new Color(1,.83f,.39f));
            Star(vh,new Vector2(r.xMax-16,r.y+19),5,new Color(1,.83f,.39f));
        }
        void Banner(VertexHelper vh,Rect r)
        {
            // Two arched seams follow the existing ribbon silhouette without repainting its art.
            for(int row=0;row<2;row++)for(int i=0;i<36;i++)
            {
                Vector2[] ends=new Vector2[2];
                for(int j=0;j<2;j++)
                {
                    float u=(i+(j==0?.12f:.70f))/36;
                    float y=(row==0?.86f:bannerBottom)-(row==0?.14f:.12f)*Mathf.Pow(u*2-1,2);
                    ends[j]=new Vector2(r.x+r.width*Mathf.Lerp(.17f,.83f,u),r.y+r.height*y);
                }
                Line(vh,ends[0]+Vector2.down,ends[1]+Vector2.down,2.3f,new Color(.56f,.16f,.20f,.6f));
                Line(vh,ends[0],ends[1],1.5f,new Color(1,.96f,.76f));
            }
            if(!bannerStars)return;
            Star(vh,new Vector2(r.x+r.width*.185f,r.y+r.height*.57f),6,new Color(1,.80f,.36f));
            Star(vh,new Vector2(r.x+r.width*.815f,r.y+r.height*.57f),6,new Color(1,.80f,.36f));
        }
        static Rect Shrink(Rect r,float n)=>new Rect(r.x+n,r.y+n,Mathf.Max(1,r.width-2*n),Mathf.Max(1,r.height-2*n));
        static Vector2 Along(Rect r,float rad,float t)
        {
            float w=r.width-2*rad,h=r.height-2*rad,arc=rad*Mathf.PI*.5f;
            float d=Mathf.Repeat(t,1)*(2*w+2*h+4*arc);
            if(d<w)return new Vector2(r.x+rad+d,r.y);d-=w;
            if(d<arc)return Arc(new Vector2(r.xMax-rad,r.y+rad),rad,-90+d/arc*90);d-=arc;
            if(d<h)return new Vector2(r.xMax,r.y+rad+d);d-=h;
            if(d<arc)return Arc(new Vector2(r.xMax-rad,r.yMax-rad),rad,d/arc*90);d-=arc;
            if(d<w)return new Vector2(r.xMax-rad-d,r.yMax);d-=w;
            if(d<arc)return Arc(new Vector2(r.x+rad,r.yMax-rad),rad,90+d/arc*90);d-=arc;
            if(d<h)return new Vector2(r.x,r.yMax-rad-d);d-=h;
            return Arc(new Vector2(r.x+rad,r.y+rad),rad,180+d/arc*90);
        }
        static Vector2 Arc(Vector2 center,float r,float a)=>center+new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad))*r;
        static Vector2[] RoundedPath(Rect r,float rad)
        {
            rad=Mathf.Clamp(rad,0,Mathf.Min(r.width,r.height)*.5f);
            var points=new Vector2[36];
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner<2?r.xMax-rad:r.x+rad,corner==0||corner==3?r.y+rad:r.yMax-rad);
                for(int i=0;i<9;i++)points[corner*9+i]=Arc(center,rad,-90+corner*90+i*11.25f);
            }
            return points;
        }
        static void Solid(VertexHelper vh,Rect r,float radius,Color c,bool gloss=false)
        {
            int first=vh.currentVertCount;
            vh.AddVert(r.center,gloss?Color.Lerp(c,Color.white,.10f):c,Vector2.zero);
            var points=RoundedPath(r,radius);
            foreach(var point in points)
            {
                var shade=gloss?Color.Lerp(Color.Lerp(c,new Color(.72f,.40f,.28f),.035f),Color.Lerp(c,Color.white,.32f),Mathf.InverseLerp(r.y,r.yMax,point.y)):c;
                vh.AddVert(point,shade,Vector2.zero);
            }
            for(int i=0;i<points.Length;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%points.Length);
        }
        static void Stitch(VertexHelper vh,Vector2 a,Vector2 b,Color thread)
        {
            Line(vh,a+Vector2.down*.55f,b+Vector2.down*.55f,1.7f,new Color(.39f,.18f,.13f,.20f));
            Line(vh,a,b,1.25f,thread);
            Dot(vh,a,.62f,thread);Dot(vh,b,.62f,thread);
        }
        static void Dot(VertexHelper vh,Vector2 center,float radius,Color c)
        {
            int first=vh.currentVertCount;vh.AddVert(center,c,Vector2.zero);
            for(int i=0;i<8;i++)vh.AddVert(Arc(center,radius,i*45),c,Vector2.zero);
            for(int i=0;i<8;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%8);
        }
        static void Corner(VertexHelper vh,Vector2 origin,Vector2 axis,float size,Color c,bool rivet)
        {
            CornerFace(vh,origin,axis,size,Color.Lerp(c,new Color(.40f,.18f,.20f),.28f));
            CornerFace(vh,origin+axis*1.3f,axis,size-2.6f,Color.Lerp(c,Color.white,.18f));
            for(int i=0;i<9;i++)
            {
                var a=origin+Vector2.Scale(axis,Arc(Vector2.zero,size-4,6+i*9));
                var b=origin+Vector2.Scale(axis,Arc(Vector2.zero,size-4,6+i*9+5));
                Stitch(vh,a,b,new Color(1,.94f,.79f));
            }
            if(rivet)
            {
                var center=origin+axis*(size*.31f);
                Solid(vh,new Rect(center.x-5,center.y-5,10,10),5,new Color(.76f,.41f,.20f));
                Solid(vh,new Rect(center.x-4,center.y-3.5f,8,8),4,new Color(1,.86f,.48f));
                Line(vh,center+new Vector2(-1.6f,-1.6f),center+new Vector2(1.6f,1.6f),1,new Color(.76f,.38f,.22f));
                Line(vh,center+new Vector2(-1.6f,1.6f),center+new Vector2(1.6f,-1.6f),1,new Color(.76f,.38f,.22f));
            }
        }
        static void CornerFace(VertexHelper vh,Vector2 origin,Vector2 axis,float size,Color c)
        {
            var points=new System.Collections.Generic.List<Vector2>();
            float round=Mathf.Min(8,size*.23f);
            for(int i=0;i<=8;i++)points.Add(Arc(Vector2.one*round,round,180+i*11.25f));
            for(int i=0;i<=18;i++)points.Add(Arc(Vector2.zero,size,i*5));
            int first=vh.currentVertCount;vh.AddVert(origin+axis*(size*.30f),c,Vector2.zero);
            foreach(var p in points)vh.AddVert(origin+Vector2.Scale(axis,p),Color.Lerp(c,Color.white,.12f),Vector2.zero);
            for(int i=0;i<points.Count;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%points.Count);
        }
        static void Diamond(VertexHelper vh,Vector2 p,float size,Color c)
        {
            int first=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
            for(int i=0;i<8;i++)vh.AddVert(Arc(p,size*(i%2==0?1:.30f),90+i*45),Color.Lerp(c,Color.white,.32f),Vector2.zero);
            for(int i=0;i<8;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%8);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            var v=(b-a).normalized;var normal=new Vector2(-v.y,v.x)*width*.5f;int n=vh.currentVertCount;
            vh.AddVert(a-normal,c,Vector2.zero);vh.AddVert(a+normal,c,Vector2.zero);vh.AddVert(b+normal,c,Vector2.zero);vh.AddVert(b-normal,c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Star(VertexHelper vh,Vector2 p,float radius,Color c)
        {
            StarFace(vh,p+Vector2.down*1.1f,radius+1,Color.Lerp(c,new Color(.35f,.20f,.17f),.28f));
            StarFace(vh,p,radius,Color.Lerp(c,Color.white,.38f));
            StarFace(vh,p+Vector2.down*.4f,radius*.68f,c);
        }
        static void StarFace(VertexHelper vh,Vector2 p,float radius,Color c)
        {
            int first=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
            for(int i=0;i<10;i++)vh.AddVert(Arc(p,radius*(i%2==0?1:.52f),90+i*36),c,Vector2.zero);
            for(int i=0;i<10;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%10);
        }
    }
}
