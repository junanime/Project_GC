using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Vampire
{
    public sealed partial class FiniteStomachMap
    {
        // Geometry is translation-invariant. Float precision and the 512x512 grid match the live sampler.
        public bool UsingBakedTerrain { get; private set; }
        const int TerrainFormat = 1;
        string BakeName => "TerrainBake/" + (IsColon ? "Colon" : "Stomach");
        float[] GeometrySettings() => new[] { Definition.worldWidth, Definition.aspect, Definition.wallWidth,
            Definition.climbableWidth, Definition.inlet.x, Definition.inlet.y, Definition.inlet.z, Definition.inlet.w,
            Definition.outlet.x, Definition.outlet.y, Definition.outlet.z, Definition.outlet.w };

        bool TryLoadTerrainBake()
        {
            var file = Resources.Load<TextAsset>(BakeName);
            if (file == null) return false;
            try
            {
                using (var compressed = new MemoryStream(file.bytes))
                using (var unpack = new GZipStream(compressed, CompressionMode.Decompress))
                using (var reader = new BinaryReader(unpack))
                {
                    if (reader.ReadInt32() != TerrainFormat || reader.ReadInt32() != FieldResolution) return false;
                    foreach (float expected in GeometrySettings()) if (expected != reader.ReadSingle()) return false;
                    int count = FieldResolution * FieldResolution;
                    var transfer = new byte[65536];
                    distanceSamples = new Color[count];
                    ReadBlock(reader, distanceSamples, count * 16, transfer);
                    var visual = distanceSamples;
                    if (IsColon) { visual = new Color[count]; ReadBlock(reader, visual, count * 16, transfer); }
                    var walls = new Color[count];
                    ReadBlock(reader, walls, count * 16, transfer);
                    if (IsColon) { floorSamples = new float[count]; ReadBlock(reader, floorSamples, count * 4, transfer); }
                    UploadTerrain(visual, walls);
                    UsingBakedTerrain = true;
                    return true;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException)
            {
                Debug.LogWarning("Terrain geometry will be calculated locally. " + e.Message);
                return false;
            }
        }

        // Stream directly into each destination, reusing a small buffer throughout the load.
        static void ReadBlock(BinaryReader reader, Array target, int byteCount, byte[] transfer)
        {
            var pinned = GCHandle.Alloc(target, GCHandleType.Pinned);
            try
            {
                int offset = 0;
                while (offset < byteCount)
                {
                    int read = reader.Read(transfer, 0, Math.Min(transfer.Length, byteCount - offset));
                    if (read == 0) throw new EndOfStreamException();
                    Marshal.Copy(transfer, 0, IntPtr.Add(pinned.AddrOfPinnedObject(), offset), read);
                    offset += read;
                }
            }
            finally { pinned.Free(); }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/24TU/Bake field geometry")]
        public static void BakeFieldGeometry()
        {
            foreach (var path in new[] { "StomachMap/Stage1", "ColonMap/Stage2" })
            {
                var host = new GameObject("Field geometry baker");
                try
                {
                    var map = host.AddComponent<FiniteStomachMap>();
                    map.Definition = Resources.Load<StomachMapDefinition>(path);
                    map.WorldRect = new Rect(Vector2.zero, map.Definition.Size);
                    map.boundary = map.CreateBoundary();
                    map.PrepareBoundary();
                    map.SampleTerrain(out var visual, out var wall);
                    string destination = "Assets/Resources/" + map.BakeName + ".bytes";
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var file = File.Create(destination))
                    using (var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal))
                    using (var output = new BinaryWriter(zip))
                    {
                        output.Write(TerrainFormat); output.Write(FieldResolution);
                        foreach (float value in map.GeometrySettings()) output.Write(value);
                        WriteBlock(output, map.distanceSamples, 16);
                        if (map.IsColon) WriteBlock(output, visual, 16);
                        WriteBlock(output, wall, 16);
                        if (map.IsColon) WriteBlock(output, map.floorSamples, 4);
                    }
                    Debug.Log("Baked field geometry " + destination);
                }
                finally { DestroyImmediate(host); }
            }
            UnityEditor.AssetDatabase.Refresh();
        }

        static void WriteBlock(BinaryWriter writer, Array values, int stride)
        {
            var bytes = new byte[values.Length * stride];
            var pinned = GCHandle.Alloc(values, GCHandleType.Pinned);
            try { Marshal.Copy(pinned.AddrOfPinnedObject(), bytes, 0, bytes.Length); }
            finally { pinned.Free(); }
            writer.Write(bytes);
        }
#endif
    }
}
