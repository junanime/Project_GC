using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class FoodBossSmoke
    {
        static IEnumerator BuriedArrivalReview(LevelManager level,Character player,Vector2 origin)
        {
            var director=level.GetComponent<BuriedBossDirector>();
            Check(director!=null&&director.PreparedStage==1&&director.Part!=null,"stage-one loading includes a random boss part");
            float distance=Vector2.Distance(director.EntryPosition,director.Part.transform.position);
            Check(distance>=5.999f&&distance<=8.001f,"part placed 6–8 units from stage entry: "+distance);
            var initial=director.Part;var choice=director.SelectedKind;
            director.PrepareStage(origin);
            Check(director.Part==initial&&director.SelectedKind==choice,"prepare/revisit never rerolls this stage");
            var altar=Object.FindObjectOfType<FinalBossSummonInteractable>();
            Check(altar!=null&&altar.OpenBossChoice(player),"old selection altar remains usable");
            Check(!initial.Activate(player),"natural part cannot summon while test choice is open");
            ApothecaryUI.Instance.CloseBossChoice();
            Check(!FinalBossSummonInteractable.IsSummoning,"cancel releases shared summon lock");
            foreach(var tip in Object.FindObjectsOfType<HudTooltip>())tip.enabled=false;
            Time.captureDeltaTime=.04f;
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                if(director.Part!=null)Object.Destroy(director.Part.gameObject);
                yield return null;
                Set(director,"<Consumed>k__BackingField",false);
                Set(director,"<SelectedKind>k__BackingField",kind);
                Vector2 at=FiniteStomachMap.SafeSpawn(origin+new Vector2(2.7f,1.0f),2.5f);
                var part=BuriedBossPart.Create(director,kind,at);
                Set(director,"<Part>k__BackingField",part);
                Position(player,at+new Vector2(-1.20f,1.1f));
                yield return new WaitForSeconds(.4f);
                var renderer=part.Presentation.PartRenderer;
                int rendererId=renderer.GetInstanceID();
                string folder="buried-"+(int)kind;Directory.CreateDirectory(Output+"/"+folder);
                int frame=0;
                for(int f=0;f<16;f++){CaptureBuried(folder+"/"+(frame++).ToString("D4"));yield return null;}
                Check(InteractionFocus.IsFocused(part),"actual proximity focus: "+kind);
                if(kind==FoodBossKind.Hurumi)
                    Check(InteractionFocus.SelectAtScreenPoint(Camera.main.WorldToScreenPoint(part.Presentation.PartBounds.center),Camera.main),"mobile touch selects the original noodle mesh");
                part.TryInteract();
                Check(BuriedBossDirector.Pending==director,"actual interaction activates part: "+kind);
                Check(!altar.OpenBossChoice(player),"test altar blocked during ground emergence: "+kind);
                Check(!part.Activate(player),"rapid double interaction rejected: "+kind);
                FoodBossRuntime boss=null;
                for(int f=0;f<115;f++)
                {
                    if(part!=null&&kind==FoodBossKind.Hurumi)
                        CheckQuiet(part.Presentation.PartRenderer.GetInstanceID()==rendererId,"the same original noodle mesh survives the pull");
                    boss=Object.FindObjectOfType<FoodBossRuntime>();
                    if(boss!=null)boss.AutoPatterns=false;
                    CaptureBuried(folder+"/"+(frame++).ToString("D4"));yield return null;
                }
                Check(boss!=null&&boss.Kind==kind&&boss.Health==boss.MaxHealth,"arrival hands off to selected full-health gameplay boss: "+kind);
                Check(!FinalBossSummonInteractable.IsSummoning&&director.Consumed,"summon lock released and stage part consumed: "+kind);
                Check(Object.FindObjectsOfType<BuriedBossEmergence>().Length==0,"all ground opening/part renderers removed after emergence: "+kind);
                Check(Vector2.Distance(at,boss.transform.position)<.1f,"arrival does not jump at gameplay handoff: "+kind);
                Check(Mathf.Abs(boss.Visual.Body.transform.localScale.x-FoodBossArt.WalkScale(kind,false))<.001f,"original boss world size preserved: "+kind);
                Object.Destroy(boss.gameObject);yield return null;
            }
            Time.captureDeltaTime=0;
            // Cancellation has to release the shared gate and rebuild a usable, clean standby.
            Set(director,"<Consumed>k__BackingField",false);
            var retry=BuriedBossPart.Create(director,FoodBossKind.Hurumi,origin+Vector2.right*3);
            Set(director,"<Part>k__BackingField",retry);
            Check(retry.Activate(player),"cancellation fixture starts");yield return null;
            retry.gameObject.SetActive(false);yield return null;yield return null;
            Check(!FinalBossSummonInteractable.IsSummoning,"disabling an emerging part releases summon gate");
            Object.Destroy(retry.gameObject);yield return null;
            var map=FiniteStomachMap.Instance;
            var colon=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");map.ChangeDefinition(colon);
            Set(StageProgression.Ensure(level),"<StageNumber>k__BackingField",2);
            var inlet=StomachOpening.Find(true);var landing=map.ClampToField(inlet.FrontLip+Vector2.down*2,map.WallWidth+.6f);
            Position(player,landing);
            yield return level.GetComponent<FieldObjectLayout>().RebuildForStage();
            Check(director.PreparedStage==2&&director.Part!=null&&!director.Consumed,"stage-two loading places its own newly selected part");
            distance=Vector2.Distance(landing,director.Part.transform.position);
            Check(distance>=5.999f&&distance<=8.001f,"colon entry part obeys 6–8 ring: "+distance);
            Check(map.NavigationDistance(director.Part.transform.position,out _,true)>=map.WallWidth+2.4f,"colon part has full boss clearance from walls and inlet");
            for(int i=0;i<100;i++)
            {
                CheckQuiet(BuriedBossDirector.TryPosition(landing,out var candidate),"colon random placement always finds valid reachable entry ring");
                CheckQuiet(Vector2.Distance(landing,candidate)>=5.999f&&Vector2.Distance(landing,candidate)<=8.001f,"placement never clamps outside ring");
            }
            Check(true,"100 colon entry placements passed");
            Capture("buried-stage2-placement");
            var savedKind=director.SelectedKind;
            Object.Destroy(director.Part.gameObject);yield return null;
            Check(director.TryAutomaticSummon(),"scheduled event recovers a dissolved/missing part without needing a surviving altar");
            float end=Time.time+5;
            while(Object.FindObjectOfType<FoodBossRuntime>()==null&&Time.time<end)yield return null;
            var recovered=Object.FindObjectOfType<FoodBossRuntime>();
            Check(recovered!=null&&recovered.Kind==savedKind,"recovered automatic summon preserves the original stage roll");
            Object.Destroy(recovered.gameObject);yield return null;
        }
        static void CheckQuiet(bool ok,string message){if(!ok)throw new Exception(message);}
        static void CaptureBuried(string name)
        {
            // Wider QA framing keeps the full-size boss below the HUD throughout its rise.
            var camera=Camera.main;float size=camera.orthographicSize;
            try{camera.orthographicSize=5.2f;Capture(name);}finally{camera.orthographicSize=size;}
        }
    }
}
