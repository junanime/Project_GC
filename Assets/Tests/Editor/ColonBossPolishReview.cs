#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    public static partial class ColonBossPresentationCapture
    {
        internal static Vector2 OpenPolishArena(FiniteStomachMap map)
        {
            var homes=Object.FindObjectsOfType<ColonBossHabitat>();
            Vector2 best=map.ClampToFloor(map.UVToWorld(new Vector2(.30f,.25f)),8);
            float score=-1;
            for(float y=.12f;y<.89f;y+=.025f)for(float x=.12f;x<.89f;x+=.025f)
            {
                Vector2 point=map.UVToWorld(new Vector2(x,y));
                if(map.FloorClearance(point,true)<7.5f)continue;
                float nearest=homes.Length==0?30:homes.Min(h=>Vector2.Distance(point,h.transform.position));
                if(nearest>score){score=nearest;best=point;}
            }
            return best;
        }
        internal static void ClearInspectionPopups()
        {
            foreach(var scroll in Object.FindObjectsOfType<PrescriptionScrollView>())scroll.SetExpanded(false);
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
        }
        static IEnumerator CapturePolish(LevelManager level)
        {
            var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;
            var director=level.GetComponent<ColonBossDirector>();
            ApothecaryUI.Instance.Show("hud");
            Vector2 arena=OpenPolishArena(map);ClearInspectionPopups();
            float originalZoom=Camera.main.orthographicSize;
            int previousRate=Time.captureFramerate;Time.captureFramerate=30;
            File.WriteAllText(Output+"/capture-method.txt","Actual Level1/Stage2 runtime, original assets and live PlayPattern routines. Fixed 30 fps simulation/camera captures, no interpolated or reconstructed frames. QA controls encounter selection, target placement, invulnerability and camera centering. Open arena is chosen away from unrelated habitats. Shark inspection zoom=6; manatee fits house, all door warnings and player inside the HUD-safe region; other zoom unchanged. Production camera is unchanged.\n");
            File.WriteAllText(Output+"/checks.txt","");
            string only=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-colonCapture="))?.Substring("-colonCapture=".Length);
            try
            {
                foreach(ColonBossKind kind in Enum.GetValues(typeof(ColonBossKind)))
                {
                    if(!string.IsNullOrEmpty(only)&&!string.Equals(kind.ToString(),only,StringComparison.OrdinalIgnoreCase))continue;
                    var home=director.Habitats.First(h=>h.Kind==kind);
                    var front=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*1.5f:(Vector2)home.transform.position+Vector2.down*2;
                    ColonContentSmoke.Position(player,map.ClampToField(front,map.WallWidth+.3f));yield return new WaitForSeconds(.8f);
                    InteractionFocus.SelectTouched(home);home.TryInteract();yield return null;
                    var boss=ColonBossRuntime.Active.FirstOrDefault(b=>b.Kind==kind&&!b.Dead);
                    if(boss==null)throw new Exception("Polish capture interaction failed: "+kind);
                    boss.AutoPatterns=false;
                    float timeout=Time.time+15;while(boss.Arriving&&Time.time<timeout)yield return null;
                    if(boss.Arriving)throw new Exception("Polish arrival timed out: "+kind);
                    if(kind!=ColonBossKind.Manatee)boss.PlaceAt(arena-Vector2.right*2);
                    Vector2 target=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*3.2f:(Vector2)boss.transform.position+Vector2.right*(kind==ColonBossKind.Hedgehog?12:3.5f)+Vector2.down*.35f;
                    ColonContentSmoke.Position(player,map.ClampToField(target,map.WallWidth+.3f));
                    boss.enabled=false;yield return new WaitForSeconds(2.4f);
                    Camera.main.orthographicSize=kind==ColonBossKind.Manatee||kind==ColonBossKind.Shark?6:originalZoom;
                    Bounds homeBounds=new Bounds(home.transform.position,Vector3.zero);
                    foreach(var r in home.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null))homeBounds.Encapsulate(r.bounds);
                    homeBounds.Encapsulate(player.transform.position+Vector3.up*1.2f);
                    if(kind==ColonBossKind.Manatee)
                    {
                        homeBounds.Encapsulate(player.transform.position+Vector3.down*.3f);
                        foreach(var door in home.Doors){homeBounds.Encapsulate((Vector3)door+new Vector3(-1.8f,-1.8f,0));homeBounds.Encapsulate((Vector3)door+new Vector3(1.8f,1.8f,0));}
                        Camera.main.orthographicSize=Mathf.Max(6,homeBounds.extents.y/.53f,homeBounds.extents.x/(16f/9*.85f));
                    }
                    int pattern=kind==ColonBossKind.Flamingo?0:kind==ColonBossKind.Shark?3:kind==ColonBossKind.Pudding?3:kind==ColonBossKind.Hedgehog?1:kind==ColonBossKind.Turtle?3:4;
                    string name=kind.ToString();string directory=Output+"/"+name;Directory.CreateDirectory(directory);
                    // This directory belongs to this capture; old frames cannot leak into a shorter rerun.
                    foreach(string file in Directory.GetFiles(directory,"*.png"))File.Delete(file);
                    File.WriteAllText(directory+"/times.csv","frame,game_seconds,pattern\n");
                    int frames=0;float start=Time.time;
                    var probe=boss.Visual.Main.gameObject.AddComponent<ColonBossTintCaptureProbe>();
                    probe.AfterTint=()=>
                    {
                        var body=boss.Visual.Main;
                        if(body.sprite==null||float.IsNaN(body.transform.position.x))throw new Exception("Missing/invalid boss artwork: "+kind);
                        Vector2 center=kind==ColonBossKind.Manatee?(Vector2)homeBounds.center:
                            kind==ColonBossKind.Shark?new Vector2(arena.x,arena.y+1.8f):
                            kind==ColonBossKind.Hedgehog?(Vector2)boss.transform.position+new Vector2(3.6f,.8f):
                            ((Vector2)boss.transform.position+(Vector2)player.transform.position)*.5f+Vector2.up*.8f;
                        var camera=Camera.main;
                        if(kind==ColonBossKind.Manatee)
                        {
                            var view=homeBounds;view.Encapsulate(player.transform.position+Vector3.down*.3f);view.Encapsulate(player.transform.position+Vector3.up*1.2f);
                            camera.orthographicSize=Mathf.Max(6,view.extents.y/.50f,view.extents.x/(16f/9*.85f));
                            center=(Vector2)view.center;
                        }
                        else if(kind==ColonBossKind.Shark)camera.orthographicSize=6;
                        camera.transform.position=new Vector3(center.x,center.y,camera.transform.position.z);
                        Frame(name+"/"+frames.ToString("D4"));
                        File.AppendAllText(directory+"/times.csv",frames+","+(Time.time-start).ToString("F5",CultureInfo.InvariantCulture)+","+boss.CurrentPattern+"\n");frames++;
                    };
                    yield return new WaitForSeconds(.4f);
                    if(!boss.PlayPattern(pattern))throw new Exception("Polish pattern failed: "+kind);
                    timeout=Time.time+25;while(boss.Busy&&Time.time<timeout&&probe.Failure==null)yield return null;
                    if(kind==ColonBossKind.Hedgehog&&probe.Failure==null&&!boss.Busy)
                    {
                        yield return new WaitForSeconds(.15f);
                        if(!boss.PlayPattern(3))throw new Exception("Chestnut recall could not start");
                        timeout=Time.time+15;while(boss.Busy&&Time.time<timeout&&probe.Failure==null)yield return null;
                        yield return new WaitForSeconds(.6f);
                        if(!boss.PlayPattern(1))throw new Exception("Returned chestnuts could not launch");
                        timeout=Time.time+15;while(boss.Busy&&Time.time<timeout&&probe.Failure==null)yield return null;
                    }
                    yield return new WaitForSeconds(.7f);
                    probe.enabled=false;var failure=probe.Failure;Object.Destroy(probe);if(failure!=null)throw failure;
                    if(boss.Busy)throw new Exception("Polish pattern timed out: "+kind);
                    File.AppendAllText(Output+"/checks.txt","PASS "+kind+" actual habitat interaction, original artwork, real pattern "+pattern+", completed and recovered; frames="+frames+" at30fps\n");
                    if(kind==ColonBossKind.Hedgehog)
                        yield return ColonChestnutLifecycleReview.Run(level,boss,(ok,message)=>{
                            if(!ok)throw new Exception("CHESTNUT_LIFECYCLE: "+message);
                            File.AppendAllText(Output+"/checks.txt","PASS "+message+"\n");
                        });
                    Object.Destroy(boss.gameObject);yield return null;Silence(level);
                    foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
                }
            }
            finally{Time.captureFramerate=previousRate;if(Camera.main!=null)Camera.main.orthographicSize=originalZoom;}
        }
    }
}
#endif
