using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Loads the CC0 low-poly models shipped in Resources/FreeAssets/Models (*.bytes).
    /// The files were converted from the original Kenney glTF files by Tools/AssetConverter
    /// into a tiny binary format: parts (with local TRS) whose triangles are grouped by flat colour.
    /// Loading needs no importer package and works identically in the editor and in builds.
    /// </summary>
    public class MeshPack
    {
        public class Part
        {
            public string name;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Mesh mesh;
            public Color[] colors; // one per sub-mesh
        }

        public readonly List<Part> parts = new List<Part>();
        public Bounds bounds;

        static readonly Dictionary<string, MeshPack> cache = new Dictionary<string, MeshPack>();

        public static MeshPack Load(string name)
        {
            if (cache.TryGetValue(name, out var mp)) return mp;
            var ta = Resources.Load<TextAsset>("FreeAssets/Models/" + name);
            if (ta == null)
            {
                Debug.LogWarning($"[MeshPack] FreeAssets/Models/{name} not found – procedural fallback will be used.");
                cache[name] = null;
                return null;
            }
            try
            {
                mp = Parse(ta.bytes, name);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MeshPack] Failed to parse {name}: {e.Message}");
                mp = null;
            }
            cache[name] = mp;
            return mp;
        }

        static MeshPack Parse(byte[] data, string name)
        {
            var mp = new MeshPack();
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                var magic = new string(br.ReadChars(4));
                if (magic != "MPK1") throw new Exception("bad magic " + magic);
                int partCount = br.ReadInt32();
                bool first = true;
                for (int p = 0; p < partCount; p++)
                {
                    var part = new Part();
                    int nameLen = br.ReadInt32();
                    part.name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                    part.position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    part.rotation = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    part.scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    int vc = br.ReadInt32();
                    var verts = new Vector3[vc];
                    var norms = new Vector3[vc];
                    for (int i = 0; i < vc; i++) verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) norms[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    int groups = br.ReadInt32();
                    var mesh = new Mesh { name = name + "_" + part.name };
                    if (vc > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = verts;
                    mesh.normals = norms;
                    mesh.subMeshCount = groups;
                    part.colors = new Color[groups];
                    for (int g = 0; g < groups; g++)
                    {
                        byte r = br.ReadByte(), gg = br.ReadByte(), b = br.ReadByte(), a = br.ReadByte();
                        part.colors[g] = new Color32(r, gg, b, a);
                        int ic = br.ReadInt32();
                        var idx = new int[ic];
                        for (int i = 0; i < ic; i++) idx[i] = br.ReadInt32();
                        mesh.SetTriangles(idx, g);
                    }
                    mesh.RecalculateBounds();
                    part.mesh = mesh;
                    mp.parts.Add(part);

                    // model-space bounds (approximate: transform the 8 corners)
                    var b0 = mesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = b0.center + Vector3.Scale(b0.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        var w = part.position + part.rotation * Vector3.Scale(part.scale, corner);
                        if (first) { mp.bounds = new Bounds(w, Vector3.zero); first = false; }
                        else mp.bounds.Encapsulate(w);
                    }
                }
            }
            return mp;
        }

        /// <summary>
        /// Creates renderers for every part under <paramref name="parent"/>.
        /// <paramref name="remap"/> may replace palette colours (team liveries).
        /// </summary>
        public GameObject Instantiate(Transform parent, string objectName, Func<string, Color, Color> remap = null, bool castShadows = true)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            foreach (var part in parts)
            {
                var go = new GameObject(part.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = part.position;
                go.transform.localRotation = part.rotation;
                go.transform.localScale = part.scale;
                go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[part.colors.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var c = remap != null ? remap(part.name, part.colors[i]) : part.colors[i];
                    mats[i] = MaterialFactory.Lit(c, 0.25f);
                }
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        public Part FindPart(string partName)
        {
            foreach (var p in parts) if (p.name == partName) return p;
            return null;
        }
    }
}
