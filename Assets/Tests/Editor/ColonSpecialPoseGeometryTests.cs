using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class ColonSpecialPoseGeometryTests
    {
        [Test] public void FinalGasPoseRefitsHitboxAndAimWithoutChangingSizeOrFeet()
        {
            var go=new GameObject("Colon special final pose regression");
            try
            {
                var body=new GameObject("Body").AddComponent<SpriteRenderer>();body.transform.SetParent(go.transform,false);
                body.sprite=ColonSpecialEffects.Art("gas",0);Assert.NotNull(body.sprite);
                body.transform.localPosition=new Vector3(.13f,.20f,0);body.transform.localScale=new Vector3(.8f,1.1f,1);
                body.transform.localRotation=Quaternion.Euler(0,0,8);
                go.transform.localScale=new Vector3(1.7f,1.7f,1);go.transform.position=new Vector3(3,-4,0);
                var feet=go.AddComponent<CircleCollider2D>();feet.radius=.31f;
                var box=body.gameObject.AddComponent<BoxCollider2D>();box.isTrigger=true;
                var aim=new GameObject("Aim center").transform;aim.SetParent(go.transform,false);
                var monster=go.AddComponent<Monster>();
                Set(monster,"monsterSpriteRenderer",body);Set(monster,"monsterHitbox",box);Set(monster,"centerTransform",aim);Set(monster,"currentHealth",20f);
                var fit=go.AddComponent<MonsterBodyFit>();fit.Configure(box,body);
                var runtime=go.AddComponent<ColonSpecialRuntime>();Set(runtime,"monster",monster);
                Set(runtime,"<Kind>k__BackingField","gas");Set(runtime,"<Pose>k__BackingField",5);
                Vector2 idleSize=box.size;Vector3 scale=body.transform.localScale,position=body.transform.localPosition;
                Quaternion rotation=body.transform.localRotation;Vector3 rootScale=go.transform.localScale;
                Invoke(runtime,"LateUpdate");
                Assert.AreSame(ColonSpecialEffects.Art("gas",5),body.sprite);
                Assert.That(box.size.y,Is.GreaterThan(idleSize.y*1.1f),"inflated gas body must expand its real hitbox");
                Assert.That(Vector3.Distance(aim.position,box.transform.TransformPoint(box.offset)),Is.LessThan(.0001f));
                Assert.AreEqual(scale,body.transform.localScale);Assert.AreEqual(position,body.transform.localPosition);
                Assert.AreEqual(rotation,body.transform.localRotation);Assert.AreEqual(rootScale,go.transform.localScale);
                Assert.AreEqual(.31f,feet.radius,"movement footprint must not grow with the animation");
                Set(runtime,"<Pose>k__BackingField",0);Invoke(runtime,"LateUpdate");
                Assert.That(Vector2.Distance(idleSize,box.size),Is.LessThan(.0001f),"returning to idle restores its own bounds");
                box.enabled=false;Invoke(runtime,"RefreshPoseGeometry");Assert.IsFalse(box.enabled,"refit cannot reactivate a dead or hidden hitbox");
            }
            finally {Object.DestroyImmediate(go);}
        }
        static void Set(object target,string name,object value)
        {
            for(var type=target.GetType();type!=null;type=type.BaseType)
            {var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null){f.SetValue(target,value);return;}}
            throw new MissingFieldException(name);
        }
        static void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        public static void Run()
        {
            try{new ColonSpecialPoseGeometryTests().FinalGasPoseRefitsHitboxAndAimWithoutChangingSizeOrFeet();Debug.Log("[ColonSpecialPoseGeometryTests] PASS final pose, aim center, retained body/feet scale, idle restore and disabled collider");EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
