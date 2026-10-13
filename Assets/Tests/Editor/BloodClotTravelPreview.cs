using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    /// <summary>Preview samples the same trajectory/frame function used by live travel.</summary>
    public static class BloodClotTravelPreview
    {
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=2.7f;camera.aspect=1.6f;
            camera.transform.position=new Vector3(-.4f,1.6f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.14f,.10f,.13f);
            var portal=new GameObject("Blood clot").AddComponent<SpriteRenderer>();
            portal.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_open.png");
            portal.transform.position=new Vector3(1,.65f,0);portal.transform.localScale=Vector3.one*(1.3f/portal.sprite.bounds.size.y);portal.sortingOrder=2;
            var actor=new GameObject("Actor").AddComponent<SpriteRenderer>();actor.sortingOrder=5;
            var label=Text(new Vector3(-.4f,3.9f,0),.48f);
            var subtitle=Text(new Vector3(-.4f,-.65f,0),.27f);
            var chick=new SpriteRenderer[3];
            for(int i=0;i<3;i++){chick[i]=new GameObject("Dizzy chick").AddComponent<SpriteRenderer>();chick[i].sprite=BloodClotTravel.Frames("Ashi")[0];chick[i].sortingOrder=7;chick[i].transform.localScale=Vector3.one*.16f;}
            var rt=new RenderTexture(640,400,24);camera.targetTexture=rt;
            var image=new Texture2D(640,400,TextureFormat.RGB24,false);
            foreach(var key in new[]{"Hyuki","Shini","Ari","Ashi"})
            {
                string folder="Library/BloodClotPreview/"+key;Directory.CreateDirectory(folder);
                var frames=BloodClotTravel.Frames(key);float size=key=="Hyuki"?.86f/.72f:1.2f;
                string name=key=="Hyuki"?"혁이":key=="Shini"?"신이":key=="Ari"?"아리":"아시";
                for(int n=0;n<110;n++)
                {
                    float seconds=n/25f;bool eject=seconds>=2.05f;float t=Mathf.Clamp01((seconds-(eject?2.05f:0))/BloodClotTravel.Duration);
                    var p=BloodClotTravel.Sample(key,eject,t,1.05f,1.0f);
                    var source=eject?new Vector3(1,0,0):new Vector3(-2.4f,0,0);
                    var destination=eject?new Vector3(-1.15f,0,0):new Vector3(1,0,0);
                    actor.sprite=frames[Mathf.Clamp(p.frame,0,frames.Length-1)];actor.flipX=eject&&key!="Hyuki";
                    actor.transform.localScale=Vector3.one*(size/frames[0].bounds.size.y);
                    actor.transform.position=Vector3.Lerp(source,destination,p.progress)+Vector3.up*(p.lift+actor.sprite.bounds.size.y*actor.transform.localScale.x*.5f);
                    actor.transform.rotation=Quaternion.Euler(0,0,p.angle);actor.color=new Color(1,1,1,p.alpha);
                    label.text=name+" · "+(eject?"혈전 배출":"혈전 다이빙");
                    subtitle.text=eject?(key=="Hyuki"?"고정 자세 · 퉁… 퉁… · 착지 유지":"착지 후 이동 입력까지 마지막 자세 유지"):(key=="Hyuki"?"사뿐사뿐 → 콩! → 최고점부터 웅크리기":"공통 1.8초 · 다른 몬스터는 계속 행동");
                    for(int i=0;i<3;i++)
                    {
                        chick[i].enabled=key=="Ari"&&eject&&t>=.72f;
                        float angle=seconds*6+i*Mathf.PI*2/3;
                        chick[i].transform.position=actor.bounds.center+Vector3.up*.8f+new Vector3(Mathf.Cos(angle)*.6f,Mathf.Sin(angle)*.14f);
                    }
                    float pulse=eject?Mathf.Sin(Mathf.Clamp01(t/.18f)*Mathf.PI):Mathf.Sin(Mathf.Clamp01((t-.72f)/.28f)*Mathf.PI);
                    portal.transform.localScale=new Vector3(1+pulse*.1f,1-pulse*.08f,1)*(1.3f/portal.sprite.bounds.size.y);
                    camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,400),0,0);image.Apply();
                    File.WriteAllBytes(folder+"/"+n.ToString("D3")+".png",image.EncodeToPNG());
                }
            }
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
            Debug.Log("BLOOD_CLOT_PREVIEWS_RENDERED");
        }
        static TextMeshPro Text(Vector3 position,float size)
        {
            var text=new GameObject("Caption").AddComponent<TextMeshPro>();
            text.font=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")?.font;
            text.fontSize=size*10;text.alignment=TextAlignmentOptions.Center;text.color=new Color(1,.9f,.8f);
            text.rectTransform.sizeDelta=new Vector2(9,1);text.transform.position=position;
            text.GetComponent<MeshRenderer>().sortingOrder=10;return text;
        }
    }
}
