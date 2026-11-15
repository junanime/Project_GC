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
        // Captures real Level1 entities, attack coroutines, warning/damage geometry and
        // the game camera. One rendered image per 1/30 game second; no interpolated poses.
        static IEnumerator LiquidPresentation(Character player,Vector2 origin)
        {
            var level=Object.FindObjectOfType<LevelManager>();level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.gameObject.SetActive(false);
            player.CollectableCollider.enabled=false;
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
            var quest=Object.FindObjectOfType<PrescriptionScrollView>();if(quest!=null)quest.SetExpanded(false);
            Time.timeScale=1;Time.captureDeltaTime=1f/50;
            Check(Resources.Load<Shader>("FoodBosses/ViscousFlow")!=null,"Stage1 liquid shader is included");
            yield return CreamFlowChecks(player,origin);
            yield return SettledCreamCheck(player,origin);
            Time.captureDeltaTime=1f/30;
            for(int phase=1;phase<=2;phase++)
            {
                yield return LiquidClip(player,origin,FoodBossKind.Rolpangi,phase,FoodBossAttack.UniqueOne,phase==1?9.2f:11.8f,"rolpangi-cream-p"+phase);
                yield return LiquidClip(player,origin,FoodBossKind.Maribi,phase,FoodBossAttack.UniqueTwo,phase==1?3.0f:5.4f,"maribi-juice-p"+phase);
            }
            yield return LiquidClip(player,origin,FoodBossKind.Maribi,2,FoodBossAttack.Dash,7.0f,"maribi-landing-p2");
            if(!Environment.GetCommandLineArgs().Contains("-foodLiquidsOnly"))
            {
                yield return LiquidClip(player,origin,FoodBossKind.Denikani,2,FoodBossAttack.UniqueTwo,6.0f,"denikani-contact-p2");
                yield return LiquidClip(player,origin,FoodBossKind.Tangdengi,2,FoodBossAttack.UniqueOne,9.0f,"tangdengi-charge-p2");
                yield return LiquidClip(player,origin,FoodBossKind.Hurumi,2,FoodBossAttack.UniqueTwo,6.0f,"hurumi-return-p2");
            }
            Time.captureDeltaTime=0;
            Check(Object.FindObjectsOfType<FoodBossFlowRibbon>().Length==0&&Object.FindObjectsOfType<FoodBossFlowFan>().Length==0,"Liquid clip cleanup leaves no attack renderers");
            File.WriteAllText(Output+"/liquid/manifest.txt","Actual Level1 play mode; 1280x720; 30 frames per game second.\nPNG sequences: rolpangi-cream-p1, rolpangi-cream-p2, maribi-juice-p1, maribi-juice-p2, maribi-landing-p2. Additional contact/charge/return clips unless -foodLiquidsOnly.\nCapture-only camera framing: 7.6 vertical half extent. Production camera unchanged.\n");
        }
        static IEnumerator SettledCreamCheck(Character player,Vector2 origin)
        {
            Position(player,origin+Vector2.right*3.2f);
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin);boss.AutoPatterns=false;
            var cream=FoodBossCreamTrail.Create(boss,new[]{origin+Vector2.down*2,origin+new Vector2(2,-2)},new[]{false,false});
            while(cream.Age<2.0f)yield return null;
            var ribbon=cream.GetComponentsInChildren<FoodBossFlowRibbon>().First(r=>r.GetComponent<MeshRenderer>().enabled);
            var mesh=ribbon.GetComponent<MeshFilter>().sharedMesh;var settled=mesh.vertices;
            float until=Time.time+.45f;while(Time.time<until)yield return null;
            Check(settled.SequenceEqual(mesh.vertices),"Deposited cream stops changing shape after settling");
            Check(mesh.uv2.Length==mesh.vertexCount&&mesh.uv3.Length==mesh.vertexCount,"Rounded cream caps retain complete cross-section lighting data");
            Object.Destroy(boss.gameObject);yield return null;
            Check(Object.FindObjectsOfType<FoodBossCreamTrail>().Length==0,"Cancelling the owner removes lingering cream and its slowdown");
        }
        static IEnumerator LiquidClip(Character player,Vector2 origin,FoodBossKind kind,int phase,FoodBossAttack attack,float seconds,string name)
        {
            string relative="liquid/"+name;Directory.CreateDirectory(Output+"/"+relative);
            Position(player,origin+new Vector2(3.4f,-.45f));player.Move(Vector2.zero);
            var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
            if(phase==2){boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;}
            boss.transform.position=origin;boss.GetComponent<Rigidbody2D>().position=origin;
            Check(boss.Play(attack),"Real liquid presentation starts: "+name);
            bool finite=true,supported=true,sawFlow=false,sawPressure=false,finished=false,sawContact=false,sawKick=false,sawNoodle=false,joined=true;
            int frames=Mathf.CeilToInt(seconds*30);
            for(int frame=0;frame<frames;frame++)
            {
                yield return null;
                sawPressure|=boss.Visual.FlowPressure>.5f;finished|=!boss.Busy;
                sawContact|=boss.Visual.ClawImpact>0;sawKick|=boss.Visual.HornKick>0;
                sawFlow|=boss.Effects.GetComponentInChildren<FoodBossCreamTrail>()!=null||boss.Effects.GetComponentInChildren<FoodBossFlowFan>()!=null;
                foreach(var noodle in boss.Effects.GetComponentsInChildren<FoodBossNoodle>())if(noodle.Points!=null)
                {sawNoodle=true;joined&=Vector2.Distance(noodle.Points[0],boss.Visual.NoodleTip)<.005f;}
                if(frame%10==0)
                {
                    foreach(var filter in boss.Effects.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null)
                        foreach(var v in filter.sharedMesh.vertices)finite&=!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y);
                    foreach(var r in boss.Effects.GetComponentsInChildren<MeshRenderer>())supported&=r.sharedMaterial!=null&&r.sharedMaterial.shader.isSupported;
                }
                CaptureLiquid(relative+"/frame-"+frame.ToString("D4"),origin+Vector2.up*.5f);
            }
            Check(finite&&supported&&finished,"Continuous supported geometry and completed real pattern: "+name);
            if(kind==FoodBossKind.Rolpangi||kind==FoodBossKind.Maribi)
            {
                Check(sawFlow,"Actual flowing cream or lemon material rendered: "+name);
                if(attack!=FoodBossAttack.Dash)Check(sawPressure&&boss.Visual.FlowPressure==0,"Pressure builds and returns to approved resting body: "+name);
            }
            if(kind==FoodBossKind.Denikani)Check(sawContact&&boss.Visual.ClawImpact==0,"Claw contact compresses once and restores its wrist");
            if(kind==FoodBossKind.Tangdengi)Check(sawKick&&boss.Visual.HornKick==0,"Horn emits with one restoring recoil");
            if(kind==FoodBossKind.Hurumi)Check(sawNoodle&&joined,"Whip and inertial return remain joined to the original noodle tip");
            Object.Destroy(boss.gameObject);yield return null;
        }
        static void CaptureLiquid(string name,Vector2 center)
        {
            var camera=Camera.main;Vector3 old=camera.transform.position;float size=camera.orthographicSize;
            var bars=Object.FindObjectOfType<PlayerCombatBars>();
            try
            {
                camera.transform.position=new Vector3(center.x,center.y,old.z);camera.orthographicSize=7.6f;
                if(bars!=null)Call(bars,"LateUpdate");Capture(name);
            }
            finally{camera.transform.position=old;camera.orthographicSize=size;if(bars!=null)Call(bars,"LateUpdate");}
        }
    }
}
