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
            Vector2 arena=map.ClampToFloor(map.UVToWorld(new Vector2(.3f,.25f)),8);
            Require(map.FloorClearance(arena,true)>6,"open floor fixture must contain the whole dash");
            ColonContentSmoke.Position(player,arena+Vector2.right*2);
            var boss=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,arena-Vector2.right*3,null);boss.AutoPatterns=false;
            float timeout=Time.time+15;while(boss.Arriving&&Time.time<timeout)yield return null;
            Require(!boss.Arriving,"arrival completed before pattern review");
            boss.enabled=false;ApothecaryUI.Instance.Show("hud");
            var body=boss.Visual.Main;var originalScale=body.transform.localScale;int captureRate=Time.captureFramerate;
            Time.captureFramerate=30;
            string report=output+"/spin-checks.txt";File.WriteAllText(report,"");
            try
            {
                foreach(int direction in new[]{1,-1})
                {
                    string clip=direction>0?"Turtle-spin-right":"Turtle-spin-left";
                    string directory=output+"/"+clip;Directory.CreateDirectory(directory);
                    File.WriteAllText(directory+"/times.csv","frame,game_seconds\n");
                    File.WriteAllText(directory+"/motion.csv","frame,body_angle,wind_angle,body_z,scale_x,scale_y,opacity,flip_x,wind_strength,plane_y,center_y\n");
                    boss.PlaceAt(arena-Vector2.right*3*direction);ColonContentSmoke.Position(player,arena+Vector2.right*2*direction);
                    boss.Visual.ResetPose();boss.Visual.Face(Vector2.right*direction);yield return new WaitForSeconds(.25f);
                    originalScale=body.transform.localScale;
                    frame(clip+"-before");
                    int captured=0,turnSamples=0;float lastAngle=0,maxAngle=0;bool[] quadrants=new bool[4];
                    float start=Time.time;bool expectedFlip=direction>0;
                    var probe=body.gameObject.AddComponent<ColonBossTintCaptureProbe>();
                    probe.AfterTint=()=>
                    {
                        var wind=boss.Effects.GetComponentInChildren<ColonTurtleSpinWind>();
                        float angle=boss.Visual.GroundSpin;
                        if(wind!=null&&angle>1)
                        {
                            Require(angle>=lastAngle-.001f,"body must not reverse or snap while spinning");
                            Require(Mathf.Abs(Mathf.DeltaAngle(body.transform.localEulerAngles.z,angle))<.05f,"actual body rotation must match the commanded turn");
                            Require(body.flipX==expectedFlip,"spin must not replace rotation with mirrored flashes");
                            Require(Mathf.Abs(body.transform.localScale.x-originalScale.x)<.005f&&Mathf.Abs(body.transform.localScale.y-originalScale.y)<.005f,"spin keeps the original body scale");
                            Require(Mathf.Abs(boss.Visual.GroundSpinPlane.localScale.y-ColonBossVisual.GroundPlaneY)<.001f,"rotation is followed by a fixed horizontal-ground projection");
                            float floor=boss.Visual.transform.position.y+boss.Visual.Lift,lowest=float.PositiveInfinity,highest=float.NegativeInfinity;
                            foreach(var vertex in boss.Visual.GroundSpinFootprint)
                            {
                                Vector3 p=vertex;if(body.flipX)p.x=-p.x;p=body.transform.TransformPoint(p);
                                lowest=Mathf.Min(lowest,p.y-floor);highest=Mathf.Max(highest,p.y-floor);
                            }
                            Require(Mathf.Abs(lowest-.015f)<.015f,"the original shell's lowest opaque outline point stays on the ground");
                            Require(highest<boss.Visual.ReferenceHeight,"the projected shell never stands taller than the original body");
                            Require(Vector2.Distance(wind.WorldCenter,boss.Visual.GroundSpinCenter)<.02f,"wind and body share the same projected rotation centre");
                            Require(body.enabled&&Mathf.Abs(body.color.a-1)<.001f,"body stays visible without opacity flashing");
                            Require(AssetDatabase.GetAssetPath(body.sprite)=="Assets/Resources/ColonBosses/turtle/Pose.png"&&body.sprite.name.EndsWith("003"),"only the authored retracted shell is used");
                            Require(wind.WindAngle>angle&&Mathf.Abs(wind.WindAngle-angle*ColonTurtleSpinWind.WindRatio)<.01f,"wind turns faster in the same direction");
                            Require(boss.Visual.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.sprite!=null&&r.sprite.texture==body.sprite.texture)==1,"no duplicated body sprites");
                            quadrants[(int)Mathf.Repeat(angle,360)/90]=true;lastAngle=angle;maxAngle=Mathf.Max(maxAngle,angle);turnSamples++;
                        }
                        frame(clip+"/"+captured.ToString("D4"));
                        string F(float x)=>x.ToString("0.#####",CultureInfo.InvariantCulture);
                        File.AppendAllText(directory+"/times.csv",captured+","+F(Time.time-start)+"\n");
                        File.AppendAllText(directory+"/motion.csv",string.Join(",",new[]{captured.ToString(),F(angle),F(wind==null?0:wind.WindAngle),F(body.transform.localEulerAngles.z),F(body.transform.localScale.x),F(body.transform.localScale.y),F(body.color.a),body.flipX.ToString(),F(wind==null?0:wind.Strength),F(boss.Visual.GroundSpinPlane.localScale.y),F(boss.Visual.GroundSpinCenter.y-boss.Visual.transform.position.y)})+"\n");captured++;
                    };
                    Require(boss.PlayPattern(0),"real pattern zero started");
                    float until=Time.time+8;while(boss.Busy&&Time.time<until&&probe.Failure==null)yield return null;
                    probe.enabled=false;var failure=probe.Failure;Object.Destroy(probe);if(failure!=null)throw failure;
                    Require(!boss.Busy,"pattern completes");yield return null;
                    Require(turnSamples>=30&&maxAngle>2100&&quadrants.All(q=>q),"all four orientations and six turns were rendered");
                    Require(boss.Effects.GetComponentInChildren<ColonTurtleSpinWind>(true)==null,"all wind objects cleaned after recovery");
                    Require(boss.Visual.GroundSpin==0&&Quaternion.Angle(body.transform.localRotation,Quaternion.identity)<.1f,"upright artwork restored");
                    Require(boss.Visual.GroundSpinProjection==0&&boss.Visual.GroundSpinPlane.localScale==Vector3.one&&boss.Visual.GroundSpinPlane.localPosition==Vector3.zero,"projection is fully removed after recovery");
                    frame(clip+"-after");File.AppendAllText(report,"PASS "+clip+"; real PlayPattern(0); frames="+captured+"; spin samples="+turnSamples+"; original texture and size; fixed ground projection and contact; no flip/alpha flashes; faster same-direction wind; cleanup\n");
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
