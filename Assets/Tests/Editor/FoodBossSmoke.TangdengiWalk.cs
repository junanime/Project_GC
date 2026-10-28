using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class FoodBossSmoke
    {
        static IEnumerator TangdengiWalkReview(Character player,Vector2 origin)
        {
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
            var quest=Object.FindObjectOfType<PrescriptionScrollView>();if(quest!=null)quest.SetExpanded(false);yield return new WaitForSecondsRealtime(.4f);
            float captureStep=Time.captureDeltaTime;Time.captureDeltaTime=.04f;
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Tangdengi,player,origin);boss.AutoPatterns=false;
            float scale=FoodBossArt.WalkScale(FoodBossKind.Tangdengi,false);
            for(int phase=0;phase<2;phase++){
                Position(player,origin);
                if(phase==1){
                    boss.SetPhaseTwoForTest();float hp=boss.Health;int f=0;
                    while(boss.Busy){boss.TakeDamage(999);if(f%8==0)CaptureTang($"tang-transition-{f:D3}",boss,player);f++;yield return null;}
                    Check(boss.Health==hp&&boss.PhaseTwo,"Tangdengi transition remains invincible and completes");
                    Check(boss.PhaseVisual.EmittedCues==5,"all five carapace and horn transition cues retained");
                }
                scale=FoodBossArt.WalkScale(FoodBossKind.Tangdengi,phase==1);
                Check(Mathf.Abs(scale*(phase==0?245:257)/100-(phase==0?4.2f:5.082f))<.001f,"phase "+(phase+1)+" preserves original world size");
                foreach(int side in new[]{1,-1}){
                    boss.transform.position=origin+Vector2.right*6.6f*side;boss.GetComponent<Rigidbody2D>().position=boss.transform.position;
                    yield return null;yield return null;var sprites=new HashSet<Sprite>();bool atlasStable=true,scaleStable=true;
                    for(int f=0;f<64;f++){
                        yield return null;
                        atlasStable&=boss.Visual.Body.sprite.texture.name==$"Walk3P{phase+1}";
                        scaleStable&=Mathf.Approximately(boss.Visual.Body.transform.localScale.x,scale);
                        if(side==1)CaptureTang($"tang-p{phase+1}-walk-{f:D3}",boss,player);
                        sprites.Add(boss.Visual.Body.sprite);
                    }
                    Check(sprites.Count==16,"all sixteen walk poses used facing "+(-side)+" phase "+(phase+1));
                    Check(atlasStable&&scaleStable,"approved atlas and scale stay fixed throughout walk");
                    Check(boss.Visual.Facing==-side,"walk orientation follows travel");
                }
                boss.transform.position=origin+Vector2.right*3;boss.GetComponent<Rigidbody2D>().position=boss.transform.position;yield return null;yield return null;
                Check(!boss.Moving&&boss.Visual.Body.sprite==FoodBossArt.Walk(FoodBossKind.Tangdengi,phase==1,0),"stopped boss returns to approved rest pose");
                CaptureTang($"tang-p{phase+1}-idle",boss,player);
                Check(boss.Play(FoodBossAttack.Dash),"dash starts with approved body");bool sawFlight=false;int dashFrame=0;
                bool previousCharge=false,flightStable=true;
                while(boss.Busy){yield return null;if(boss.Visual.Charge&&previousCharge){sawFlight=true;flightStable&=boss.Visual.Body.sprite==FoodBossArt.Walk(FoodBossKind.Tangdengi,phase==1,17);var wing=boss.Visual.Body.transform.parent.Find("Wing").GetComponent<SpriteRenderer>();flightStable&=wing.enabled;var hinge=new Vector3(wing.sprite.bounds.size.x*.42f,-wing.sprite.bounds.size.y*.33f,0);var joint=wing.transform.localPosition+wing.transform.localRotation*Vector3.Scale(hinge,wing.transform.localScale);flightStable&=Vector3.Distance(joint,new Vector3(.55f,1.05f,0)*boss.Visual.BeetleArtRatio)<.001f;if(dashFrame++%8==0)CaptureTang($"tang-p{phase+1}-flight-{dashFrame:D3}",boss,player);}previousCharge=boss.Visual.Charge;}
                Check(flightStable,"dash retains six folded yellow legs");
                Check(sawFlight&&boss.Visual.Body.enabled,"flight pose and ground body restore correctly");
            }
            // Freeze the animation together with the rest of gameplay; never animate through TAB/settings.
            boss.transform.position=origin+Vector2.right*6.6f;boss.GetComponent<Rigidbody2D>().position=boss.transform.position;yield return null;yield return null;
            Time.timeScale=0;var pausedSprite=boss.Visual.Body.sprite;yield return new WaitForSecondsRealtime(.15f);Check(pausedSprite==boss.Visual.Body.sprite,"paused walking frame stays fixed");Time.timeScale=1;
            Object.Destroy(boss.gameObject);Time.captureDeltaTime=captureStep;yield return null;
        }
        static void CaptureTang(string name,FoodBossRuntime boss,Character player)
        {
            var camera=Camera.main;Vector3 position=camera.transform.position;
            // Frame both subjects for this QA capture; gameplay camera and zoom are restored immediately.
            var center=Vector3.Lerp(player.transform.position,boss.transform.position,.65f)+Vector3.up*.85f;
            center.z=position.z;camera.transform.position=center;
            var bars=Object.FindObjectOfType<PlayerCombatBars>();if(bars!=null)Call(bars,"LateUpdate");
            try{Capture(name);}finally{camera.transform.position=position;if(bars!=null)Call(bars,"LateUpdate");}
        }
    }
}
