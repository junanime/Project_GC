#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Source PNGs are read independently of the imported sprites and baked hit masks.
    // This detects stale metadata/masks after replacing art without changing live import flags.
    public static class ColonBossArtIdentityChecks
    {
        const string Root = "Assets/Resources/ColonBosses/";
        [Serializable] sealed class Frame { public string name; public int x, y, width, height; }
        [Serializable] sealed class Sheet
        {
            public int frameCount, atlasWidth, atlasHeight, sourcePivotX, sourcePivotY;
            public float pixelsPerUnit, pivotX, pivotY;
            public Frame[] frames;
        }
        [Serializable] sealed class Art
        {
            public float pixelsPerUnit, referencePixelWidth, referencePixelHeight;
            public int walkFrameCount, poseCount, summonCount;
            public Vector2 pipeFaceUV, mouthUV;
            public NutPose[] nutPoses;
        }
        [Serializable] sealed class NutPose { public int pose; public bool visible; public Vector2[] positions; public float[] heights; }
        sealed class Mask { public int width, height; public byte[] bits; }
        sealed class Source : IDisposable
        {
            public string path; public Sheet sheet; public Texture2D texture; public Color32[] pixels;
            public void Dispose() { if (texture != null) Object.DestroyImmediate(texture); }
        }
        static int checks;
        static string output;
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidDataException("COLON_ART_IDENTITY_FAIL " + message);
            checks++; File.AppendAllText(output + "/asset-checks.txt", message + "\n");
        }
        [MenuItem("Tools/24 Hours/Validate Stage 2 Boss Identity Art")]
        public static void Run() => Validate("Library/ColonBossIdentityReview");
        public static void Validate(string destination)
        {
            output = destination; checks = 0; Directory.CreateDirectory(output);
            File.WriteAllText(output + "/asset-checks.txt", "");
            File.WriteAllText(output + "/asset-geometry.csv", "boss,sheet,frame,cell_width,cell_height,pivot_x_pixels,pivot_y_pixels,visible_width,visible_height,bottom_from_pivot\n");
            try
            {
                var masks = ReadMasks();
                foreach (string id in new[] { "hedgehog", "manatee" }) ValidateBoss(id, masks);
                File.WriteAllText(output + "/asset-result.txt", "PASS checks=" + checks + "; complete alpha-mask comparison; fixed imported anchors; neutral identity pixels match\n");
            }
            catch (Exception e) { File.WriteAllText(output + "/asset-result.txt", "FAIL\n" + e); throw; }
        }
        static Dictionary<string, Mask> ReadMasks()
        {
            var result = new Dictionary<string, Mask>();
            using (var reader = new BinaryReader(File.OpenRead(Root + "NeedleSurfaces.bytes")))
            {
                int count = reader.ReadInt32(); Check(count > 0 && count < 1000, "valid needle mask entry count");
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString(); int w = reader.ReadInt32(), h = reader.ReadInt32(), length = reader.ReadInt32();
                    Check(w > 0 && h > 0 && length == ((long)w * h + 7) / 8, "needle mask length " + key);
                    Check(!result.ContainsKey(key), "unique needle mask " + key);
                    byte[] bytes = reader.ReadBytes(length); Check(bytes.Length == length, "complete needle mask " + key);
                    result.Add(key, new Mask { width = w, height = h, bits = bytes });
                }
                Check(reader.BaseStream.Position == reader.BaseStream.Length, "needle mask file has no stale trailing bytes");
            }
            return result;
        }
        static Source Read(string id, string name)
        {
            string path = Root + id + "/" + name;
            var source = new Source { path = path + ".png", sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(path + ".json")), texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) };
            Check(ImageConversion.LoadImage(source.texture, File.ReadAllBytes(source.path), false), "source PNG readable " + path);
            source.pixels = source.texture.GetPixels32(); return source;
        }
        static void ValidateBoss(string id, Dictionary<string, Mask> masks)
        {
            var art = JsonUtility.FromJson<Art>(File.ReadAllText(Root + id + "/BossArt.json"));
            var sources = new Dictionary<string, Source>();
            try
            {
                string[] names = id == "hedgehog" ? new[] { "Walk", "WalkBare", "Idle", "IdleBare", "Pose", "Summon", "Dormant" } : new[] { "Walk", "Idle", "Pose", "Summon", "Dormant" };
                foreach (string name in names) sources.Add(name, Read(id, name));
                var walk = sources["Walk"]; Check(art.pixelsPerUnit > 0 && art.referencePixelWidth > 0 && art.referencePixelHeight > 0, "valid reference geometry " + id);
                foreach (var pair in sources) ValidateSheet(id, pair.Key, pair.Value, walk.sheet, art, masks);
                Check(walk.sheet.frames.Length == art.walkFrameCount, "walk frame count matches definition " + id);
                Check(sources["Pose"].sheet.frames.Length == art.poseCount && art.poseCount == 8, "eight imported action poses " + id);
                Check(sources["Summon"].sheet.frames.Length == art.summonCount, "summon frame count matches definition " + id);
                var neutral = PixelsAtAnchor(walk, 0);
                Check(Same(neutral, PixelsAtAnchor(sources["Idle"], 0)), "idle uses exact walk-neutral identity and anchor " + id);
                Check(Same(neutral, PixelsAtAnchor(sources["Pose"], 0)), "pose zero uses exact walk-neutral identity and anchor " + id);
                Check(Same(neutral, PixelsAtAnchor(sources["Summon"], sources["Summon"].sheet.frames.Length - 1)), "arrival completes on exact walk-neutral identity and anchor " + id);
                Check(Same(PixelsAtAnchor(sources["Dormant"], 0), PixelsAtAnchor(sources["Summon"], 0)), "dormant-to-arrival starts without changed source identity " + id);
                var bounds = Bounds(walk, walk.sheet.frames[0]);
                Check(Mathf.Abs(bounds.width - art.referencePixelWidth) <= 1 && Mathf.Abs(bounds.height - art.referencePixelHeight) <= 1, "reference size agrees with neutral source pixels " + id);
                ValidateSockets(id, art, sources["Pose"]);
                if (id == "hedgehog")
                {
                    Check(sources["WalkBare"].sheet.frames.Length == art.walkFrameCount, "bare movement frame count unchanged");
                    Check(Same(PixelsAtAnchor(sources["WalkBare"], 0), PixelsAtAnchor(sources["IdleBare"], 0)), "bare idle uses exact bare walk neutral");
                }
            }
            finally { foreach (var s in sources.Values) s.Dispose(); }
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static void ValidateSockets(string id, Art art, Source poses)
        {
            foreach (var item in new[] { new { name="mouthUV", point=art.mouthUV }, new { name="pipeFaceUV", point=art.pipeFaceUV } })
            {
                // Zero deliberately selects the established runtime fallback for older assets.
                if (item.point == Vector2.zero) { File.AppendAllText(output + "/asset-checks.txt", "INFO legacy socket fallback " + id + "/" + item.name + "\n"); continue; }
                Check(Finite(item.point.x) && Finite(item.point.y) && item.point.x >= 0 && item.point.x <= 1 && item.point.y >= 0 && item.point.y <= 1, "finite in-cell socket UV " + id + "/" + item.name);
                int index = item.name == "pipeFaceUV" ? 6 : 0; var f = poses.sheet.frames[index];
                int x = Mathf.Clamp(Mathf.FloorToInt(f.width * item.point.x), 0, f.width - 1), y = Mathf.Clamp(Mathf.FloorToInt(f.height * item.point.y), 0, f.height - 1);
                Check(poses.pixels[(f.y+y)*poses.texture.width+f.x+x].a > 32, "socket points onto authored body " + id + "/" + item.name);
            }
            if (id != "hedgehog") return;
            if (art.nutPoses == null || art.nutPoses.Length == 0) { File.AppendAllText(output + "/asset-checks.txt", "INFO legacy chestnut socket fallback\n"); return; }
            var used = new HashSet<int>();
            foreach (var pose in art.nutPoses)
            {
                Check(pose != null && pose.pose >= 0 && pose.pose < art.poseCount && used.Add(pose.pose), "unique valid chestnut pose index");
                if (!pose.visible) continue;
                Check(pose.positions != null && pose.heights != null && pose.positions.Length == 3 && pose.heights.Length == 3, "three chestnut socket positions and heights for pose " + pose.pose);
                var f = poses.sheet.frames[pose.pose];
                for (int i=0;i<3;i++)
                {
                    Vector2 p=pose.positions[i]; float h=pose.heights[i];
                    Check(Finite(p.x)&&Finite(p.y)&&Finite(h)&&h>0&&h<=art.referencePixelHeight/art.pixelsPerUnit, "finite proportionate chestnut attachment " + pose.pose + "/" + i);
                    Vector2 pixel=p*art.pixelsPerUnit+new Vector2(f.width*poses.sheet.pivotX,f.height*poses.sheet.pivotY);
                    Check(pixel.x>=0&&pixel.x<f.width&&pixel.y>=0&&pixel.y<f.height, "chestnut anchor lies within its authored pose cell " + pose.pose + "/" + i);
                }
            }
            if (used.Count < art.poseCount) File.AppendAllText(output + "/asset-checks.txt", "INFO " + (art.poseCount-used.Count) + " chestnut poses use established runtime fallback\n");
        }
        static void ValidateSheet(string id, string name, Source s, Sheet reference, Art art, Dictionary<string, Mask> masks)
        {
            var meta = s.sheet; string label = id + "/" + name;
            Check(meta.frames != null && meta.frames.Length > 0 && meta.frames.Length == meta.frameCount, "frame metadata count " + label);
            Check(meta.atlasWidth == s.texture.width && meta.atlasHeight == s.texture.height, "PNG matches atlas metadata dimensions " + label);
            Check(meta.pixelsPerUnit == art.pixelsPerUnit && meta.sourcePivotX == reference.sourcePivotX && meta.sourcePivotY == reference.sourcePivotY, "shared original canvas anchor and PPU " + label);
            var importer = AssetImporter.GetAtPath(s.path) as TextureImporter;
            Check(importer != null && importer.spritePixelsPerUnit == meta.pixelsPerUnit && importer.spriteImportMode == SpriteImportMode.Multiple, "imported PPU and sprite mode " + label);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Check(settings.spriteMeshType == SpriteMeshType.FullRect && !importer.mipmapEnabled && importer.filterMode == FilterMode.Point && importer.npotScale == TextureImporterNPOTScale.None, "full-rect unscaled point-filtered cells " + label);
            var imported = AssetDatabase.LoadAllAssetsAtPath(s.path).OfType<Sprite>().ToDictionary(x => x.name);
            Check(imported.Count == meta.frames.Length, "no missing or stale imported cells " + label);
            Check(masks.TryGetValue(label, out var mask), "needle surface exists " + label);
            Check(mask.width == s.texture.width && mask.height == s.texture.height, "needle surface dimensions follow PNG " + label);
            int wrong = 0;
            for (int i = 0; i < s.pixels.Length; i++) if ((s.pixels[i].a > 127) != ((mask.bits[i >> 3] & (1 << (i & 7))) != 0)) wrong++;
            Check(wrong == 0, "all " + s.pixels.Length + " alpha pixels agree with needle surface " + label);
            var names = new HashSet<string>(); var rectangles = new List<Rect>();
            foreach (var f in meta.frames)
            {
                Check(names.Add(f.name) && f.x >= 0 && f.y >= 0 && f.width > 0 && f.height > 0 && f.x + f.width <= s.texture.width && f.y + f.height <= s.texture.height, "unique in-bounds frame " + label + " " + f.name);
                var cell = new Rect(f.x, f.y, f.width, f.height);
                Check(!rectangles.Any(r => r.Overlaps(cell)), "animation cells do not overlap " + label + " " + f.name); rectangles.Add(cell);
                Check(imported.TryGetValue(f.name, out var sprite), "imported frame name " + label + " " + f.name);
                Check(sprite.rect == new Rect(f.x, f.y, f.width, f.height) && Vector2.Distance(sprite.pivot, new Vector2(f.width * meta.pivotX, f.height * meta.pivotY)) < .002f, "imported rectangle and fixed pivot " + label + " " + f.name);
                Check(Mathf.Abs(meta.pivotX * f.width - Mathf.Round(meta.pivotX * f.width)) < .002f && Mathf.Abs(meta.pivotY * f.height - Mathf.Round(meta.pivotY * f.height)) < .002f, "integer pixel anchor avoids cross-sheet drift " + label + " " + f.name);
                var b = Bounds(s, f);
                Check(b.width > 0 && b.height > 0 && b.xMin > 0 && b.yMin > 0 && b.xMax < f.width && b.yMax < f.height, "nonempty silhouette retains unclipped transparent border " + label + " " + f.name);
                File.AppendAllText(output + "/asset-geometry.csv", string.Join(",", new[] { id, name, f.name, f.width.ToString(), f.height.ToString(), F(f.width * meta.pivotX), F(f.height * meta.pivotY), F(b.width), F(b.height), F(b.yMin - f.height * meta.pivotY) }) + "\n");
            }
        }
        static string F(float n) => n.ToString("0.####", CultureInfo.InvariantCulture);
        static Rect Bounds(Source s, Frame f)
        {
            int minX = f.width, minY = f.height, maxX = -1, maxY = -1;
            for (int y = 0; y < f.height; y++) for (int x = 0; x < f.width; x++) if (s.pixels[(f.y + y) * s.texture.width + f.x + x].a > 32) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            return maxX < 0 ? Rect.zero : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        static Dictionary<Vector2Int, uint> PixelsAtAnchor(Source s, int index)
        {
            var f = s.sheet.frames[index]; int px = Mathf.RoundToInt(f.width * s.sheet.pivotX), py = Mathf.RoundToInt(f.height * s.sheet.pivotY);
            var result = new Dictionary<Vector2Int, uint>();
            for (int y = 0; y < f.height; y++) for (int x = 0; x < f.width; x++)
            {
                Color32 c = s.pixels[(f.y + y) * s.texture.width + f.x + x]; if (c.a == 0) continue;
                result.Add(new Vector2Int(x - px, y - py), (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24));
            }
            return result;
        }
        static bool Same(Dictionary<Vector2Int, uint> a, Dictionary<Vector2Int, uint> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var value) && value == p.Value);
    }
}
#endif
