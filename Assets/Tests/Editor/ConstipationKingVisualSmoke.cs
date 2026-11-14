#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Checks actual imported source identity, anchors and rendered runtime poses.</summary>
    public static class ConstipationKingVisualSmoke
    {
        const string Output = "Library/ConstipationKingVisualProof";
        const string Root = "Assets/Resources/ConstipationKing/";
        [Serializable] sealed class Source { public string file, sha256; public int bytes; }
        [Serializable] sealed class Manifest { public Source[] files; }
        static int checks;
        static void Check(bool value, string detail)
        {
            if (!value) throw new InvalidDataException("KING_VISUAL_FAIL " + detail);
            checks++; File.AppendAllText(Output + "/checks.txt", detail + "\n");
        }

        [MenuItem("Tools/24 Hours/Validate Constipation King Visuals")]
        public static void Run()
        {
            Directory.CreateDirectory(Output); checks = 0;
            File.WriteAllText(Output + "/result.txt", "RUNNING"); File.WriteAllText(Output + "/checks.txt", "");
            GameObject host = null, cameraObject = null;
            try
            {
                Vampire.Editor.ConstipationKingArtImporter.Prepare();
                ConstipationKingNeedleSurfaceSmoke.Validate(Check);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "SourceIdentity.json"));
                Check(manifest != null && manifest.files.Length == 15, "all fifteen original source PNGs recorded");
                foreach (var entry in manifest.files)
                {
                    byte[] bytes = File.ReadAllBytes(Root + entry.file);
                    using (var hash = SHA256.Create())
                    {
                        string digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                        Check(bytes.Length == entry.bytes && digest == entry.sha256, "original source bytes preserved: " + entry.file);
                    }
                }
                foreach (string name in new[] { "Body", "Outlet", "StaffFoot6", "Crown", "Tribute0", "Tribute1", "Tribute2", "Booster0", "Booster1", "Booster2", "Booster3" })
                    Check(ConstipationKingVisual.Get(name) != null, "imported named sprite: " + name);
                var body = ConstipationKingVisual.Get("Body");
                Check(Mathf.Abs(body.pixelsPerUnit - 261.25f) < .01f && body.rect.width == 1254, "A body retains original full-resolution image and 480px reference scale");
                for (int i = 0; i <= 10; i++)
                {
                    var pose = ConstipationKingVisual.Get("Pose" + i);
                    Check(pose != null && pose.rect.y == 44 && pose.rect.height == 436 && pose.pixelsPerUnit == 100,
                        "V3 fixed scale, only obsolete underside excluded: " + i);
                }
                host = new GameObject("Isolated king visual verification"); host.transform.position = new Vector3(10000, 10000);
                var visual = ConstipationKingVisual.Create(host.transform);
                foreach (Transform t in host.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                cameraObject = new GameObject("Isolated king proof camera"); var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 3.8f;
                camera.transform.position = new Vector3(10000, 10002.7f, -10);
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.91f, .61f, .57f); camera.nearClipPlane = .1f; camera.farClipPlane = 30;
                visual.Tick(.6f); Check(visual.CentralJetCount == 1, "idle has exactly one central connected water column");
                float time = visual.AnimationTime; Vector2 mouth = visual.Mouth; visual.Tick(0);
                Check(visual.AnimationTime == time && visual.Mouth == mouth, "zero-delta paused sample does not advance visuals");
                Capture(camera, "01-idle-A");
                visual.SetMoving(true); visual.Face(Vector2.right); visual.SetPose(KingPose.Move);
                for (int i = 0; i < 90; i++) visual.Tick(1f / 60);
                Check(Mathf.Abs(visual.LeanDegrees + 5) < .01f, "rightward movement leans forward five degrees");
                Capture(camera, "02-move-A-right");
                visual.Face(Vector2.left); for (int i = 0; i < 90; i++) visual.Tick(1f / 60);
                Check(Mathf.Abs(visual.LeanDegrees - 5) < .01f, "leftward movement mirrors five-degree forward lean");
                Capture(camera, "03-move-A-left");
                visual.SetMoving(false); visual.Face(Vector2.right);
                for (int i = 0; i < 60; i++) visual.Tick(1f / 60);
                float fixedScale = visual.Body.transform.localScale.x;
                int shot = 4;
                foreach (KingPose pose in Enum.GetValues(typeof(KingPose)))
                {
                    visual.SetPhase(2); visual.SetPose(pose); visual.SetPoseProgress(.6f);
                    visual.SetMoving(pose == KingPose.Dash); visual.Tick(.1f);
                    Check(visual.Body.sprite != null && Mathf.Abs(visual.Body.transform.localScale.x - fixedScale) < .001f, "same body scale throughout " + pose);
                    if (pose == KingPose.DashCharge) Check(visual.DisplayedPoseIndex == 4, "dash pressure warning uses approved straining pose, separate from open-mouth charge");
                    Check(visual.CentralJetCount == 1, "central A jet remains attached during " + pose);
                    var mesh = visual.GetComponentInChildren<MeshFilter>().sharedMesh;
                    var jetOrigin = (mesh.vertices[0] + mesh.vertices[1]) * .5f;
                    var worldOrigin = visual.transform.TransformPoint(jetOrigin);
                    Check(Vector2.Distance(worldOrigin, visual.Outlet) < .002f, "water begins at actual transformed outlet: " + pose);
                    Capture(camera, (shot++).ToString("D2") + "-" + pose);
                }
                visual.Opacity = 0; visual.Tick(.1f);
                Check(!visual.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "zero opacity hides body, outlet, booster, water, foam and trails together");
                var props = new GameObject("Approved procedural prop gallery"); props.transform.SetParent(host.transform, false);
                string[] propKeys = { "PoopProjectile", "Plunger", "PaperRoll", "PoopMinion" };
                for (int i = 0; i < propKeys.Length; i++)
                {
                    var sprite = ConstipationKingPropArt.Get(propKeys[i]);
                    Check(sprite != null && sprite != BombCircleArt.Sprite, "authored or V3 procedural prop, no plain-circle fallback: " + propKeys[i]);
                    var r = new GameObject(propKeys[i]).AddComponent<SpriteRenderer>(); r.transform.SetParent(props.transform, false); r.gameObject.layer = 31;
                    r.sprite = sprite; r.sortingOrder = 510; r.transform.localPosition = new Vector2(-3 + i * 2, 2.5f);
                    r.transform.localScale = Vector3.one * 1.65f / sprite.bounds.size.x;
                }
                Capture(camera, "20-props-blob-plunger-roll-minion"); Object.DestroyImmediate(props);
                Check(ConstipationKingPropArt.Get("OverflowPool") != null && ConstipationKingPropArt.FloorMaterial != null,
                    "textured overflow pool and floor-occlusion shader available");
                var courier = new GameObject("Royal tribute carrier proof"); courier.transform.SetParent(host.transform, false);
                courier.transform.localPosition = new Vector2(-.5f, 1.7f); courier.AddComponent<SpriteRenderer>();
                var carrierVisual = ConstipationKingPropArt.ConfigureCarrier(courier, 1);
                foreach (Transform child in courier.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
                carrierVisual.Tick(.5f, Vector2.right, false, 0, host.transform.position + Vector3.up * 4);
                Check(courier.GetComponentsInChildren<SpriteRenderer>().Length == 2, "tribute has a monster courier and a separate carried food sprite");
                Capture(camera, "21-monster-carries-tribute");
                carrierVisual.Tick(.5f, Vector2.right, true, .7f, host.transform.position + new Vector3(2, 4));
                Capture(camera, "22-bow-and-mouth-delivery");
                Object.DestroyImmediate(courier);
                File.WriteAllText(Output + "/result.txt", "PASS checks=" + checks + "\nOriginal PNG hashes + actual imported sprites + runtime camera renders.\n");
                Debug.Log("KING_VISUAL_PASS checks=" + checks);
            }
            catch (Exception e) { File.WriteAllText(Output + "/result.txt", "FAIL\n" + e); throw; }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                if (host != null) Object.DestroyImmediate(host);
            }
        }

        static void Capture(Camera camera, string name)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 720, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
