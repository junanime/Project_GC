using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vampire;
public static class SnailBossArtPreview
{
    [MenuItem("Tools/Junhan2/Export roll cake idle preview")]
    public static void Run()
    {
        SnailBossSetup.Install();SnailBossChecks.Run();
        var root=new GameObject("Snail idle export");var cameraObject=new GameObject("Snail art camera");
        var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.75f;
        camera.transform.position=new Vector3(0,1.9f,-30);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(1,.98f,.95f);camera.cullingMask=1<<30;
        var target=new RenderTexture(1024,768,24);camera.targetTexture=target;
        var visual=root.AddComponent<SnailBossVisual>();visual.Configure(4.2f);
        foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>())sr.gameObject.layer=30;
        Directory.CreateDirectory("Logs/SnailIdle");
        var apply=typeof(SnailBossVisual).GetMethod("ApplyPose",BindingFlags.Instance|BindingFlags.NonPublic);
        var clock=typeof(SnailBossVisual).GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            for(int phase=1;phase<=2;phase++)
            {
                visual.SetPhase(phase==2);
                for(int frame=0;frame<12;frame++)
                {
                    clock.SetValue(visual,frame*(Mathf.PI*2/2.2f/12));
                    apply.Invoke(visual,new object[]{.24f});camera.Render();
                    var old=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(1024,768,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,1024,768),0,0);image.Apply();
                    File.WriteAllBytes("Logs/SnailIdle/phase"+phase+"-"+frame.ToString("00")+".png",image.EncodeToPNG());
                    RenderTexture.active=old;Object.DestroyImmediate(image);
                }
            }
            Debug.Log("SNAIL_IDLE_EXPORT_PASS");
        }
        finally {camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(root);}
    }
}
