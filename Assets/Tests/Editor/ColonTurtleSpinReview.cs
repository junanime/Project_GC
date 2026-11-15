#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class ColonTurtleSpinReview
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception("TURTLE_SPIN: "+message);}
        public static IEnumerator Capture(LevelManager level,string output,Action<string> frame)
        {
            var map=FiniteStomachMap.Instance;var player=level.PlayerCharacter;
            Vector2 arena=ColonBossPresentationCapture.OpenPolishArena(map);
            Require(map.FloorClearance(arena,true)>6,"open floor fixture must contain the whole dash");
            ColonContentSmoke.Position(player,arena+Vector2.right*2);
            var boss=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,arena-Vector2.right*3,null);boss.AutoPatterns=false;
            float timeout=Time.time+15;while(boss.Arriving&&Time.time<timeout)yield return null;
            Require(!boss.Arriving,"arrival completed before pattern review");
            boss.enabled=false;ApothecaryUI.Instance.Show("hud");ColonBossPresentationCapture.ClearInspectionPopups();
            var body=boss.Visual.Main;var originalScale=body.transform.localScale;int captureRate=Time.captureFramerate;
            Time.captureFramerate=30;
            string report=output+"/spin-checks.txt";File.WriteAllText(report,"");
            try
            {
                foreach(int direction in new[]{1,-1})
                {
                    string clip=direction>0?"Turtle-spin-right":"Turtle-spin-left";
                    string directory=output+"/"+clip;Directory.CreateDirectory(directory);
                    foreach(string previous in Directory.GetFiles(directory,"*.png"))File.Delete(previous);
                    File.WriteAllText(directory+"/times.csv","frame,game_seconds\n");
                    File.WriteAllText(directory+"/motion.csv","frame,body_angle,wind_angle,body_z,scale_x,scale_y,opacity,flip_x,wind_strength,plane_y,center_y\n");
                    boss.PlaceAt(arena-Vector2.right*3*direction);ColonContentSmoke.Position(player,arena+Vector2.right*2*direction);
                    boss.Visual.ResetPose();boss.Visual.Face(Vector2.right*direction);yield return new WaitForSeconds(.25f);
                    originalScale=body.transform.localScale;
                    frame(clip+"-before");
                    int captured=0,turnSamples=0;float lastAngle=0,maxAngle=0;bool[] quadrants=new bool[4],views=new bool[8];
                    float start=Time.time;bool expectedFlip=direction>0;
                    var probe=body.gameObject.AddComponent<ColonBossTintCaptureProbe>();
                    probe.AfterTint=()=>
                    {
                        var wind=boss.Effects.GetComponentInChildren<ColonTurtleSpinWind>();
                        float angle=boss.Visual.GroundSpin;
                        if(wind!=null&&angle>1)
                        {
                            Require(angle>=lastAngle-.001f,"body must not reverse or snap while spinning");
                            Require(Mathf.Abs(Mathf.DeltaAngle(body.transform.localEulerAngles.z,0))<.05f,"shell must stay upright: yaw must never rotate the body upside-down");
                            var surface=new MaterialPropertyBlock();body.GetPropertyBlock(surface);
                            Require(surface.GetFloat("_Spin")==0,"no partial-shell UV warp or stationary underlying shell");
                            int view=Mathf.FloorToInt(Mathf.Repeat(angle*(expectedFlip?-1:1),360)/45)%8;
                            Require(boss.Visual.GroundSpinFrame==view,"complete turntable view follows the same facing-corrected spin clock");views[view]=true;
                            Require(Mathf.Abs(boss.Visual.GroundSpinPlane.localPosition.x)<.001f,"rotation stays on the central vertical axis, without lateral sliding/shivering");
                            Require(wind.GetComponentsInChildren<LineRenderer>(true).Length==8,"four front/back wind orbits include an inner belt around the underside");
                            Require(body.flipX==expectedFlip,"spin must not replace rotation with mirrored flashes");
                            Vector2 pixels=ColonTurtleSpinFrames.PixelSize(view);
                            Require(Mathf.Abs(body.transform.localScale.x*pixels.x-originalScale.x*ColonTurtleSpinFrames.ReferenceWidth)<.02f&&
                                Mathf.Abs(body.transform.localScale.y*pixels.y-originalScale.y*ColonTurtleSpinFrames.ReferenceHeight)<.02f,
                                "every turntable view has the approved shell's same visible width and height");
                            Require(boss.Visual.GroundSpinPlane.localScale==Vector3.one,"original shell proportions stay fixed throughout horizontal spin");
                            float floor=boss.Visual.transform.position.y+boss.Visual.Lift,lowest=float.PositiveInfinity,highest=float.NegativeInfinity;
                            foreach(var vertex in boss.Visual.GroundSpinFootprint)
                            {
                                Vector3 p=vertex;if(body.flipX)p.x=-p.x;p=body.transform.TransformPoint(p);
                                lowest=Mathf.Min(lowest,p.y-floor);highest=Mathf.Max(highest,p.y-floor);
                            }
                            Require(Mathf.Abs(lowest-.015f)<.015f,"the original shell's lowest opaque outline point stays on the ground");
                            Require(highest<boss.Visual.ReferenceHeight,"the upright shell never stands taller than the original body");
                            Require(Vector2.Distance(wind.WorldCenter,boss.Visual.GroundSpinCenter)<.02f,"wind and body share the same rotation centre");
                            Require(body.enabled&&Mathf.Abs(body.color.a-1)<.001f,"body stays visible without opacity flashing");
                            Require(AssetDatabase.GetAssetPath(body.sprite.texture)=="Assets/Resources/ColonBosses/turtle/Spin.png"&&body.sprite.name=="Spin_"+view.ToString("D3"),"one complete retracted shell view includes green top, ivory rim and orange underside");
                            Require(wind.WindAngle>angle&&Mathf.Abs(wind.WindAngle-angle*ColonTurtleSpinWind.WindRatio)<.01f,"wind turns faster in the same direction");
                            Require(boss.Visual.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.sprite!=null&&AssetDatabase.GetAssetPath(r.sprite.texture).StartsWith("Assets/Resources/ColonBosses/turtle/"))==1,"exactly one turtle body renderer: no static walk/pose shell under the turntable");
                            quadrants[(int)Mathf.Repeat(angle,360)/90]=true;lastAngle=angle;maxAngle=Mathf.Max(maxAngle,angle);turnSamples++;
                        }
                        var camera=Camera.main;camera.transform.position=new Vector3(arena.x,arena.y+1.5f,camera.transform.position.z);
                        frame(clip+"/"+captured.ToString("D4"));
                        string F(float x)=>x.ToString("0.#####",CultureInfo.InvariantCulture);
                        File.AppendAllText(directory+"/times.csv",captured+","+F(Time.time-start)+"\n");
                        File.AppendAllText(directory+"/motion.csv",string.Join(",",new[]{captured.ToString(),F(angle),F(wind==null?0:wind.WindAngle),F(body.transform.localEulerAngles.z),F(body.transform.localScale.x),F(body.transform.localScale.y),F(body.color.a),body.flipX.ToString(),F(wind==null?0:wind.Strength),F(boss.Visual.GroundSpinPlane.localScale.y),F(boss.Visual.GroundSpinCenter.y-boss.Visual.transform.position.y)})+"\n");captured++;
                    };
                    Require(boss.PlayPattern(0),"real pattern zero started");
                    float until=Time.time+8;while(boss.Busy&&Time.time<until&&probe.Failure==null)yield return null;
                    probe.enabled=false;var failure=probe.Failure;Object.Destroy(probe);if(failure!=null)throw failure;
                    Require(!boss.Busy,"pattern completes");yield return null;
                    Require(turnSamples>=30&&maxAngle>2100&&quadrants.All(q=>q),"all four yaw orientations and six horizontal turns were rendered");
                    Require(views.All(v=>v),"all eight full-shell yaw views were displayed during actual spin");
                    Require(boss.Effects.GetComponentInChildren<ColonTurtleSpinWind>(true)==null,"all wind objects cleaned after recovery");
                    Require(boss.Visual.GroundSpin==0&&Quaternion.Angle(body.transform.localRotation,Quaternion.identity)<.1f,"upright artwork restored");
                    Require(boss.Visual.GroundSpinProjection==0&&boss.Visual.GroundSpinPlane.localScale==Vector3.one&&boss.Visual.GroundSpinPlane.localPosition==Vector3.zero,"spin surface blend and contact offset are removed after recovery");
                    frame(clip+"-after");File.AppendAllText(report,"PASS "+clip+"; real PlayPattern(0); frames="+captured+"; spin samples="+turnSamples+"; all 8 complete yaw views displayed; same visible size and central vertical axis; whole shell/rim/belly/stalk turn; no static body, UV overlay or shiver; upright fixed contact; zero Z rotation; no flip/alpha flashes; faster front/back wind with inner underside belt; cleanup\n");
                }
                // The capture above disabled Update to hold a fixed camera/target composition.
                // Re-enable the component: Unity does not send a second OnDisable to an already
                // disabled behaviour when its GameObject is deactivated. Exercise the live game path.
                boss.PlaceAt(arena-Vector2.right*3);ColonContentSmoke.Position(player,arena+Vector2.right*2);
                boss.enabled=true;Require(boss.isActiveAndEnabled,"interruption fixture uses the enabled runtime lifecycle");
                Require(boss.PlayPattern(0),"interruption fixture starts");
                float limit=Time.time+5;while(boss.Visual.GroundSpin<300&&Time.time<limit)yield return null;
                Require(boss.Visual.GroundSpin>=300,"interruption occurs during active spin");
                float pausedAngle=boss.Visual.GroundSpin;Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);
                Require(Mathf.Abs(boss.Visual.GroundSpin-pausedAngle)<.001f,"pause freezes both rotation clocks");Time.timeScale=1;
                boss.gameObject.SetActive(false);yield return null;
                Require(boss.Effects.GetComponentsInChildren<Renderer>().All(r=>!r.enabled||!r.gameObject.activeInHierarchy),"disable removes every live wind renderer");
                Require(boss.Visual.GroundSpin==0&&boss.Visual.GroundSpinProjection==0&&boss.Visual.Roll==0&&boss.CurrentPattern==-1&&!boss.Busy,"disable clears the interrupted pose and pattern");
                ColonContentSmoke.Position(player,(Vector2)boss.transform.position+Vector2.right*2);
                boss.gameObject.SetActive(true);yield return new WaitForSeconds(.25f);
                Require(boss.isActiveAndEnabled&&!boss.Moving,"reactivated runtime idles normally with automatic patterns disabled");
                Require(boss.Visual.Main.enabled&&Quaternion.Angle(body.transform.localRotation,Quaternion.identity)<.1f,"reactivated body resumes its original orientation");
                Require(AssetDatabase.GetAssetPath(body.sprite)=="Assets/Resources/ColonBosses/turtle/Walk.png","reactivation restores original neutral artwork");
                Require(boss.Visual.GroundSpinPlane.localScale==Vector3.one&&boss.Visual.GroundSpinPlane.localPosition==Vector3.zero,"reactivation has no residual ground projection");
                Require(boss.Effects.GetComponentInChildren<ColonTurtleSpinWind>(true)==null,"reactivation has no orphaned wind");
                frame("Turtle-spin-reactivated");
                File.AppendAllText(report,"PASS pause, mid-spin disable cleanup and reactivated neutral artwork\n");
            }
            finally{Time.timeScale=1;Time.captureFramerate=captureRate;if(boss!=null)Object.Destroy(boss.gameObject);}
        }
    }
}
#endif
